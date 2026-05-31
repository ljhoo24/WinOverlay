using Microsoft.Extensions.DependencyInjection;
using OverlayApp.Avalonia.Platform;
using OverlayApp.Avalonia.Views;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;
using OverlayApp.Core.Services;
using OverlayApp.Core.ViewModels;

namespace OverlayApp.Avalonia;

public static class Composition
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        // Settings: load once, share the instance across the app.
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton(sp =>
        {
            var settingsService = sp.GetRequiredService<ISettingsService>();
            return settingsService.Load();
        });

        // Platform/UI services.
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        services.AddSingleton<AvaloniaOverlayController>();
        services.AddSingleton<IOverlayController>(sp => sp.GetRequiredService<AvaloniaOverlayController>());
        services.AddSingleton<IGlobalHotkeyService, Win32GlobalHotkeyService>();
        services.AddSingleton<ITrayService, AvaloniaTrayService>();
        services.AddSingleton<IStartupService, WindowsStartupService>();

        // Core services.
        services.AddSingleton<System.Net.Http.HttpClient>(_ =>
        {
            var client = new System.Net.Http.HttpClient
            {
                Timeout = System.TimeSpan.FromSeconds(15),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OverlayApp/0.1");
            return client;
        });
        services.AddSingleton<ClockService>();
        services.AddSingleton<IWeatherService, WeatherService>();
        services.AddSingleton<IGeolocationService, GeolocationService>();
        services.AddSingleton<WeatherUpdater>();
        services.AddSingleton<IAlarmService, AvaloniaAlarmService>();
        services.AddSingleton<TimerService>();

        // ViewModels.
        services.AddSingleton<OverlayViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views.
        services.AddSingleton<OverlayWindow>();
        services.AddTransient<SettingsWindow>();

        return services.BuildServiceProvider();
    }
}
