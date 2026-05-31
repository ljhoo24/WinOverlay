using System;
using Avalonia;
using OverlayApp.Avalonia.Views;
using OverlayApp.Core.Abstractions;

namespace OverlayApp.Avalonia.Platform;

public sealed class AvaloniaOverlayController : IOverlayController
{
    private OverlayWindow? _window;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _desiredClickThrough = true;

    public event EventHandler? PositionChanged;

    public event EventHandler? SizeChanged;

    public void Attach(OverlayWindow window)
    {
        _window = window;
        _window.Opened += OnWindowOpened;
        _window.PositionChanged += (_, _) => PositionChanged?.Invoke(this, EventArgs.Empty);
        _window.Resized += (_, _) => SizeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        var handle = _window?.TryGetPlatformHandle();
        if (handle is null) return;
        _hwnd = handle.Handle;

        Win32Interop.AddExStyle(_hwnd, Win32Interop.WS_EX_TOOLWINDOW | Win32Interop.WS_EX_NOACTIVATE);
        ApplyClickThrough();
    }

    public bool IsVisible => _window?.IsVisible ?? false;

    public void Show() => _window?.Show();

    public void Hide() => _window?.Hide();

    public void SetClickThrough(bool enabled)
    {
        _desiredClickThrough = enabled;
        if (_hwnd != IntPtr.Zero) ApplyClickThrough();
    }

    private void ApplyClickThrough()
    {
        if (_desiredClickThrough)
        {
            Win32Interop.AddExStyle(_hwnd, Win32Interop.WS_EX_TRANSPARENT | Win32Interop.WS_EX_LAYERED);
        }
        else
        {
            Win32Interop.RemoveExStyle(_hwnd, Win32Interop.WS_EX_TRANSPARENT);
        }
    }

    public void SetOpacity(double value)
    {
        if (_window is null) return;
        _window.Opacity = Math.Clamp(value, 0.0, 1.0);
    }

    public void SetTopMost(bool enabled)
    {
        if (_window is null) return;
        _window.Topmost = enabled;
    }

    public (double X, double Y) GetPosition()
    {
        if (_window is null) return (0, 0);
        return (_window.Position.X, _window.Position.Y);
    }

    public void SetPosition(double x, double y)
    {
        if (_window is null) return;
        _window.Position = new PixelPoint((int)x, (int)y);
    }

    public (double Width, double Height) GetSize()
    {
        if (_window is null) return (0, 0);
        return (_window.Width, _window.Height);
    }

    public void SetSize(double width, double height)
    {
        if (_window is null) return;
        if (width > 0) _window.Width = width;
        if (height > 0) _window.Height = height;
    }
}
