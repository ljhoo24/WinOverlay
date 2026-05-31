using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;

namespace OverlayApp.Core.Services;

public sealed class WeatherService : IWeatherService
{
    private readonly HttpClient _http;
    private readonly AppSettings _settings;

    public WeatherService(HttpClient http, AppSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public Task<WeatherInfo> GetByCoordinatesAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var url = BuildUrl($"lat={latitude.ToString(CultureInfo.InvariantCulture)}&lon={longitude.ToString(CultureInfo.InvariantCulture)}");
        return FetchAsync(url, ct);
    }

    public Task<WeatherInfo> GetByCityAsync(string cityName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return Task.FromResult(new WeatherInfo { HasError = true, ErrorMessage = "도시명이 비어 있음" });
        }
        var url = BuildUrl($"q={Uri.EscapeDataString(cityName)}");
        return FetchAsync(url, ct);
    }

    private string BuildUrl(string query)
    {
        var key = _settings.WeatherCommon.ApiKey;
        var units = _settings.WeatherCommon.Unit == TemperatureUnit.Fahrenheit ? "imperial" : "metric";
        return $"https://api.openweathermap.org/data/2.5/weather?{query}&appid={Uri.EscapeDataString(key)}&units={units}&lang=kr";
    }

    private async Task<WeatherInfo> FetchAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_settings.WeatherCommon.ApiKey))
        {
            return new WeatherInfo { HasError = true, ErrorMessage = "API 키 없음" };
        }

        try
        {
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                return new WeatherInfo
                {
                    HasError = true,
                    ErrorMessage = $"HTTP {(int)resp.StatusCode}",
                };
            }

            var data = await resp.Content.ReadFromJsonAsync<OwmResponse>(cancellationToken: ct);
            if (data is null)
            {
                return new WeatherInfo { HasError = true, ErrorMessage = "응답 파싱 실패" };
            }

            return new WeatherInfo
            {
                LocationName = data.Name ?? string.Empty,
                Temperature = data.Main?.Temp ?? 0,
                Unit = _settings.WeatherCommon.Unit,
                Condition = data.Weather is { Length: > 0 } w ? w[0].Description ?? string.Empty : string.Empty,
                FetchedAt = DateTimeOffset.Now,
            };
        }
        catch (Exception ex)
        {
            return new WeatherInfo { HasError = true, ErrorMessage = ex.Message };
        }
    }

    private sealed class OwmResponse
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("main")] public OwmMain? Main { get; set; }
        [JsonPropertyName("weather")] public OwmWeather[]? Weather { get; set; }
    }

    private sealed class OwmMain
    {
        [JsonPropertyName("temp")] public double Temp { get; set; }
    }

    private sealed class OwmWeather
    {
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("main")] public string? Main { get; set; }
    }
}
