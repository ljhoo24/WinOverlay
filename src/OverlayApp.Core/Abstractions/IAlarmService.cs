namespace OverlayApp.Core.Abstractions;

public interface IAlarmService
{
    void Fire(string title, string message, bool playSound);
}
