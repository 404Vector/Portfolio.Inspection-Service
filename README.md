# Portfolio.Inspection-Service

A .NET 10 inspection service system consisting of a desktop GUI client, two gRPC backend services, and shared libraries.

## Architecture

```
┌─────────────────────────────────────────────────────┐
│            InspectionClient (Avalonia GUI)           │
└──────────────┬──────────────────────┬───────────────┘
               │ gRPC                 │ gRPC
               ▼                      ▼
┌──────────────────────┐  ┌──────────────────────────┐
│   InspectionService  │  │   FrameGrabberService    │
│   localhost:5044     │  │   localhost:5273         │
└──────────┬───────────┘  └───────────┬──────────────┘
           │ SharedMemory (MMF)        │ SharedMemory (MMF)
           │         Consumer          │         Producer
           └──────────────────────────┘
                         │
              ┌──────────▼──────────────────┐
              │        Core Libraries        │
              │  Core / Core.Logging         │
              │  Core.SharedMemory           │
              │  Core.FrameGrabber           │
              └──────────────────────────────┘
```

### Dependency Direction

```
InspectionClient    →  Core, Core.Logging
FrameGrabberService →  Core, Core.FrameGrabber, Core.SharedMemory
InspectionService   →  Core, Core.Logging, Core.SharedMemory
Core.Logging        →  Core
Core.SharedMemory   →  Core
Core.FrameGrabber   →  Core
Core                →  (BCL only)
```

**Rules:**
- No direct project references between services — communicate via gRPC only
- `Core` does not reference any other internal project
- `unsafe` code is allowed only in `Core.SharedMemory`
- `InspectionClient` does not access shared memory directly — via gRPC only

## Projects

| Project | Type | Role |
|---|---|---|
| **Core** | Shared Library | Common interfaces, data structures, domain enums |
| **Core.Logging** | Shared Library | Service-wide logging standardization wrapper |
| **Core.SharedMemory** | Shared Library | MMF-based ring buffer implementation (unsafe code isolated) |
| **Core.FrameGrabber** | Shared Library | Frame grabber domain contracts and dynamic capability API |
| **FrameGrabberService** | gRPC Service | Frame acquisition and shared memory write (Producer) |
| **InspectionService** | gRPC Service | Shared memory read (Consumer) and inspection logic |
| **InspectionClient** | Desktop App | Cross-platform desktop UI (Avalonia) — controls and monitors services via gRPC |

## Technology Stack

- **.NET 10 / C#**
- **Avalonia 11** — cross-platform desktop UI
- **gRPC / ASP.NET Core** — inter-service communication over HTTP/2
- **Shared Memory (MMF)** — high-throughput frame transfer via ring buffer
- **CommunityToolkit.Mvvm** — MVVM source generators
- **Serilog** — structured file logging
- **NUnit** — unit and integration tests
- **Computer Vision**
  - Object Detection (Rule-Based)
  - RANSAC
  - Circle Fitting
  - Defect Inspection

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Getting Started

### Build

```bash
dotnet build
```

### Run

Start each component in a separate terminal:

```bash
# Frame grabber (start first — SharedMemory producer)
dotnet run --project src/FrameGrabberService/FrameGrabberService.csproj

# Inspection service
dotnet run --project src/InspectionService/InspectionService.csproj

# Desktop client
dotnet run --project src/InspectionClient/InspectionClient.csproj
```

### Test

```bash
dotnet test

# Run a specific test
dotnet test --filter "FullyQualifiedName~SomeTest"
```

## Service Endpoints

| Service | HTTP | HTTPS |
|---|---|---|
| InspectionService | `http://localhost:5044` | `https://localhost:7108` |
| FrameGrabberService | `http://localhost:5273` | `https://localhost:7262` |

## Branching

Branch names must follow the pattern `<prefix>/<description>` (lowercase, hyphen-separated).

| Prefix | Use case |
|--------|----------|
| `feature/` | New functionality |
| `fix/` | Bug fixes |
| `refactor/` | Code restructuring |
| `chore/` | Build, deps, CI |
| `docs/` | Documentation only |

Examples: `feature/circle-detection-threshold`, `fix/framegrabber-null-frame`

PRs with non-conforming branch names are blocked by the `Branch Naming Check` status check.

## CI/CD

Every PR targeting `main` triggers the following automated checks:

| Check | Workflow | Description |
|-------|----------|-------------|
| `Branch Naming Check` | `branch-naming.yml` | Enforces branch naming convention |
| `Gemini Code Review` | `gemini-review.yml` | AI code review — posts checklist comment and blocks merge on failure |
| `Auto Merge` | `auto-merge.yml` | Squash-merges PR with Gemini-generated commit message once all checks pass |

**Flow:**
1. Open a PR → branch naming is validated
2. Gemini reviews the diff and posts a checklist comment
3. If all checks pass → `Auto Merge` squash-merges the PR with a Gemini-generated commit message
4. If Gemini requests changes → merge is blocked until the diff is fixed and re-reviewed

**Required setup (one-time, repo admin):**

1. Add `GEMINI_API_KEY` as a repository secret:
   `Settings → Secrets and variables → Actions → New repository secret`

2. Enable branch protection on `main`:
   `Settings → Branches → Add branch ruleset`
   - Branch name pattern: `main`
   - Enable **Require status checks to pass**
     - Required checks: `Gemini Code Review`, `Check Branch Naming Convention`
   - Enable **Require a pull request before merging**
