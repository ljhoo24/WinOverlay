using System.Runtime.InteropServices;
using Avalonia.Threading;
using OverlayApp.Avalonia.Views;
using OverlayApp.Core.Abstractions;

namespace OverlayApp.Avalonia.Platform;

public sealed class AvaloniaAlarmService : IAlarmService
{
    private const uint MB_ICONEXCLAMATION = 0x00000030;

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);

    public void Fire(string title, string message, bool playSound)
    {
        if (playSound)
        {
            MessageBeep(MB_ICONEXCLAMATION);
        }

        // Pop the alarm window on the UI thread. Topmost + activated so the user notices it.
        Dispatcher.UIThread.Post(() =>
        {
            var window = new AlarmWindow(title, message);
            window.Show();
            window.Activate();
        });
    }
}
