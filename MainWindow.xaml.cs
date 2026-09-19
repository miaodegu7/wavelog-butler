using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
namespace WavelogButler;
public partial class MainWindow : Window
{
    private readonly Database database;
    private readonly FriendLookup lookup = new();
    private CancellationTokenSource? syncCancellation;
    private CancellationTokenSource? lookupCancellation;
    private string currentCall = "";
    private List<LogRow> rows = [];
    private bool closing;
    public MainWindow() : this(new Database()) { }
    internal MainWindow(Database database)
    {
        this.database = database;
        InitializeComponent();
        RenderAccounts(); RenderGroups();
        Closing += (_, args) =>
        {
            if(syncCancellation!=null) { args.Cancel=true; syncCancellation.Cancel(); Status.Text="正在取消同步，结束后可关闭窗口。"; return; }
            closing=true; lookupCancellation?.Cancel(); lookup.Dispose(); database.Dispose();
        };
    }
    private void ShowSearch(object sender,RoutedEventArgs args) { SearchPage.Visibility=Visibility.Visible; AccountsPage.Visibility=Visibility.Collapsed; }
    private void ShowAccounts(object sender,RoutedEventArgs args) { SearchPage.Visibility=Visibility.Collapsed; AccountsPage.Visibility=Visibility.Visible; RenderAccounts(); }
    private async void SearchClick(object sender,RoutedEventArgs args) => await Search();
    private async void SearchKey(object sender,KeyEventArgs args) { if(args.Key==Key.Enter) await Search(); }
    private async Task Search()
    {
        var call=CallInput.Text.Trim().ToUpperInvariant();
        if(!Regex.IsMatch(call,@"^[A-Z0-9/]{3,30}$")) { MessageBox.Show(this,"请输入有效的完整呼号。","查询"); return; }
        lookupCancellation?.Cancel();
        var cancellation=new CancellationTokenSource(); lookupCancellation=cancellation;
        currentCall=call; FriendCall.Text=call;
        rows=database.Search(call); RenderGroups();
        var local=rows.Where(row=>!row.Missing).Select(row=>row.Contact).ToList();
        string Local(string field) => local.Select(contact=>contact.Get(field)).FirstOrDefault(value=>value.Length>0)??"未记录";
        Country.Text=Local("COUNTRY"); Email.Text=Local("EMAIL"); Address.Text=Local("ADDRESS"); Manager.Text="QSL Manager / 寄卡途径："+Local("QSL_VIA");
        LookupStatus.Text="资料暂来自已保存日志；正在查询 QRZ…";
        try
        {
            var user=database.GetSetting("qrz.user"); var secret=database.GetSetting("qrz.secret");
            if(user.Length==0 || secret.Length==0)
            {
                LookupStatus.Text="当前资料来自日志；需要在“资料查询设置”登录具备 XML 查询权限的 QRZ 账号，才能自动获取公开地址和邮箱。";
                if(database.GetSetting("qrz.reminded")=="")
                {
                    database.SetSetting("qrz.reminded","yes");
                    MessageBox.Show(this,"自动查询国家、邮箱和地址需要配置 QRZ 登录及 XML 查询权限。\n请打开左侧“资料查询设置”。通联搜索不受影响。","需要 QRZ 登录");
                }
                return;
            }
            var info=await lookup.Fetch(call,user,SecretStorage.Unprotect(secret),cancellation.Token);
            if(cancellation.IsCancellationRequested || closing) return;
            Country.Text=info.Country.Length>0?info.Country:"未公开 / 无权限";
            Email.Text=info.Email.Length>0?info.Email:"未公开 / 无权限";
            Address.Text=info.Address.Length>0?info.Address:"未公开 / 无权限";
            Manager.Text="QSL Manager："+(info.Manager.Length>0?info.Manager:"未公开，请核对对方主页的 QSL 指引");
            LookupStatus.Text=info.Source+" · 邮寄地址不一定是 QSL 收件地址，请人工核对。";
        }
        catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { }
        catch(Exception exception)
        {
            if(!closing && !cancellation.IsCancellationRequested) LookupStatus.Text="联网查询未完成；当前显示日志资料。"+(exception is InvalidOperationException?exception.Message:"请检查网络或 QRZ 查询权限。");
        }
        finally { if(ReferenceEquals(lookupCancellation,cancellation)) lookupCancellation=null; cancellation.Dispose(); }
    }
    private Border Card(UIElement child) => new() { Style=(Style)FindResource("Card"), Child=child };
    private static TextBlock Text(string text,int size=13) => new() { Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10) };
    private Button Button(string text,RoutedEventHandler handler) { var button=new Button{Content=text}; button.Click+=handler; return button; }
    private void RenderGroups()
    {
        Groups.Children.Clear();
        var accounts=database.Accounts();
        Summary.Text=currentCall.Length==0?"按我方呼号分组 · 每个账户独立成卡":$"{currentCall} · {rows.Count} 条已保存通联 · {accounts.Count} 个账户";
        if(accounts.Count==0) { Groups.Children.Add(Card(Text("还没有日志来源。前往“日志管理”添加 Wavelog 账户，然后同步日志。",16))); return; }
        foreach(var account in accounts.OrderByDescending(account=>rows.Count(row=>row.AccountId==account.Id)))
        {
            var matches=rows.Where(row=>row.AccountId==account.Id).ToList();
            var calls=string.Join(" / ",account.Stations.Select(station=>station.Call).Where(call=>call.Length>0).Distinct());
            if(calls.Length==0) calls="待同步台站";
            var panel=new StackPanel();
            var state=!account.Synced.HasValue?"尚未完成同步 · 无法确定":matches.Count==0?"已保存范围内未检索到通联":$"{matches.Count} 次通联";
            var header=new StackPanel(); header.Children.Add(Text(calls,21)); header.Children.Add(Text(state));
            var expander=new Expander{Header=header,IsExpanded=matches.Count>0,Content=panel};
            panel.Children.Add(Text("最后同步："+(account.Synced?.ToLocalTime().ToString("yyyy-MM-dd HH:mm")??"从未同步")+(account.Error.Length>0?" · 同步异常："+account.Error:"")));
            if(matches.Count>0)
            {
                var grid=new DataGrid { ItemsSource=matches,AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,HeadersVisibility=DataGridHeadersVisibility.Column,GridLinesVisibility=DataGridGridLinesVisibility.Horizontal,HorizontalGridLinesBrush=new SolidColorBrush(Color.FromRgb(235,239,245)),RowHeight=42,ColumnHeaderHeight=38,BorderThickness=new Thickness(0),Background=Brushes.White,AlternatingRowBackground=new SolidColorBrush(Color.FromRgb(248,250,253)),MaxHeight=440,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto };
                foreach(var (title,path,width) in new (string,string,double)[]{("我方呼号","OwnCall",105),("日期 UTC","Date",100),("时间 UTC","Time",90),("频率 / 卫星","Channel",175),("模式","Mode",70),("发 / 收报告","Reports",110),("纸卡已寄","Qsl",85),("记录状态","State",160)})
                    grid.Columns.Add(new DataGridTextColumn{Header=title,Binding=new Binding(path),Width=new DataGridLength(width)});
                panel.Children.Add(grid);
            }
            Groups.Children.Add(Card(expander));
        }
    }
    private void RenderAccounts()
    {
        AccountCards.Children.Clear();
        foreach(var account in database.Accounts())
        {
            var panel=new StackPanel(); panel.Children.Add(Text(account.Name,20)); panel.Children.Add(Text(account.Url));
            panel.Children.Add(Text("台站呼号："+string.Join(" / ",account.Stations.Select(station=>station.Call).Distinct())));
            panel.Children.Add(Text("最后同步："+(account.Synced?.ToLocalTime().ToString("g")??"从未同步")+"  "+account.Error));
            var actions=new WrapPanel();
            actions.Children.Add(Button("编辑连接",(_,_)=>EditAccount(account)));
            actions.Children.Add(Button("移除来源与本地日志",(_,_)=>
            {
                if(MessageBox.Show(this,"删除此来源及本地日志？Wavelog 服务器不受影响。建议先备份数据库。","移除来源",MessageBoxButton.YesNo)==MessageBoxResult.Yes)
                    try { database.Remove(account); RefreshLocal(); RenderAccounts(); } catch(Exception){ MessageBox.Show(this,"删除失败，请检查数据库权限。"); }
            }));
            panel.Children.Add(actions); var card=Card(panel); card.IsEnabled=syncCancellation==null; AccountCards.Children.Add(card);
        }
    }
    private void AddAccount(object sender,RoutedEventArgs args)=>EditAccount(null);
    private void EditAccount(Account? account)
    {
        var dialog=new SettingsDialog(this,"Wavelog 连接",new[]{("账户备注",account?.Name??"",false),("HTTPS 根地址",account?.Url??"",false),("只读 API Key（编辑时留空保留）","",true)},"密钥使用 Windows 加密保存。修改服务器地址请新增来源，避免混淆旧日志。");
        if(dialog.ShowDialog()!=true) return;
        var values=dialog.Values;
        if(values[0].Length==0 || (account==null && values[2].Length==0) || !Uri.TryCreate(values[1],UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.UserInfo.Length>0 || uri.Query.Length>0 || uri.Fragment.Length>0)
        { MessageBox.Show(this,"请填写备注、合法的 HTTPS 根地址及 API Key。不要使用 /api 地址。"); return; }
        if(account!=null && account.Url.TrimEnd('/')!=values[1].TrimEnd('/')) { MessageBox.Show(this,"更换服务器请新增来源；旧日志会继续保留。"); return; }
        try
        {
            var target=account??new Account(); target.Name=values[0]; target.Url=values[1].TrimEnd('/');
            if(values[2].Length>0) target.Secret=SecretStorage.Protect(values[2]);
            database.SaveAccount(target); RenderAccounts(); RenderGroups();
        }
        catch(Exception){MessageBox.Show(this,"保存失败，请检查磁盘权限。");}
    }
    private void ConfigureLookup(object sender,RoutedEventArgs args)
    {
        var dialog=new SettingsDialog(this,"QRZ 资料查询登录",new[]{("QRZ 用户名",database.GetSetting("qrz.user"),false),("QRZ 密码（留空保留）","",true)},"需要 QRZ XML 查询权限 / 相应订阅。凭据只在本机加密保存，通过 HTTPS 发送至 QRZ 官方接口。清空用户名可停用查询。");
        if(dialog.ShowDialog()!=true) return;
        try
        {
            var values=dialog.Values;
            if(values[0].Length>0 && values[0]!=database.GetSetting("qrz.user") && values[1].Length==0) { MessageBox.Show(this,"更换用户名时请同时填写密码。"); return; }
            database.SetSetting("qrz.user",values[0]);
            if(values[1].Length>0) database.SetSetting("qrz.secret",SecretStorage.Protect(values[1]));
            if(values[0].Length==0) database.SetSetting("qrz.secret","");
            Status.Text="资料查询设置已保存，下次搜索自动查询。";
        }
        catch(Exception){ MessageBox.Show(this,"保存查询设置失败。"); }
    }
    private async void Sync(object sender,RoutedEventArgs args)
    {
        if(syncCancellation!=null)return;
        var accounts=database.Accounts(); if(accounts.Count==0){MessageBox.Show(this,"请先添加账户。");return;}
        syncCancellation=new CancellationTokenSource(); SyncButton.IsEnabled=false; AddAccountButton.IsEnabled=false; CancelButton.IsEnabled=true; RenderAccounts();
        var failures=0;
        try
        {
            using var api=new WavelogApi();
            foreach(var account in accounts)
            {
                try
                {
                    var result=await api.Download(account,new Progress<string>(message=>Status.Text=message),syncCancellation.Token);
                    syncCancellation.Token.ThrowIfCancellationRequested();
                    account.Stations=result.Stations; account.Synced=DateTime.UtcNow; account.Error="";
                    Status.Text="正在写入 SQLite 数据库…";
                    database.SaveAccount(account,result.Contacts);
                }
                catch(OperationCanceledException) when(syncCancellation.IsCancellationRequested){break;}
                catch(Exception exception)
                {
                    failures++;
                    var saved=database.Accounts().First(item=>item.Id==account.Id);
                    saved.Error=exception is InvalidOperationException or InvalidDataException?exception.Message:"连接、响应解析或数据库写入失败，请核对设置。";
                    database.SaveAccount(saved);
                }
            }
            RefreshLocal();
            Status.Text=syncCancellation.IsCancellationRequested?"已取消；已保存日志不受影响。":$"同步结束 · {failures} 个来源失败 · 日志已持久化保存";
        }
        catch(Exception){Status.Text="同步失败；已提交的数据保留，请检查磁盘空间和数据库权限。";}
        finally{syncCancellation.Dispose();syncCancellation=null;SyncButton.IsEnabled=true;AddAccountButton.IsEnabled=true;CancelButton.IsEnabled=false;RenderAccounts();}
    }
    private void RefreshLocal(){ rows=currentCall.Length>0?database.Search(currentCall):[];RenderGroups(); }
    private void CancelSync(object sender,RoutedEventArgs args)=>syncCancellation?.Cancel();
    private void OpenQrz(object sender,RoutedEventArgs args)=>Open("https://www.qrz.com/db/");
    private void OpenClubLog(object sender,RoutedEventArgs args)=>Open("https://clublog.org/logsearch.php?log=");
    private void Open(string prefix)
    {
        if(currentCall.Length==0)return;
        try{Process.Start(new ProcessStartInfo(prefix+Uri.EscapeDataString(currentCall)){UseShellExecute=true});}catch(Exception){MessageBox.Show(this,"无法打开默认浏览器。");}
    }
    private void Backup(object sender,RoutedEventArgs args)
    {
        var dialog=new SaveFileDialog{Filter="SQLite 数据库|*.db",FileName="wavelog-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".db"};
        if(dialog.ShowDialog(this)!=true)return;
        try
        {
            if(Path.GetFullPath(dialog.FileName).Equals(Path.Combine(Database.Folder,"logs.db"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException();
            database.Backup(dialog.FileName);Status.Text="数据库备份完成。凭据仅能由当前 Windows 用户解密。";
        }
        catch(Exception){MessageBox.Show(this,"备份失败，请选择非当前数据库的可写路径。");}
    }
    private void Export(object sender,RoutedEventArgs args)
    {
        if(rows.Count==0){MessageBox.Show(this,"当前没有通联可导出。");return;}
        var dialog=new SaveFileDialog{Filter="CSV 清单|*.csv",FileName="QSL-"+currentCall.Replace('/','_')+".csv"};
        if(dialog.ShowDialog(this)!=true)return;
        try
        {
            var lines=new List<string>{"我方呼号,友台呼号,日期UTC,时间UTC,频率或卫星,模式,发收报告,纸卡已寄,记录状态,国家,邮箱,地址,资料来源"};
            lines.AddRange(rows.Select(row=>string.Join(",",new[]{row.OwnCall,currentCall,row.Date,row.Time,row.Channel,row.Mode,row.Reports,row.Qsl,row.State,Country.Text,Email.Text,Address.Text,LookupStatus.Text}.Select(Csv))));
            File.WriteAllLines(dialog.FileName,lines,new UTF8Encoding(true));Status.Text="寄卡清单已导出；远端未返回的记录请核对后使用。";
        }
        catch(Exception){MessageBox.Show(this,"导出失败，请检查文件是否被占用。");}
    }
    private static string Csv(string value)
    {
        if(value.TrimStart() is {Length:>0} trimmed && "=+-@".Contains(trimmed[0]))value="'"+value;
        return "\""+value.Replace("\"","\"\"")+"\"";
    }
}
