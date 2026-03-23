# Gemini Context: Portfolio.Inspection-Service

## Project Overview

This project is a .NET application for an "Inspection Service". It consists of three services:

*   **Inspector**: A GUI application built with Avalonia for the user interface.
*   **FrameGrabberService**: A gRPC service for grabbing frames.
*   **InspectionService**: A gRPC service for inspection logic.

The projects are organized in a solution file `Portfolio.Inspection-Service.sln`.

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

## Development Conventions

No development conventions are currently documented.

**TODO:**
*   Document any coding style guidelines.
*   Outline the testing strategy for the project.
*   Add instructions for contributing to the project.
*   Flesh out architecture, tech stack, and conventions.

---
*This file is kept in sync with CLAUDE.md*
