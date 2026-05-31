using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OverlayApp.Core.Abstractions;
using OverlayApp.Core.Models;
using OverlayApp.Core.Services;

namespace OverlayApp.Core.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IOverlayController _controller;
    private readonly IGlobalHotkeyService _hotkeys;
    private readonly OverlayViewModel _overlay;
    private readonly WeatherUpdater _weatherUpdater;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private int _opacityPercent;

    [ObservableProperty]
    private bool _isAdjustMode;

    [ObservableProperty]
    private bool _hotkeyCtrl;

    [ObservableProperty]
    private bool _hotkeyAlt;

    [ObservableProperty]
    private bool _hotkeyShift;

    [ObservableProperty]
    private bool _hotkeyWin;

    [ObservableProperty]
    private string _hotkeyKey = "O";

    [ObservableProperty]
    private string _hotkeyStatus = string.Empty;

    [ObservableProperty]
    private bool _use24Hour;

    [ObservableProperty]
    private bool _useFahrenheit;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private int _refreshMinutes;

    [ObservableProperty]
    private bool _locationWeatherEnabled;

    [ObservableProperty]
    private bool _locationConsent;

    [ObservableProperty]
    private bool _cityWeatherEnabled;

    [ObservableProperty]
    private string _cityName = string.Empty;

    [ObservableProperty]
    private bool _worldClockEnabled;

    public ObservableCollection<WorldClockEntryViewModel> WorldClockEntries => _overlay.WorldClockEntries;

    public IReadOnlyList<string> AvailableTimeZoneIds { get; } = TimeZoneInfo.GetSystemTimeZones().Select(z => z.Id).OrderBy(s => s).ToList();

    public SettingsViewModel(
        ISettingsService settingsService,
        IOverlayController controller,
        IGlobalHotkeyService hotkeys,
        OverlayViewModel overlay,
        WeatherUpdater weatherUpdater,
        AppSettings settings)
    {
        _settingsService = settingsService;
        _controller = controller;
        _hotkeys = hotkeys;
        _overlay = overlay;
        _weatherUpdater = weatherUpdater;
        _settings = settings;

        _opacityPercent = (int)(_settings.Overlay.Opacity * 100);
        _isAdjustMode = _overlay.IsAdjustMode;

        var mods = _settings.ToggleHotkey.Modifiers;
        _hotkeyCtrl = mods.HasFlag(HotkeyModifiers.Control);
        _hotkeyAlt = mods.HasFlag(HotkeyModifiers.Alt);
        _hotkeyShift = mods.HasFlag(HotkeyModifiers.Shift);
        _hotkeyWin = mods.HasFlag(HotkeyModifiers.Win);
        _hotkeyKey = _settings.ToggleHotkey.Key;
        _hotkeyStatus = $"현재: {_settings.ToggleHotkey}";

        _use24Hour = _settings.Clock.Use24Hour;
        _useFahrenheit = _settings.WeatherCommon.Unit == TemperatureUnit.Fahrenheit;
        _apiKey = _settings.WeatherCommon.ApiKey;
        _refreshMinutes = _settings.WeatherCommon.RefreshMinutes;
        _locationWeatherEnabled = _settings.LocationWeather.Enabled;
        _locationConsent = _settings.LocationWeather.ConsentGranted;
        _cityWeatherEnabled = _settings.CityWeather.Enabled;
        _cityName = _settings.CityWeather.CityName;
        _worldClockEnabled = _settings.WorldClock.Enabled;

        foreach (var entry in WorldClockEntries)
        {
            entry.Changed += OnWorldClockEntryChanged;
        }
        WorldClockEntries.CollectionChanged += (_, _) =>
        {
            SyncWorldClockToSettings();
            _overlay.RefreshWorldClockVisibility();
        };
    }

    public AppSettings Settings => _settings;

    partial void OnOpacityPercentChanged(int value)
    {
        var clamped = System.Math.Clamp(value, 0, 100);
        _settings.Overlay.Opacity = clamped / 100.0;
        _controller.SetOpacity(_settings.Overlay.Opacity);
        Persist();
    }

    partial void OnIsAdjustModeChanged(bool value)
    {
        _overlay.IsAdjustMode = value;
        if (value && !_controller.IsVisible)
        {
            _controller.Show();
            _settings.Overlay.Visible = true;
            Persist();
        }
    }

    partial void OnUse24HourChanged(bool value)
    {
        _settings.Clock.Use24Hour = value;
        _overlay.Use24Hour = value;
        Persist();
    }

    partial void OnUseFahrenheitChanged(bool value)
    {
        _settings.WeatherCommon.Unit = value ? TemperatureUnit.Fahrenheit : TemperatureUnit.Celsius;
        Persist();
        _ = _weatherUpdater.TriggerRefresh();
    }

    partial void OnApiKeyChanged(string value)
    {
        _settings.WeatherCommon.ApiKey = (value ?? string.Empty).Trim();
        Persist();
    }

    partial void OnRefreshMinutesChanged(int value)
    {
        _settings.WeatherCommon.RefreshMinutes = System.Math.Max(1, value);
        Persist();
        _weatherUpdater.Reschedule();
    }

    partial void OnLocationWeatherEnabledChanged(bool value)
    {
        if (value && !LocationConsent)
        {
            // Don't enable without consent — revert and let the user check consent first.
            LocationWeatherEnabled = false;
            return;
        }
        _settings.LocationWeather.Enabled = value;
        Persist();
        _ = _weatherUpdater.TriggerRefresh();
    }

    partial void OnLocationConsentChanged(bool value)
    {
        _settings.LocationWeather.ConsentGranted = value;
        if (!value)
        {
            // Consent revoked → disable location weather and clear cached coords.
            _settings.LocationWeather.Enabled = false;
            _settings.LocationWeather.LastLatitude = null;
            _settings.LocationWeather.LastLongitude = null;
            LocationWeatherEnabled = false;
        }
        Persist();
    }

    partial void OnCityWeatherEnabledChanged(bool value)
    {
        _settings.CityWeather.Enabled = value;
        Persist();
        _ = _weatherUpdater.TriggerRefresh();
    }

    partial void OnCityNameChanged(string value)
    {
        _settings.CityWeather.CityName = value ?? string.Empty;
        Persist();
    }

    partial void OnWorldClockEnabledChanged(bool value)
    {
        _settings.WorldClock.Enabled = value;
        _overlay.RefreshWorldClockVisibility();
        Persist();
    }

    [RelayCommand]
    private void AddWorldClock()
    {
        var entry = new WorldClockEntryViewModel("새 항목", TimeZoneInfo.Local.Id);
        entry.Changed += OnWorldClockEntryChanged;
        WorldClockEntries.Add(entry);
    }

    [RelayCommand]
    private void RemoveWorldClock(WorldClockEntryViewModel? entry)
    {
        if (entry is null) return;
        entry.Changed -= OnWorldClockEntryChanged;
        WorldClockEntries.Remove(entry);
    }

    private void OnWorldClockEntryChanged(object? sender, EventArgs e) => SyncWorldClockToSettings();

    private void SyncWorldClockToSettings()
    {
        _settings.WorldClock.Entries = WorldClockEntries
            .Select(vm => new WorldClockEntry { Label = vm.Label, TimeZoneId = vm.TimeZoneId })
            .ToList();
        Persist();
    }

    [RelayCommand]
    private void ApplyHotkey()
    {
        var mods = HotkeyModifiers.None;
        if (HotkeyCtrl) mods |= HotkeyModifiers.Control;
        if (HotkeyAlt) mods |= HotkeyModifiers.Alt;
        if (HotkeyShift) mods |= HotkeyModifiers.Shift;
        if (HotkeyWin) mods |= HotkeyModifiers.Win;

        var def = new HotkeyDefinition
        {
            Modifiers = mods,
            Key = (HotkeyKey ?? string.Empty).Trim(),
        };

        _hotkeys.Unregister("toggle-overlay");
        if (_hotkeys.Register("toggle-overlay", def))
        {
            _settings.ToggleHotkey = def;
            Persist();
            HotkeyStatus = $"등록됨: {def}";
        }
        else
        {
            _hotkeys.Register("toggle-overlay", _settings.ToggleHotkey);
            HotkeyStatus = $"등록 실패 (다른 앱이 사용 중일 수 있음). 현재: {_settings.ToggleHotkey}";
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task RefreshWeather()
    {
        await _weatherUpdater.TriggerRefresh();
    }

    private void Persist() => _settingsService.Save(_settings);
}
