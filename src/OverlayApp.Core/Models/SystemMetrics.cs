namespace OverlayApp.Core.Models;

/// <summary>
/// 한 번의 시스템 지표 스냅샷. 읽지 못한 값은 null.
/// </summary>
public sealed class SystemMetrics
{
    public double? MemoryUsedGb { get; set; }

    public double? MemoryTotalGb { get; set; }

    public double? MemoryPercent { get; set; }

    public double? CpuLoadPercent { get; set; }

    public double? CpuTempC { get; set; }

    public double? GpuTempC { get; set; }

    public double? GpuLoadPercent { get; set; }

    /// <summary>온도 지표를 요청했지만 관리자 권한이 없어 읽지 못한 상태.</summary>
    public bool ElevationRequiredForTemps { get; set; }
}
