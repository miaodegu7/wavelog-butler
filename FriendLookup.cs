using System.Xml;
using System.Xml.Linq;
namespace WavelogButler;
internal sealed record FriendInfo(string Country,string Email,string Address,string Manager,string Source);
internal sealed class FriendLookup : IDisposable
{
    private readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect=false }) { Timeout=TimeSpan.FromSeconds(25) };
    private static string Value(XElement? element,string name) => element?.Elements().FirstOrDefault(child=>child.Name.LocalName==name)?.Value.Trim()??"";
    private async Task<XElement> Request(string query,CancellationToken token)
    {
        using var response=await client.GetAsync("https://xmldata.qrz.com/xml/current/?"+query,token);
        if(!response.IsSuccessStatusCode) throw new InvalidOperationException("QRZ 请求失败，请检查网络和查询权限。");
        var content=await response.Content.ReadAsStringAsync(token);
        using var reader=XmlReader.Create(new StringReader(content),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null});
        return XElement.Load(reader);
    }
    public async Task<FriendInfo> Fetch(string call,string user,string password,CancellationToken token)
    {
        var login=await Request("username="+Uri.EscapeDataString(user)+";password="+Uri.EscapeDataString(password)+";agent=WavelogButler2",token);
        var session=login.Elements().FirstOrDefault(element=>element.Name.LocalName=="Session");
        var key=Value(session,"Key");
        if(key.Length==0) throw new InvalidOperationException("QRZ 登录失败。请在资料查询设置中检查账号密码及 XML 查询订阅。");
        var result=await Request("s="+Uri.EscapeDataString(key)+";callsign="+Uri.EscapeDataString(call),token);
        var callsign=result.Elements().FirstOrDefault(element=>element.Name.LocalName=="Callsign");
        var resultSession=result.Elements().FirstOrDefault(element=>element.Name.LocalName=="Session");
        if(Value(resultSession,"Error").Length>0 || callsign==null) throw new InvalidOperationException("QRZ 未返回资料：呼号不存在、登录过期或没有查询权限。请打开 QRZ 核对。");
        var country=Value(callsign,"country");
        var address=string.Join(Environment.NewLine,new[]{Value(callsign,"fname")+" "+Value(callsign,"name"),Value(callsign,"addr1"),Value(callsign,"addr2"),Value(callsign,"state")+" "+Value(callsign,"zip"),country}.Select(value=>value.Trim()).Where(value=>value.Length>0));
        if(Value(callsign,"addr1").Length==0 && Value(callsign,"addr2").Length==0) address="";
        return new FriendInfo(country,Value(callsign,"email"),address,Value(callsign,"qslmgr"),"QRZ 官方 XML · "+DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
    }
    public void Dispose()=>client.Dispose();
}
