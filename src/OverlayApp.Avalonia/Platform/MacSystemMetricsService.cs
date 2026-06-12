using System;
using System.Diagnostics;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;

namespace OverlayApp.Avalonia.Platform;

/// <summary>
/// macOS 시스템 지표 — 최소 구현.
/// 메모리: sysctl hw.memsize + vm_stat 파싱. CPU 사용률/온도, GPU: 미지원(null).
/// </summary>
public sealed class MacSystemMetricsService : ISystemMetricsService
{
    public SystemMetrics Read(bool needTemps)
    {
        var m = new SystemMetrics();
        if (!OperatingSystem.IsMacOS()) return m;

        try
        {
            ReadMemory(m);
        }
        catch
        {
            // 지표는 best-effort. 실패 시 해당 값만 null로 둔다.
        }

        // 온도는 macOS에서 SMC 접근 제한으로 미지원. "권한 필요" 표시도 띄우지 않는다.
        m.ElevationRequiredForTemps = false;
        return m;
    }

    private static void ReadMemory(SystemMetrics m)
    {
        var totalBytes = ReadSysctlLong("hw.memsize");
        if (totalBytes <= 0) return;

        // vm_stat: "Pages free: 12345." 형태. 페이지 크기는 첫 줄에 명시.
        var output = RunCommand("/usr/bin/vm_stat");
        if (output is null) return;

        long pageSize = 4096;
        long freePages = 0, inactivePages = 0, speculativePages = 0;
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("Mach Virtual Memory Statistics", StringComparison.Ordinal))
            {
                var open = line.IndexOf("page size of ", StringComparison.Ordinal);
                if (open >= 0)
                {
                    var rest = line[(open + "page size of ".Length)..];
                    var num = rest.Split(' ')[0];
                    if (long.TryParse(num, out var ps) && ps > 0) pageSize = ps;
                }
                continue;
            }

            static long ParseValue(string l)
            {
                var idx = l.IndexOf(':');
                if (idx < 0) return 0;
                var v = l[(idx + 1)..].Trim().TrimEnd('.');
                return long.TryParse(v, out var n) ? n : 0;
            }

            if (line.StartsWith("Pages free", StringComparison.Ordinal)) freePages = ParseValue(line);
            else if (line.StartsWith("Pages inactive", StringComparison.Ordinal)) inactivePages = ParseValue(line);
            else if (line.StartsWith("Pages speculative", StringComparison.Ordinal)) speculativePages = ParseValue(line);
        }

        var availableBytes = (freePages + inactivePages + speculativePages) * pageSize;
        var usedBytes = Math.Max(0, totalBytes - availableBytes);

        const double gb = 1024.0 * 1024 * 1024;
        m.MemoryTotalGb = totalBytes / gb;
        m.MemoryUsedGb = usedBytes / gb;
        m.MemoryPercent = totalBytes > 0 ? usedBytes * 100.0 / totalBytes : null;
    }

    private static long ReadSysctlLong(string name)
    {
        var output = RunCommand("/usr/sbin/sysctl", $"-n {name}");
        return long.TryParse(output?.Trim(), out var v) ? v : 0;
    }

    private static string? RunCommand(string file, string args = "")
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            return output;
        }
        catch
        {
            return null;
        }
    }
}
