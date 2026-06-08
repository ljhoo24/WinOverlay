namespace OverlayApp.Core.Models;

public enum OverlayCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>UI 프레임워크 비의존 모니터 정보. Platform 계층에서 채운다.</summary>
public sealed class MonitorInfo
{
    public int Index { get; init; }

    public bool IsPrimary { get; init; }

    public int PixelWidth { get; init; }

    public int PixelHeight { get; init; }

    public string Label =>
        $"모니터 {Index + 1} ({PixelWidth}×{PixelHeight}){(IsPrimary ? " · 주" : string.Empty)}";

    public override string ToString() => Label;
}
