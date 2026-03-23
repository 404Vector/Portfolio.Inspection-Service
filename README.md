# Portfolio.Inspection-Service

A .NET 9 inspection service system consisting of a desktop GUI client and two gRPC backend services.

## Architecture

```
┌─────────────────────────────────────────────────────┐
│               Inspector (Avalonia GUI)               │
└──────────────┬──────────────────────┬───────────────┘
               │ gRPC                 │ gRPC
               ▼                      ▼
┌──────────────────────┐  ┌──────────────────────────┐
│   InspectionService  │  │   FrameGrabberService    │
│   localhost:5044     │  │   localhost:5273         │
└──────────────────────┘  └──────────────────────────┘
```

| Component | Role |
|---|---|
| **Inspector** | Cross-platform desktop UI (Avalonia) |
| **InspectionService** | gRPC service — inspection logic (object detection, RANSAC, circle fitting, defect inspection) |
| **FrameGrabberService** | gRPC service — frame acquisition via shared memory |

## Technology Stack

- **.NET 10 / C#**
- **Avalonia 11** — cross-platform desktop UI
- **gRPC / ASP.NET Core** — inter-service communication over HTTP/2
- **Shared Memory** — high-throughput frame transfer
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
# Frame grabber (start first)
dotnet run --project src/FrameGrabberService/FrameGrabberService.csproj

# Inspection service
dotnet run --project src/InspectionService/InspectionService.csproj

# Desktop client
dotnet run --project src/Inspector/Inspector.csproj
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

Both status checks must pass before a PR can be merged into `main`.
