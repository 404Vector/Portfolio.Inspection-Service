# VirtualFrameGrabberServer

## 역할

클라이언트로부터 웨이퍼 이미지와 ScanPlan을 전달받아 Shot별 FOV 영역을 crop하여 프레임을 생성하고,
**SharedMemory 링버퍼에 기록하는 gRPC 서버 (Producer)**.
gRPC를 통해 클라이언트가 Frame Grabber를 제어(설정, 획득 시작/정지, SW 트리거)하고 프레임 스트림을 구독할 수 있다.

## 포함 대상

- gRPC 서버 구현 (`framegrabber.proto` 기반, 네임스페이스: `VirtualFrameGrabberServer.Grpc`)
- `Services/VirtualFrameGrabberService` — ScanPlan 기반 IFrameGrabber 구현체. 웨이퍼 이미지에서 Shot별 crop 수행.
- `Services/FramePumpHostedService` — `IFrameGrabber` → `SharedMemoryRingBuffer` 단일 프로듀서 (lifecycle은 gRPC 서비스가 제어)
- `Services/FrameGrabberGrpcService` — gRPC RPC 구현 및 FramePump 오케스트레이션
- `Services/GrabberParameterStoreService` — 파라미터 레지스트리 및 GrabberConfig 바인딩

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

## VirtualFrameGrabberService 동작 흐름

1. 클라이언트가 `SetParameterWithStream("die_image", ...)` 으로 Die 이미지(PNG/JPEG 인코딩) 전달
2. 클라이언트가 `SetParameterWithStream("scan_plan", JSON bytes)` 으로 ScanPlan 전달
3. `StartAcquisition` → Boustrophedon 순서로 Shot 순회 시작
4. 각 Shot: FOV 물리좌표(µm) → Die 로컬좌표 → Die 이미지 리샘플링 → GrabberConfig.Width×Height 해상도의 GrabbedFrame 생성
5. 모든 Shot 완료 시 자동으로 Idle 상태 전환

### 프레임 생성 (CropShotFrame)

- 출력 프레임 크기 = `GrabberConfig.Width × Height` (카메라 센서 해상도)
- FOV 물리 영역 내 각 출력 픽셀의 웨이퍼 좌표를 계산
- 해당 좌표가 속하는 Die를 `DieMap.FindByCoordinate()`로 탐색
- Die 로컬 좌표로 변환 후 Die 이미지에서 리샘플링 (Die 이미지 해상도 ↔ Die 물리 크기 비율)
- Die 영역 밖 픽셀은 검정(0)

### 좌표 변환

- 이미지 관례: 원점 = 좌상단, Y↓
- 웨이퍼 좌표: 원점 = 중심, Y↑
- Die 이미지 ↔ Die 물리: `dieScaleX = dieImageWidth / dieWidthUm`, `dieScaleY = dieImageHeight / dieHeightUm`
- 출력 프레임 ↔ FOV 물리: `umPerPxX = fovWidthUm / outWidth`, `umPerPxY = fovHeightUm / outHeight`

## FramePump lifecycle

- `FramePumpHostedService`는 `IHostedService` 구현. 앱 종료 시 Hosting이 StopAsync를 호출.
- `StartAcquisition` RPC → `_grabber.StartAsync()` 후 `_pump.StartPump()` 호출.
- `StopAcquisition` RPC → `_pump.StopPumpAsync()` 후 `_grabber.StopAsync()` 호출.
- `Start` / `StopAsync`는 `lock`으로 보호하여 SPSC 전제를 보장한다.
- `FrameWritten` 이벤트 — 프레임이 링버퍼에 기록된 직후 발행. SW 트리거 등 단일 프레임 완료 대기에 사용.

## SharedMemory 사용 원칙

- `GrabbedFrame`을 `SharedMemoryRingBuffer.Write()`에 직접 전달하지 않는다.
- `GrabbedFrame` → 원시 파라미터(`byte[]`, `width`, `height`, `PixelFormat` 등)로 변환 후 `Write()` 호출.
- `Write()` 완료 후 반환된 `FrameInfo`를 gRPC 응답(`FrameHandle`)으로 변환하여 전달.

## 동적 파라미터 / 명령

- `IFrameGrabber.GetParameters()` / `GetCommands()`로 구현체가 지원하는 항목을 노출한다.
- gRPC: `GetCapabilities`, `GetParameter`, `SetParameter`, `SetParameterWithStream`, `ExecuteCommand` RPC로 클라이언트에 노출.
- `ParameterValue`는 `oneof(int64, double, bool, string, bytes)` — proto 타입은 `Core.Grpc.FrameGrabber` 네임스페이스.
- `SetParameterWithStream` — Client Streaming RPC. 대용량 바이너리 데이터(Die 이미지 등)를 chunk 단위로 전송.

### 지원 파라미터

| 키 | 타입 | 설명 |
|---|---|---|
| `frame_rate_hz` | Double | 연속 모드 프레임 레이트 (1.0–1000.0 Hz) |
| `pixel_format` | String | 출력 픽셀 포맷 (Mono8/Rgb8/Bgr8) |
| `acquisition_mode` | String | 획득 모드 (Continuous/Triggered) |
| `die_image` | Bytes | Die 이미지 PNG/JPEG 인코딩 (`SetParameterWithStream` 사용) |
| `scan_plan` | Bytes | ScanPlan JSON (UTF-8 bytes, `SetParameterWithStream` 사용) |

## 설계 원칙

- `IFrameGrabber` 구현체는 SRP에 따라 프레임 생성 책임만 가진다 — 링버퍼 접근 금지.
- `FramePumpHostedService`는 `IFrameGrabber`와 `SharedMemoryRingBuffer` 사이의 단일 책임 어댑터다.
- gRPC 서비스 메서드는 얇게 유지한다: 입력 파싱 → 도메인 객체 호출 → 응답 매핑. 비즈니스 로직은 서비스 메서드 내부에 작성하지 않는다.

## 네임스페이스

- `VirtualFrameGrabberServer.Services` — 서비스 구현체, gRPC 서비스
- `VirtualFrameGrabberServer.Utils` — 매퍼 유틸리티
- `Core.Grpc.FrameGrabber` — proto 생성 코드 (`Core.Grpc` 프로젝트, `csharp_namespace`)
