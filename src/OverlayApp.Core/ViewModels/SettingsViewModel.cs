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
    private readonly IElevationService _elevation;
    private readonly OverlayViewModel _overlay;
    private readonly WeatherUpdater _weatherUpdater;
    private readonly TimerService _timerService;
    private readonly SystemMetricsUpdater _systemUpdater;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _hotkeysEnabled;

    [ObservableProperty]
    private int _opacityPercent;

    [ObservableProperty]
    private bool _isAdjustMode;

    public ObservableCollection<MonitorInfo> Monitors { get; } = new();

    [ObservableProperty]
    private MonitorInfo? _selectedMonitor;

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
    private bool _cityWeatherEnabled;

    [ObservableProperty]
    private bool _worldClockEnabled;

    [ObservableProperty]
    private bool _timerEnabled;

    [ObservableProperty]
    private bool _timerSoundEnabled;

    [ObservableProperty]
    private string _timerStatus = "정지됨";

    [ObservableProperty]
    private bool _systemEnabled;

    [ObservableProperty]
    private bool _showMemory;

    [ObservableProperty]
    private bool _showCpuLoad;

    [ObservableProperty]
    private bool _showCpuTemp;

    [ObservableProperty]
    private bool _showGpuTemp;

    [ObservableProperty]
    private int _systemRefreshSeconds;

    [ObservableProperty]
    private bool _needsElevationForTemps;

    public ObservableCollection<WorldClockEntryViewModel> WorldClockEntries => _overlay.WorldClockEntries;

    public ObservableCollection<TimerInstanceViewModel> TimerItems { get; } = new();

    public ObservableCollection<CityWeatherEntryViewModel> CityWeatherEntries { get; } = new();

    public int MaxCityWeatherEntries => CityWeatherSettings.MaxCities;

    public IReadOnlyList<string> AvailableTimeZoneIds { get; } =
        TimeZoneInfo.GetSystemTimeZones().Select(z => z.Id).OrderBy(s => s).ToList();

    public SettingsViewModel(
        ISettingsService settingsService,
        IOverlayController controller,
        IGlobalHotkeyService hotkeys,
        IStartupService startup,
        IElevationService elevation,
        OverlayViewModel overlay,
        WeatherUpdater weatherUpdater,
        TimerService timerService,
        SystemMetricsUpdater systemUpdater,
        AppSettings settings)
    {
        _settingsService = settingsService;
        _controller = controller;
        _hotkeys = hotkeys;
        _startup = startup;
        _elevation = elevation;
        _overlay = overlay;
        _weatherUpdater = weatherUpdater;
        _timerService = timerService;
        _systemUpdater = systemUpdater;
        _settings = settings;

        _startWithWindows = _settings.StartWithWindows;
        _hotkeysEnabled = _settings.HotkeysEnabled;
        _opacityPercent = (int)(_settings.Overlay.Opacity * 100);
        _isAdjustMode = _overlay.IsAdjustMode;

        RefreshMonitors();

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
        _cityWeatherEnabled = _settings.CityWeather.Enabled;
        _worldClockEnabled = _settings.WorldClock.Enabled;

        foreach (var entry in _settings.CityWeather.Cities)
        {
            var vm = new CityWeatherEntryViewModel(entry);
            vm.Changed += OnCityWeatherEntryChanged;
            CityWeatherEntries.Add(vm);
        }
        CityWeatherEntries.CollectionChanged += (_, _) =>
        {
            SyncCityWeatherToSettings();
            AddCityWeatherCommand.NotifyCanExecuteChanged();
            _overlay.RefreshCityWeatherDisplay();
        };

        _timerEnabled = _settings.Timer.Enabled;
        _timerSoundEnabled = _settings.Timer.SoundEnabled;

        _systemEnabled = _settings.System.Enabled;
        _showMemory = _settings.System.ShowMemory;
        _showCpuLoad = _settings.System.ShowCpuLoad;
        _showCpuTemp = _settings.System.ShowCpuTemp;
        _showGpuTemp = _settings.System.ShowGpuTemp;
        _systemRefreshSeconds = _settings.System.RefreshSeconds;
        RecomputeNeedsElevation();

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

    public bool IsElevated => _elevation.IsElevated;

    private void RecomputeNeedsElevation()
    {
        NeedsElevationForTemps = _settings.System.AnyTempRequested && !_elevation.IsElevated;
    }

    private void ApplySystemMetrics()
    {
        _settings.System.Enabled = SystemEnabled;
        _settings.System.ShowMemory = ShowMemory;
        _settings.System.ShowCpuLoad = ShowCpuLoad;
        _settings.System.ShowCpuTemp = ShowCpuTemp;
        _settings.System.ShowGpuTemp = ShowGpuTemp;
        _settings.System.RefreshSeconds = System.Math.Max(1, SystemRefreshSeconds);
        Persist();
        RecomputeNeedsElevation();
        _systemUpdater.Apply();
        _overlay.RefreshSystemDisplay();
    }

    partial void OnSystemEnabledChanged(bool value) => ApplySystemMetrics();

    partial void OnShowMemoryChanged(bool value) => ApplySystemMetrics();

    partial void OnShowCpuLoadChanged(bool value) => ApplySystemMetrics();

    partial void OnShowCpuTempChanged(bool value) => ApplySystemMetrics();

    partial void OnShowGpuTempChanged(bool value) => ApplySystemMetrics();

    partial void OnSystemRefreshSecondsChanged(int value) => ApplySystemMetrics();

    [RelayCommand]
    private void RestartElevated()
    {
        _elevation.RestartElevated();
        // 성공 시 앱이 종료되고 관리자 인스턴스가 뜬다. 실패(UAC 취소) 시 여기로 돌아온다.
    }

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

    [RelayCommand]
    private void RefreshMonitors()
    {
        var prev = SelectedMonitor?.Index;
        Monitors.Clear();
        foreach (var m in _controller.GetMonitors()) Monitors.Add(m);
        SelectedMonitor =
            Monitors.FirstOrDefault(m => m.Index == prev)
            ?? Monitors.FirstOrDefault(m => m.IsPrimary)
            ?? Monitors.FirstOrDefault();
    }

    [RelayCommand]
    private void SnapCorner(string? corner)
    {
        if (Monitors.Count == 0) RefreshMonitors();
        var monitor = SelectedMonitor ?? Monitors.FirstOrDefault();
        if (monitor is null) return;
        if (!Enum.TryParse<OverlayCorner>(corner, out var c)) return;

        _controller.SnapToCorner(monitor.Index, c);

        // SnapToCorner은 프로그램 변경이라 PositionChanged 자동저장 경로를 타지 않음 → 직접 저장.
        var (x, y) = _controller.GetPosition();
        _settings.Overlay.X = x;
        _settings.Overlay.Y = y;

        if (!_controller.IsVisible)
        {
            _controller.Show();
            _settings.Overlay.Visible = true;
        }
        Persist();
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

    partial void OnCityWeatherEnabledChanged(bool value)
    {
        _settings.CityWeather.Enabled = value;
        Persist();
        _overlay.RefreshCityWeatherDisplay();
        _ = _weatherUpdater.TriggerRefresh();
    }

    [RelayCommand(CanExecute = nameof(CanAddCityWeather))]
    private void AddCityWeather()
    {
        if (!CanAddCityWeather()) return;
        var entry = new CityWeatherEntry();
        var vm = new CityWeatherEntryViewModel(entry);
        vm.Changed += OnCityWeatherEntryChanged;
        CityWeatherEntries.Add(vm);
    }

    private bool CanAddCityWeather() => CityWeatherEntries.Count < CityWeatherSettings.MaxCities;

    [RelayCommand]
    private void RemoveCityWeather(CityWeatherEntryViewModel? entry)
    {
        if (entry is null) return;
        entry.Changed -= OnCityWeatherEntryChanged;
        CityWeatherEntries.Remove(entry);
    }

    private void OnCityWeatherEntryChanged(object? sender, EventArgs e)
    {
        SyncCityWeatherToSettings();
        _ = _weatherUpdater.TriggerRefresh();
    }

    private void SyncCityWeatherToSettings()
    {
        _settings.CityWeather.Cities = CityWeatherEntries.Select(vm => vm.ToModel()).ToList();
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
