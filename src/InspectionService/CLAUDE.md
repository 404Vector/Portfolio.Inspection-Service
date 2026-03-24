# InspectionService

## 역할

SharedMemory 링버퍼에서 프레임을 읽고(Consumer) **검사 로직을 수행하는 gRPC 서버**.
검사 결과를 gRPC 응답으로 InspectionApp에 제공한다.

## 포함 대상

- gRPC 서버 구현 (`greet.proto` → 추후 `inspection.proto`로 대체 예정)
- `SharedMemoryRingBufferReader` 사용 — 링버퍼에서 프레임 읽기 (Consumer)
- 검사 알고리즘 (Image Processing / Computer Vision)
  - Object Detection (Rule-Based)
  - RANSAC
  - Circle Fitting
  - Defect Inspection
- 검사 결과 도메인 모델

## 제외 대상

- 프레임 획득 로직 (FrameGrabberService에서 처리)
- SharedMemory Writer 코드 (Producer는 FrameGrabberService)
- UI 로직

## 의존성 규칙

```
InspectionService → Core, Core.Logging, Core.SharedMemory
InspectionService → Grpc.AspNetCore (NuGet)
```

- `FrameGrabberService`, `InspectionApp` 프로젝트를 참조하지 않는다.

## SharedMemory 사용 원칙

- `SharedMemoryRingBufferReader`를 통해서만 프레임 데이터에 접근한다.
- `FrameInfo`의 `SlotIndex`와 `SharedMemoryKey`로 MMF 슬롯을 직접 읽는다.
- 슬롯 상태(`SlotState`)를 확인 후 읽기를 수행한다 (Ready 상태에서만 읽기).

## 검사 알고리즘 설계 원칙

- 알고리즘 구현은 순수 함수 또는 상태 없는 서비스로 작성한다.
- 이미지 처리 결과는 `Core`에 정의된 공통 결과 타입으로 반환한다.

## 네임스페이스

`InspectionService.*`
