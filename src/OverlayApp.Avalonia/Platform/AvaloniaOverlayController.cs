using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Threading;
using OverlayApp.Avalonia.Views;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;

namespace OverlayApp.Avalonia.Platform;

public sealed class AvaloniaOverlayController : IOverlayController
{
    private OverlayWindow? _window;
    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _nsWindow = IntPtr.Zero;
    private bool _desiredClickThrough = true;
    private bool _desiredTopmost = true;
#if WINDOWS
    private DispatcherTimer? _topmostTimer;
#endif

    // 복원 값. 창이 열리기 전에 들어오면 보관했다가 Opened에서 적용.
    private double? _wantX, _wantY, _wantW, _wantH;

    // 창 Opened 이후에만 위치/크기 변경을 바깥으로 알린다(시작 시 복원 노이즈 저장 방지).
    private bool _ready;

    // 프로그램이 위치/크기를 적용하는 동안엔 사용자 변경으로 오인해 저장하지 않게 막는다.
    private bool _applying;

    public event EventHandler? PositionChanged;

    public event EventHandler? SizeChanged;

    public void Attach(OverlayWindow window)
    {
        _window = window;
        _window.Opened += OnWindowOpened;
        // 사용자 이동/리사이즈 시 실제값을 _want*에 반영해, 숨김→표시나 재시작에서
        // 마지막 상태로 정확히 복원되게 한다(드래그는 _window.Width 등을 갱신하지 않음).
        _window.PositionChanged += (_, _) =>
        {
            if (!_ready || _applying) return;
            var p = _window.Position;
            _wantX = p.X;
            _wantY = p.Y;
            PositionChanged?.Invoke(this, EventArgs.Empty);
        };
        _window.Resized += (_, _) =>
        {
            if (!_ready || _applying) return;
            var cs = _window.ClientSize;
            if (cs.Width > 0) _wantW = cs.Width;
            if (cs.Height > 0) _wantH = cs.Height;
            SizeChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        var handle = _window?.TryGetPlatformHandle();
        if (handle is null) return;
        _hwnd = handle.Handle;

#if WINDOWS
        Win32Interop.AddExStyle(_hwnd, Win32Interop.WS_EX_TOOLWINDOW | Win32Interop.WS_EX_NOACTIVATE);
#else
        if (handle is global::Avalonia.Platform.IMacOSTopLevelPlatformHandle mac)
        {
            _nsWindow = mac.NSWindow;
        }
        if (_desiredTopmost) MacInterop.SetStatusLevel(_nsWindow);
#endif
        ApplyClickThrough();

        // 핸들이 생긴 뒤에 크기 → 위치 순으로 복원.
        ApplySize();
        ApplyPosition(forceVisible: false);

        _ready = true;

        // Topmost는 다른 앱이 자기 창을 HWND_TOPMOST로 올리면 그 아래로 밀릴 수 있다
        // (특히 WS_EX_NOACTIVATE라 활성화로 z-order가 안 올라옴). 주기적으로 재선언.
        StartTopmostWatch();

        // 부팅 자동시작 시 보조 모니터가 늦게 붙는 경우, 복원 시점엔 해당 화면이
        // 아직 없어서 좌표가 화면 밖으로 판정될 수 있다. 잠시 동안 몇 번 더
        // 재적용하여 모니터가 준비된 뒤 원래 자리로 맞춘다. 마지막 시도에서만
        // 그래도 화면 밖이면 Primary로 강제 보정.
        ScheduleRestoreRetries();
    }

    private void ScheduleRestoreRetries()
    {
        var attempts = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        timer.Tick += (_, _) =>
        {
            attempts++;
            var last = attempts >= 4;
            ApplySize();
            ApplyPosition(forceVisible: last);
            if (last) timer.Stop();
        };
        timer.Start();
    }

    public bool IsVisible => _window?.IsVisible ?? false;

    public void Show()
    {
        if (_window is null) return;
        _window.Show();
        // Avalonia가 표시 시 XAML 기본 크기로 되돌릴 수 있어, 마지막 크기/위치를 재적용.
        if (_ready)
        {
            ApplySize();
            ApplyPosition(forceVisible: true);
        }
    }

    public void Hide() => _window?.Hide();

    public void SetClickThrough(bool enabled)
    {
        _desiredClickThrough = enabled;
        if (_hwnd != IntPtr.Zero) ApplyClickThrough();
    }

    private void ApplyClickThrough()
    {
#if WINDOWS
        if (_desiredClickThrough)
        {
            Win32Interop.AddExStyle(_hwnd, Win32Interop.WS_EX_TRANSPARENT | Win32Interop.WS_EX_LAYERED);
        }
        else
        {
            Win32Interop.RemoveExStyle(_hwnd, Win32Interop.WS_EX_TRANSPARENT);
        }
#else
        MacInterop.SetIgnoresMouseEvents(_nsWindow, _desiredClickThrough);
#endif
    }

    public void SetOpacity(double value)
    {
        if (_window is null) return;
        _window.Opacity = Math.Clamp(value, 0.0, 1.0);
    }

    public void SetTopMost(bool enabled)
    {
        _desiredTopmost = enabled;
        if (_window is null) return;
        _window.Topmost = enabled;
#if WINDOWS
        if (enabled) Win32Interop.ReassertTopmost(_hwnd);
#else
        if (enabled) MacInterop.SetStatusLevel(_nsWindow);
#endif
    }

    private void StartTopmostWatch()
    {
#if WINDOWS
        _topmostTimer?.Stop();
        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _topmostTimer.Tick += (_, _) =>
        {
            if (_desiredTopmost && _hwnd != IntPtr.Zero && (_window?.IsVisible ?? false))
                Win32Interop.ReassertTopmost(_hwnd);
        };
        _topmostTimer.Start();
#else
        // macOS는 NSWindow.level이 유지되므로 주기 재선언이 필요 없다.
#endif
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
        if (_ready) ApplyPosition(forceVisible: true);
    }

    private void ApplyPosition(bool forceVisible)
    {
        if (_window is null || _wantX is not double wx || _wantY is not double wy) return;

        var x = (int)Math.Round(wx);
        var y = (int)Math.Round(wy);

        try
        {
            var screens = _window.Screens;
            if (screens is not null && screens.All.Count > 0)
            {
                var pt = new PixelPoint(x, y);
                var target = screens.ScreenFromPoint(pt);

                // 좌표를 담은 화면이 아직 없으면(부팅 시 보조 모니터 미준비):
                //  - 일반 시도: 손대지 않고 저장값 그대로 둔다(다음 재시도에서 맞춤).
                //  - 마지막 시도/사용자 호출: Primary로 강제 보정해 최소한 보이게.
                if (target is null)
                {
                    if (!forceVisible)
                    {
                        SetPositionRaw(x, y);
                        return;
                    }
                    target = screens.Primary ?? screens.All[0];
                }

                var scale = _window.RenderScaling;
                if (scale <= 0) scale = 1;
                var wpx = (int)Math.Ceiling((_wantW ?? _window.Width) * scale);
                var hpx = (int)Math.Ceiling((_wantH ?? _window.Height) * scale);

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

        SetPositionRaw(x, y);
    }

    private void SetPositionRaw(int x, int y)
    {
        if (_window is null) return;
        BeginApplying();
        _window.Position = new PixelPoint(x, y);
        EndApplyingDeferred();
    }

    // 위치/크기 변경은 플랫폼에서 비동기로 되돌아올 수 있어, 다음 디스패처
    // 사이클까지 _applying을 유지해 프로그램 변경이 저장으로 오인되지 않게 한다.
    private void BeginApplying() => _applying = true;

    private void EndApplyingDeferred()
        => Dispatcher.UIThread.Post(() => _applying = false, DispatcherPriority.Background);

    public (double Width, double Height) GetSize()
    {
        if (_window is null) return (0, 0);
        // 드래그 리사이즈는 Width/Height를 갱신하지 않으므로 실제 크기(ClientSize)를 반환.
        var cs = _window.ClientSize;
        return (cs.Width, cs.Height);
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
        BeginApplying();
        if (_wantW is double w && w > 0) _window.Width = w;
        if (_wantH is double h && h > 0) _window.Height = h;
        EndApplyingDeferred();
    }

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        var screens = _window?.Screens;
        if (screens is null) return list;
        for (var i = 0; i < screens.All.Count; i++)
        {
            var s = screens.All[i];
            list.Add(new MonitorInfo
            {
                Index = i,
                IsPrimary = s.IsPrimary,
                PixelWidth = s.Bounds.Width,
                PixelHeight = s.Bounds.Height,
            });
        }
        return list;
    }

    public void SnapToCorner(int monitorIndex, OverlayCorner corner, int marginDip)
    {
        if (_window is null) return;
        var screens = _window.Screens;
        if (screens is null || screens.All.Count == 0) return;

        var idx = Math.Clamp(monitorIndex, 0, screens.All.Count - 1);
        var screen = screens.All[idx];

        var scale = screen.Scaling;
        if (scale <= 0) scale = 1;

        // WorkingArea는 작업표시줄 제외 영역이라 여백 0이어도 작업표시줄과 안 겹침.
        var margin = Math.Clamp(marginDip, 0, 20);
        var marginPx = (int)Math.Round(margin * scale);

        // 사용자가 테두리 드래그로 리사이즈하면 _wantW/_wantH는 갱신되지 않으므로
        // 실제 현재 크기(ClientSize)를 우선 사용해야 구석에 정확히 맞는다.
        var cs = _window.ClientSize;
        var w = cs.Width > 0 ? cs.Width : (_wantW ?? _window.Width);
        var h = cs.Height > 0 ? cs.Height : (_wantH ?? _window.Height);
        var wpx = (int)Math.Ceiling(w * scale);
        var hpx = (int)Math.Ceiling(h * scale);

        var (ax, ay, aw, ah) = GetWorkArea(screen);
        var left = ax + marginPx;
        var top = ay + marginPx;
        var right = ax + Math.Max(0, aw - wpx - marginPx);
        var bottom = ay + Math.Max(0, ah - hpx - marginPx);

        var (x, y) = corner switch
        {
            OverlayCorner.TopLeft => (left, top),
            OverlayCorner.TopRight => (right, top),
            OverlayCorner.BottomLeft => (left, bottom),
            OverlayCorner.BottomRight => (right, bottom),
            _ => (left, top),
        };

        _wantX = x;
        _wantY = y;
        SetPositionRaw(x, y);
    }

    /// <summary>대상 모니터의 작업영역(작업표시줄 제외)을 물리 픽셀로 반환.</summary>
    private static (int X, int Y, int W, int H) GetWorkArea(global::Avalonia.Platform.Screen screen)
    {
#if WINDOWS
        // Avalonia WorkingArea가 작업표시줄(특히 상단/측면)을 반영 못하는 환경 대비,
        // Win32 GetMonitorInfo.rcWork로 진짜 작업영역을 조회한다.
        var b = screen.Bounds;
        var cx = b.X + (b.Width / 2);
        var cy = b.Y + (b.Height / 2);
        if (Win32Interop.TryGetWorkArea(cx, cy, out var x, out var y, out var w, out var h))
            return (x, y, w, h);
#endif
        var wa = screen.WorkingArea;
        return (wa.X, wa.Y, wa.Width, wa.Height);
    }
}
