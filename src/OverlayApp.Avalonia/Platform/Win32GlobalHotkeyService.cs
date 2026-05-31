using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;

namespace OverlayApp.Avalonia.Platform;

public sealed class Win32GlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const int HWND_MESSAGE = -3;
    private static readonly string ClassName = "OverlayApp_HotkeyMsgWnd_" + Guid.NewGuid().ToString("N");

    private readonly IUiDispatcher _dispatcher;
    private readonly Dictionary<string, int> _ids = new();
    private readonly Dictionary<int, string> _idsReverse = new();
    private int _nextHotkeyId = 1;
    private IntPtr _hwnd;
    private Native.WndProcDelegate? _wndProc;
    private ushort _classAtom;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public Win32GlobalHotkeyService(IUiDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        CreateMessageWindow();
    }

    private void CreateMessageWindow()
    {
        var hInstance = Native.GetModuleHandle(null);
        _wndProc = WndProc;

        var wc = new Native.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<Native.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = ClassName,
        };

        _classAtom = Native.RegisterClassEx(ref wc);
        if (_classAtom == 0)
        {
            throw new InvalidOperationException(
                $"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");
        }

        _hwnd = Native.CreateWindowEx(
            WS_EX_TOOLWINDOW,
            ClassName,
            "OverlayAppHotkey",
            0,
            0, 0, 0, 0,
            new IntPtr(HWND_MESSAGE),
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
        }
    }

    private IntPtr WndProc(IntPtr h, uint msg, IntPtr w, IntPtr l)
    {
        if (msg == WM_HOTKEY)
        {
            var hotkeyId = w.ToInt32();
            if (_idsReverse.TryGetValue(hotkeyId, out var stringId))
            {
                _dispatcher.Post(() =>
                    HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(stringId)));
            }
        }
        return Native.DefWindowProc(h, msg, w, l);
    }

    public bool Register(string id, HotkeyDefinition definition)
    {
        if (_hwnd == IntPtr.Zero) return false;

        // If already registered under this id, unregister first.
        Unregister(id);

        if (!TryGetVirtualKey(definition.Key, out var vk)) return false;

        var hotkeyId = _nextHotkeyId++;
        var mods = (uint)definition.Modifiers;
        if (!Native.RegisterHotKey(_hwnd, hotkeyId, mods, vk))
        {
            return false;
        }

        _ids[id] = hotkeyId;
        _idsReverse[hotkeyId] = id;
        return true;
    }

    public void Unregister(string id)
    {
        if (!_ids.TryGetValue(id, out var hotkeyId)) return;
        Native.UnregisterHotKey(_hwnd, hotkeyId);
        _ids.Remove(id);
        _idsReverse.Remove(hotkeyId);
    }

    private static bool TryGetVirtualKey(string key, out uint vk)
    {
        if (string.IsNullOrEmpty(key)) { vk = 0; return false; }

        var k = key.Trim().ToUpperInvariant();

        // Single letter or digit: VK matches uppercase ASCII.
        if (k.Length == 1)
        {
            var c = k[0];
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
            {
                vk = c;
                return true;
            }
        }

        // F1..F24.
        if (k.Length >= 2 && k[0] == 'F' && int.TryParse(k.AsSpan(1), out var n) && n >= 1 && n <= 24)
        {
            vk = (uint)(0x70 + (n - 1));
            return true;
        }

        // A few named keys.
        switch (k)
        {
            case "SPACE": vk = 0x20; return true;
            case "ENTER": case "RETURN": vk = 0x0D; return true;
            case "TAB": vk = 0x09; return true;
            case "ESC": case "ESCAPE": vk = 0x1B; return true;
        }

        vk = 0;
        return false;
    }

    public void Dispose()
    {
        foreach (var hotkeyId in _ids.Values)
        {
            Native.UnregisterHotKey(_hwnd, hotkeyId);
        }
        _ids.Clear();
        _idsReverse.Clear();

        if (_hwnd != IntPtr.Zero)
        {
            Native.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        if (_classAtom != 0)
        {
            Native.UnregisterClass(ClassName, Native.GetModuleHandle(null));
            _classAtom = 0;
        }
    }

    private static class Native
    {
        public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
            public IntPtr hIconSm;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int width, int height,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
