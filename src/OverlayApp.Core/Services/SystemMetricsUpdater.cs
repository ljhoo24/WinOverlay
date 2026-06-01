using System;
using System.Threading;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;

namespace OverlayApp.Core.Services;

/// <summary>
/// 전용 백그라운드 타이머로 시스템 지표를 주기적으로 읽어 UI 스레드로 전달한다.
/// 날씨 갱신과 동일한 패턴. 활성화 상태가 아니면 폴링을 멈춘다.
/// </summary>
public sealed class SystemMetricsUpdater : IDisposable
{
    private readonly ISystemMetricsService _service;
    private readonly IUiDispatcher _dispatcher;
    private readonly AppSettings _settings;
    private readonly Timer _timer;
    private int _reading;

    public SystemMetrics? Latest { get; private set; }

    public event EventHandler? Updated;

    public SystemMetricsUpdater(
        ISystemMetricsService service,
        IUiDispatcher dispatcher,
        AppSettings settings)
    {
        _service = service;
        _dispatcher = dispatcher;
        _settings = settings;
        _timer = new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>설정 변경 후 호출. Enabled 여부에 따라 폴링을 켜고 끈다.</summary>
    public void Apply()
    {
        if (_settings.System.Enabled)
        {
            var ms = Math.Max(1, _settings.System.RefreshSeconds) * 1000;
            _timer.Change(0, ms);
        }
        else
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            Latest = null;
            _dispatcher.Post(() => Updated?.Invoke(this, EventArgs.Empty));
        }
    }

    private void OnTimer(object? state)
    {
        // 폴링이 겹치지 않게(LHM Update가 느릴 수 있음) 재진입 방지.
        if (Interlocked.Exchange(ref _reading, 1) == 1) return;
        try
        {
            var metrics = _service.Read(_settings.System.AnyTempRequested);
            Latest = metrics;
            _dispatcher.Post(() => Updated?.Invoke(this, EventArgs.Empty));
        }
        catch
        {
            // 지표 읽기 실패가 앱을 멈추게 하지 않는다.
        }
        finally
        {
            Interlocked.Exchange(ref _reading, 0);
        }
    }

    public void Dispose() => _timer.Dispose();
}
