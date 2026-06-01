using OverlayApp.Core.Models;

namespace OverlayApp.Core.Abstractions;

public interface ISystemMetricsService
{
    /// <summary>
    /// 현재 시스템 지표를 읽는다. 온도가 필요한데 권한이 없으면 해당 값은 null이고
    /// 결과의 ElevationRequiredForTemps가 true가 된다. 예외를 던지지 않는다.
    /// </summary>
    /// <param name="needTemps">온도(CPU/GPU) 지표를 요청하는지. false면 무권한 지표만 읽는다.</param>
    SystemMetrics Read(bool needTemps);
}
