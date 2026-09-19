namespace WavelogButler;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try { Application.Run(new MainForm()); }
        catch (Exception) { MessageBox.Show("启动失败，请检查本地数据文件是否损坏或被占用。数据未自动删除。", "Wavelog 管家"); }
    }
}
