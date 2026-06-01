namespace OverlayApp.Core.Models;

[System.Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

public sealed class HotkeyDefinition
{
    public bool Enabled { get; set; } = true;

    public HotkeyModifiers Modifiers { get; set; } = HotkeyModifiers.Control | HotkeyModifiers.Alt;

    public string Key { get; set; } = "O";

    public override string ToString()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(Key);
        var combo = string.Join(" + ", parts);
        return Enabled ? combo : $"{combo} (사용 안 함)";
    }
}
