using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;

namespace OverlayApp.Avalonia.Platform;

[SupportedOSPlatform("windows")]
public sealed class WindowsSystemMetricsService : ISystemMetricsService, IDisposable
{
    private const double BytesPerGb = 1024d * 1024d * 1024d;

    private readonly IElevationService _elevation;

    // CPU load via GetSystemTimes diff.
    private ulong _prevIdle, _prevKernel, _prevUser;
    private bool _haveCpuBaseline;

    // LibreHardwareMonitor (관리자 권한일 때만 lazy open).
    private Computer? _computer;
    private bool _lhmTried;
    private bool _lhmOk;

    public WindowsSystemMetricsService(IElevationService elevation)
    {
        _elevation = elevation;
    }

    public SystemMetrics Read(bool needTemps)
    {
        var m = new SystemMetrics();
        ReadMemory(m);
        ReadCpuLoad(m);

        if (needTemps)
        {
            if (!_elevation.IsElevated)
            {
                m.ElevationRequiredForTemps = true;
            }
            else
            {
                ReadHardware(m);
            }
        }

        return m;
    }

    // ---- Memory ---------------------------------------------------------

    private static void ReadMemory(SystemMetrics m)
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status)) return;

        var totalGb = status.ullTotalPhys / BytesPerGb;
        var usedGb = (status.ullTotalPhys - status.ullAvailPhys) / BytesPerGb;
        m.MemoryTotalGb = totalGb;
        m.MemoryUsedGb = usedGb;
        m.MemoryPercent = status.dwMemoryLoad;
    }

    // ---- CPU load -------------------------------------------------------

    private void ReadCpuLoad(SystemMetrics m)
    {
        if (!GetSystemTimes(out var idleFt, out var kernelFt, out var userFt)) return;

        var idle = ToU64(idleFt);
        var kernel = ToU64(kernelFt); // kernel은 idle을 포함.
        var user = ToU64(userFt);

        if (_haveCpuBaseline)
        {
            var idleDiff = idle - _prevIdle;
            var kernelDiff = kernel - _prevKernel;
            var userDiff = user - _prevUser;
            var total = kernelDiff + userDiff;
            if (total > 0)
            {
                var busy = total - idleDiff;
                m.CpuLoadPercent = Math.Clamp(100.0 * busy / total, 0, 100);
            }
        }

        _prevIdle = idle;
        _prevKernel = kernel;
        _prevUser = user;
        _haveCpuBaseline = true;
    }

    // ---- Temps / GPU via LibreHardwareMonitor ---------------------------

    private void ReadHardware(SystemMetrics m)
    {
        if (!_lhmTried)
        {
            _lhmTried = true;
            try
            {
                _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
                _computer.Open();
                _lhmOk = true;
            }
            catch
            {
                _lhmOk = false;
                _computer = null;
            }
        }

        if (!_lhmOk || _computer is null) return;

        try
        {
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                switch (hw.HardwareType)
                {
                    case HardwareType.Cpu:
                        m.CpuTempC ??= PickCpuTemp(hw);
                        break;
                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                        m.GpuTempC ??= PickSensor(hw, SensorType.Temperature, "GPU Core")
                                       ?? FirstSensor(hw, SensorType.Temperature);
                        m.GpuLoadPercent ??= PickSensor(hw, SensorType.Load, "GPU Core")
                                             ?? FirstSensor(hw, SensorType.Load);
                        break;
                }
            }
        }
        catch
        {
            // 센서 읽기 실패는 무시 (값은 null로 남음).
        }
    }

    private static double? PickCpuTemp(IHardware hw)
        => PickSensor(hw, SensorType.Temperature, "CPU Package")
           ?? PickSensor(hw, SensorType.Temperature, "Core (Tctl")
           ?? AverageCoreTemp(hw)
           ?? FirstSensor(hw, SensorType.Temperature);

    private static double? AverageCoreTemp(IHardware hw)
    {
        var vals = hw.Sensors
            .Where(s => s.SensorType == SensorType.Temperature
                        && s.Value.HasValue
                        && s.Name.Contains("CPU Core", StringComparison.OrdinalIgnoreCase))
            .Select(s => (double)s.Value!.Value)
            .ToList();
        return vals.Count > 0 ? vals.Average() : null;
    }

    private static double? PickSensor(IHardware hw, SensorType type, string nameContains)
    {
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType == type && s.Value.HasValue
                && s.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
            {
                return s.Value.Value;
            }
        }
        return null;
    }

    private static double? FirstSensor(IHardware hw, SensorType type)
    {
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType == type && s.Value.HasValue)
            {
                return s.Value.Value;
            }
        }
        return null;
    }

    public void Dispose()
    {
        try { _computer?.Close(); }
        catch { /* ignore */ }
        _computer = null;
    }

    // ---- Win32 ----------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;
    }

    private static ulong ToU64(FILETIME ft) => ((ulong)ft.High << 32) | ft.Low;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);
}
