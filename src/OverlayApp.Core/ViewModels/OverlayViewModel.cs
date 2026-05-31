using System;
using System.Collections.ObjectModel;
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

    public ObservableCollection<WorldClockEntryViewModel> WorldClockEntries { get; } = new();

    public OverlayViewModel(
        ClockService clockService,
        IOverlayController controller,
        WeatherUpdater weatherUpdater,
        AppSettings settings)
    {
        _clockService = clockService;
        _controller = controller;
        _weatherUpdater = weatherUpdater;
        _settings = settings;
        Use24Hour = settings.Clock.Use24Hour;

        foreach (var e in settings.WorldClock.Entries)
        {
            WorldClockEntries.Add(new WorldClockEntryViewModel(e.Label, e.TimeZoneId));
        }
        RefreshWorldClockVisibility();

        _clockService.Tick += OnTick;
        _weatherUpdater.Updated += OnWeatherUpdated;
        UpdateTime();
        UpdateWorldClocks();
        UpdateWeatherDisplay();
        _clockService.Start();
        _weatherUpdater.Start();
    }

    public void RefreshWorldClockVisibility()
    {
        WorldClockVisible = _settings.WorldClock.Enabled && WorldClockEntries.Count > 0;
    }

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
    }
}
