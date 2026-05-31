# 상시 정보 오버레이 프로그램 — 구현 명세서

## 0. 이 문서의 목적

이 문서는 Claude Code가 구현할 데스크톱 오버레이 프로그램의 명세서다.
**1차 구현은 Avalonia UI로 진행하되, 추후 WPF로 전환할 수 있도록 설계해야 한다.**
따라서 아래 "핵심 설계 원칙(3장)"은 기능 요구사항보다 우선하는 제약이며, 어떤 경우에도 위반해서는 안 된다.

구현 시 이 문서의 장 번호를 근거로 작업 단위를 나누고, 단계(9장)를 따라 점진적으로 구현한다.

---

## 1. 프로그램 개요

화면 위에 상시 정보를 표시하는 오버레이 유틸리티다.

- 실행 후 창을 띄우지 않고 시스템 트레이로 최소화 상태로 시작한다.
- 전역 단축키로 오버레이 표시/숨김을 토글한다.
- 오버레이에는 현재 시간, (옵션) 세계 시간, (옵션·승인 필요) 현재 위치 기반 날씨, (옵션) 사용자가 지정한 지역의 날씨를 표시한다.
- 옵션 기능은 사용자가 활성화하지 않으면 오버레이에 표시되지 않는다.
- 트레이 아이콘을 더블클릭하면 설정창이 열린다.
- 설정창에서 옵션, 오버레이 위치 조정, 투명도를 제어한다.

대상 플랫폼은 현재 Windows 단독이다. 단, 3장의 설계 원칙에 따라 핵심 로직은 플랫폼/프레임워크에 독립적으로 작성한다.

---

## 2. 기술 스택

| 항목 | 선택 | 비고 |
|---|---|---|
| 런타임 | .NET 8 (LTS) | |
| UI 프레임워크 | Avalonia UI 11.x (최신 안정판) | View 계층에만 사용 |
| MVVM | CommunityToolkit.Mvvm | Avalonia·WPF 양쪽에서 동일하게 동작하므로 ViewModel 이식성 확보 |
| 의존성 주입 | Microsoft.Extensions.DependencyInjection | 인터페이스–구현 연결에 사용 |
| 설정 저장 | System.Text.Json | 파일 기반 JSON |
| HTTP | System.Net.Http (HttpClient) | 날씨/지오로케이션 호출 |
| 날씨 API | OpenWeatherMap (기본). API 키는 설정에서 입력 | 무료 티어 호출 한도 고려 |
| 위치 추정 | IP 기반 지오로케이션 서비스 | 정밀 위치 불필요, 프레임워크 비의존 선택 |

> 트레이 아이콘은 Avalonia 내장 `TrayIcon`을 사용하되, **반드시 인터페이스를 통해 사용한다(3·5장 참조).**
> 클릭 통과·전역 단축키는 Windows에서 Win32 P/Invoke로 구현한다. 이 코드는 WPF로 전환해도 거의 그대로 재사용된다.

---

## 3. 핵심 설계 원칙 (전환 대비) — 최우선 제약

전체 솔루션을 다음 세 계층으로 분리한다.

1. **Core 계층 (UI 프레임워크 비의존)**
   - 모든 ViewModel, 서비스, 모델, 추상화 인터페이스를 포함한다.
   - **Avalonia/WPF 등 어떤 UI 프레임워크도 참조하지 않는다.** `Avalonia.*` 네임스페이스 using이 단 한 줄도 있어서는 안 된다.
   - 화면에 표시할 모든 상태와 동작은 ViewModel의 속성·커맨드로 노출한다.

2. **플랫폼 연동 계층 (인터페이스 + 구현)**
   - 클릭 통과, 전역 단축키, 트레이, 창 투명도/위치/항상위 등 OS·프레임워크에 의존하는 기능은 **인터페이스로 추상화(5장)** 하고, 구현은 View 계층 쪽에 둔다.
   - Core는 인터페이스에만 의존한다. 구현체는 DI로 주입한다.

3. **View 계층 (Avalonia 전용)**
   - XAML, View, App 부트스트랩, 플랫폼 인터페이스의 Avalonia 구현을 포함한다.
   - **전환 시 재작성되는 부분은 원칙적으로 이 계층뿐이다.**

### DO

