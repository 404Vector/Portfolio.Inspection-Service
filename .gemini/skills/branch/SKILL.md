# Branch Skill

This skill helps you create and switch to a new git branch automatically.

## Goal

When invoked, you will automatically create or switch to a git branch, ensuring it follows the project's naming conventions. This process should be immediate and not require user confirmation.

## Branch Naming Convention

All branch names must follow this pattern: `^(feature|fix|refactor|chore|docs)/[a-z0-9-]+$`

| Prefix      | Use case                    |
|-------------|-----------------------------|
| `feature/`  | New functionality           |
| `fix/`      | Bug fixes                   |
| `refactor/` | Code restructuring          |
| `chore/`    | Build, dependencies, CI     |
| `docs/`     | Documentation only          |

**Description Rules:**
- The description part must consist of lowercase letters, digits, and hyphens only.
- It should be short and meaningful (2–5 words).
- Do not use slashes, underscores, or uppercase letters in the description.

## Procedure

Follow these steps to create a branch automatically:

### 1. Determine the Branch Name

Your first priority is to determine the branch name without asking the user.

- **Check for a user-provided hint:** If the user invoked the skill with a name (e.g., `/branch feature/add-login-button`), use that name as your primary candidate.
- **Generate from git state:** If no hint is provided, you MUST generate a name. Analyze the output of `git status --short` and `git diff HEAD --stat`. Based on the changes, infer the type of work (feature, fix, etc.) and create a descriptive branch name. For example, if you see changes in documentation files, a `docs/update-readme` name would be appropriate.

### 2. Validate the Branch Name

The determined branch name MUST be validated against the regex `^(feature|fix|refactor|chore|docs)/[a-z0-9-]+$`.

If the name is invalid, you should attempt to fix it to match the convention. For example, convert `feat/My-New_Thing` to `feature/my-new-thing`. Do not ask the user for a correction. If you cannot create a valid name, you should terminate the skill with an explanation.

### 3. Check for Existing Branch and Act

You MUST check if a branch with the validated name already exists.

Use the `run_shell_command` tool to execute:
`git branch --list 'THE_BRANCH_NAME'`

- **If the branch exists:** The command will return the branch name. Immediately switch to it by running `git switch 'THE_BRANCH_NAME'`. Then, proceed to Step 5 to confirm the action to the user.
- **If the branch does not exist:** The command will return empty output. Proceed to the next step to create it.

### 4. Create and Switch to the New Branch

If the branch name is valid and does not already exist, create and switch to it in a single step.

Use the `run_shell_command` tool to execute:
`git switch -c 'THE_BRANCH_NAME'`

### 5. Confirm the Result

After the operation (switching or creating), confirm the outcome by running `git branch --show-current`.

Inform the user of the action you took.
- **For creation:** `✓ Created and checked out new branch: feature/your-new-feature.`
- **For switching:** `✓ Switched to existing branch: feature/your-new-feature.`
