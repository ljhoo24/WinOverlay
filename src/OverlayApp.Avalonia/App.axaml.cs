using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using OverlayApp.Avalonia.Platform;
using OverlayApp.Avalonia.Views;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;
using OverlayApp.Core.Services;
using OverlayApp.Core.ViewModels;

namespace OverlayApp.Avalonia;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    private SettingsWindow? _settingsWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Services = Composition.Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // No main window — the app lives in the tray and only shows the overlay/settings on demand.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var settings = Services.GetRequiredService<AppSettings>();
            var controller = Services.GetRequiredService<AvaloniaOverlayController>();

            // Build the overlay window and attach it to the controller.
            var overlayWindow = Services.GetRequiredService<OverlayWindow>();
            overlayWindow.DataContext = Services.GetRequiredService<OverlayViewModel>();
            controller.Attach(overlayWindow);

            controller.SetOpacity(settings.Overlay.Opacity);
            controller.SetTopMost(true);
            controller.SetPosition(settings.Overlay.X, settings.Overlay.Y);
            controller.SetSize(settings.Overlay.Width, settings.Overlay.Height);
            if (settings.Overlay.Visible)
            {
                controller.Show();
            }

            // Default to click-through on; adjust mode is the only state that disables it.
            // Apply after Show() so the HWND exists.
            controller.SetClickThrough(true);

            // Persist position/size whenever the user drags or resizes the overlay (adjust mode).
            var settingsService = Services.GetRequiredService<ISettingsService>();
            controller.PositionChanged += (_, _) =>
            {
                var pos = controller.GetPosition();
                settings.Overlay.X = pos.X;
                settings.Overlay.Y = pos.Y;
                settingsService.Save(settings);
            };
            controller.SizeChanged += (_, _) =>
            {
                var sz = controller.GetSize();
                settings.Overlay.Width = sz.Width;
                settings.Overlay.Height = sz.Height;
                settingsService.Save(settings);
            };

            // Tray.
            var tray = Services.GetRequiredService<ITrayService>();
            tray.Initialize(new List<TrayMenuItem>
            {
                new("open-settings", "설정 열기"),
                new("toggle-overlay", "오버레이 표시/숨김"),
                new("exit", "종료"),
            });
            tray.DoubleClicked += (_, _) => ShowSettings();
            tray.MenuItemClicked += OnTrayMenuClicked;

            // Global hotkeys.
            var hotkeys = Services.GetRequiredService<IGlobalHotkeyService>();
            var timer = Services.GetRequiredService<TimerService>();
            // Force TimerService construction so its ClockService tick subscription is alive even
            // when no view has resolved it yet.
            _ = timer.IsActive;

            hotkeys.HotkeyPressed += (_, e) =>
            {
                switch (e.Id)
                {
                    case "toggle-overlay":
                        ToggleOverlay();
                        break;
                    case "open-settings":
                        ShowSettings();
                        break;
                    case "timer-toggle":
                        timer.Toggle();
                        Services!.GetRequiredService<OverlayViewModel>().RefreshTimerVisibility();
                        break;
                    case "timer-visibility":
                        ToggleTimerVisibility();
                        break;
                }
            };
            RegisterIfEnabled(hotkeys, "toggle-overlay", settings.ToggleHotkey);
            RegisterIfEnabled(hotkeys, "open-settings", settings.OpenSettingsHotkey);
            RegisterIfEnabled(hotkeys, "timer-toggle", settings.Timer.ToggleHotkey);
            RegisterIfEnabled(hotkeys, "timer-visibility", settings.Timer.VisibilityHotkey);

            // If auto-start was previously enabled, refresh the registry value with the current
            // exe path — Velopack updates can change the path across versions.
            var startup = Services.GetRequiredService<IStartupService>();
            if (settings.StartWithWindows)
            {
                startup.Enable();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnTrayMenuClicked(object? sender, TrayMenuItemClickedEventArgs e)
    {
        switch (e.Id)
        {
            case "open-settings":
                ShowSettings();
                break;
            case "toggle-overlay":
                ToggleOverlay();
                break;
            case "exit":
                Shutdown();
                break;
        }
    }

    private void ShowSettings()
    {
        if (Services is null) return;

        if (_settingsWindow is null)
        {
            _settingsWindow = Services.GetRequiredService<SettingsWindow>();
            _settingsWindow.DataContext = Services.GetRequiredService<SettingsViewModel>();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }

    private static void RegisterIfEnabled(IGlobalHotkeyService hotkeys, string id, HotkeyDefinition def)
    {
        if (def.Enabled) hotkeys.Register(id, def);
    }

    private void ToggleTimerVisibility()
    {
        if (Services is null) return;
        var settings = Services.GetRequiredService<AppSettings>();
        var settingsService = Services.GetRequiredService<ISettingsService>();
        settings.Timer.Enabled = !settings.Timer.Enabled;
        settingsService.Save(settings);
        Services.GetRequiredService<OverlayViewModel>().RefreshTimerVisibility();
    }

    private void ToggleOverlay()
    {
        if (Services is null) return;
        var controller = Services.GetRequiredService<IOverlayController>();
        var settings = Services.GetRequiredService<AppSettings>();
        var settingsService = Services.GetRequiredService<ISettingsService>();

        if (controller.IsVisible)
        {
            controller.Hide();
            settings.Overlay.Visible = false;
        }
        else
        {
            controller.Show();
            settings.Overlay.Visible = true;
        }
        settingsService.Save(settings);
    }

    private void Shutdown()
    {
        if (Services is { } sp)
        {
            sp.GetRequiredService<ITrayService>().Dispose();
            (sp as IDisposable)?.Dispose();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
