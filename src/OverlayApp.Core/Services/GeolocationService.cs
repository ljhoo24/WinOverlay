using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OverlayApp.Core.Abstractions;

namespace OverlayApp.Core.Services;

public sealed class GeolocationService : IGeolocationService
{
    private readonly HttpClient _http;

    public GeolocationService(HttpClient http)
    {
        _http = http;
    }

    public async Task<GeolocationResult> GetCurrentAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<IpApiResponse>("https://ipapi.co/json/", ct);
            if (resp is null)
            {
                return new GeolocationResult { HasError = true, ErrorMessage = "응답 없음" };
            }
            if (!string.IsNullOrEmpty(resp.Error))
            {
                return new GeolocationResult { HasError = true, ErrorMessage = resp.Reason ?? resp.Error };
            }
            return new GeolocationResult
            {
                Latitude = resp.Latitude,
                Longitude = resp.Longitude,
                LocationName = string.IsNullOrEmpty(resp.City) ? resp.Country : resp.City,
            };
        }
        catch (Exception ex)
        {
            return new GeolocationResult { HasError = true, ErrorMessage = ex.Message };
        }
    }

    private sealed class IpApiResponse
    {
        [JsonPropertyName("latitude")] public double Latitude { get; set; }
        [JsonPropertyName("longitude")] public double Longitude { get; set; }
        [JsonPropertyName("city")] public string? City { get; set; }
        [JsonPropertyName("country_name")] public string? Country { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("reason")] public string? Reason { get; set; }
    }
}
