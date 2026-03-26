# FrameGrabberService

## 역할

카메라 또는 이미지 소스에서 프레임을 획득하고 **SharedMemory 링버퍼에 기록하는 gRPC 서버 (Producer)**.
gRPC를 통해 클라이언트가 Frame Grabber를 제어(설정, 획득 시작/정지, SW 트리거)하고 프레임 스트림을 구독할 수 있다.

## 포함 대상

- gRPC 서버 구현 (`framegrabber.proto` 기반, 네임스페이스: `FrameGrabberService.Grpc`)
- `Grabbers/` — `IFrameGrabber` 구현체 (카메라 드라이버, 시뮬레이터 등)
  - `MockFrameGrabber` — 그래디언트 패턴 생성 시뮬레이터
  - `FramePump` — `IFrameGrabber` → `SharedMemoryRingBuffer` 단일 프로듀서 (순수 클래스, lifecycle은 gRPC 서비스가 제어)
- `Services/FrameGrabberGrpcService` — gRPC RPC 구현 및 FramePump 오케스트레이션

## 제외 대상

- 검사(Inspection) 비즈니스 로직
- InspectionService 직접 호출 (gRPC로만 통신)
- SharedMemory Reader 코드 (Consumer는 InspectionService)
- `IFrameGrabber` 인터페이스 및 관련 모델 정의 (`Core.FrameGrabber`에 위치)

## 의존성 규칙

```
FrameGrabberService → Core, Core.FrameGrabber, Core.Logging, Core.SharedMemory
FrameGrabberService → Grpc.AspNetCore (NuGet)
```

- `InspectionService`, `InspectionClient` 프로젝트를 참조하지 않는다.

## FramePump lifecycle

- `FramePump`는 `BackgroundService`를 상속하지 않는 순수 클래스.
- `StartAcquisition` RPC → `_grabber.StartAsync()` 후 `_pump.Start()` 호출.
- `StopAcquisition` RPC → `_pump.StopAsync()` 후 `_grabber.StopAsync()` 호출.
- 앱 종료 시 `Program.cs`의 `ApplicationStopping` 훅에서 `_pump.StopAsync()` 호출.

## SharedMemory 사용 원칙

- `GrabbedFrame`을 `SharedMemoryRingBuffer.Write()`에 직접 전달하지 않는다.
- `GrabbedFrame` → 원시 파라미터(byte[], width, height, PixelFormat 등)로 변환 후 `Write()` 호출.
- `Write()` 완료 후 반환된 `FrameInfo`를 gRPC 응답(`FrameHandle`)으로 변환하여 전달.

## 동적 파라미터 / 명령

- `IFrameGrabber.GetParameters()` / `GetCommands()`로 구현체가 지원하는 항목을 노출한다.
- gRPC: `GetCapabilities`, `GetParameter`, `SetParameter`, `ExecuteCommand` RPC로 클라이언트에 노출.
- `ParameterValue`는 `oneof(int64, double, bool, string)` — proto 타입은 `FrameGrabberService.Grpc` 네임스페이스.

## 네임스페이스

- `FrameGrabberService.Grabbers` — 구현체
- `FrameGrabberService.Services` — gRPC 서비스
- `FrameGrabberService.Grpc` — proto 생성 코드 (csharp_namespace)
