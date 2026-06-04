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

    // 복원 값. 창이 열리기 전에 들어오면 보관했다가 Opened에서 적용.
    private double? _wantX, _wantY, _wantW, _wantH;

    // 창 Opened 이후에만 위치/크기 변경을 바깥으로 알린다(시작 시 복원 노이즈 저장 방지).
    private bool _ready;

    public event EventHandler? PositionChanged;

    public event EventHandler? SizeChanged;

    public void Attach(OverlayWindow window)
    {
        _window = window;
        _window.Opened += OnWindowOpened;
        _window.PositionChanged += (_, _) => { if (_ready) PositionChanged?.Invoke(this, EventArgs.Empty); };
        _window.Resized += (_, _) => { if (_ready) SizeChanged?.Invoke(this, EventArgs.Empty); };
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        var handle = _window?.TryGetPlatformHandle();
        if (handle is null) return;
        _hwnd = handle.Handle;

        Win32Interop.AddExStyle(_hwnd, Win32Interop.WS_EX_TOOLWINDOW | Win32Interop.WS_EX_NOACTIVATE);
        ApplyClickThrough();

        // 핸들이 생긴 뒤에 크기 → 위치 순으로 복원 (위치를 나중에 잡아 WM 이동 보정).
        ApplySize();
        ApplyPosition();

        _ready = true;
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
        _wantX = x;
        _wantY = y;
        if (_ready) ApplyPosition();
    }

    private void ApplyPosition()
    {
        if (_window is null || _wantX is not double wx || _wantY is not double wy) return;

        var x = (int)Math.Round(wx);
        var y = (int)Math.Round(wy);

        // 멀티 모니터: 저장된 좌표가 현재 화면 밖이면 보이는 영역 안으로 보정.
        try
        {
            var screens = _window.Screens;
            if (screens is not null && screens.All.Count > 0)
            {
                var scale = _window.RenderScaling;
                if (scale <= 0) scale = 1;
                var wpx = (int)Math.Ceiling((_wantW ?? _window.Width) * scale);
                var hpx = (int)Math.Ceiling((_wantH ?? _window.Height) * scale);

                var pt = new PixelPoint(x, y);
                var target = screens.ScreenFromPoint(pt) ?? screens.Primary ?? screens.All[0];
                var area = target.WorkingArea;

                var maxX = area.X + Math.Max(0, area.Width - wpx);
                var maxY = area.Y + Math.Max(0, area.Height - hpx);
                x = Math.Clamp(x, area.X, maxX);
                y = Math.Clamp(y, area.Y, maxY);
            }
        }
        catch
        {
            // 화면 정보 못 읽어도 저장값 그대로 사용.
        }

        _window.Position = new PixelPoint(x, y);
    }

    public (double Width, double Height) GetSize()
    {
        if (_window is null) return (0, 0);
        return (_window.Width, _window.Height);
    }

    public void SetSize(double width, double height)
    {
        if (width > 0) _wantW = width;
        if (height > 0) _wantH = height;
        if (_ready) ApplySize();
    }

    private void ApplySize()
    {
        if (_window is null) return;
        if (_wantW is double w && w > 0) _window.Width = w;
        if (_wantH is double h && h > 0) _window.Height = h;
    }
}
