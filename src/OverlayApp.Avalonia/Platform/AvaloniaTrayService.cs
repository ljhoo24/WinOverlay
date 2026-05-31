using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using OverlayApp.Core.Abstractions;

namespace OverlayApp.Avalonia.Platform;

public sealed class AvaloniaTrayService : ITrayService
{
    private TrayIcon? _trayIcon;

    public event EventHandler? DoubleClicked;

    public event EventHandler<TrayMenuItemClickedEventArgs>? MenuItemClicked;

    public void Initialize(IReadOnlyList<TrayMenuItem> menuItems)
    {
        _trayIcon = new TrayIcon
        {
            ToolTipText = "Overlay App",
            Icon = LoadIcon(),
            IsVisible = true,
        };

        var menu = new NativeMenu();
        foreach (var item in menuItems)
        {
            var menuItem = new NativeMenuItem(item.Text);
            var capturedId = item.Id;
            menuItem.Click += (_, _) => MenuItemClicked?.Invoke(this, new TrayMenuItemClickedEventArgs(capturedId));
            menu.Add(menuItem);
        }

        _trayIcon.Menu = menu;
        _trayIcon.Clicked += OnTrayClicked;

        TrayIcon.SetIcons(global::Avalonia.Application.Current!, new TrayIcons { _trayIcon });
    }

    private void OnTrayClicked(object? sender, EventArgs e)
    {
        // Avalonia's TrayIcon.Clicked fires for left-click. Treat as the "double-click open settings"
        // behavior on platforms where double-click isn't natively distinguished.
        DoubleClicked?.Invoke(this, EventArgs.Empty);
    }

    private static WindowIcon LoadIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://WinOverlay/Assets/tray.png"));
        return new WindowIcon(stream);
    }

    public void Dispose()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Clicked -= OnTrayClicked;
            _trayIcon.IsVisible = false;
            _trayIcon = null;
        }
    }
}