- ViewModel에는 `CommunityToolkit.Mvvm`의 `ObservableObject`, `[ObservableProperty]`, `RelayCommand`만 사용한다.
- 비즈니스 로직은 전부 Core의 서비스로 옮긴다. View의 code-behind에는 로직을 두지 않는다.
- 플랫폼 기능은 인터페이스 호출로만 사용한다.
- 창 핸들(HWND)이 필요한 코드는 인터페이스 구현체 내부로 격리한다.

### DON'T

- Core 프로젝트에서 Avalonia 타입을 절대 참조하지 않는다.
- ViewModel이 View의 컨트롤·창 객체를 직접 참조하지 않는다.
- 클릭 통과/단축키/트레이 호출을 View나 ViewModel에 인라인으로 작성하지 않는다(반드시 인터페이스 경유).
- Avalonia 고유의 정교한 스타일/애니메이션에 과투자하지 않는다. 스타일은 단순하게 유지하고, 사용한 Avalonia 고유 스타일은 별도로 표시(주석)해 전환 시 식별 가능하게 한다.

---

## 4. 솔루션 구조

```
OverlayApp.sln
├─ src/
│  ├─ OverlayApp.Core/                 # UI 프레임워크 비의존
│  │  ├─ Models/
│  │  │  ├─ AppSettings.cs
│  │  │  ├─ WeatherInfo.cs
│  │  │  └─ WorldClockEntry.cs
│  │  ├─ Abstractions/                 # 플랫폼 연동 인터페이스
│  │  │  ├─ IOverlayController.cs
│  │  │  ├─ IGlobalHotkeyService.cs
│  │  │  ├─ ITrayService.cs
│  │  │  ├─ ISettingsService.cs
│  │  │  ├─ IWeatherService.cs
│  │  │  └─ IGeolocationService.cs
│  │  ├─ Services/                     # 프레임워크 비의존 구현
│  │  │  ├─ SettingsService.cs
│  │  │  ├─ WeatherService.cs
│  │  │  ├─ GeolocationService.cs
│  │  │  └─ ClockService.cs
│  │  └─ ViewModels/
│  │     ├─ OverlayViewModel.cs
│  │     └─ SettingsViewModel.cs
│  │
│  └─ OverlayApp.Avalonia/             # View 계층 (전환 시 교체 대상)
│     ├─ Views/
│     │  ├─ OverlayWindow.axaml
│     │  └─ SettingsWindow.axaml
│     ├─ Platform/                     # 인터페이스의 Avalonia/Win32 구현
│     │  ├─ AvaloniaOverlayController.cs
│     │  ├─ Win32GlobalHotkeyService.cs
│     │  └─ AvaloniaTrayService.cs
│     ├─ App.axaml(.cs)
│     ├─ Program.cs
│     └─ Composition.cs                # DI 등록
```

> `SettingsService`, `WeatherService`, `GeolocationService`, `ClockService`는 UI 비의존이므로 Core에 둔다.
> `IOverlayController`, `IGlobalHotkeyService`, `ITrayService`의 **구현**은 창 핸들/프레임워크 의존성이 있으므로 View 계층(`Platform/`)에 둔다.

---

## 5. 추상화 인터페이스 명세

각 인터페이스는 Core의 `Abstractions/`에 정의한다. 메서드명·책임만 명세하며 구현 코드는 작성 단계에서 정한다.

### IOverlayController
오버레이 창의 플랫폼 동작을 제어한다. 구현체는 창 핸들에 접근한다.
- `SetClickThrough(bool enabled)` — 입력 통과 on/off. on이면 마우스 클릭이 아래 창으로 전달된다.
- `SetOpacity(double value)` — 0.0~1.0 투명도.
- `SetTopMost(bool enabled)` — 항상 위.
- `GetPosition()` / `SetPosition(x, y)` — 화면 좌표. 멀티 모니터에서 보이는 영역 안으로 보정한다.
- 창을 작업표시줄/Alt-Tab에 노출하지 않도록 도구 창 스타일을 적용한다.

### IGlobalHotkeyService
- `Register(string id, HotkeyDefinition def)` / `Unregister(string id)`
- 단축키 입력 시 발생하는 이벤트(예: `HotkeyPressed`)를 노출한다.
- Windows 구현은 Win32 `RegisterHotKey` 사용.

### ITrayService
- 트레이 아이콘 표시/제거.
- 컨텍스트 메뉴 항목(설정 열기, 종료 등) 제공.
- 더블클릭 이벤트(`DoubleClicked`)를 노출한다.

