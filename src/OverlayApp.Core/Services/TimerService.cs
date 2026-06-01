using System;
using System.Collections.Generic;
using System.Linq;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;

namespace OverlayApp.Core.Services;

public enum TimerState
{
    Idle,
    Running,
    Paused,
}

public sealed class TimerRuntime
{
    public TimerInstance Spec { get; internal set; }

    public TimerState State { get; internal set; } = TimerState.Idle;

    internal DateTime? TargetEnd;
    internal TimeSpan? FrozenRemaining;

    public TimerRuntime(TimerInstance spec) => Spec = spec;

    internal string GetModeLabel()
    {
        if (!string.IsNullOrWhiteSpace(Spec.Label)) return Spec.Label;
        return Spec.Mode == TimerMode.Duration
            ? $"{Spec.DurationMinutes}분 타이머"
            : $"{Spec.ClockTimeHour:D2}:{Spec.ClockTimeMinute:D2} 알람";
    }

    internal void StartFresh()
    {
        var now = DateTime.Now;
        if (Spec.Mode == TimerMode.Duration)
        {
            TargetEnd = now.AddMinutes(System.Math.Max(0, Spec.DurationMinutes));
        }
        else
        {
            var target = new DateTime(now.Year, now.Month, now.Day,
                System.Math.Clamp(Spec.ClockTimeHour, 0, 23),
                System.Math.Clamp(Spec.ClockTimeMinute, 0, 59), 0);
            if (target <= now) target = target.AddDays(1);
            TargetEnd = target;
        }
        FrozenRemaining = null;
        State = TimerState.Running;
    }

    internal void Pause()
    {
        if (State != TimerState.Running || TargetEnd is not { } end) return;
        var remaining = end - DateTime.Now;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        FrozenRemaining = remaining;
        // Duration은 target을 잊고 resume 시 재계산. ClockTime은 target 유지.
        if (Spec.Mode == TimerMode.Duration) TargetEnd = null;
        State = TimerState.Paused;
    }

    internal void Resume()
    {
        if (State != TimerState.Paused) return;
        if (Spec.Mode == TimerMode.Duration && FrozenRemaining is { } r)
        {
            TargetEnd = DateTime.Now.Add(r);
        }
        FrozenRemaining = null;
        State = TimerState.Running;
    }

    internal void ResetToIdle()
    {
        State = TimerState.Idle;
        TargetEnd = null;
        FrozenRemaining = null;
    }

    public string GetDisplayText()
    {
        if (State == TimerState.Idle) return string.Empty;

        TimeSpan remaining;
        if (State == TimerState.Running && TargetEnd is { } end)
        {
            remaining = end - DateTime.Now;
        }
        else if (State == TimerState.Paused && FrozenRemaining is { } fr)
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

        var prefix = GetModeLabel();
        return State == TimerState.Paused
            ? $"{prefix}  일시정지 {formatted}"
            : $"{prefix}  남은 {formatted}";
    }
}

/// <summary>
/// 여러 개의 타이머를 동시 관리. 단축키 토글은 일괄 동작.
/// </summary>
public sealed class TimerService : IDisposable
{
    private readonly ClockService _clock;
    private readonly IAlarmService _alarm;
    private readonly AppSettings _settings;

    private readonly Dictionary<string, TimerRuntime> _runtimes = new();

    public event EventHandler? Changed;

    public TimerService(ClockService clock, IAlarmService alarm, AppSettings settings)
    {
        _clock = clock;
        _alarm = alarm;
        _settings = settings;
        Sync();
        _clock.Tick += OnTick;
    }

    public IReadOnlyCollection<TimerRuntime> Runtimes => _runtimes.Values;

    public bool HasAnyActive => _runtimes.Values.Any(r => r.State != TimerState.Idle);

    public bool HasAnyRunning => _runtimes.Values.Any(r => r.State == TimerState.Running);

    public bool HasAnyPaused => _runtimes.Values.Any(r => r.State == TimerState.Paused);

    /// <summary>설정의 Items 목록과 내부 runtime 맵을 동기화. 추가/제거된 항목 반영.</summary>
    public void Sync()
    {
        var ids = _settings.Timer.Items.Select(i => i.Id).ToHashSet();
        foreach (var key in _runtimes.Keys.ToList())
        {
            if (!ids.Contains(key)) _runtimes.Remove(key);
        }
        foreach (var item in _settings.Timer.Items)
        {
            if (_runtimes.TryGetValue(item.Id, out var rt))
            {
                rt.Spec = item; // label/mode/시간 변경 가능
            }
            else
            {
                _runtimes[item.Id] = new TimerRuntime(item);
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 일괄 토글:
    /// - 하나라도 Running → 모두 Pause
    /// - 모두 Paused (또는 Paused+Idle 섞임) → Paused를 Resume
    /// - 모두 Idle → 모두 StartFresh
    /// </summary>
    public void Toggle()
    {
        Sync();
        if (_runtimes.Count == 0) return;

        if (HasAnyRunning)
        {
            foreach (var rt in _runtimes.Values) rt.Pause();
        }
        else if (HasAnyPaused)
        {
            foreach (var rt in _runtimes.Values) rt.Resume();
        }
        else
        {
            foreach (var rt in _runtimes.Values) rt.StartFresh();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void StartAll()
    {
        Sync();
        foreach (var rt in _runtimes.Values) rt.StartFresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void StopAll()
    {
        if (!HasAnyActive) return;
        foreach (var rt in _runtimes.Values) rt.ResetToIdle();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!HasAnyActive) return;

        var toFire = new List<TimerRuntime>();
        foreach (var rt in _runtimes.Values)
        {
            if (rt.State == TimerState.Running && rt.TargetEnd is { } end && DateTime.Now >= end)
            {
                toFire.Add(rt);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);

        foreach (var rt in toFire)
        {
            var label = rt.GetModeLabel();
            _alarm.Fire("타이머 종료", $"{label} ({DateTime.Now:HH:mm:ss})", _settings.Timer.SoundEnabled);
            rt.ResetToIdle();
        }

        if (toFire.Count > 0) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _clock.Tick -= OnTick;
    }
}
