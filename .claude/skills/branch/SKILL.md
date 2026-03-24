---
name: branch
description: >
  Analyzes current git changes and creates a new branch.
  Triggered when the user requests "create branch", "branch", "/branch",
  or when another skill workflow needs to create a branch.
argument-hint: "[description hint] [--auto]"
disable-model-invocation: false
user-invocable: true
allowed-tools:
  - Bash(git status *)
  - Bash(git diff *)
  - Bash(git log *)
  - Bash(git branch *)
  - Bash(git switch *)
model: haiku
context: ~
agent: ~
hooks: ~
---

# Branch Creation

## Branch Naming Convention

Pattern: `^(feature|fix|refactor|chore|docs)/[a-z0-9-]+$`

| Prefix      | Use case                    |
|-------------|-----------------------------|
| `feature/`  | New functionality           |
| `fix/`      | Bug fixes                   |
| `refactor/` | Code restructuring          |
| `chore/`    | Build, deps, CI             |
| `docs/`     | Documentation only          |

Description rules:
- Lowercase letters, digits, and hyphens only
- No slashes, underscores, or uppercase letters
- Keep it short and meaningful (2–5 words)

## Current State

- Current branch: !`git branch --show-current`
- Git status: !`git status --short`
- Diff summary: !`git diff HEAD --stat`
- Recent commits: !`git log --oneline -5`

## User Hint (optional)

$ARGUMENTS

## Flags

- `--auto`: Skip confirmation prompt and create the branch immediately.

## Procedure

### Step 1 — Check current branch

Read the "Current branch" value.

If **not `main`**, print this warning and continue:
> ⚠️ Currently on `<branch>`, not `main`. Branching from a non-main branch.

### Step 2 — Analyze changes

Read the git status and diff summary to understand the nature of the work in progress.
Use `$ARGUMENTS` as a hint for the branch name if provided.

### Step 3 — Propose branch name

Select the appropriate prefix from the table above and compose a concise slug.
Verify the name matches `^(feature|fix|refactor|chore|docs)/[a-z0-9-]+$`.

If `--auto` is present in `$ARGUMENTS`, skip the prompt and proceed directly to Step 4.

Otherwise, output the proposal and wait for user input:
> Proposed branch name: `<branch-name>`
> Proceed? (yes / edit / cancel)

**Wait for response.**
- `yes` → proceed to Step 4
- `edit` → ask for the preferred name, then proceed to Step 4
- `cancel` → abort and print `Cancelled.`

### Step 4 — Create and checkout branch

Execute:

```
git switch -c <branch-name>
```

Print the result:

> ✓ Created and checked out branch `<branch-name>`.
> To undo: `git switch - && git branch -d <branch-name>`
