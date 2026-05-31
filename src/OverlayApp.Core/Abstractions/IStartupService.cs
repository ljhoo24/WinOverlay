namespace OverlayApp.Core.Abstractions;

public interface IStartupService
{
    bool IsEnabled { get; }

    void Enable();

    void Disable();
}
