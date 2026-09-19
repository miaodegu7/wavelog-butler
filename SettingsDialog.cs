using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace WavelogButler;
internal sealed class SettingsDialog : Window
{
    private readonly List<Control> inputs=[];
    public string[] Values=>inputs.Select(control=>control is PasswordBox password?password.Password:((TextBox)control).Text.Trim()).ToArray();
    public SettingsDialog(Window owner,string title,(string Label,string Value,bool Secret)[] fields,string hint)
    {
        Owner=owner;Title=title;Width=580;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=new SolidColorBrush(Color.FromRgb(243,245,250));FontFamily=owner.FontFamily;FontSize=14;
        var panel=new StackPanel{Margin=new Thickness(28)};Content=panel;
        panel.Children.Add(new TextBlock{Text=title,FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,24)});
        foreach(var field in fields)
        {
            panel.Children.Add(new TextBlock{Text=field.Label,Margin=new Thickness(0,0,0,8)});
            Control input=field.Secret?new PasswordBox{Padding=new Thickness(10)}:new TextBox{Text=field.Value,Padding=new Thickness(10)};
            input.Margin=new Thickness(0,0,0,18);inputs.Add(input);panel.Children.Add(input);
        }
        panel.Children.Add(new TextBlock{Text=hint,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.SlateGray,Margin=new Thickness(0,0,0,20)});
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        var cancel=new Button{Content="取消",Padding=new Thickness(24,10,24,10),Margin=new Thickness(0,0,10,0),IsCancel=true};
        var save=new Button{Content="保存",Padding=new Thickness(24,10,24,10),IsDefault=true};save.Click+=(_,_)=>DialogResult=true;
        buttons.Children.Add(cancel);buttons.Children.Add(save);panel.Children.Add(buttons);
    }
}
