using System;
using System.Runtime.InteropServices;

namespace OverlayApp.Avalonia.Platform;

/// <summary>
/// macOS NSWindow 제어용 objc 런타임 호출. Windows 타깃에서도 컴파일되지만
/// 호출은 macOS에서만 일어난다(Composition에서 분기).
/// </summary>
internal static class MacInterop
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";

    // NSStatusWindowLevel = 25. 일반 앱 창/플로팅 패널 위에 머문다.
    private const long StatusWindowLevel = 25;

    [DllImport(LibObjC, EntryPoint = "sel_registerName")]
    private static extern IntPtr SelRegisterName(string name);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void MsgSendVoidBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool value);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void MsgSendVoidLong(IntPtr receiver, IntPtr selector, long value);

    /// <summary>클릭 통과: NSWindow.ignoresMouseEvents.</summary>
    public static void SetIgnoresMouseEvents(IntPtr nsWindow, bool ignore)
    {
        if (nsWindow == IntPtr.Zero) return;
        try { MsgSendVoidBool(nsWindow, SelRegisterName("setIgnoresMouseEvents:"), ignore); }
        catch { /* objc 미가용 환경(비 macOS) 등 — 무시 */ }
    }

    /// <summary>항상 위: NSWindow.level을 status 레벨로 올린다.</summary>
    public static void SetStatusLevel(IntPtr nsWindow)
    {
        if (nsWindow == IntPtr.Zero) return;
        try { MsgSendVoidLong(nsWindow, SelRegisterName("setLevel:"), StatusWindowLevel); }
        catch { /* 무시 */ }
    }
}
