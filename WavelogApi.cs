using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace WavelogButler;
internal sealed class WavelogApi : IDisposable
{
    private readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(2) };
    public void Dispose() => client.Dispose();
    private static async Task<JsonDocument> Read(HttpResponseMessage response, CancellationToken token)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"API 返回 HTTP {(int)response.StatusCode}；请检查网址、权限及密钥。重定向不会自动跟随。");
            var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("status", out var status)
                && status.GetString() is "failed" or "error")
            {
                document.Dispose();
                throw new InvalidOperationException("Wavelog 拒绝请求，请检查 API 只读权限和台站配置。");
            }
            return document;
        }
    }
    public async Task<(List<Station> Stations, List<Contact> Contacts)> Download(Account account, IProgress<string> progress, CancellationToken token)
    {
        var key = SecretStorage.Unprotect(account.Secret);
        var root = account.Url.TrimEnd('/') + "/api/";
        using var stationResponse = await Read(await client.GetAsync(root + "station_info/" + Uri.EscapeDataString(key), token), token);
        if (stationResponse.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("台站接口未返回列表，请确认 Wavelog 根地址。");
        var stations = stationResponse.RootElement.EnumerateArray().Select(item => new Station(
            item.GetProperty("station_id").ToString(), item.GetProperty("station_callsign").ToString(),
            item.GetProperty("station_profile_name").ToString())).ToList();
        var contacts = new List<Contact>();
        foreach (var station in stations)
        {
            long cursor = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                progress.Report($"{account.Name} / {station.Call}：已读取 {contacts.Count:N0} 条");
                using var response = await Read(await client.PostAsJsonAsync(root + "get_contacts_adif", new
                { key, station_id = station.Id, fetchfromid = cursor, limit = 1000 }, token), token);
                var body = response.RootElement;
                if (!body.TryGetProperty("adif", out var adif)) throw new InvalidDataException("日志接口缺少 ADIF 数据。");
                var text = adif.ValueKind == JsonValueKind.Null ? "" : adif.GetString() ?? "";
                var page = ParseAdif(text, station.Id);
                if (page.Count == 0)
                {
                    if (body.TryGetProperty("exported_qsos", out var exported) && exported.ToString() != "0")
                        throw new InvalidDataException("服务器报告有日志，但返回内容无法解析。");
                    break;
                }
                if (!body.TryGetProperty("lastfetchedid", out var last) || !long.TryParse(last.ToString(), out var next) || next <= cursor)
                    throw new InvalidDataException("分页游标没有前进；为防止重复导入，已停止同步。");
                contacts.AddRange(page);
                cursor = next;
            }
        }
        return (stations, contacts);
    }
    internal static List<Contact> ParseAdif(string text, string stationId)
    {
        var result = new List<Contact>();
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var headerEnd = text.IndexOf("<EOH>", StringComparison.OrdinalIgnoreCase);
        var position = headerEnd < 0 ? 0 : headerEnd + 5;
        while (position < text.Length)
        {
            var start = text.IndexOf('<', position);
            if (start < 0) break;
            var end = text.IndexOf('>', start);
            if (end < 0) throw new InvalidDataException("ADIF 标签不完整。");
            var tag = text[(start + 1)..end].Trim();
            position = end + 1;
            if (tag.Equals("EOH", StringComparison.OrdinalIgnoreCase)) { fields.Clear(); continue; }
            if (tag.Equals("EOR", StringComparison.OrdinalIgnoreCase))
            {
                if (fields.ContainsKey("CALL")) result.Add(new Contact { StationId = stationId, Fields = fields });
                fields = new(StringComparer.OrdinalIgnoreCase);
                continue;
            }
            var match = Regex.Match(tag, @"^([A-Za-z0-9_]+):(\d+)(?::[A-Za-z])?$");
            if (!match.Success) throw new InvalidDataException("不支持的 ADIF 标签格式。");
            var length = int.Parse(match.Groups[2].Value);
            if (length > text.Length - position) throw new InvalidDataException("ADIF 字段长度超出数据范围。");
            fields[match.Groups[1].Value.ToUpperInvariant()] = text.Substring(position, length).Trim();
            position += length;
        }
        if (fields.ContainsKey("CALL")) throw new InvalidDataException("ADIF 通联记录缺少 EOR 结束标记。");
        return result;
    }
}
