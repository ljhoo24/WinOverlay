using CommunityToolkit.Mvvm.ComponentModel;

namespace OverlayApp.Core.ViewModels;

public sealed partial class WeatherLineViewModel : ObservableObject
{
    public string Id { get; }

    [ObservableProperty]
    private string _text = string.Empty;

    public WeatherLineViewModel(string id) => Id = id;
}
