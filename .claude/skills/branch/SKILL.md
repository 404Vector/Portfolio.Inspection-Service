---
name: branch
description: Create a new git branch following this project's naming conventions. Use when the user runs /branch or asks to create a new branch.
argument-hint: [description hint]
allowed-tools: Bash(git status:*), Bash(git diff:*), Bash(git log:*), Bash(git branch:*), Bash(git switch:*)
model: sonnet
---

# Branch Creation

## Branch Naming Convention

Pattern: `^(feature|fix|refactor|chore|docs)/[a-z0-9-]+$`

| Prefix    | Use case                        |
|-----------|---------------------------------|
| `feature/`| New functionality               |
| `fix/`    | Bug fixes                       |
| `refactor/`| Code restructuring             |
| `chore/`  | Build, deps, CI                 |
| `docs/`   | Documentation only              |

Description rules:
- Lowercase letters, digits, and hyphens only
- No slashes, underscores, or uppercase letters
- Keep it short and meaningful (2–5 words)

## Context

- Current branch: !`git branch --show-current`
- Git status (short): !`git status --short`
- Staged/unstaged diff summary: !`git diff HEAD --stat`
- Recent commits: !`git log --oneline -5`

## User hint (optional)

$ARGUMENTS

## Your task

Follow these steps in order:

### Step 1 — Check current branch

Read the "Current branch" value above.

- If it is **not** `main`, warn the user:
  > ⚠️ You are currently on `<branch>`, not `main`. Branching from a non-main branch is usually unintentional.
  Ask whether to proceed or abort. **Wait for their response before continuing.**

### Step 2 — Analyse changes

Read the git status and diff summary above to understand what kind of work is in progress.
If `$ARGUMENTS` is non-empty, incorporate it as a naming hint.

### Step 3 — Propose a branch name

Choose:
- The most fitting prefix from the table above
- A concise, hyphen-separated slug (e.g. `add-frame-grabber-grpc`, `fix-shared-memory-race`)

Validate mentally: the proposed name must match `^(feature|fix|refactor|chore|docs)/[a-z0-9-]+$`.

Output **only** the proposed name, then ask the user to confirm:
> Proposed branch name: `feature/my-feature`
> Proceed? (yes / edit / cancel)

**Wait for their response before continuing.**

### Step 4 — Create and checkout the branch

Once the user confirms (or provides an edited name), run:

```
git switch -c <branch-name>
```

Report the result. Do not do anything else.
