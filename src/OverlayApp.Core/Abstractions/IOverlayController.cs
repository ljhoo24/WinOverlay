using System.Collections.Generic;
using OverlayApp.Core.Models;

namespace OverlayApp.Core.Abstractions;

public interface IOverlayController
{
    void SetClickThrough(bool enabled);

    void SetOpacity(double value);

    void SetTopMost(bool enabled);

    void Show();

    void Hide();

    bool IsVisible { get; }

    (double X, double Y) GetPosition();

    void SetPosition(double x, double y);

    (double Width, double Height) GetSize();

    void SetSize(double width, double height);

    /// <summary>연결된 모니터 목록. 화면 정보 없으면 빈 목록.</summary>
    IReadOnlyList<MonitorInfo> GetMonitors();

    /// <summary>지정 모니터의 네 꼭지점 중 하나에 오버레이를 맞춘다(마진 포함).</summary>
    void SnapToCorner(int monitorIndex, OverlayCorner corner);
}
