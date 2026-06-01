using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;

namespace OverlayApp.Core.Services;

public sealed class WeatherUpdater : IDisposable
{
    private readonly IWeatherService _weather;
    private readonly IUiDispatcher _dispatcher;
    private readonly AppSettings _settings;
    private readonly Timer _timer;

    private readonly Dictionary<string, WeatherInfo> _cityWeathers = new();

    /// <summary>도시 항목 ID → WeatherInfo. Idle 또는 미조회 항목은 누락될 수 있음.</summary>
    public IReadOnlyDictionary<string, WeatherInfo> CityWeathers => _cityWeathers;

    public event EventHandler? Updated;

    public WeatherUpdater(
        IWeatherService weather,
        IUiDispatcher dispatcher,
        AppSettings settings)
    {
        _weather = weather;
        _dispatcher = dispatcher;
        _settings = settings;
        _timer = new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        Reschedule();
        _ = RefreshAsync();
    }

    public void Reschedule()
    {
        var minutes = System.Math.Max(1, _settings.WeatherCommon.RefreshMinutes);
        var ms = (long)minutes * 60 * 1000;
        _timer.Change(ms, ms);
    }

    public Task TriggerRefresh() => RefreshAsync();

    private void OnTimer(object? state) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        if (!_settings.CityWeather.Enabled || _settings.CityWeather.Cities.Count == 0)
        {
            _cityWeathers.Clear();
            _dispatcher.Post(() => Updated?.Invoke(this, EventArgs.Empty));
            return;
        }

        var entries = _settings.CityWeather.Cities.ToList();
        var tasks = entries.Select(async e =>
        {
            var info = string.IsNullOrWhiteSpace(e.CityName)
                ? new WeatherInfo { HasError = true, ErrorMessage = "도시명 비어 있음" }
                : await _weather.GetByCityAsync(e.CityName);
            return (e.Id, info);
        });

        var results = await Task.WhenAll(tasks);

        // Replace map (remove stale, keep latest)
        var validIds = entries.Select(e => e.Id).ToHashSet();
        foreach (var key in _cityWeathers.Keys.ToList())
        {
            if (!validIds.Contains(key)) _cityWeathers.Remove(key);
        }
        foreach (var (id, info) in results)
        {
            _cityWeathers[id] = info;
        }

        _dispatcher.Post(() => Updated?.Invoke(this, EventArgs.Empty));
    }

    public void Dispose() => _timer.Dispose();
}