### ISettingsService
- `Load()` / `Save(AppSettings settings)`
- 저장 위치는 사용자 AppData 경로의 JSON 파일.
- 최초 실행 시 기본값으로 생성한다.

### IWeatherService
- `GetByCoordinatesAsync(lat, lon)` / `GetByCityAsync(cityName)` → `WeatherInfo`
- API 키·단위(섭씨/화씨)는 설정에서 받는다.
- 호출 실패 시 예외를 던지지 않고 결과에 오류 상태를 담아 반환한다(오버레이가 깨지지 않게).

### IGeolocationService
- `GetCurrentAsync()` → 좌표(위도/경도) 및 추정 지역명.
- IP 기반 추정. 동의가 없으면 호출하지 않는다(동의 흐름은 6.8 참조).

---

## 6. 기능 요구사항

### 6.1 시작 동작 / 트레이
- 실행 시 창을 표시하지 않고 트레이 아이콘만 표시한다.
- 트레이 더블클릭 → 설정창 열기.
- 트레이 컨텍스트 메뉴: "설정 열기", "오버레이 표시/숨김", "종료".
- **단일 인스턴스**: Mutex로 중복 실행을 막는다. 이미 실행 중이면 새 인스턴스는 종료한다.

### 6.2 오버레이 창
- 투명 배경, 테두리 없음, 항상 위.
- 작업표시줄/Alt-Tab에 노출되지 않는다.
- 활성화된 항목들을 세로로 배치(시간 → 세계 시간 → 날씨 → 지정 지역 날씨 순).
- 비활성 항목은 렌더링하지 않는다(공간도 차지하지 않음).
- 표시/숨김 상태는 단축키와 트레이 메뉴로 토글된다.

### 6.3 전역 단축키
- 오버레이 표시/숨김 토글.
- 단축키 조합은 설정에서 변경 가능. 기본값: `Ctrl + Alt + O`.
- 앱이 포커스를 갖지 않은 상태에서도 동작해야 한다.

### 6.4 위치 조정 모드
- 설정창의 "위치 조정" 버튼으로 조정 모드 진입/해제.
- **조정 모드 ON**: 오버레이가 마우스 입력을 받고, 드래그로 이동 가능. 시각적으로 조정 중임을 알 수 있게 한다(예: 테두리 강조).
- **조정 모드 OFF**: `IOverlayController.SetClickThrough(true)`로 클릭 통과 상태가 되어 오버레이가 클릭되지 않고 단순 표시만 된다.
- 이동된 위치는 설정에 저장하고 다음 실행 시 복원한다.

### 6.5 투명도
- 설정창에서 슬라이더로 0~100% 조절.
- 변경 시 즉시 오버레이에 반영(`IOverlayController.SetOpacity`).
- 설정에 저장·복원.

### 6.6 현재 시간 (항상 표시)
- 로컬 시간을 초 단위로 갱신.
- 12/24시간 형식은 설정에서 선택 가능(기본 24시간).

### 6.7 세계 시간 (옵션)
- 사용자가 타임존을 1개 이상 추가/삭제할 수 있다.
- 각 항목은 라벨 + 해당 시간.
- 옵션 비활성 시 오버레이에 미표시.

### 6.8 현재 날씨 — 위치 기반 (옵션, 최초 승인 필요)
- 이 옵션을 처음 활성화할 때 **위치 정보 사용 동의 안내를 띄우고, 사용자가 동의해야만** 지오로케이션을 호출한다.
- 동의 여부는 설정에 저장한다. 동의가 없으면 이 항목은 비활성으로 간주한다.
- 추정 좌표로 날씨를 조회해 표시(지역명, 기온, 상태).
- 갱신 주기는 설정 가능(기본 10분). API 한도를 넘지 않게 한다.

### 6.9 지정 지역 날씨 (옵션)
- 사용자가 입력한 지역명(도시)의 날씨를 표시.
- 6.8과 독립적으로 동작(동의 불필요).
- 옵션 비활성 시 미표시.

### 6.10 설정창
- 옵션 토글: 세계 시간, 위치 기반 날씨, 지정 지역 날씨.
- 세계 시간 항목 추가/삭제.
- 지정 지역명 입력.
- 시간 형식, 단위(℃/℉), 날씨 API 키, 갱신 주기.
- 단축키 변경.
- 위치 조정 버튼.
- 투명도 슬라이더.
- 설정 변경은 즉시(또는 저장 버튼으로) 오버레이에 반영.

