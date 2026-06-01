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
    private readonly SystemMetricsUpdater _systemUpdater;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private string _timeText = string.Empty;

    [ObservableProperty]
    private bool _use24Hour = true;

    [ObservableProperty]
    private bool _isAdjustMode;

    [ObservableProperty]
    private bool _cityWeatherVisible;

    [ObservableProperty]
    private bool _worldClockVisible;

    [ObservableProperty]
    private bool _timerVisible;

    [ObservableProperty]
    private string _systemText = string.Empty;

    [ObservableProperty]
    private bool _systemVisible;

    public ObservableCollection<WorldClockEntryViewModel> WorldClockEntries { get; } = new();

    public ObservableCollection<WeatherLineViewModel> CityWeatherLines { get; } = new();

    public ObservableCollection<TimerLineViewModel> TimerLines { get; } = new();

    public OverlayViewModel(
        ClockService clockService,
        IOverlayController controller,
        WeatherUpdater weatherUpdater,
        TimerService timerService,
        SystemMetricsUpdater systemUpdater,
        AppSettings settings)
    {
        _clockService = clockService;
        _controller = controller;
        _weatherUpdater = weatherUpdater;
        _timerService = timerService;
        _systemUpdater = systemUpdater;
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
        _systemUpdater.Updated += OnSystemUpdated;
        UpdateTime();
        UpdateWorldClocks();
        UpdateCityWeatherDisplay();
        UpdateTimerDisplay();
        UpdateSystemDisplay();
        _clockService.Start();
        _weatherUpdater.Start();
    }

    public void RefreshWorldClockVisibility()
    {
        WorldClockVisible = _settings.WorldClock.Enabled && WorldClockEntries.Count > 0;
    }

    public void RefreshTimerVisibility() => UpdateTimerDisplay();

    public void RefreshCityWeatherDisplay() => UpdateCityWeatherDisplay();

    public void RefreshSystemDisplay() => UpdateSystemDisplay();

    partial void OnIsAdjustModeChanged(bool value)
    {
        _controller.SetClickThrough(!value);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        UpdateTime();
        UpdateWorldClocks();
    }

    private void OnWeatherUpdated(object? sender, EventArgs e) => UpdateCityWeatherDisplay();

    private void OnTimerChanged(object? sender, EventArgs e) => UpdateTimerDisplay();

    private void OnSystemUpdated(object? sender, EventArgs e) => UpdateSystemDisplay();

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

        var active = _timerService.Runtimes.Where(r => r.State != TimerState.Idle).ToList();
        var activeIds = active.Select(r => r.Spec.Id).ToHashSet();

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

    private void UpdateCityWeatherDisplay()
    {
        var enabled = _settings.CityWeather.Enabled;
        var entries = _settings.CityWeather.Cities;

        if (!enabled || entries.Count == 0)
        {
            if (CityWeatherLines.Count > 0) CityWeatherLines.Clear();
            CityWeatherVisible = false;
            return;
        }

        var validIds = entries.Select(e => e.Id).ToHashSet();
        for (var i = CityWeatherLines.Count - 1; i >= 0; i--)
        {
            if (!validIds.Contains(CityWeatherLines[i].Id)) CityWeatherLines.RemoveAt(i);
        }

        foreach (var entry in entries)
        {
            var line = CityWeatherLines.FirstOrDefault(l => l.Id == entry.Id);
            if (line is null)
            {
                line = new WeatherLineViewModel(entry.Id);
                CityWeatherLines.Add(line);
            }
            if (_weatherUpdater.CityWeathers.TryGetValue(entry.Id, out var info))
            {
                line.Text = FormatWeather(info, fallbackLabel: entry.CityName);
            }
            else
            {
                line.Text = string.IsNullOrWhiteSpace(entry.CityName)
                    ? "(도시명 미입력)"
                    : $"{entry.CityName}: 조회 중...";
            }
        }

        CityWeatherVisible = CityWeatherLines.Count > 0;
    }

    private void UpdateSystemDisplay()
    {
        var s = _settings.System;
        if (!s.Enabled)
        {
            SystemText = string.Empty;
            SystemVisible = false;
            return;
        }

        var m = _systemUpdater.Latest;
        var lines = new System.Collections.Generic.List<string>();

        if (s.ShowCpuLoad || s.ShowCpuTemp)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (s.ShowCpuLoad)
                parts.Add(m?.CpuLoadPercent is double l ? $"{l:0}%" : "--%");
            if (s.ShowCpuTemp)
            {
                if (m?.ElevationRequiredForTemps == true) parts.Add("온도 권한필요");
                else parts.Add(m?.CpuTempC is double t ? $"{t:0}°C" : "--°C");
            }
            lines.Add("CPU " + string.Join(" · ", parts));
        }

        if (s.ShowGpuTemp)
        {
            string gpu;
            if (m?.ElevationRequiredForTemps == true)
            {
                gpu = "온도 권한필요";
            }
            else
            {
                var parts = new System.Collections.Generic.List<string>();
                if (m?.GpuTempC is double gt) parts.Add($"{gt:0}°C");
                if (m?.GpuLoadPercent is double gl) parts.Add($"{gl:0}%");
                gpu = parts.Count > 0 ? string.Join(" · ", parts) : "--";
            }
            lines.Add("GPU " + gpu);
        }

        if (s.ShowMemory)
        {
            if (m?.MemoryUsedGb is double u && m?.MemoryTotalGb is double tot)
                lines.Add($"RAM {u:0.0}/{tot:0.0}GB ({m.MemoryPercent:0}%)");
            else
                lines.Add("RAM --");
        }

        SystemText = string.Join("\n", lines);
        SystemVisible = lines.Count > 0;
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
        _systemUpdater.Updated -= OnSystemUpdated;
    }
}
