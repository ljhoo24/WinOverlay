using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using OverlayApp.Core.ViewModels;

namespace OverlayApp.Avalonia.Views;

public partial class OverlayWindow : Window
{
    private const double EdgeThickness = 6;

    private Border? _rootBorder;

    private static readonly Cursor DefaultCursor = new(StandardCursorType.Arrow);
    private static readonly Cursor NsCursor = new(StandardCursorType.SizeNorthSouth);
    private static readonly Cursor WeCursor = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor NwseCursor = new(StandardCursorType.TopLeftCorner);
    private static readonly Cursor NeswCursor = new(StandardCursorType.TopRightCorner);

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
            if (!vm.IsAdjustMode) Cursor = DefaultCursor;
        }
    }

    private void UpdateAdjustVisual(bool isAdjustMode)
    {
        _rootBorder ??= this.FindControl<Border>("RootBorder");
        if (_rootBorder is null) return;
        _rootBorder.Tag = isAdjustMode ? "adjust" : null;
    }

    private bool IsAdjustMode => DataContext is OverlayViewModel { IsAdjustMode: true };

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsAdjustMode)
        {
            if (Cursor != DefaultCursor) Cursor = DefaultCursor;
            return;
        }

        var (edge, cursor) = HitTestEdge(e);
        Cursor = cursor;
        // edge is consumed only on pressed event
        _ = edge;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsAdjustMode) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var (edge, _) = HitTestEdge(e);
        if (edge is { } we)
        {
            BeginResizeDrag(we, e);
        }
        else
        {
            BeginMoveDrag(e);
        }
    }

    private (WindowEdge? edge, Cursor cursor) HitTestEdge(PointerEventArgs e)
    {
        var p = e.GetPosition(this);
        var w = Bounds.Width;
        var h = Bounds.Height;

        var left = p.X <= EdgeThickness;
        var right = p.X >= w - EdgeThickness;
        var top = p.Y <= EdgeThickness;
        var bottom = p.Y >= h - EdgeThickness;

        if (top && left) return (WindowEdge.NorthWest, NwseCursor);
        if (top && right) return (WindowEdge.NorthEast, NeswCursor);
        if (bottom && left) return (WindowEdge.SouthWest, NeswCursor);
        if (bottom && right) return (WindowEdge.SouthEast, NwseCursor);
        if (top) return (WindowEdge.North, NsCursor);
        if (bottom) return (WindowEdge.South, NsCursor);
        if (left) return (WindowEdge.West, WeCursor);
        if (right) return (WindowEdge.East, WeCursor);
        return (null, DefaultCursor);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
