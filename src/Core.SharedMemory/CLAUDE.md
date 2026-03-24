# Core.SharedMemory

## 역할

MMF(Memory-Mapped File) 기반 **링버퍼의 읽기/쓰기 구현**. unsafe 코드를 이 프로젝트에만 격리한다.
FrameGrabberService(Producer)와 InspectionService(Consumer) 모두가 참조한다.

## 포함 대상

- `SharedMemoryRingBuffer` — Writer(Producer) 구현. SPSC, OverwriteOldest 정책.
- `SharedMemoryRingBufferReader` — Reader(Consumer) 구현. (신규 작성 예정)
- `FrameInfo` — 프레임 메타데이터 레코드 (gRPC 변환의 원본 데이터)
- `RingBufferOptions` — 링버퍼 설정 (슬롯 수, 해상도, PixelFormat 등)
- `RingBufferLayout` / `RingBufferHeader` / `SlotHeader` — 메모리 레이아웃 상수 및 구조체

## 제외 대상

- 프레임 획득 로직 (FrameGrabberService 내부에서 처리)
- gRPC 변환 로직 (각 서비스에서 처리)
- 비즈니스 로직

## 의존성 규칙

```
Core.SharedMemory → Core
```

- `Core` 외 다른 내부 프로젝트를 참조하지 않는다.
- **`AllowUnsafeBlocks = true`** — 의도적. 이 프로젝트에만 허용. 포인터 연산은 이 경계 내에서만 사용한다.

## Write() API 설계 원칙

`GrabbedFrame`(FrameGrabberService 전용 타입)에 의존하지 않는다.
Write 시그니처는 원시 파라미터(byte[], width, height, PixelFormat 등)를 받는다.
FrameGrabberService에서 `GrabbedFrame` → 원시 파라미터로 변환 후 호출한다.

## 메모리 레이아웃

```
[ Offset  0 ] RingBufferHeader (64 bytes, cache-line)
[ Offset 64 ] Slot[0]: SlotHeader(64) + PixelData
[ Offset 64 + slotTotalSize ] Slot[1] ...
```

## 네임스페이스

`Core.SharedMemory.*`
