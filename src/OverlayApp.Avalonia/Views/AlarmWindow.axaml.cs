using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OverlayApp.Avalonia.Views;

public partial class AlarmWindow : Window
{
    public AlarmWindow()
    {
        InitializeComponent();
    }

    public AlarmWindow(string title, string message) : this()
    {
        var t = this.FindControl<TextBlock>("TitleText");
        var m = this.FindControl<TextBlock>("MessageText");
        if (t is not null) t.Text = title;
        if (m is not null) m.Text = message;
    }

    private void OnConfirm(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
