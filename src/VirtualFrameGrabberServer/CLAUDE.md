# VirtualFrameGrabberServer

## 역할

카메라 또는 이미지 소스에서 프레임을 획득하고 **SharedMemory 링버퍼에 기록하는 gRPC 서버 (Producer)**.
gRPC를 통해 클라이언트가 Frame Grabber를 제어(설정, 획득 시작/정지, SW 트리거)하고 프레임 스트림을 구독할 수 있다.

## 포함 대상

- gRPC 서버 구현 (`framegrabber.proto` 기반, 네임스페이스: `VirtualFrameGrabberServer.Grpc`)
- `Grabbers/` — `IFrameGrabber` 구현체
  - `FileFrameGrabber` — 스토리지 이미지를 읽어 그랩 이벤트를 발생시키는 구현체
  - `FramePump` — `IFrameGrabber` → `SharedMemoryRingBuffer` 단일 프로듀서 (순수 클래스, lifecycle은 gRPC 서비스가 제어)
- `Services/FrameGrabberGrpcService` — gRPC RPC 구현 및 FramePump 오케스트레이션

## 제외 대상

- 검사(Inspection) 비즈니스 로직
- InspectionServer 직접 호출 (gRPC로만 통신)
- SharedMemory Reader 코드 (Consumer는 InspectionServer)
- `IFrameGrabber` 인터페이스 및 관련 모델 정의 (`Core.FrameGrabber`에 위치)

## 의존성 규칙

```
VirtualFrameGrabberServer → Core, Core.FrameGrabber, Core.Grpc, Core.Logging, Core.SharedMemory
VirtualFrameGrabberServer → Grpc.AspNetCore (NuGet, gRPC 호스팅용)
```

- `InspectionServer`, `InspectionClient` 프로젝트를 참조하지 않는다.

## FramePump lifecycle

- `FramePump`는 `BackgroundService`를 상속하지 않는 순수 클래스 (`IAsyncDisposable` 구현).
- `StartAcquisition` RPC → `_grabber.StartAsync()` 후 `_pump.Start()` 호출.
- `StopAcquisition` RPC → `_pump.StopAsync()` 후 `_grabber.StopAsync()` 호출.
- 앱 종료 시 `Program.cs`의 `ApplicationStopping` 훅에서 `_pump.StopAsync()` 호출.
- `Start` / `StopAsync`는 `lock`으로 보호하여 SPSC 전제를 보장한다.
- `FrameWritten` 이벤트 — 프레임이 링버퍼에 기록된 직후 발행. SW 트리거 등 단일 프레임 완료 대기에 사용.

## SharedMemory 사용 원칙

- `GrabbedFrame`을 `SharedMemoryRingBuffer.Write()`에 직접 전달하지 않는다.
- `GrabbedFrame` → 원시 파라미터(`byte[]`, `width`, `height`, `PixelFormat` 등)로 변환 후 `Write()` 호출.
- `Write()` 완료 후 반환된 `FrameInfo`를 gRPC 응답(`FrameHandle`)으로 변환하여 전달.

## 동적 파라미터 / 명령

- `IFrameGrabber.GetParameters()` / `GetCommands()`로 구현체가 지원하는 항목을 노출한다.
- gRPC: `GetCapabilities`, `GetParameter`, `SetParameter`, `ExecuteCommand` RPC로 클라이언트에 노출.
- `ParameterValue`는 `oneof(int64, double, bool, string)` — proto 타입은 `Core.Grpc.FrameGrabber` 네임스페이스.

## 설계 원칙

- `IFrameGrabber` 구현체는 SRP에 따라 프레임 생성 책임만 가진다 — 링버퍼 접근 금지.
- `FramePump`는 `IFrameGrabber`와 `SharedMemoryRingBuffer` 사이의 단일 책임 어댑터다.
- gRPC 서비스 메서드는 얇게 유지한다: 입력 파싱 → 도메인 객체 호출 → 응답 매핑. 비즈니스 로직은 서비스 메서드 내부에 작성하지 않는다.

## 네임스페이스

- `VirtualFrameGrabberServer.Grabbers` — 구현체
- `VirtualFrameGrabberServer.Services` — gRPC 서비스
- `Core.Grpc.FrameGrabber` — proto 생성 코드 (`Core.Grpc` 프로젝트, `csharp_namespace`)
