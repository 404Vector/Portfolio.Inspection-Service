# InspectionClient

## 역할

검사 시스템의 **Avalonia GUI 클라이언트**. 운영자가 서비스를 제어하고 결과를 모니터링하는 애플리케이션.
gRPC **클라이언트**로서 `FrameGrabberService`, `InspectionService`와 통신한다.

## 포함 대상

- Avalonia Views / ViewModels (MVVM 패턴)
- gRPC 클라이언트 코드 (서비스 호출)
- UI 상태 관리
- 결과 시각화 (이미지 렌더링, 검사 결과 표시)
- 서비스 로그 출력

현재 구현된 ViewModel:
- `ViewModelBase` — 공통 로깅/Execute 래퍼
- `MainWindowViewModel` — 탭 내비게이션 및 타이틀 관리
- `InspectionViewModel` — 검사 화면
- `HistoryViewModel` — 이력 화면
- `OpticSettingViewModel` — 광학계 설정 화면
- `AppSettingViewModel` — 앱 설정 화면

## 제외 대상

- 검사 비즈니스 로직 (InspectionService에서 처리)
- 프레임 획득 로직 (FrameGrabberService에서 처리)

## 의존성 규칙

```
InspectionClient → Core, Core.Grpc, Core.Logging, Core.SharedMemory
InspectionClient → Grpc.Net.ClientFactory (gRPC 클라이언트, NuGet)
InspectionClient → Avalonia (NuGet)
InspectionClient → CommunityToolkit.Mvvm (NuGet)
```

- `Core.SharedMemory`는 프레임 픽셀 데이터 읽기 전용으로 참조한다.
  `FrameGrabberService`로부터 `FrameHandle`(gRPC)을 수신한 뒤,
  `SharedMemoryRingBufferReader`로 해당 슬롯의 픽셀 데이터를 읽어 UI에 표시한다.
- 서비스 프로젝트(`FrameGrabberService`, `InspectionService`)를 직접 참조하지 않는다.

## UI 아키텍처 — MVVM

### 기본 원칙

- **패턴:** MVVM. View는 ViewModel에만 의존하고, ViewModel은 View를 알지 못한다.
- **바인딩:** `AvaloniaUseCompiledBindingsByDefault = true` 유지 (컴파일 타임 바인딩 검증).
- ViewModel은 gRPC 응답(`proto` 생성 타입)을 직접 바인딩하지 않는다. 전용 UI 모델(예: `Models/`)로 매핑 후 바인딩한다.

### ViewModel 설계 규칙

- `ViewModelBase`를 상속하고, `CommunityToolkit.Mvvm`의 `[ObservableProperty]` / `[RelayCommand]`를 사용한다.
- 사용자 액션에서 발생하는 예외는 `Execute()` 래퍼를 통해 일관되게 로그를 남긴다.
- ViewModel은 서비스/gRPC 호출만 조율한다 — 직접 비즈니스 로직을 포함하지 않는다.
- 상태는 `[ObservableProperty]` 프로퍼티로만 관리한다. `INotifyPropertyChanged`를 수동으로 구현하지 않는다.
- ViewModel 간 직접 참조는 금지한다. 공유 상태가 필요하면 서비스나 메시지를 통해 통신한다.

### View 설계 규칙

- View의 코드 비하인드(`.axaml.cs`)에는 UI 초기화 외 로직을 작성하지 않는다.
- 이벤트 핸들러가 필요한 경우 ViewModel의 Command로 위임한다.
- 사용자 정의 컨트롤(`Controls/`)은 재사용 가능한 UI 요소만 포함하며, ViewModel과 결합하지 않는다.
- ViewModel이 필요한 View는 `Views/`에 둔다. `Controls/`는 ViewModel 없이 동작하는 순수 UI 컴포넌트만 허용한다.

### 서비스 / DI

- 모든 의존성은 생성자 주입(Constructor Injection)으로 제공한다.
- `IHostBuilder` 기반 DI 컨테이너(`Startup.cs`)를 통해 ViewModel과 서비스를 등록한다.

## 네임스페이스

`InspectionClient.*`
