#!/usr/bin/env python3
"""Collect current git state for commit skill."""

import subprocess


def run(cmd):
    result = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    return result.stdout.strip()


def main():
    branch = run("git branch --show-current")
    status = run("git status --short")
    staged_diff = run("git diff --cached")
    unstaged_stat = run("git diff --stat")
    log = run("git log --oneline -5")

    lines = []
    lines.append(f"### 브랜치: {branch}")
    lines.append("")

    lines.append("### 변경 파일")
    lines.append("```")
    lines.append(status if status else "(없음)")
    lines.append("```")
    lines.append("")

    if staged_diff:
        # 너무 긴 diff는 잘라서 주입
        truncated = staged_diff[:4000] + ("\n...(truncated)" if len(staged_diff) > 4000 else "")
        lines.append("### Staged diff")
        lines.append("```diff")
        lines.append(truncated)
        lines.append("```")
    else:
        lines.append("### Staged diff: 없음 (아직 `git add` 전)")
    lines.append("")

    if unstaged_stat:
        lines.append("### Unstaged 변경 통계")
        lines.append("```")
        lines.append(unstaged_stat)
        lines.append("```")
        lines.append("")

    lines.append("### 최근 커밋 (참고용)")
    lines.append("```")
    lines.append(log if log else "(없음)")
    lines.append("```")

    print("\n".join(lines))


if __name__ == "__main__":
    main()
