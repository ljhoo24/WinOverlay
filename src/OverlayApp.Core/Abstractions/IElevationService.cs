namespace OverlayApp.Core.Abstractions;

public interface IElevationService
{
    /// <summary>현재 프로세스가 관리자 권한으로 실행 중인지.</summary>
    bool IsElevated { get; }

    /// <summary>
    /// 관리자 권한으로 앱을 재실행한다. 성공 시 현재(비관리자) 인스턴스는 종료된다.
    /// 사용자가 UAC를 취소하면 false를 반환하고 아무 일도 일어나지 않는다.
    /// </summary>
    bool RestartElevated();
}
