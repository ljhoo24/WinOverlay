using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using OverlayApp.Core.ViewModels;

namespace OverlayApp.Avalonia.Views;

public partial class OverlayWindow : Window
{
    private Border? _rootBorder;

    public OverlayWindow()
    {
        InitializeComponent();
    }

    public OverlayWindow(OverlayViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is OverlayViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateAdjustVisual(vm.IsAdjustMode);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OverlayViewModel.IsAdjustMode) && DataContext is OverlayViewModel vm)
        {
            UpdateAdjustVisual(vm.IsAdjustMode);
        }
    }

    private void UpdateAdjustVisual(bool isAdjustMode)
    {
        _rootBorder ??= this.FindControl<Border>("RootBorder");
        if (_rootBorder is null) return;
        _rootBorder.Tag = isAdjustMode ? "adjust" : null;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is OverlayViewModel vm && vm.IsAdjustMode && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
