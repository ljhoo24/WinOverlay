using System;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;

namespace OverlayApp.Avalonia.Platform;

/// <summary>
/// macOS 전역 단축키는 미구현(Carbon RegisterEventHotKey + 손이 큰 작업).
/// Register는 항상 false를 반환해 설정 UI에 "등록 실패"로 표시된다.
/// 오버레이 토글 등은 트레이 메뉴로 대신한다.
/// </summary>
public sealed class MacGlobalHotkeyService : IGlobalHotkeyService
{
#pragma warning disable CS0067 // 이벤트 미사용 — 인터페이스 계약용
    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
#pragma warning restore CS0067

    public bool Register(string id, HotkeyDefinition definition) => false;

    public void Unregister(string id)
    {
    }
}
