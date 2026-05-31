using System;
using System.Threading;
using System.Threading.Tasks;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;

namespace OverlayApp.Core.Services;

public sealed class WeatherUpdater : IDisposable
{
    private readonly IWeatherService _weather;
    private readonly IGeolocationService _geo;
    private readonly ISettingsService _settingsService;
    private readonly IUiDispatcher _dispatcher;
    private readonly AppSettings _settings;
    private readonly Timer _timer;

    public WeatherInfo? LocationWeather { get; private set; }

    public WeatherInfo? CityWeather { get; private set; }

    public event EventHandler? Updated;

    public WeatherUpdater(
        IWeatherService weather,
        IGeolocationService geo,
        ISettingsService settingsService,
        IUiDispatcher dispatcher,
        AppSettings settings)
    {
        _weather = weather;
        _geo = geo;
        _settingsService = settingsService;
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
        var locTask = RefreshLocationAsync();
        var cityTask = RefreshCityAsync();
        await Task.WhenAll(locTask, cityTask);
        _dispatcher.Post(() => Updated?.Invoke(this, EventArgs.Empty));
    }

    private async Task RefreshLocationAsync()
    {
        var s = _settings.LocationWeather;
        if (!s.Enabled || !s.ConsentGranted)
        {
            LocationWeather = null;
            return;
        }

        double lat, lon;
        if (s.LastLatitude is { } cachedLat && s.LastLongitude is { } cachedLon)
        {
            lat = cachedLat;
            lon = cachedLon;
        }
        else
        {
            var geo = await _geo.GetCurrentAsync();
            if (geo.HasError)
            {
                LocationWeather = new WeatherInfo { HasError = true, ErrorMessage = geo.ErrorMessage };
                return;
            }
            lat = geo.Latitude;
            lon = geo.Longitude;
            s.LastLatitude = lat;
            s.LastLongitude = lon;
            _settingsService.Save(_settings);
        }

        LocationWeather = await _weather.GetByCoordinatesAsync(lat, lon);
    }

    private async Task RefreshCityAsync()
    {
        var s = _settings.CityWeather;
        if (!s.Enabled || string.IsNullOrWhiteSpace(s.CityName))
        {
            CityWeather = null;
            return;
        }
        CityWeather = await _weather.GetByCityAsync(s.CityName);
    }

    public void Dispose() => _timer.Dispose();
}
