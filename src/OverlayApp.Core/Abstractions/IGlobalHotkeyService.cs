using System;
using OverlayApp.Core.Models;

namespace OverlayApp.Core.Abstractions;

public sealed class HotkeyPressedEventArgs : EventArgs
{
    public string Id { get; }

    public HotkeyPressedEventArgs(string id) => Id = id;
}

public interface IGlobalHotkeyService
{
    event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    bool Register(string id, HotkeyDefinition definition);

    void Unregister(string id);
}
