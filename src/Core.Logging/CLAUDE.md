# Core.Logging

## 역할

서비스 전반의 **로깅 표준화**. `Microsoft.Extensions.Logging` 위에 얇은 래퍼를 제공하여
모든 서비스가 일관된 방식으로 로그를 기록하도록 한다.

## 포함 대상

- `ILogger` 팩토리 / 빌더 래퍼
- 구조적 로깅(Structured Logging) 공통 컨벤션
- 공통 로그 카테고리 상수
- 로그 포맷 / 출력 설정 (콘솔, 파일 등)

## 제외 대상

- 서비스별 로그 메시지 정의 (각 서비스 내부에서 정의)
- 비즈니스 로직
- unsafe 코드

## 의존성 규칙

```
Core.Logging → Core
Core.Logging → Microsoft.Extensions.Logging (NuGet)
```

- `Core` 외 다른 내부 프로젝트를 참조하지 않는다.
- `AllowUnsafeBlocks`는 활성화하지 않는다.

## 네임스페이스

`Core.Logging.*`
