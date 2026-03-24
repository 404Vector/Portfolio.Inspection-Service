# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Portfolio.Inspection-Service is a .NET application for an inspection service consisting of the following projects:

**Shared Libraries**
- **Core**: 공통 인터페이스, 공통 데이터 구조, 도메인 열거형. 의존성 최하단.
- **Core.Logging**: 서비스 전반 로깅 표준화 래퍼.
- **Core.SharedMemory**: MMF 기반 링버퍼 구현. unsafe 코드 격리.

**Services**
- **FrameGrabberService**: gRPC 서비스. 프레임 획득 및 SharedMemory Write (Producer).
- **InspectionService**: gRPC 서비스. SharedMemory Read(Consumer) 및 검사 로직.

**Application**
- **InspectionApp**: Avalonia GUI 클라이언트. gRPC를 통해 서비스와 통신.
  - 현재 폴더명: `src/Inspector/` → 추후 `src/InspectionApp/`으로 이름 변경 예정.

The projects are organized under `Portfolio.Inspection-Service.sln`.

## Architecture

### 의존성 방향

```
InspectionApp       →  Core, Core.Logging
FrameGrabberService →  Core, Core.Logging, Core.SharedMemory
InspectionService   →  Core, Core.Logging, Core.SharedMemory
Core.Logging        →  Core
Core.SharedMemory   →  Core
Core                →  (외부 패키지 없음 또는 BCL만)
```

**규칙:**
- 서비스 간 직접 프로젝트 참조 금지 — gRPC를 통해서만 통신
- Core는 다른 내부 프로젝트를 참조하지 않음
- unsafe 코드는 Core.SharedMemory에만 허용 (`AllowUnsafeBlocks = true`)
- InspectionApp은 SharedMemory에 직접 접근하지 않음 — gRPC 경유

### 프로젝트별 상세 설계 원칙

각 프로젝트 하위 `CLAUDE.md` 참조.

## Commands

```bash
dotnet build        # Build the solution
dotnet test         # Run all tests
dotnet test --filter "FullyQualifiedName~SomeTest"  # Run a single test

# Run individual services
dotnet run --project src/Inspector/Inspector.csproj
dotnet run --project src/FrameGrabberService/FrameGrabberService.csproj
dotnet run --project src/InspectionService/InspectionService.csproj
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

## Notes

- `GEMINI.md` contains open TODOs for fleshing out architecture, tech stack, and conventions — update both files as the project takes shape.
