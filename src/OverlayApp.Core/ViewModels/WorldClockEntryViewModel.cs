using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OverlayApp.Core.ViewModels;

public sealed partial class WorldClockEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _timeZoneId = "UTC";

    [ObservableProperty]
    private string _timeText = string.Empty;

    public event EventHandler? Changed;

    public WorldClockEntryViewModel(string label, string timeZoneId)
    {
        _label = label;
        _timeZoneId = timeZoneId;
    }

    public string DisplayText => $"{Label}  {TimeText}";

    public void UpdateTime(bool use24Hour)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            TimeText = local.ToString(use24Hour ? "HH:mm" : "h:mm tt");
        }
        catch
        {
            TimeText = "(invalid TZ)";
        }
        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnLabelChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayText));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnTimeZoneIdChanged(string value) => Changed?.Invoke(this, EventArgs.Empty);
}
