using System;
using System.Collections.Generic;

namespace OverlayApp.Core.Abstractions;

public sealed class TrayMenuItem
{
    public string Id { get; }

    public string Text { get; }

    public TrayMenuItem(string id, string text)
    {
        Id = id;
        Text = text;
    }
}

public sealed class TrayMenuItemClickedEventArgs : EventArgs
{
    public string Id { get; }

    public TrayMenuItemClickedEventArgs(string id) => Id = id;
}

public interface ITrayService
{
    event EventHandler? DoubleClicked;

    event EventHandler<TrayMenuItemClickedEventArgs>? MenuItemClicked;

    void Initialize(IReadOnlyList<TrayMenuItem> menuItems);

    void Dispose();
}
