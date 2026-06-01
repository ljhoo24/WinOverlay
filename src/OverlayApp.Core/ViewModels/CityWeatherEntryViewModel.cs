using System;
using CommunityToolkit.Mvvm.ComponentModel;
using OverlayApp.Core.Models;

namespace OverlayApp.Core.ViewModels;

public sealed partial class CityWeatherEntryViewModel : ObservableObject
{
    public string Id { get; }

    [ObservableProperty]
    private string _cityName = string.Empty;

    public event EventHandler? Changed;

    public CityWeatherEntryViewModel(CityWeatherEntry source)
    {
        Id = source.Id;
        _cityName = source.CityName;
    }

    public CityWeatherEntry ToModel() => new()
    {
        Id = Id,
        CityName = (CityName ?? string.Empty).Trim(),
    };

    partial void OnCityNameChanged(string value) => Changed?.Invoke(this, EventArgs.Empty);
}
