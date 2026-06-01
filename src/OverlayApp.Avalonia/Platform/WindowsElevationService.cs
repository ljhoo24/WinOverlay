using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using OverlayApp.Core.Abstractions;

namespace OverlayApp.Avalonia.Platform;

[SupportedOSPlatform("windows")]
public sealed class WindowsElevationService : IElevationService
{
    public bool IsElevated
    {
        get
        {
            try
            {
                using var id = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    public bool RestartElevated()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return false;

        // 새 인스턴스가 단일-인스턴스 뮤텍스를 점유할 수 있도록 먼저 해제.
        Program.ReleaseSingleInstanceMutex();

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas",
            });
        }
        catch
        {
            // UAC 취소 또는 실패 → 뮤텍스 복구하고 계속 실행.
            Program.TryReacquireSingleInstanceMutex();
            return false;
        }

        // 성공: 현재(비관리자) 인스턴스를 정리 후 종료.
        Dispatcher.UIThread.Post(() =>
        {
            if (global::Avalonia.Application.Current is App app)
            {
                app.RequestShutdown();
            }
            else if (global::Avalonia.Application.Current?.ApplicationLifetime
                     is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            else
            {
                Environment.Exit(0);
            }
        });
        return true;
    }
}
