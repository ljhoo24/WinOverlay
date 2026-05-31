namespace OverlayApp.Core.Models;

public enum TemperatureUnit
{
    Celsius,
    Fahrenheit,
}

public sealed class WeatherInfo
{
    public string LocationName { get; set; } = string.Empty;

    public double Temperature { get; set; }

    public TemperatureUnit Unit { get; set; } = TemperatureUnit.Celsius;

    public string Condition { get; set; } = string.Empty;

    public System.DateTimeOffset FetchedAt { get; set; }

    public bool HasError { get; set; }

    public string? ErrorMessage { get; set; }
}
