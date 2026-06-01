using System;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;

namespace OverlayApp.Core.Services;

public enum TimerState
{
    Idle,
    Running,
    Paused,
}

public sealed class TimerService : IDisposable
{
    private readonly ClockService _clock;
    private readonly IAlarmService _alarm;
    private readonly AppSettings _settings;

    private TimerState _state = TimerState.Idle;
    private TimerMode _currentMode;
    private DateTime? _targetEnd;
    private TimeSpan? _frozenRemaining;
    private string _modeLabel = string.Empty;

    public TimerState State => _state;

    public bool IsActive => _state != TimerState.Idle;

    public event EventHandler? Changed;

    public TimerService(ClockService clock, IAlarmService alarm, AppSettings settings)
    {
        _clock = clock;
        _alarm = alarm;
        _settings = settings;
        _clock.Tick += OnTick;
    }

    /// <summary>
    /// 단일 단축키 토글: Idle→시작, Running→일시정지, Paused→재개.
    /// </summary>
    public void Toggle()
    {
        switch (_state)
        {
            case TimerState.Idle:
                StartFresh();
                break;
            case TimerState.Running:
                Pause();
                break;
            case TimerState.Paused:
                Resume();
                break;
        }
    }

    public void StartFresh()
    {
        var s = _settings.Timer;
        var now = DateTime.Now;
        _currentMode = s.Mode;

        if (s.Mode == TimerMode.Duration)
        {
            var minutes = System.Math.Max(0, s.DurationMinutes);
            _targetEnd = now.AddMinutes(minutes);
            _modeLabel = $"{minutes}분 타이머";
        }
        else
        {
            var target = new DateTime(now.Year, now.Month, now.Day,
                System.Math.Clamp(s.ClockTimeHour, 0, 23),
                System.Math.Clamp(s.ClockTimeMinute, 0, 59), 0);
            if (target <= now) target = target.AddDays(1);
            _targetEnd = target;
            _modeLabel = target.ToString("HH:mm 알람");
        }

        _frozenRemaining = null;
        _state = TimerState.Running;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        if (_state != TimerState.Running || _targetEnd is not { } end) return;

        var remaining = end - DateTime.Now;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        _frozenRemaining = remaining;

        // Duration 모드는 target을 잊는다 (resume 시 now 기준으로 재계산).
        // ClockTime 모드는 target 시각을 그대로 유지한다 (resume 시 그 시각까지의 남은 시간이 자동 계산).
        if (_currentMode == TimerMode.Duration)
        {
            _targetEnd = null;
        }

        _state = TimerState.Paused;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Resume()
    {
        if (_state != TimerState.Paused) return;

        if (_currentMode == TimerMode.Duration && _frozenRemaining is { } r)
        {
            _targetEnd = DateTime.Now.Add(r);
        }
        // ClockTime: _targetEnd는 pause 동안 유지되었으므로 그대로 사용.

        _frozenRemaining = null;
        _state = TimerState.Running;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>완전 중단: Idle로 되돌린다. 단축키엔 매핑되지 않고 설정창 "정지" 버튼 전용.</summary>
    public void Stop()
    {
        if (_state == TimerState.Idle) return;
        _state = TimerState.Idle;
        _targetEnd = null;
        _frozenRemaining = null;
        _modeLabel = string.Empty;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public string GetDisplayText()
    {
        if (_state == TimerState.Idle) return string.Empty;

        TimeSpan remaining;
        if (_state == TimerState.Running && _targetEnd is { } end)
        {
            remaining = end - DateTime.Now;
        }
        else if (_state == TimerState.Paused && _frozenRemaining is { } fr)
        {
            remaining = fr;
        }
        else
        {
            return string.Empty;
        }

        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        var formatted = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";

        return _state == TimerState.Paused
            ? $"{_modeLabel}  일시정지 {formatted}"
            : $"{_modeLabel}  남은 {formatted}";
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_state == TimerState.Idle) return;

        Changed?.Invoke(this, EventArgs.Empty);

        if (_state == TimerState.Running && _targetEnd is { } end && DateTime.Now >= end)
        {
            var firedLabel = _modeLabel;
            _alarm.Fire("타이머 종료", $"{firedLabel} ({DateTime.Now:HH:mm:ss})", _settings.Timer.SoundEnabled);
            _state = TimerState.Idle;
            _targetEnd = null;
            _frozenRemaining = null;
            _modeLabel = string.Empty;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        _clock.Tick -= OnTick;
    }
}
