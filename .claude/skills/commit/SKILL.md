---
name: commit
description: >
  현재 git 변경사항을 검토하고 커밋을 수행합니다.
  사용자가 "커밋", "commit", "변경사항 저장", "git commit" 등을 요청할 때 트리거됩니다.
argument-hint: ~
disable-model-invocation: false
user-invocable: true
allowed-tools:
  - Bash
model: haiku
context: fork
agent: general-purpose
hooks: ~
---

# Commit

## 현재 Git 상태

`!python3 /Users/4by4_khs/repos/Portfolio.Inspection-Service/.claude/skills/commit/scripts/collect_git_state.py`

## 절차

1. **변경사항 파악** — 위 주입된 git 상태를 분석합니다.
   - staged/unstaged 파일 구분
   - 변경 내용의 목적과 범위 파악

2. **커밋 단위 결정** — 논리적 단위로 나눌지 한 번에 커밋할지 판단합니다.
   - 관련 없는 변경이 섞여 있으면 사용자에게 분리 여부를 묻습니다.

3. **스테이징** — 커밋할 파일을 `git add`로 스테이징합니다.
   - 민감한 파일(.env, credentials 등)은 절대 포함하지 않습니다.

4. **커밋 메시지 작성** — 아래 규칙을 따릅니다.
   - `type(scope): subject` 형식 (Conventional Commits)
   - type: feat, fix, refactor, test, docs, chore, style
   - subject: 명령형, 50자 이하
   - 필요 시 본문에 why 설명
   - 마지막 줄: `Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>`

5. **커밋 실행** — HEREDOC으로 메시지를 전달합니다.

6. **결과 확인** — `git log --oneline -3`으로 커밋이 정상 생성됐는지 확인합니다.

## 주의사항

- pre-commit hook 실패 시 `--no-verify` 사용 금지 — 원인을 파악하고 수정합니다.
- 커밋이 명시적으로 요청된 경우에만 실행합니다.
- push는 별도로 요청받기 전까지 수행하지 않습니다.
