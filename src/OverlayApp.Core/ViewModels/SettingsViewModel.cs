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
    private readonly IStartupService _startup;
    private readonly OverlayViewModel _overlay;
    private readonly WeatherUpdater _weatherUpdater;
    private readonly TimerService _timerService;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _hotkeysEnabled;

    [ObservableProperty]
    private int _opacityPercent;

    [ObservableProperty]
    private bool _isAdjustMode;

    public HotkeyEditorViewModel ToggleHotkeyEditor { get; }

    public HotkeyEditorViewModel OpenSettingsHotkeyEditor { get; }

    public HotkeyEditorViewModel TimerToggleHotkeyEditor { get; }

    public HotkeyEditorViewModel TimerVisibilityHotkeyEditor { get; }

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

    [ObservableProperty]
    private bool _timerEnabled;

    [ObservableProperty]
    private bool _timerSoundEnabled;

    [ObservableProperty]
    private string _timerStatus = "정지됨";

    public ObservableCollection<WorldClockEntryViewModel> WorldClockEntries => _overlay.WorldClockEntries;

    public ObservableCollection<TimerInstanceViewModel> TimerItems { get; } = new();

    public IReadOnlyList<string> AvailableTimeZoneIds { get; } =
        TimeZoneInfo.GetSystemTimeZones().Select(z => z.Id).OrderBy(s => s).ToList();

    public SettingsViewModel(
        ISettingsService settingsService,
        IOverlayController controller,
        IGlobalHotkeyService hotkeys,
        IStartupService startup,
        OverlayViewModel overlay,
        WeatherUpdater weatherUpdater,
        TimerService timerService,
        AppSettings settings)
    {
        _settingsService = settingsService;
        _controller = controller;
        _hotkeys = hotkeys;
        _startup = startup;
        _overlay = overlay;
        _weatherUpdater = weatherUpdater;
        _timerService = timerService;
        _settings = settings;

        _startWithWindows = _settings.StartWithWindows;
        _hotkeysEnabled = _settings.HotkeysEnabled;
        _opacityPercent = (int)(_settings.Overlay.Opacity * 100);
        _isAdjustMode = _overlay.IsAdjustMode;

        Func<bool> isMaster = () => _hotkeysEnabled;
        ToggleHotkeyEditor = new HotkeyEditorViewModel(
            title: "오버레이 표시/숨김",
            hotkeyId: "toggle-overlay",
            hotkeys: _hotkeys,
            read: () => _settings.ToggleHotkey,
            write: def => _settings.ToggleHotkey = def,
            persist: Persist,
            isMasterEnabled: isMaster);
        OpenSettingsHotkeyEditor = new HotkeyEditorViewModel(
            title: "설정창 열기",
            hotkeyId: "open-settings",
            hotkeys: _hotkeys,
            read: () => _settings.OpenSettingsHotkey,
            write: def => _settings.OpenSettingsHotkey = def,
            persist: Persist,
            isMasterEnabled: isMaster);
        TimerToggleHotkeyEditor = new HotkeyEditorViewModel(
            title: "타이머 일괄 시작/일시정지/재개",
            hotkeyId: "timer-toggle",
            hotkeys: _hotkeys,
            read: () => _settings.Timer.ToggleHotkey,
            write: def => _settings.Timer.ToggleHotkey = def,
            persist: Persist,
            isMasterEnabled: isMaster);
        TimerVisibilityHotkeyEditor = new HotkeyEditorViewModel(
            title: "타이머 오버레이 표시 토글",
            hotkeyId: "timer-visibility",
            hotkeys: _hotkeys,
            read: () => _settings.Timer.VisibilityHotkey,
            write: def => _settings.Timer.VisibilityHotkey = def,
            persist: Persist,
            isMasterEnabled: isMaster);

        _use24Hour = _settings.Clock.Use24Hour;
        _useFahrenheit = _settings.WeatherCommon.Unit == TemperatureUnit.Fahrenheit;
        _apiKey = _settings.WeatherCommon.ApiKey;
        _refreshMinutes = _settings.WeatherCommon.RefreshMinutes;
        _locationWeatherEnabled = _settings.LocationWeather.Enabled;
        _locationConsent = _settings.LocationWeather.ConsentGranted;
        _cityWeatherEnabled = _settings.CityWeather.Enabled;
        _cityName = _settings.CityWeather.CityName;
        _worldClockEnabled = _settings.WorldClock.Enabled;

        _timerEnabled = _settings.Timer.Enabled;
        _timerSoundEnabled = _settings.Timer.SoundEnabled;

        // TimerItems collection 초기화 (저장된 항목 로드)
        foreach (var spec in _settings.Timer.Items)
        {
            var vm = new TimerInstanceViewModel(spec);
            vm.Changed += OnTimerItemChanged;
            TimerItems.Add(vm);
        }
        TimerItems.CollectionChanged += (_, _) => SyncTimerToSettings();

        _timerService.Changed += (_, _) => OnTimerStateChanged();
        OnTimerStateChanged();

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

    private void RefreshTimerStatus()
    {
        var rts = _timerService.Runtimes;
        if (rts.Count == 0) { TimerStatus = "등록된 타이머 없음"; return; }
        if (_timerService.HasAnyRunning)
        {
            var running = rts.Count(r => r.State == TimerState.Running);
            TimerStatus = $"진행 중: {running}개";
        }
        else if (_timerService.HasAnyPaused)
        {
            var paused = rts.Count(r => r.State == TimerState.Paused);
            TimerStatus = $"일시정지: {paused}개";
        }
        else
        {
            TimerStatus = $"정지됨 (등록 {rts.Count}개)";
        }
    }

    public string TimerToggleButtonText
    {
        get
        {
            if (_timerService.HasAnyRunning) return "일시정지";
            if (_timerService.HasAnyPaused) return "재개";
            return "시작";
        }
    }

    private void OnTimerStateChanged()
    {
        OnPropertyChanged(nameof(TimerToggleButtonText));
        RefreshTimerStatus();
        _overlay.RefreshTimerVisibility();
    }

    public AppSettings Settings => _settings;

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (value) _startup.Enable();
        else _startup.Disable();
        _settings.StartWithWindows = value;
        Persist();
    }

    partial void OnHotkeysEnabledChanged(bool value)
    {
        _settings.HotkeysEnabled = value;

        // 4개 단축키 일괄 등록/해제.
        var ids = new (string id, HotkeyDefinition def)[]
        {
            ("toggle-overlay", _settings.ToggleHotkey),
            ("open-settings", _settings.OpenSettingsHotkey),
            ("timer-toggle", _settings.Timer.ToggleHotkey),
            ("timer-visibility", _settings.Timer.VisibilityHotkey),
        };

        foreach (var (id, def) in ids)
        {
            _hotkeys.Unregister(id);
            if (value) _hotkeys.Register(id, def);
        }

        // 에디터 상태 텍스트 갱신
        foreach (var editor in new[] { ToggleHotkeyEditor, OpenSettingsHotkeyEditor, TimerToggleHotkeyEditor, TimerVisibilityHotkeyEditor })
        {
            editor.Status = value
                ? $"등록됨: {EditorDef(editor)}"
                : $"마스터 OFF — 저장만: {EditorDef(editor)}";
        }

        Persist();

        static string EditorDef(HotkeyEditorViewModel ed)
        {
            var mods = HotkeyModifiers.None;
            if (ed.Ctrl) mods |= HotkeyModifiers.Control;
            if (ed.Alt) mods |= HotkeyModifiers.Alt;
            if (ed.Shift) mods |= HotkeyModifiers.Shift;
            if (ed.Win) mods |= HotkeyModifiers.Win;
            return new HotkeyDefinition { Modifiers = mods, Key = ed.Key }.ToString();
        }
    }

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

    partial void OnTimerEnabledChanged(bool value)
    {
        _settings.Timer.Enabled = value;
        _overlay.RefreshTimerVisibility();
        Persist();
    }

    partial void OnTimerSoundEnabledChanged(bool value)
    {
        _settings.Timer.SoundEnabled = value;
        Persist();
    }

    [RelayCommand]
    private void AddTimerItem()
    {
        var spec = new TimerInstance { Label = "새 타이머" };
        var vm = new TimerInstanceViewModel(spec);
        vm.Changed += OnTimerItemChanged;
        TimerItems.Add(vm);
    }

    [RelayCommand]
    private void RemoveTimerItem(TimerInstanceViewModel? item)
    {
        if (item is null) return;
        item.Changed -= OnTimerItemChanged;
        TimerItems.Remove(item);
    }

    private void OnTimerItemChanged(object? sender, EventArgs e) => SyncTimerToSettings();

    private void SyncTimerToSettings()
    {
        _settings.Timer.Items = TimerItems.Select(vm => vm.ToModel()).ToList();
        _timerService.Sync();
        Persist();
    }

    [RelayCommand]
    private void ToggleTimer()
    {
        _timerService.Toggle();
    }

    [RelayCommand]
    private void StopTimer()
    {
        _timerService.StopAll();
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task RefreshWeather()
    {
        await _weatherUpdater.TriggerRefresh();
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

    private void Persist() => _settingsService.Save(_settings);
}
