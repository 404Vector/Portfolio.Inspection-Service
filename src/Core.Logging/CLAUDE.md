# Core.Logging

## 역할

서비스 전반의 **로깅 표준화**. `Microsoft.Extensions.Logging` 위에 얇은 래퍼를 제공하여
모든 서비스가 일관된 방식으로 로그를 기록하도록 한다.

## 포함 대상

- `ILogService` — 서비스 전반에서 사용하는 로깅 인터페이스
- `CompositeLogService` — 여러 `ILogService` 구현을 묶는 합성체
- `MicrosoftLogService` — `Microsoft.Extensions.Logging` 기반 구현
- `FileLoggerFactory` — 파일 출력 로거 팩토리
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

## 설계 원칙

- `ILogService`는 ISP에 따라 최소한의 로깅 메서드만 정의한다 (`Debug`, `Info`, `Warning`, `Error`).
- `CompositeLogService`는 구성(Composition)으로 여러 출력 대상을 지원한다 — 상속 금지.
- 로그 호출 시 `caller` 컨텍스트(클래스, 메서드명)는 `[CallerMemberName]` 등 컴파일러 지원 특성을 활용한다.

## 네임스페이스

`Core.Logging.*`
