using System.Collections.Generic;

namespace OverlayApp.Core.Models;

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; } = false;

    public bool HotkeysEnabled { get; set; } = true;

    public OverlaySettings Overlay { get; set; } = new();

    public HotkeyDefinition ToggleHotkey { get; set; } = new();

    public HotkeyDefinition OpenSettingsHotkey { get; set; } = new()
    {
        Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt,
        Key = "S",
    };

    public ClockSettings Clock { get; set; } = new();

    public WorldClockSettings WorldClock { get; set; } = new();

    public CityWeatherSettings CityWeather { get; set; } = new();

    public WeatherCommonSettings WeatherCommon { get; set; } = new();

    public TimerSettings Timer { get; set; } = new();

    public SystemMetricsSettings System { get; set; } = new();
}

public sealed class SystemMetricsSettings
{
    public bool Enabled { get; set; } = false;

    public bool ShowMemory { get; set; } = true;

    public bool ShowCpuLoad { get; set; } = true;

    public bool ShowCpuTemp { get; set; } = false;

    public bool ShowGpuTemp { get; set; } = false;

    public int RefreshSeconds { get; set; } = 2;

    /// <summary>온도 항목(CPU/GPU)을 하나라도 켰는지.</summary>
    public bool AnyTempRequested => ShowCpuTemp || ShowGpuTemp;
}

public enum TimerMode
{
    Duration,
    ClockTime,
}

public sealed class TimerInstance
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString("N");

    public string Label { get; set; } = string.Empty;

    public TimerMode Mode { get; set; } = TimerMode.Duration;

    public int DurationMinutes { get; set; } = 5;

    public int ClockTimeHour { get; set; } = 22;

    public int ClockTimeMinute { get; set; } = 0;
}

public sealed class TimerSettings
{
    public bool Enabled { get; set; } = false;

    public bool SoundEnabled { get; set; } = true;

    public List<TimerInstance> Items { get; set; } = new();

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

public sealed class CityWeatherSettings
{
    public const int MaxCities = 3;

    public bool Enabled { get; set; }

    public List<CityWeatherEntry> Cities { get; set; } = new();
}

public sealed class CityWeatherEntry
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString("N");

    public string CityName { get; set; } = string.Empty;
}

public sealed class WeatherCommonSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public TemperatureUnit Unit { get; set; } = TemperatureUnit.Celsius;

    public int RefreshMinutes { get; set; } = 10;
}
