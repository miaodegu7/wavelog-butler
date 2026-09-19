using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
namespace WavelogButler;

public sealed class MainForm : Form
{
    private readonly Store store = Store.Load();
    private readonly TextBox search = new() { Width = 210, PlaceholderText = "输入友台呼号，如 JA1XXX" };
    private readonly DataGridView accounts = Grid();
    private readonly DataGridView contacts = Grid();
    private readonly Label status = new() { AutoSize = true, Text = "添加账号后同步；只读取 Wavelog，不修改原始日志。", Padding = new Padding(8) };
    private readonly FlowLayoutPanel toolbar = new() { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6), WrapContents = true };
    private readonly Button cancel = new() { Text = "取消同步", AutoSize = true, Enabled = false };
    private CancellationTokenSource? syncToken;
    private List<(Account Account, Contact Contact)> matched = [];
    public MainForm()
    {
        Text = "Wavelog 管家 · 多台站集中寄卡";
        Size = new Size(1250, 780);
        MinimumSize = new Size(940, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);
        AddButton("添加账号", (_, _) => EditAccount(null));
        AddButton("编辑账号", (_, _) => { if (SelectedAccount() is { } account) EditAccount(account); });
        AddButton("移除账号", (_, _) => RemoveAccount());
        AddButton("同步全部", async (_, _) => await Synchronize());
        cancel.Click += (_, _) => syncToken?.Cancel();
        toolbar.Controls.Add(cancel);
        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(accounts, 0, 1);
        var queryBar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        queryBar.Controls.Add(search);
        foreach (var (title, action) in new (string, Action)[] {
            ("搜索通联", RefreshResults), ("QRZ", () => OpenFriend(false)), ("Club Log", () => OpenFriend(true)),
            ("导出当前寄卡清单", Export) })
        {
            var button = new Button { Text = title, AutoSize = true };
            button.Click += (_, _) => action();
            queryBar.Controls.Add(button);
        }
        search.KeyDown += (_, args) => { if (args.KeyCode == Keys.Enter) { RefreshResults(); args.SuppressKeyPress = true; } };
        layout.Controls.Add(queryBar, 0, 2);
        layout.Controls.Add(contacts, 0, 3);
        layout.Controls.Add(status, 0, 4);
        FormClosing += (_, args) => { if (syncToken != null) { args.Cancel = true; syncToken.Cancel(); status.Text = "正在取消同步，结束后可以关闭窗口。"; } };
        RefreshResults();
    }
    private static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None, AutoGenerateColumns = true
    };
    private void AddButton(string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += handler;
        toolbar.Controls.Add(button);
    }
    private Account? SelectedAccount() => accounts.CurrentRow?.Index is int index && index >= 0 && index < store.Accounts.Count ? store.Accounts[index] : null;
    private void EditAccount(Account? account)
    {
        using var dialog = new AccountDialog(account);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var target = account ?? new Account();
        var changedSource = target.Url != dialog.ServerUrl || dialog.ApiKey.Length > 0;
        target.Name = dialog.AccountName;
        target.Url = dialog.ServerUrl;
        if (dialog.ApiKey.Length > 0) target.Secret = SecretStorage.Protect(dialog.ApiKey);
        if (changedSource) { target.Synced = null; target.Contacts.Clear(); target.Stations.Clear(); target.Error = ""; }
        if (account == null) store.Accounts.Add(target);
        Save();
        RefreshResults();
    }
    private void RemoveAccount()
    {
        var account = SelectedAccount();
        if (account == null || MessageBox.Show(this, $"移除 {account.Name} 和本地缓存？不会删除 Wavelog 上的日志。", "确认移除", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        store.Accounts.Remove(account);
        Save();
        RefreshResults();
    }
    private bool Save()
    {
        try { store.Save(); return true; }
        catch (Exception) { MessageBox.Show(this, "保存失败，请检查磁盘空间和目录权限。本次更改仅在内存中。", "保存失败"); return false; }
    }
    private async Task Synchronize()
    {
        if (store.Accounts.Count == 0) { MessageBox.Show(this, "请先添加至少一个账号。"); return; }
        syncToken = new CancellationTokenSource();
        foreach (Control control in toolbar.Controls) control.Enabled = control == cancel;
        var failures = 0;
        try
        {
            using var api = new WavelogApi();
            foreach (var account in store.Accounts)
            {
                try
                {
                    var downloaded = await api.Download(account, new Progress<string>(message => status.Text = message), syncToken.Token);
                    account.Stations = downloaded.Stations;
                    account.Contacts = downloaded.Contacts;
                    account.Synced = DateTime.UtcNow;
                    account.Error = "";
                }
                catch (OperationCanceledException) when (syncToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    failures++;
                    account.Error = exception is InvalidOperationException or InvalidDataException ? exception.Message : "连接失败、超时或响应格式不兼容，请检查服务器与密钥。";
                }
                Save();
                RefreshResults();
            }
            status.Text = syncToken.IsCancellationRequested ? "同步已取消；未完成账号保留旧缓存。" : $"同步结束：{failures} 个账号失败。上方显示每个账号的数据状态。";
        }
        finally
        {
            syncToken.Dispose();
            syncToken = null;
            foreach (Control control in toolbar.Controls) control.Enabled = control != cancel;
        }
    }
    private string Query => search.Text.Trim().ToUpperInvariant();
    private void RefreshResults()
    {
        var query = Query;
        matched = store.Accounts.SelectMany(account => account.Contacts
            .Where(contact => query.Length > 0 && contact.Get("CALL").Equals(query, StringComparison.OrdinalIgnoreCase))
            .Select(contact => (account, contact))).OrderBy(pair => pair.contact.Get("QSO_DATE")).ThenBy(pair => pair.contact.Get("TIME_ON")).ToList();
        accounts.DataSource = store.Accounts.Select(account => new
        {
            账号 = account.Name,
            台站呼号 = string.Join(", ", account.Stations.Select(station => station.Call).Distinct()),
            查询结果 = query.Length == 0 ? "请输入友台呼号" : !account.Synced.HasValue ? "无法确定（未完成同步）" :
                matched.Any(pair => pair.Account == account) ? "有通联" : "已导入范围内未检索到",
            命中条数 = matched.Count(pair => pair.Account == account),
            日志总数 = account.Contacts.Count,
            最后同步 = account.Synced?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "从未同步",
            数据状态 = account.Error.Length > 0 ? "同步失败：" + account.Error : account.Synced.HasValue ? "缓存可用" : "待同步"
        }).ToList();
        contacts.DataSource = matched.Select(pair => new
        {
            账号 = pair.Account.Name,
            我方呼号 = OwnCall(pair.Account, pair.Contact),
            台站位置 = pair.Account.Stations.FirstOrDefault(station => station.Id == pair.Contact.StationId)?.Name ?? "",
            友台 = pair.Contact.Get("CALL"),
            日期UTC = pair.Contact.Get("QSO_DATE"), 时间UTC = pair.Contact.Get("TIME_ON"),
            波段 = pair.Contact.Get("BAND"), 频率MHz = pair.Contact.Get("FREQ"),
            模式 = pair.Contact.Get("MODE"), 子模式 = pair.Contact.Get("SUBMODE"),
            发报告 = pair.Contact.Get("RST_SENT"), 收报告 = pair.Contact.Get("RST_RCVD"),
            纸卡已寄 = pair.Contact.Get("QSL_SENT"), 纸卡已收 = pair.Contact.Get("QSL_RCVD"),
            QSL经理 = pair.Contact.Get("QSL_VIA"), 地址待核对 = pair.Contact.Get("ADDRESS"), 备注 = pair.Contact.Get("COMMENT")
        }).ToList();
    }
    private static string OwnCall(Account account, Contact contact) => contact.Get("STATION_CALLSIGN") is { Length: > 0 } call
        ? call : account.Stations.FirstOrDefault(station => station.Id == contact.StationId)?.Call ?? "";
    private void OpenFriend(bool clublog)
    {
        if (!Regex.IsMatch(Query, @"^[A-Z0-9/]{3,30}$")) { MessageBox.Show(this, "请先输入有效呼号。"); return; }
        var url = clublog ? "https://clublog.org/logsearch.php?log=" + Uri.EscapeDataString(Query) : "https://www.qrz.com/db/" + Uri.EscapeDataString(Query);
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception) { MessageBox.Show(this, "无法打开默认浏览器。"); }
    }
    private void Export()
    {
        RefreshResults();
        if (matched.Count == 0) { MessageBox.Show(this, "当前呼号没有可导出的通联。"); return; }
        using var dialog = new SaveFileDialog { Filter = "CSV 清单 (*.csv)|*.csv", FileName = "QSL-" + Regex.Replace(Query, "[^A-Z0-9]", "_") + ".csv" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var lines = new List<string> { "账号,我方呼号,友台呼号,日期UTC,时间UTC,波段,模式,发报告,收报告,纸卡寄出状态,QSL经理,地址待核对,QRZ,ClubLog" };
            foreach (var pair in matched)
            {
                var contact = pair.Contact;
                lines.Add(string.Join(",", new[] { pair.Account.Name, OwnCall(pair.Account, contact), contact.Get("CALL"), contact.Get("QSO_DATE"),
                    contact.Get("TIME_ON"), contact.Get("BAND"), contact.Get("MODE"), contact.Get("RST_SENT"), contact.Get("RST_RCVD"),
                    contact.Get("QSL_SENT"), contact.Get("QSL_VIA"), contact.Get("ADDRESS"),
                    "https://www.qrz.com/db/" + Uri.EscapeDataString(contact.Get("CALL")),
                    "https://clublog.org/logsearch.php?log=" + Uri.EscapeDataString(contact.Get("CALL")) }.Select(Csv)));
            }
            File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
            status.Text = $"已导出 {matched.Count} 条通联。寄出前请核对地址与 QSL 指引。";
        }
        catch (Exception) { MessageBox.Show(this, "导出失败，请确认目标文件没有被占用，并检查写入权限。"); }
    }
    private static string Csv(string value)
    {
        if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
