using System;
using System.Threading;
using Avalonia;
using Velopack;

namespace OverlayApp.Avalonia;

internal static class Program
{
    private const string SingleInstanceMutexName = "Global\\OverlayApp.SingleInstance.v1";

    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack: handle install/update/uninstall hooks before anything UI-related.
        // When invoked by the updater, this returns after processing and the process exits.
        VelopackApp.Build().Run();

        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            // Another instance is already running; exit silently.
            return 0;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