### 6.11 설정 영속화
- 모든 설정을 7장의 스키마로 JSON 저장.
- 앱 시작 시 로드하여 오버레이/설정창 초기 상태를 구성.

---

## 7. 데이터 모델 (설정 스키마)

`AppSettings`에 포함될 항목(필드 단위로 정의한다):

- 오버레이: 표시 여부, X/Y 위치, 투명도(0.0~1.0).
- 단축키: 토글 단축키 정의(수정키 + 키).
- 시간: 12/24시간 형식.
- 세계 시간: 옵션 활성 여부, 항목 목록(각 항목 = 라벨 + 타임존 ID).
- 위치 기반 날씨: 옵션 활성 여부, 위치 사용 동의 여부, 마지막 조회 좌표(캐시용).
- 지정 지역 날씨: 옵션 활성 여부, 도시명.
- 날씨 공통: API 키, 단위(℃/℉), 갱신 주기(분).

> API 키 등 민감 값은 평문 JSON에 저장되므로, 저장 위치는 사용자 전용 AppData 경로로 한다. (필요 시 후속 단계에서 보호 방식 강화 검토)

---

## 8. 비기능 요구사항

- **경량 상주**: 상시 백그라운드 실행을 전제로 메모리·CPU 점유를 낮게 유지. 타이머·날씨 갱신은 필요한 주기로만 동작.
- **안정성**: 네트워크/날씨 API 실패가 오버레이 렌더링이나 앱 동작을 중단시키지 않는다. 실패 시 이전 값 또는 오류 표기를 유지.
- **이식성**: 3장 원칙 준수. Core 프로젝트는 컴파일 시 어떤 UI 프레임워크도 참조하지 않아야 한다(빌드로 검증 가능해야 함).
- **단일 인스턴스** 보장.

---

## 9. 구현 단계 (Phase)

각 단계는 빌드·실행 가능한 상태로 마무리한다.

- **Phase 0 — 스캐폴딩**: 솔루션·두 프로젝트 생성, DI(`Composition.cs`) 구성, Core가 UI 비의존임을 보장하는 구조 확립.
- **Phase 1 — 트레이 + 설정창 골격**: 트레이 상주, 더블클릭으로 빈 설정창 열기, 종료. 단일 인스턴스(Mutex).
- **Phase 2 — 오버레이 기본**: 투명·항상위·도구창 스타일의 오버레이에 현재 시간 표시.
- **Phase 3 — 클릭 통과 / 위치 조정 / 투명도**: `IOverlayController` 구현, 조정 모드 토글·드래그 이동·위치 저장, 투명도 슬라이더 연동.
- **Phase 4 — 전역 단축키**: `IGlobalHotkeyService`(Win32) 구현, 토글 동작, 설정에서 단축키 변경.
- **Phase 5 — 날씨**: `IWeatherService`·`IGeolocationService` 구현, 위치 동의 흐름(6.8), 지정 지역 날씨(6.9).
- **Phase 6 — 세계 시간 / 옵션 표시 로직**: 옵션 on/off에 따른 오버레이 항목 동적 표시/숨김.
- **Phase 7 — 설정 영속화 / 마무리**: 전체 설정 저장·복원, 예외 처리, 점검.

---

## 10. WPF 전환 시 영향 범위 (참고)

이 명세대로 구현되면 전환 시 다음과 같이 갈린다.

- **재사용(거의 무수정)**: `OverlayApp.Core` 전체 — 모델, 서비스, ViewModel, 인터페이스. Win32 P/Invoke 로직(클릭 통과·단축키)도 본질적으로 동일하므로 핵심 구현이 재사용된다.
- **재작성**: `OverlayApp.Avalonia`의 XAML View, App 부트스트랩, 인터페이스 구현체의 프레임워크 의존 부분(창 핸들 획득 방식, 트레이 구현). 트레이는 WPF에서 별도 라이브러리(NotifyIcon 계열)로 교체.
- **확인 권장**: 본 구현 착수 전, "반투명 + 클릭 통과 토글" 최소 동작을 우선 검증한 뒤 나머지 기능을 쌓는다.
