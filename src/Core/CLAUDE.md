# Core

## 역할

모든 프로젝트가 공유하는 **계약 계층(Contract Layer)**. 인터페이스, 공통 데이터 구조, 도메인 열거형을 정의한다.
의존성 그래프의 최하단으로, 다른 내부 프로젝트를 참조하지 않는다.

## 포함 대상

- 공통 인터페이스 (예: `IInspectionResult`)
- 공통 도메인 열거형 (예: `PixelFormat`, `InspectionStatus`, `GrabberState`, `AcquisitionMode`, `LightMode`, `TriggerMode`, `TriggerActivation`, `ObjectiveMagnification`)
- 공통 값 객체 / DTO (`record` 또는 `readonly struct`)
- 공통 예외 타입 (예: `FrameAcquisitionException`, `InspectionException`)

## 제외 대상

- 구현체 (구현은 각 서비스 또는 `Core.*` 확장 라이브러리에)
- unsafe 코드
- gRPC, Avalonia 등 프레임워크 의존성
- 서비스 비즈니스 로직

## 의존성 규칙

```
Core → (없음. BCL만 허용)
```

- 외부 NuGet 패키지는 원칙적으로 추가하지 않는다.
- `AllowUnsafeBlocks`는 활성화하지 않는다.

## 설계 원칙

- 인터페이스는 ISP(Interface Segregation)에 따라 작고 목적이 명확하게 분리한다.
- DTO/값 객체는 `record` 또는 `readonly struct`로 정의하여 불변성을 보장한다.
- 열거형은 비즈니스 도메인 개념을 명확히 표현하며, 숫자 값을 임의로 변경하지 않는다.
- 예외 클래스는 `System.Exception`을 직접 상속하지 않고 적절한 중간 계층에서 상속한다.

## 네임스페이스

`Core.*`
