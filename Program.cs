using System.Windows;
namespace WavelogButler;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try { var app = new Application(); app.Run(new MainWindow()); }
        catch (Exception exception) { MessageBox.Show("启动失败，数据未删除：" + exception.Message, "Wavelog 管家"); }
    }
}
