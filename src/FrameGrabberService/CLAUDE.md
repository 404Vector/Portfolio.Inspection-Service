# FrameGrabberService

## 역할

카메라 또는 이미지 소스에서 프레임을 획득하고 **SharedMemory 링버퍼에 기록하는 gRPC 서버 (Producer)**.
프레임 획득 완료 시 gRPC 스트림 또는 이벤트로 InspectionService에 알린다.

## 포함 대상

- gRPC 서버 구현 (`framegrabber.proto` 기반)
- 프레임 획득 로직 (`Grabbers/` — 카메라 드라이버, 시뮬레이터 등)
- `FramePumpService` — 획득한 프레임을 `SharedMemoryRingBuffer.Write()`에 전달
- `GrabbedFrame` — 이 서비스 내부 전용 프레임 데이터 타입

## 제외 대상

- 검사(Inspection) 비즈니스 로직
- InspectionService 직접 호출 (gRPC로만 통신)
- SharedMemory Reader 코드 (Consumer는 InspectionService)

## 의존성 규칙

```
FrameGrabberService → Core, Core.Logging, Core.SharedMemory
FrameGrabberService → Grpc.AspNetCore (NuGet)
```

- `InspectionService`, `InspectionApp` 프로젝트를 참조하지 않는다.

## SharedMemory 사용 원칙

- `GrabbedFrame`을 `Core.SharedMemory.SharedMemoryRingBuffer.Write()`에 직접 전달하지 않는다.
- `GrabbedFrame` → 원시 파라미터(byte[], width, height, PixelFormat 등)로 변환 후 Write() 호출.
- Write() 완료 후 반환된 `FrameInfo`를 gRPC 응답(`FrameHandle`)으로 변환하여 전달.

## 네임스페이스

`FrameGrabberService.*`
