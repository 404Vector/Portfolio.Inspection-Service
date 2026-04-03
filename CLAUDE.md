# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Portfolio.Inspection-Service is a .NET application for an inspection service consisting of the following projects:

**Shared Libraries**
- **Core**: 공통 인터페이스, 공통 데이터 구조, 도메인 열거형. 의존성 최하단.
- **Core.Grpc**: gRPC proto 중앙 저장소. 모든 서비스의 proto 파일과 생성 코드를 포함.
- **Core.Logging**: 서비스 전반 로깅 표준화 래퍼.
- **Core.SharedMemory**: MMF 기반 링버퍼 구현. unsafe 코드 격리.
- **Core.Recipe**: 검사 레시피 인터페이스 및 모델 정의.

**Servers**
- **VirtualFrameGrabberServer**: gRPC 서버. 프레임 획득 및 SharedMemory Write (Producer).
- **InspectionServer**: gRPC 서버. SharedMemory Read(Consumer) 및 검사 로직.

**Application**
- **InspectionClient**: Avalonia GUI 클라이언트. gRPC를 통해 서비스를 제어·모니터링.

The projects are organized under `Portfolio.Inspection-Service.sln`.

## Architecture

### 의존성 방향

```
InspectionClient         →  Core, Core.Grpc, Core.Logging, Core.Recipe, Core.SharedMemory
VirtualFrameGrabberServer →  Core, Core.FrameGrabber, Core.Grpc, Core.Logging, Core.SharedMemory
InspectionServer         →  Core, Core.Grpc, Core.Logging, Core.Recipe, Core.SharedMemory
Core.Grpc           →  (proto 생성 코드만 — Grpc.AspNetCore NuGet)
Core.Logging        →  Core
Core.Recipe         →  Core
Core.SharedMemory   →  Core
Core                →  (외부 패키지 없음 또는 BCL만)
```

**규칙:**
- 서비스 간 직접 프로젝트 참조 금지 — gRPC를 통해서만 통신
- Core는 다른 내부 프로젝트를 참조하지 않음
- unsafe 코드는 Core.SharedMemory에만 허용 (`AllowUnsafeBlocks = true`)
- InspectionClient는 SharedMemory에 직접 접근하지 않음 — gRPC 경유

### 프로젝트별 상세 설계 원칙

각 프로젝트 하위 `CLAUDE.md` 참조.

## Commands

```bash
dotnet build        # Build the solution
dotnet test         # Run all tests
dotnet test --filter "FullyQualifiedName~SomeTest"  # Run a single test

# Run individual services
dotnet run --project src/InspectionClient/InspectionClient.csproj
dotnet run --project src/VirtualFrameGrabberServer/VirtualFrameGrabberServer.csproj
dotnet run --project src/InspectionServer/InspectionServer.csproj
```

## Technology Stack

- **.NET 10 / C#**
- **Avalonia** (GUI)
- **gRPC**
- **Shared Memory**
- **Image Processing / Computer Vision**
  - Object Detection (Rule-Based)
  - RANSAC
  - Circle Fitting
  - Defect Inspection
- Test frameworks anticipated: **NUnit** or **MSTest**

## CI/CD — Gemini Code Review

Every PR targeting `main` triggers an automated Gemini code review (`.github/workflows/gemini-review.yml`).

**Flow:**
1. Open a PR → Gemini reviews the diff and posts a checklist comment
2. If all items pass → commit status `Gemini Code Review` is set to `success`
3. Branch protection requires that status to pass → merge is unblocked
4. If Gemini requests changes → status is `failure` → merge is blocked until the diff is fixed and re-reviewed

**Required setup (one-time, repo admin):**

1. Add `GEMINI_API_KEY` as a repository secret:
   `Settings → Secrets and variables → Actions → New repository secret`

2. Enable branch protection on `main`:
   `Settings → Branches → Add branch ruleset`
   - Branch name pattern: `main`
   - Enable **Restrict pushes that create files** (no direct push)
   - Enable **Require status checks to pass**
     - Add required check: `Gemini Code Review`
   - Enable **Require a pull request before merging**

## Coding Conventions

### C# — Google Style Guide 준수

- 들여쓰기: 스페이스 2칸
- 중괄호: K&R 스타일 (여는 중괄호를 같은 줄에)
- 네이밍:
  - 클래스/인터페이스/enum/메서드: `PascalCase`
  - 로컬 변수/파라미터: `camelCase`
  - private 필드: `_camelCase` (언더스코어 접두사)
  - 상수: `PascalCase`
- 파일 하나에 클래스/인터페이스 하나
- `var` 사용: 타입이 오른쪽에서 명확히 드러날 때만 사용
- 불필요한 `this.` 생략
- `using` 지시문: 파일 상단, 알파벳순 정렬
- 접근 제한자는 항상 명시 (`private`, `public` 등 생략 금지)

자세한 내용: [Google C# Style Guide](https://google.github.io/styleguide/csharp-style.html)

### Python — Google Style Guide 준수

- 들여쓰기: 스페이스 4칸
- 네이밍:
  - 클래스: `PascalCase`
  - 함수/변수: `snake_case`
  - 상수: `UPPER_SNAKE_CASE`
  - private: `_single_underscore` 접두사
- 타입 힌트 필수 (`def foo(x: int) -> str:`)
- docstring: Google 스타일 (`Args:`, `Returns:`, `Raises:` 섹션)
- 한 줄 최대 80자

자세한 내용: [Google Python Style Guide](https://google.github.io/styleguide/pyguide.html)

## OOP 설계 원칙

모든 클래스/인터페이스 설계 시 아래 원칙을 준수한다.

### SOLID

- **S** (Single Responsibility): 클래스는 하나의 책임만 가진다.
- **O** (Open/Closed): 확장에는 열려 있고, 수정에는 닫혀 있어야 한다. 구현 변경보다 인터페이스 추가를 우선한다.
- **L** (Liskov Substitution): 하위 타입은 상위 타입을 대체할 수 있어야 한다. `override`는 계약을 깨지 않는 범위 내에서만 허용한다.
- **I** (Interface Segregation): 인터페이스는 클라이언트가 사용하지 않는 메서드를 강제하지 않도록 작게 분리한다.
- **D** (Dependency Inversion): 구체 클래스 대신 인터페이스에 의존한다. 의존성은 DI 컨테이너를 통해 주입한다.

### 추가 원칙

- **캡슐화**: 내부 상태는 `private`으로 보호하고, 필요한 경우에만 프로퍼티 또는 메서드로 노출한다.
- **구성 우선(Composition over Inheritance)**: 상속보다 인터페이스 구현과 구성을 우선한다. 구현 상속은 명확한 IS-A 관계에서만 사용한다.
- **불변성 선호**: 가변 상태를 최소화한다. DTO/값 객체는 `record` 또는 `readonly struct`로 정의한다.
- **Fail Fast**: 잘못된 인자는 생성자 또는 메서드 진입 시점에 즉시 검증한다 (`ArgumentNullException`, `ArgumentOutOfRangeException`).

## Notes

- `GEMINI.md` contains open TODOs for fleshing out architecture, tech stack, and conventions — update both files as the project takes shape.
