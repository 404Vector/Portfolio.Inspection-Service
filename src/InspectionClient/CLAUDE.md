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

## 제외 대상

- SharedMemory 직접 접근 — **반드시 gRPC 경유**
- 검사 비즈니스 로직 (InspectionService에서 처리)
- 프레임 획득 로직 (FrameGrabberService에서 처리)

## 의존성 규칙

```
InspectionClient → Core, Core.Logging
InspectionClient → Grpc.Net.Client (gRPC 클라이언트, NuGet)
InspectionClient → Avalonia (NuGet)
```

- `Core.SharedMemory`를 참조하지 않는다.
- 서비스 프로젝트(`FrameGrabberService`, `InspectionService`)를 직접 참조하지 않는다.

## UI 아키텍처

- **패턴:** MVVM (Avalonia 권장 방식)
- **바인딩:** `AvaloniaUseCompiledBindingsByDefault = true` 유지
- ViewModel은 gRPC 응답을 직접 바인딩하지 않고, 전용 UI 모델로 매핑 후 바인딩한다.

## 네임스페이스

`InspectionClient.*`
