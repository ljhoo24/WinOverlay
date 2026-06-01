using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;
using OverlayApp.Core.Services;

namespace OverlayApp.Core.ViewModels;

public sealed partial class OverlayViewModel : ObservableObject, IDisposable
{
    private readonly ClockService _clockService;
    private readonly IOverlayController _controller;
    private readonly WeatherUpdater _weatherUpdater;
    private readonly TimerService _timerService;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private string _timeText = string.Empty;

    [ObservableProperty]
    private bool _use24Hour = true;

    [ObservableProperty]
    private bool _isAdjustMode;

    [ObservableProperty]
    private string _locationWeatherText = string.Empty;

    [ObservableProperty]
    private bool _locationWeatherVisible;

    [ObservableProperty]
    private string _cityWeatherText = string.Empty;

    [ObservableProperty]
    private bool _cityWeatherVisible;

    [ObservableProperty]
    private bool _worldClockVisible;

    [ObservableProperty]
    private bool _timerVisible;

    public ObservableCollection<WorldClockEntryViewModel> WorldClockEntries { get; } = new();

    public ObservableCollection<TimerLineViewModel> TimerLines { get; } = new();

    public OverlayViewModel(
        ClockService clockService,
        IOverlayController controller,
        WeatherUpdater weatherUpdater,
        TimerService timerService,
        AppSettings settings)
    {
        _clockService = clockService;
        _controller = controller;
        _weatherUpdater = weatherUpdater;
        _timerService = timerService;
        _settings = settings;
        Use24Hour = settings.Clock.Use24Hour;

        foreach (var e in settings.WorldClock.Entries)
        {
            WorldClockEntries.Add(new WorldClockEntryViewModel(e.Label, e.TimeZoneId));
        }
        RefreshWorldClockVisibility();

        _clockService.Tick += OnTick;
        _weatherUpdater.Updated += OnWeatherUpdated;
        _timerService.Changed += OnTimerChanged;
        UpdateTime();
        UpdateWorldClocks();
        UpdateWeatherDisplay();
        UpdateTimerDisplay();
        _clockService.Start();
        _weatherUpdater.Start();
    }

    public void RefreshWorldClockVisibility()
    {
        WorldClockVisible = _settings.WorldClock.Enabled && WorldClockEntries.Count > 0;
    }

    public void RefreshTimerVisibility() => UpdateTimerDisplay();

    partial void OnIsAdjustModeChanged(bool value)
    {
        _controller.SetClickThrough(!value);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        UpdateTime();
        UpdateWorldClocks();
    }

    private void OnWeatherUpdated(object? sender, EventArgs e) => UpdateWeatherDisplay();

    private void OnTimerChanged(object? sender, EventArgs e) => UpdateTimerDisplay();

    private void UpdateTime()
    {
        var format = Use24Hour ? "HH:mm:ss" : "h:mm:ss tt";
        TimeText = DateTime.Now.ToString(format);
    }

    private void UpdateWorldClocks()
    {
        foreach (var entry in WorldClockEntries)
        {
            entry.UpdateTime(Use24Hour);
        }
    }

    private void UpdateTimerDisplay()
    {
        if (!_settings.Timer.Enabled)
        {
            if (TimerLines.Count > 0) TimerLines.Clear();
            TimerVisible = false;
            return;
        }

        // Diff-merge: keep only active runtimes, in-place text update for existing lines.
        var active = _timerService.Runtimes.Where(r => r.State != TimerState.Idle).ToList();
        var activeIds = active.Select(r => r.Spec.Id).ToHashSet();

        // 제거: 더 이상 active가 아닌 줄
        for (var i = TimerLines.Count - 1; i >= 0; i--)
        {
            if (!activeIds.Contains(TimerLines[i].Id)) TimerLines.RemoveAt(i);
        }

        foreach (var rt in active)
        {
            var line = TimerLines.FirstOrDefault(l => l.Id == rt.Spec.Id);
            if (line is null)
            {
                line = new TimerLineViewModel(rt.Spec.Id);
                TimerLines.Add(line);
            }
            line.Text = rt.GetDisplayText();
        }

        TimerVisible = TimerLines.Count > 0;
    }

    private void UpdateWeatherDisplay()
    {
        var loc = _weatherUpdater.LocationWeather;
        if (_settings.LocationWeather.Enabled && _settings.LocationWeather.ConsentGranted && loc is not null)
        {
            LocationWeatherText = FormatWeather(loc, fallbackLabel: "현재 위치");
            LocationWeatherVisible = true;
        }
        else
        {
            LocationWeatherVisible = false;
        }

        var city = _weatherUpdater.CityWeather;
        if (_settings.CityWeather.Enabled && city is not null)
        {
            CityWeatherText = FormatWeather(city, fallbackLabel: _settings.CityWeather.CityName);
            CityWeatherVisible = true;
        }
        else
        {
            CityWeatherVisible = false;
        }
    }

    private static string FormatWeather(WeatherInfo w, string fallbackLabel)
    {
        if (w.HasError) return $"{fallbackLabel}: {w.ErrorMessage}";
        var unit = w.Unit == TemperatureUnit.Fahrenheit ? "°F" : "°C";
        var name = string.IsNullOrEmpty(w.LocationName) ? fallbackLabel : w.LocationName;
        return $"{name} {w.Temperature:0.#}{unit} · {w.Condition}";
    }

    partial void OnUse24HourChanged(bool value)
    {
        UpdateTime();
        UpdateWorldClocks();
    }

    public void Dispose()
    {
        _clockService.Tick -= OnTick;
        _weatherUpdater.Updated -= OnWeatherUpdated;
        _timerService.Changed -= OnTimerChanged;
    }
}
