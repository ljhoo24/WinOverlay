using System;
using CommunityToolkit.Mvvm.ComponentModel;
using OverlayApp.Core.Models;

namespace OverlayApp.Core.ViewModels;

public sealed partial class TimerInstanceViewModel : ObservableObject
{
    public string Id { get; }

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private bool _modeDuration;

    [ObservableProperty]
    private bool _modeClockTime;

    [ObservableProperty]
    private int _durationMinutes;

    [ObservableProperty]
    private int _clockHour;

    [ObservableProperty]
    private int _clockMinute;

    public event EventHandler? Changed;

    public TimerInstanceViewModel(TimerInstance source)
    {
        Id = source.Id;
        _label = source.Label;
        _modeDuration = source.Mode == TimerMode.Duration;
        _modeClockTime = source.Mode == TimerMode.ClockTime;
        _durationMinutes = source.DurationMinutes;
        _clockHour = source.ClockTimeHour;
        _clockMinute = source.ClockTimeMinute;
    }

    public TimerInstance ToModel() => new()
    {
        Id = Id,
        Label = Label ?? string.Empty,
        Mode = ModeClockTime ? TimerMode.ClockTime : TimerMode.Duration,
        DurationMinutes = System.Math.Max(0, DurationMinutes),
        ClockTimeHour = System.Math.Clamp(ClockHour, 0, 23),
        ClockTimeMinute = System.Math.Clamp(ClockMinute, 0, 59),
    };

    partial void OnLabelChanged(string value) => Changed?.Invoke(this, EventArgs.Empty);

    partial void OnDurationMinutesChanged(int value) => Changed?.Invoke(this, EventArgs.Empty);

    partial void OnClockHourChanged(int value) => Changed?.Invoke(this, EventArgs.Empty);

    partial void OnClockMinuteChanged(int value) => Changed?.Invoke(this, EventArgs.Empty);

    partial void OnModeDurationChanged(bool value)
    {
        if (value && ModeClockTime) ModeClockTime = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnModeClockTimeChanged(bool value)
    {
        if (value && ModeDuration) ModeDuration = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
