using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;

namespace OverlayApp.Core.ViewModels;

public sealed partial class HotkeyEditorViewModel : ObservableObject
{
    private readonly string _hotkeyId;
    private readonly IGlobalHotkeyService _hotkeys;
    private readonly Func<HotkeyDefinition> _read;
    private readonly Action<HotkeyDefinition> _write;
    private readonly Action _persist;
    private readonly Func<bool> _isMasterEnabled;

    public string Title { get; }

    [ObservableProperty] private bool _ctrl;
    [ObservableProperty] private bool _alt;
    [ObservableProperty] private bool _shift;
    [ObservableProperty] private bool _win;
    [ObservableProperty] private string _key = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    public HotkeyEditorViewModel(
        string title,
        string hotkeyId,
        IGlobalHotkeyService hotkeys,
        Func<HotkeyDefinition> read,
        Action<HotkeyDefinition> write,
        Action persist,
        Func<bool> isMasterEnabled)
    {
        Title = title;
        _hotkeyId = hotkeyId;
        _hotkeys = hotkeys;
        _read = read;
        _write = write;
        _persist = persist;
        _isMasterEnabled = isMasterEnabled;

        var def = read();
        _ctrl = def.Modifiers.HasFlag(HotkeyModifiers.Control);
        _alt = def.Modifiers.HasFlag(HotkeyModifiers.Alt);
        _shift = def.Modifiers.HasFlag(HotkeyModifiers.Shift);
        _win = def.Modifiers.HasFlag(HotkeyModifiers.Win);
        _key = def.Key;
        _status = _isMasterEnabled() ? $"현재: {def}" : $"마스터 OFF — 저장만: {def}";
    }

    [RelayCommand]
    private void Apply()
    {
        var mods = HotkeyModifiers.None;
        if (Ctrl) mods |= HotkeyModifiers.Control;
        if (Alt) mods |= HotkeyModifiers.Alt;
        if (Shift) mods |= HotkeyModifiers.Shift;
        if (Win) mods |= HotkeyModifiers.Win;

        var def = new HotkeyDefinition
        {
            Modifiers = mods,
            Key = (Key ?? string.Empty).Trim(),
        };

        _hotkeys.Unregister(_hotkeyId);

        if (!_isMasterEnabled())
        {
            // 마스터 OFF 상태에선 OS 등록은 건너뛰고 설정만 저장
            _write(def);
            _persist();
            Status = $"마스터 OFF — 저장만: {def}";
            return;
        }

        if (_hotkeys.Register(_hotkeyId, def))
        {
            _write(def);
            _persist();
            Status = $"등록됨: {def}";
        }
        else
        {
            _hotkeys.Register(_hotkeyId, _read());
            Status = $"등록 실패 (다른 앱이 사용 중일 수 있음). 현재: {_read()}";
        }
    }
}
