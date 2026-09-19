namespace WavelogButler;
internal sealed class AccountDialog : Form
{
    private readonly TextBox name = new() { Dock = DockStyle.Fill };
    private readonly TextBox url = new() { Dock = DockStyle.Fill, PlaceholderText = "https://example.com/wavelog" };
    private readonly TextBox key = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    public string AccountName => name.Text.Trim();
    public string ServerUrl => url.Text.Trim().TrimEnd('/');
    public string ApiKey => key.Text.Trim();
    public AccountDialog(Account? account)
    {
        Text = account == null ? "添加 Wavelog 账号" : "编辑账号（更换来源会清空旧缓存）";
        ClientSize = new Size(600, 280);
        Font = new Font("Microsoft YaHei UI", 10);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var (label, control) in new (string, Control)[] { ("账号备注", name), ("网站根地址", url), ("只读 API Key", key) })
        {
            var row = layout.Controls.Count / 2;
            layout.Controls.Add(new Label { Text = label, AutoSize = true }, 0, row);
            layout.Controls.Add(control, 1, row);
        }
        var hint = new Label { Text = "密钥仅在本机使用 Windows 加密保存。编辑时留空表示保留。\n使用 HTTPS 根地址，不要填 /api；请关闭 Wavelog 的仅活动日志限制，以获取全部台站。", AutoSize = true, MaximumSize = new Size(550, 0) };
        layout.Controls.Add(hint, 0, 3);
        layout.SetColumnSpan(hint, 2);
        var save = new Button { Text = "保存", AutoSize = true };
        save.Click += (_, _) =>
        {
            if (AccountName.Length == 0 || (account == null && ApiKey.Length == 0)) { MessageBox.Show(this, "请填写账号备注和 API 密钥。"); return; }
            if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var address) || address.Scheme != "https" || address.UserInfo.Length > 0 || address.Query.Length > 0 || address.Fragment.Length > 0)
            { MessageBox.Show(this, "请输入不含用户名、密码、查询参数的 HTTPS 网站根地址。"); return; }
            DialogResult = DialogResult.OK;
        };
        layout.Controls.Add(save, 1, 4);
        Controls.Add(layout);
        AcceptButton = save;
        name.Text = account?.Name ?? "";
        url.Text = account?.Url ?? "";
    }
}
