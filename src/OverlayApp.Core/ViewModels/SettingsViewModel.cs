using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;
using OverlayApp.Core.Services;

namespace OverlayApp.Core.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IOverlayController _controller;
    private readonly IGlobalHotkeyService _hotkeys;
    private readonly OverlayViewModel _overlay;
    private readonly WeatherUpdater _weatherUpdater;
    private readonly TimerService _timerService;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private int _opacityPercent;

    [ObservableProperty]
    private bool _isAdjustMode;

    public HotkeyEditorViewModel ToggleHotkeyEditor { get; }

    public HotkeyEditorViewModel TimerStartHotkeyEditor { get; }

    public HotkeyEditorViewModel TimerStopHotkeyEditor { get; }

    [ObservableProperty]
    private bool _use24Hour;

    [ObservableProperty]
    private bool _useFahrenheit;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private int _refreshMinutes;

    [ObservableProperty]
    private bool _locationWeatherEnabled;

    [ObservableProperty]
    private bool _locationConsent;

    [ObservableProperty]
    private bool _cityWeatherEnabled;

    [ObservableProperty]
    private string _cityName = string.Empty;

    [ObservableProperty]
    private bool _worldClockEnabled;

    [ObservableProperty]
    private bool _timerEnabled;

    [ObservableProperty]
    private bool _timerSoundEnabled;

    [ObservableProperty]
    private bool _timerModeDuration;

    [ObservableProperty]
    private bool _timerModeClockTime;

    [ObservableProperty]
    private int _timerDurationMinutes;

    [ObservableProperty]
    private int _timerClockHour;

    [ObservableProperty]
    private int _timerClockMinute;

    [ObservableProperty]
    private string _timerStatus = "정지됨";

    public ObservableCollection<WorldClockEntryViewModel> WorldClockEntries => _overlay.WorldClockEntries;

    public IReadOnlyList<string> AvailableTimeZoneIds { get; } =
        TimeZoneInfo.GetSystemTimeZones().Select(z => z.Id).OrderBy(s => s).ToList();

    public SettingsViewModel(
        ISettingsService settingsService,
        IOverlayController controller,
        IGlobalHotkeyService hotkeys,
        OverlayViewModel overlay,
        WeatherUpdater weatherUpdater,
        TimerService timerService,
        AppSettings settings)
    {
        _settingsService = settingsService;
        _controller = controller;
        _hotkeys = hotkeys;
        _overlay = overlay;
        _weatherUpdater = weatherUpdater;
        _timerService = timerService;
        _settings = settings;

        _opacityPercent = (int)(_settings.Overlay.Opacity * 100);
        _isAdjustMode = _overlay.IsAdjustMode;

        ToggleHotkeyEditor = new HotkeyEditorViewModel(
            title: "오버레이 표시/숨김",
            hotkeyId: "toggle-overlay",
            hotkeys: _hotkeys,
            read: () => _settings.ToggleHotkey,
            write: def => _settings.ToggleHotkey = def,
            persist: Persist);
        TimerStartHotkeyEditor = new HotkeyEditorViewModel(
            title: "타이머 시작",
            hotkeyId: "timer-start",
            hotkeys: _hotkeys,
            read: () => _settings.Timer.StartHotkey,
            write: def => _settings.Timer.StartHotkey = def,
            persist: Persist);
        TimerStopHotkeyEditor = new HotkeyEditorViewModel(
            title: "타이머 중지",
            hotkeyId: "timer-stop",
            hotkeys: _hotkeys,
            read: () => _settings.Timer.StopHotkey,
            write: def => _settings.Timer.StopHotkey = def,
            persist: Persist);

        _use24Hour = _settings.Clock.Use24Hour;
        _useFahrenheit = _settings.WeatherCommon.Unit == TemperatureUnit.Fahrenheit;
        _apiKey = _settings.WeatherCommon.ApiKey;
        _refreshMinutes = _settings.WeatherCommon.RefreshMinutes;
        _locationWeatherEnabled = _settings.LocationWeather.Enabled;
        _locationConsent = _settings.LocationWeather.ConsentGranted;
        _cityWeatherEnabled = _settings.CityWeather.Enabled;
        _cityName = _settings.CityWeather.CityName;
        _worldClockEnabled = _settings.WorldClock.Enabled;

        _timerEnabled = _settings.Timer.Enabled;
        _timerSoundEnabled = _settings.Timer.SoundEnabled;
        _timerModeDuration = _settings.Timer.Mode == TimerMode.Duration;
        _timerModeClockTime = _settings.Timer.Mode == TimerMode.ClockTime;
        _timerDurationMinutes = _settings.Timer.DurationMinutes;
        _timerClockHour = _settings.Timer.ClockTimeHour;
        _timerClockMinute = _settings.Timer.ClockTimeMinute;

        _timerService.Changed += (_, _) => RefreshTimerStatus();
        RefreshTimerStatus();

        foreach (var entry in WorldClockEntries)
        {
            entry.Changed += OnWorldClockEntryChanged;
        }
        WorldClockEntries.CollectionChanged += (_, _) =>
        {
            SyncWorldClockToSettings();
            _overlay.RefreshWorldClockVisibility();
        };
    }

    private void RefreshTimerStatus()
    {
        TimerStatus = _timerService.IsRunning ? _timerService.GetDisplayText() : "정지됨";
    }

    public AppSettings Settings => _settings;

    partial void OnOpacityPercentChanged(int value)
    {
        var clamped = System.Math.Clamp(value, 0, 100);
        _settings.Overlay.Opacity = clamped / 100.0;
        _controller.SetOpacity(_settings.Overlay.Opacity);
        Persist();
    }

    partial void OnIsAdjustModeChanged(bool value)
    {
        _overlay.IsAdjustMode = value;
        if (value && !_controller.IsVisible)
        {
            _controller.Show();
            _settings.Overlay.Visible = true;
            Persist();
        }
    }

    partial void OnUse24HourChanged(bool value)
    {
        _settings.Clock.Use24Hour = value;
        _overlay.Use24Hour = value;
        Persist();
    }

    partial void OnUseFahrenheitChanged(bool value)
    {
        _settings.WeatherCommon.Unit = value ? TemperatureUnit.Fahrenheit : TemperatureUnit.Celsius;
        Persist();
        _ = _weatherUpdater.TriggerRefresh();
    }

    partial void OnApiKeyChanged(string value)
    {
        _settings.WeatherCommon.ApiKey = (value ?? string.Empty).Trim();
        Persist();
    }

    partial void OnRefreshMinutesChanged(int value)
    {
        _settings.WeatherCommon.RefreshMinutes = System.Math.Max(1, value);
        Persist();
        _weatherUpdater.Reschedule();
    }

    partial void OnLocationWeatherEnabledChanged(bool value)
    {
        if (value && !LocationConsent)
        {
            LocationWeatherEnabled = false;
            return;
        }
        _settings.LocationWeather.Enabled = value;
        Persist();
        _ = _weatherUpdater.TriggerRefresh();
    }

    partial void OnLocationConsentChanged(bool value)
    {
        _settings.LocationWeather.ConsentGranted = value;
        if (!value)
        {
            _settings.LocationWeather.Enabled = false;
            _settings.LocationWeather.LastLatitude = null;
            _settings.LocationWeather.LastLongitude = null;
            LocationWeatherEnabled = false;
        }
        Persist();
    }

    partial void OnCityWeatherEnabledChanged(bool value)
    {
        _settings.CityWeather.Enabled = value;
        Persist();
        _ = _weatherUpdater.TriggerRefresh();
    }

    partial void OnCityNameChanged(string value)
    {
        _settings.CityWeather.CityName = value ?? string.Empty;
        Persist();
    }

    partial void OnWorldClockEnabledChanged(bool value)
    {
        _settings.WorldClock.Enabled = value;
        _overlay.RefreshWorldClockVisibility();
        Persist();
    }

    partial void OnTimerEnabledChanged(bool value)
    {
        _settings.Timer.Enabled = value;
        _overlay.RefreshTimerVisibility();
        Persist();
    }

    partial void OnTimerSoundEnabledChanged(bool value)
    {
        _settings.Timer.SoundEnabled = value;
        Persist();
    }

    partial void OnTimerModeDurationChanged(bool value)
    {
        if (value)
        {
            if (TimerModeClockTime) TimerModeClockTime = false;
            _settings.Timer.Mode = TimerMode.Duration;
            Persist();
        }
    }

    partial void OnTimerModeClockTimeChanged(bool value)
    {
        if (value)
        {
            if (TimerModeDuration) TimerModeDuration = false;
            _settings.Timer.Mode = TimerMode.ClockTime;
            Persist();
        }
    }

    partial void OnTimerDurationMinutesChanged(int value)
    {
        _settings.Timer.DurationMinutes = System.Math.Max(0, value);
        Persist();
    }

    partial void OnTimerClockHourChanged(int value)
    {
        _settings.Timer.ClockTimeHour = System.Math.Clamp(value, 0, 23);
        Persist();
    }

    partial void OnTimerClockMinuteChanged(int value)
    {
        _settings.Timer.ClockTimeMinute = System.Math.Clamp(value, 0, 59);
        Persist();
    }

    [RelayCommand]
    private void StartTimer()
    {
        _timerService.Start();
        if (TimerEnabled) _overlay.RefreshTimerVisibility();
    }

    [RelayCommand]
    private void StopTimer()
    {
        _timerService.Stop();
        _overlay.RefreshTimerVisibility();
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task RefreshWeather()
    {
        await _weatherUpdater.TriggerRefresh();
    }

    [RelayCommand]
    private void AddWorldClock()
    {
        var entry = new WorldClockEntryViewModel("새 항목", TimeZoneInfo.Local.Id);
        entry.Changed += OnWorldClockEntryChanged;
        WorldClockEntries.Add(entry);
    }

    [RelayCommand]
    private void RemoveWorldClock(WorldClockEntryViewModel? entry)
    {
        if (entry is null) return;
        entry.Changed -= OnWorldClockEntryChanged;
        WorldClockEntries.Remove(entry);
    }

    private void OnWorldClockEntryChanged(object? sender, EventArgs e) => SyncWorldClockToSettings();

    private void SyncWorldClockToSettings()
    {
        _settings.WorldClock.Entries = WorldClockEntries
            .Select(vm => new WorldClockEntry { Label = vm.Label, TimeZoneId = vm.TimeZoneId })
            .ToList();
        Persist();
    }

    private void Persist() => _settingsService.Save(_settings);
}
