using System;
using System.Threading;
using OverlayApp.Core.Abstractions;

namespace OverlayApp.Core.Services;

public sealed class ClockService : IDisposable
{
    private readonly IUiDispatcher _dispatcher;
    private readonly Timer _timer;

    public event EventHandler? Tick;

    public ClockService(IUiDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _timer = new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        var now = DateTime.Now;
        var msUntilNextSecond = 1000 - now.Millisecond;
        _timer.Change(msUntilNextSecond, 1000);
    }

    public void Stop() => _timer.Change(Timeout.Infinite, Timeout.Infinite);

    private void OnTimer(object? state)
    {
        _dispatcher.Post(() => Tick?.Invoke(this, EventArgs.Empty));
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
