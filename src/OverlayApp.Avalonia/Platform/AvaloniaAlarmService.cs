using Avalonia.Threading;
using OverlayApp.Avalonia.Views;
using OverlayApp.Core.Abstractions;
#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace OverlayApp.Avalonia.Platform;

public sealed class AvaloniaAlarmService : IAlarmService
{
#if WINDOWS
    private const uint MB_ICONEXCLAMATION = 0x00000030;

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);
#endif

    public void Fire(string title, string message, bool playSound)
    {
        if (playSound)
        {
#if WINDOWS
            MessageBeep(MB_ICONEXCLAMATION);
#else
            PlayMacSound();
#endif
        }

        // Pop the alarm window on the UI thread. Topmost + activated so the user notices it.
        Dispatcher.UIThread.Post(() =>
        {
            var window = new AlarmWindow(title, message);
            window.Show();
            window.Activate();
        });
    }

#if !WINDOWS
    private static void PlayMacSound()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/bin/afplay",
                Arguments = "/System/Library/Sounds/Glass.aiff",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch
        {
            // 사운드는 best-effort.
        }
    }
#endif
}
