using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace WavelogButler;
internal sealed class Database : IDisposable
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WavelogButler");
    private readonly SqliteConnection connection;
    private SqliteTransaction? activeTransaction;
    public Database(string? folder = null)
    {
        folder ??= Folder;
        Directory.CreateDirectory(folder);
        connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(folder, "logs.db") }.ToString());
        connection.Open();
        Execute("PRAGMA foreign_keys=ON; CREATE TABLE IF NOT EXISTS accounts(id TEXT PRIMARY KEY, data TEXT NOT NULL); CREATE TABLE IF NOT EXISTS contacts(account TEXT NOT NULL REFERENCES accounts(id) ON DELETE CASCADE, identity TEXT NOT NULL, call TEXT NOT NULL, data TEXT NOT NULL, missing INTEGER NOT NULL DEFAULT 0, PRIMARY KEY(account,identity)); CREATE INDEX IF NOT EXISTS call_index ON contacts(call); CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);");
        var legacy = Path.Combine(folder, "accounts.json");
        if (GetSetting("migration") == "" && File.Exists(legacy))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(legacy));
            foreach (var item in document.RootElement.GetProperty("Accounts").EnumerateArray())
            {
                var account = item.Deserialize<Account>()!;
                if (Accounts().All(existing => existing.Id != account.Id)) SaveAccount(account, account.Contacts);
            }
            SetSetting("migration", "done");
        }
    }
    private void Execute(string sql, params (string, object)[] parameters)
    {
        using var command = connection.CreateCommand(); command.CommandText = sql;
        command.Transaction = activeTransaction;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }
    public List<Account> Accounts()
    {
        using var command = connection.CreateCommand(); command.CommandText = "SELECT data FROM accounts ORDER BY rowid";
        using var reader = command.ExecuteReader(); var result = new List<Account>();
        while (reader.Read()) result.Add(JsonSerializer.Deserialize<Account>(reader.GetString(0))!);
        return result;
    }
    public void SaveAccount(Account account, List<Contact>? contacts = null)
    {
        using var transaction = connection.BeginTransaction();
        activeTransaction = transaction;
        try
        {
            var metadata = new Account { Id = account.Id, Name = account.Name, Url = account.Url, Secret = account.Secret, Synced = account.Synced, Error = account.Error, Stations = account.Stations };
            Execute("INSERT INTO accounts VALUES($id,$data) ON CONFLICT(id) DO UPDATE SET data=excluded.data", ("$id",account.Id.ToString()), ("$data",JsonSerializer.Serialize(metadata)));
            if (contacts != null)
            {
                Execute("UPDATE contacts SET missing=1 WHERE account=$id", ("$id",account.Id.ToString()));
                var occurrences = new Dictionary<string,int>();
                foreach (var contact in contacts)
                {
                    var identity = JsonSerializer.Serialize(new[] {contact.StationId,contact.Get("CALL"),contact.Get("QSO_DATE"),contact.Get("TIME_ON"),contact.Get("BAND"),contact.Get("MODE"),contact.Get("FREQ"),contact.Get("SAT_NAME")});
                    var occurrence = occurrences.GetValueOrDefault(identity); occurrences[identity] = occurrence + 1;
                    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity + ":" + occurrence)));
                    Execute("INSERT INTO contacts VALUES($account,$identity,$call,$data,0) ON CONFLICT(account,identity) DO UPDATE SET call=excluded.call,data=excluded.data,missing=0",
                        ("$account",account.Id.ToString()),("$identity",hash),("$call",contact.Get("CALL").Trim().ToUpperInvariant()),("$data",JsonSerializer.Serialize(contact)));
                }
            }
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
        finally { activeTransaction = null; }
    }
    public List<LogRow> Search(string call)
    {
        var accounts=Accounts().ToDictionary(account=>account.Id);
        using var command = connection.CreateCommand(); command.CommandText = "SELECT account,data,missing FROM contacts WHERE call=$call"; command.Parameters.AddWithValue("$call",call);
        using var reader = command.ExecuteReader(); var rows = new List<LogRow>();
        while(reader.Read())
        {
            var accountId=Guid.Parse(reader.GetString(0));
            var contact=JsonSerializer.Deserialize<Contact>(reader.GetString(1))!;
            var station=accounts.GetValueOrDefault(accountId)?.Stations.FirstOrDefault(item=>item.Id==contact.StationId);
            rows.Add(new LogRow(accountId,contact,reader.GetInt32(2)!=0,station?.Name is {Length:>0} name ? name : "台站 ID："+contact.StationId));
        }
        return rows.OrderBy(row=>row.Contact.Get("QSO_DATE")).ThenBy(row=>row.Contact.Get("TIME_ON")).ToList();
    }
    public void Remove(Account account) => Execute("DELETE FROM accounts WHERE id=$id",("$id",account.Id.ToString()));
    public string GetSetting(string key)
    {
        using var command=connection.CreateCommand(); command.CommandText="SELECT value FROM settings WHERE key=$key"; command.Parameters.AddWithValue("$key",key); return command.ExecuteScalar()?.ToString()??"";
    }
    public void SetSetting(string key,string value) => Execute("INSERT INTO settings VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value",("$key",key),("$value",value));
    public void Backup(string path) { using var destination=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path}.ToString()); destination.Open(); connection.BackupDatabase(destination); }
    public void Dispose()=>connection.Dispose();
}
internal sealed record LogRow(Guid AccountId,Contact Contact,bool Missing,string StationName = "")
{
    public string OwnCall => Contact.Get("STATION_CALLSIGN");
    public string Date => DateTime.TryParseExact(Contact.Get("QSO_DATE"),"yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var date) ? date.ToString("yyyy-MM-dd") : Contact.Get("QSO_DATE");
    public string Time => Contact.Get("TIME_ON") is {Length:6} time ? time[..2]+":"+time[2..4]+":"+time[4..] : Contact.Get("TIME_ON");
    public string Channel => Contact.Get("SAT_NAME") is {Length:>0} satellite ? satellite : Contact.Get("PROP_MODE").Equals("SAT",StringComparison.OrdinalIgnoreCase) ? "卫星名称未记录" : Contact.Get("BAND")+" · "+Contact.Get("FREQ")+" MHz";
    public string Mode => Contact.Get("MODE");
    public string Reports => Contact.Get("RST_SENT")+" / "+Contact.Get("RST_RCVD");
    public string Qsl => Contact.Get("QSL_SENT");
    public string State => Missing ? "远端未返回 · 待核对" : "已保存";
}
