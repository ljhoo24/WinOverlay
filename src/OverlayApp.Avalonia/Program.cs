using System;
using System.Threading;
using Avalonia;
using Velopack;

namespace OverlayApp.Avalonia;

internal static class Program
{
    private const string SingleInstanceMutexName = "Global\\OverlayApp.SingleInstance.v1";

    private static Mutex? _mutex;

    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack: handle install/update/uninstall hooks before anything UI-related.
        // When invoked by the updater, this returns after processing and the process exits.
        VelopackApp.Build().Run();

        _mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            // Another instance is already running; exit silently.
            _mutex.Dispose();
            _mutex = null;
            return 0;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// 관리자 권한 재실행 직전에 단일 인스턴스 뮤텍스를 풀어, 새 인스턴스가 점유할 수 있게 한다.
    /// </summary>
    public static void ReleaseSingleInstanceMutex()
    {
        try { _mutex?.ReleaseMutex(); } catch { /* not owned */ }
        _mutex?.Dispose();
        _mutex = null;
    }

    /// <summary>UAC 취소 등으로 재실행이 무산됐을 때 뮤텍스를 다시 잡는다(베스트 에포트).</summary>
    public static void TryReacquireSingleInstanceMutex()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out _);
        }
        catch
        {
            _mutex = null;
        }
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
