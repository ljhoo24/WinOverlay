using System.Collections.Generic;

namespace OverlayApp.Core.Models;

public sealed class AppSettings
{
    public OverlaySettings Overlay { get; set; } = new();

    public HotkeyDefinition ToggleHotkey { get; set; } = new();

    public ClockSettings Clock { get; set; } = new();

    public WorldClockSettings WorldClock { get; set; } = new();

    public LocationWeatherSettings LocationWeather { get; set; } = new();

    public CityWeatherSettings CityWeather { get; set; } = new();

    public WeatherCommonSettings WeatherCommon { get; set; } = new();

    public TimerSettings Timer { get; set; } = new();
}

public enum TimerMode
{
    Duration,
    ClockTime,
}

public sealed class TimerSettings
{
    public bool Enabled { get; set; } = false;

    public TimerMode Mode { get; set; } = TimerMode.Duration;

    public int DurationMinutes { get; set; } = 5;

    public int ClockTimeHour { get; set; } = 22;

    public int ClockTimeMinute { get; set; } = 0;

    public bool SoundEnabled { get; set; } = true;

    public HotkeyDefinition ToggleHotkey { get; set; } = new()
    {
        Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt,
        Key = "T",
    };

    public HotkeyDefinition VisibilityHotkey { get; set; } = new()
    {
        Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt,
        Key = "Y",
    };
}

public sealed class OverlaySettings
{
    public bool Visible { get; set; } = true;

    public double X { get; set; } = 40;

    public double Y { get; set; } = 40;

    public double Width { get; set; } = 220;

    public double Height { get; set; } = 80;

    public double Opacity { get; set; } = 0.85;
}

public sealed class ClockSettings
{
    public bool Use24Hour { get; set; } = true;
}

public sealed class WorldClockSettings
{
    public bool Enabled { get; set; }

    public List<WorldClockEntry> Entries { get; set; } = new();
}

public sealed class LocationWeatherSettings
{
    public bool Enabled { get; set; }

    public bool ConsentGranted { get; set; }

    public double? LastLatitude { get; set; }

    public double? LastLongitude { get; set; }
}

public sealed class CityWeatherSettings
{
    public bool Enabled { get; set; }

    public string CityName { get; set; } = string.Empty;
}

public sealed class WeatherCommonSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public TemperatureUnit Unit { get; set; } = TemperatureUnit.Celsius;

    public int RefreshMinutes { get; set; } = 10;
}
