using OverlayApp.Core.Abstractions;

namespace OverlayApp.Avalonia.Platform;

/// <summary>
/// macOS에는 UAC 승격 개념이 없고, 온도 지표도 미지원이라 승격이 필요 없다.
/// 항상 "승격 불필요/불가" 상태로 동작한다.
/// </summary>
public sealed class MacElevationService : IElevationService
{
    public bool IsElevated => true; // 온도 지표 미지원 → "권한 필요" 안내가 뜨지 않게 한다.

    public bool RestartElevated() => false;
}
