using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OverlayApp.Core.ViewModels;

namespace OverlayApp.Avalonia.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
