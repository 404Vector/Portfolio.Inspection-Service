# Core

## 역할

모든 프로젝트가 공유하는 **계약 계층(Contract Layer)**. 인터페이스, 공통 데이터 구조, 도메인 열거형을 정의한다.
의존성 그래프의 최하단으로, 다른 내부 프로젝트를 참조하지 않는다.

## 포함 대상

- 공통 인터페이스 (예: `IFrameSource`, `IInspectionResult`)
- 공통 도메인 열거형 (예: `PixelFormat`, `InspectionStatus`)
- 공통 값 객체 / DTO (record 또는 readonly struct)
- 공통 예외 타입

## 제외 대상

- 구현체 (구현은 각 서비스 또는 Core.* 확장 라이브러리에)
- unsafe 코드
- gRPC, Avalonia 등 프레임워크 의존성
- 서비스 비즈니스 로직

## 의존성 규칙

```
Core → (없음. BCL만 허용)
```

- 외부 NuGet 패키지는 원칙적으로 추가하지 않는다.
- `AllowUnsafeBlocks`는 활성화하지 않는다.

## 네임스페이스

`Core.*`
