namespace OverlayApp.Core.Models;

public sealed class WorldClockEntry
{
    public string Label { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = "UTC";
}
