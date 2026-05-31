using System;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;

namespace OverlayApp.Core.Services;

public sealed class TimerService : IDisposable
{
    private readonly ClockService _clock;
    private readonly IAlarmService _alarm;
    private readonly AppSettings _settings;

    private DateTime? _targetEnd;
    private string _modeAtStart = string.Empty;

    public bool IsRunning => _targetEnd.HasValue;

    public event EventHandler? Changed;

    public TimerService(ClockService clock, IAlarmService alarm, AppSettings settings)
    {
        _clock = clock;
        _alarm = alarm;
        _settings = settings;
        _clock.Tick += OnTick;
    }

    public void Start()
    {
        var s = _settings.Timer;
        var now = DateTime.Now;

        if (s.Mode == TimerMode.Duration)
        {
            var minutes = System.Math.Max(0, s.DurationMinutes);
            _targetEnd = now.AddMinutes(minutes);
            _modeAtStart = $"{minutes}분 타이머";
        }
        else
        {
            var target = new DateTime(now.Year, now.Month, now.Day,
                System.Math.Clamp(s.ClockTimeHour, 0, 23),
                System.Math.Clamp(s.ClockTimeMinute, 0, 59), 0);
            // If the target time today has already passed, schedule for tomorrow.
            if (target <= now) target = target.AddDays(1);
            _targetEnd = target;
            _modeAtStart = target.ToString("HH:mm 알람");
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _targetEnd = null;
        _modeAtStart = string.Empty;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public string GetDisplayText()
    {
        if (_targetEnd is not { } end) return string.Empty;
        var remaining = end - DateTime.Now;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        var formatted = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        return $"{_modeAtStart}  남은 {formatted}";
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_targetEnd is not { } end) return;

        Changed?.Invoke(this, EventArgs.Empty);

        if (DateTime.Now >= end)
        {
            var firedTitle = _modeAtStart;
            _alarm.Fire("타이머 종료", $"{firedTitle} ({DateTime.Now:HH:mm:ss})", _settings.Timer.SoundEnabled);
            _targetEnd = null;
            _modeAtStart = string.Empty;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        _clock.Tick -= OnTick;
    }
}
