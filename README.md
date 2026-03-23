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

## CI/CD

Every PR targeting `main` triggers an automated Claude Code review via `.github/workflows/claude-review.yml`.

- Claude reviews the diff and posts a checklist comment
- All checklist items must pass for the `Claude Code Review` status check to succeed
- Branch protection on `main` requires this status check before merging
