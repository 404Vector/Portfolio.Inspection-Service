# Core.FrameGrabber

## 역할

FrameGrabber 도메인의 **계약 계층(Contract Layer)**. `IFrameGrabber` 인터페이스와 관련 모델을 정의한다.
`FrameGrabberService`의 구현체(`MockFrameGrabber` 등)가 이 인터페이스를 구현한다.

## 포함 대상

- `IFrameGrabber` — 프레임 획득 제어, 동적 파라미터/명령 계약
- `GrabbedFrame` — 획득된 프레임 데이터 (픽셀 버퍼 + 메타데이터)
- `GrabberConfig` — 획득 설정 (모드, 해상도, 프레임레이트 등)
- `GrabberStatus` — 현재 상태 스냅샷
- `ParameterDescriptor` / `ParameterValue` — 동적 파라미터 메타데이터 및 값 타입
- `CommandDescriptor` / `CommandResult` — 동적 명령 메타데이터 및 실행 결과

## 제외 대상

- 구현체 (구현은 `FrameGrabberService.Grabbers.*`)
- gRPC, SharedMemory 등 인프라 의존성
- 검사(Inspection) 관련 타입

## 의존성 규칙

```
Core.FrameGrabber → Core
```

- 외부 NuGet 패키지를 추가하지 않는다.
- `AllowUnsafeBlocks`를 활성화하지 않는다.

## 네임스페이스

- `Core.FrameGrabber.Interfaces`
- `Core.FrameGrabber.Models`
