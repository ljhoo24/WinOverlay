# WinOverlay

화면 위에 시간 · 세계 시간 · 날씨를 상시 표시하는 Windows 데스크톱 오버레이 유틸리티입니다.

- 실행 후 창 없이 트레이로 상주
- 전역 단축키(기본 `Ctrl + Alt + O`)로 오버레이 토글
- 오버레이는 항상 위, 클릭 통과(조정 모드 시에만 입력 수신), 작업표시줄/Alt-Tab 미노출
- 12/24시 시계, 사용자 지정 타임존(세계 시간), 위치 기반 날씨(동의 필요), 도시 지정 날씨

## 스택

| 항목 | 선택 |
|---|---|
| 런타임 | .NET 8 |
| UI | Avalonia 12.0.4 (View 계층 한정) |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| 설정 | System.Text.Json (`%APPDATA%\OverlayApp\settings.json`) |
| 날씨 | OpenWeatherMap (`appid` 필요) |
| 지오로케이션 | IP 기반 (`ipapi.co`) |

## 구조 — Core/View 분리

WPF 등 다른 UI 프레임워크로 전환 가능하도록 다음 두 어셈블리로 나뉘어 있습니다.

```
src/
├─ OverlayApp.Core/          # UI 프레임워크 비의존
│  ├─ Models/                # AppSettings, WeatherInfo, HotkeyDefinition, ...
│  ├─ Abstractions/          # IOverlayController, IGlobalHotkeyService, ITrayService, ...
│  ├─ Services/              # SettingsService, ClockService, WeatherService, GeolocationService, WeatherUpdater
│  └─ ViewModels/            # OverlayViewModel, SettingsViewModel, WorldClockEntryViewModel
└─ OverlayApp.Avalonia/      # View 계층 (전환 시 교체 대상)
   ├─ Views/                 # OverlayWindow.axaml, SettingsWindow.axaml
   ├─ Platform/              # Win32 P/Invoke, Avalonia TrayIcon, OverlayController 구현
   ├─ Assets/                # 트레이 아이콘
   ├─ App.axaml(.cs)
   ├─ Program.cs             # 단일 인스턴스 Mutex, Avalonia 부트스트랩
   └─ Composition.cs         # DI 컨테이너 구성
```

`OverlayApp.Core`는 `Avalonia.*` 네임스페이스를 한 줄도 참조하지 않습니다(컴파일 가능 여부로 검증).

플랫폼 의존 기능(클릭 통과 / 전역 단축키 / 트레이 / 창 투명도)은 모두 Core의 인터페이스로 추상화되어 있고, 구현은 `OverlayApp.Avalonia/Platform/`에 위치합니다.

- 클릭 통과: `WS_EX_TRANSPARENT | WS_EX_LAYERED` 토글
- Alt-Tab/작업표시줄 미노출: `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`
- 전역 단축키: `HWND_MESSAGE` 메시지 전용 윈도우 + 커스텀 WndProc + `RegisterHotKey`

## 빌드 / 실행

전제: .NET 8 SDK.

```powershell
dotnet build OverlayApp.slnx
dotnet run --project src/OverlayApp.Avalonia
```

## 배포 빌드 (설치 마법사)

[Velopack](https://github.com/velopack/velopack) 기반 self-contained 단일 파일 설치 마법사를 생성합니다 — 타깃 PC에 .NET 8 미설치라도 실행 가능합니다.

전제: `vpk` 글로벌 툴 설치.

```powershell
dotnet tool install -g vpk
```

빌드:

```powershell
.\tools\build-release.ps1                  # csproj의 <Version> 사용
.\tools\build-release.ps1 -Version 0.2.0   # 명시
```

산출물 위치 — `dist/`:

| 파일 | 용도 |
|---|---|
| `WinOverlay-win-Setup.exe` | **설치 마법사** — 더블클릭 설치, 시작 메뉴/바탕화면 단축키, 제어판 제거 항목 등록 |
| `WinOverlay-win-Portable.zip` | 압축 풀어 그대로 실행 (휴대용) |
| `WinOverlay-0.1.0-full.nupkg`, `RELEASES`, `*.json` | Velopack 자동 업데이트용 패키지 |

스크립트는 다음 두 단계를 수행합니다.

1. `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` → `publish/`
2. `vpk pack` → `dist/`

자동 업데이트 서버 연동은 아직 구성하지 않았습니다(Velopack 패키지는 만들지만 업로드 위치 미설정). 추후 GitHub Releases에 `dist/`를 업로드하면 `UpdateManager`를 통해 자동 업데이트 가능합니다.

## 사용

- **첫 실행**: 트레이에 아이콘이 표시되고, 오버레이가 좌상단에 나타납니다.
- **단축키**: `Ctrl + Alt + O`로 오버레이 표시/숨김. 설정창에서 변경 가능.
- **트레이 클릭**: 설정창 열기. 컨텍스트 메뉴로 표시/숨김 토글 및 종료.
- **위치 조정**: 설정 → 일반 → "조정 모드 ON" → 오버레이를 드래그하여 이동 → "조정 모드 OFF". 조정 모드 OFF 시 클릭이 아래로 통과합니다.
- **투명도**: 설정 → 일반의 슬라이더로 즉시 변경.
- **시계 형식**: 설정 → 시간 → 24시간 형식 체크박스.
- **세계 시간**: 설정 → 시간 → "+ 항목 추가" 후 라벨과 TimeZone ID 입력. 시스템 타임존 자동 완성 제공.
- **날씨**:
  - 설정 → 날씨 → OpenWeatherMap API 키 입력
  - 단위(℃/℉)와 갱신 주기(분, 기본 10) 선택
  - 현재 위치 날씨: **"위치 사용에 동의합니다"** 체크 후 토글 활성화 가능
  - 지정 지역 날씨: 도시명 입력 후 토글 활성화

## 설정 파일

위치: `%APPDATA%\OverlayApp\settings.json`

스키마(JSON):

```jsonc
{
  "overlay":         { "visible": true, "x": 40, "y": 40, "opacity": 0.85 },
  "toggleHotkey":    { "modifiers": 3, "key": "O" },           // modifiers: Alt=1, Ctrl=2, Shift=4, Win=8 (flags)
  "clock":           { "use24Hour": true },
  "worldClock":      { "enabled": false, "entries": [] },
  "locationWeather": { "enabled": false, "consentGranted": false, "lastLatitude": null, "lastLongitude": null },
  "cityWeather":     { "enabled": false, "cityName": "" },
  "weatherCommon":   { "apiKey": "", "unit": 0, "refreshMinutes": 10 }  // unit: 0=Celsius, 1=Fahrenheit
}
```

기본값으로 생성되며, 변경 사항은 즉시 저장됩니다.

## WPF 전환 가이드

- **재사용**: `OverlayApp.Core` 전체 + `Platform/Win32Interop.cs`, `Platform/Win32GlobalHotkeyService.cs` (Win32 P/Invoke 부분은 거의 그대로 동작)
- **재작성**: `OverlayApp.Avalonia/` 내 XAML View, `App`/`Program` 부트스트랩, `AvaloniaTrayService` (WPF에선 NotifyIcon 계열 라이브러리로 교체), `AvaloniaOverlayController`의 창 핸들 획득 방식, `AvaloniaUiDispatcher`
- **검증 우선**: "반투명 + 클릭 통과 토글" 최소 동작이 새 View에서 잘 도는지부터 확인 후 나머지 기능을 쌓기를 권장

## 라이선스

미정.
