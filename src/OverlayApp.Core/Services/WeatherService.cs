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
        var key = (_settings.WeatherCommon.ApiKey ?? string.Empty).Trim();
        var units = _settings.WeatherCommon.Unit == TemperatureUnit.Fahrenheit ? "imperial" : "metric";
        return $"https://api.openweathermap.org/data/2.5/weather?{query}&appid={Uri.EscapeDataString(key)}&units={units}&lang=kr";
    }

    private async Task<WeatherInfo> FetchAsync(string url, CancellationToken ct)
    {
        var trimmedKey = (_settings.WeatherCommon.ApiKey ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmedKey))
        {
            return new WeatherInfo { HasError = true, ErrorMessage = "API 키 없음" };
        }

        try
        {
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                var hint = (int)resp.StatusCode switch
                {
                    401 => "API 키가 잘못되었거나 활성화 대기 중 (가입 후 최대 2시간)",
                    404 => "도시명을 찾을 수 없음",
                    429 => "호출 한도 초과",
                    _ => null,
                };
                var msg = hint is null
                    ? $"HTTP {(int)resp.StatusCode}: {Truncate(body, 120)}"
                    : $"HTTP {(int)resp.StatusCode} — {hint}";
                return new WeatherInfo { HasError = true, ErrorMessage = msg };
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

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s.Substring(0, max));

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
