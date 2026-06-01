using CommunityToolkit.Mvvm.ComponentModel;

namespace OverlayApp.Core.ViewModels;

public sealed partial class TimerLineViewModel : ObservableObject
{
    public string Id { get; }

    [ObservableProperty]
    private string _text = string.Empty;

    public TimerLineViewModel(string id) => Id = id;
}
