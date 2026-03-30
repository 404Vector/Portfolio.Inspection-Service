---
name: commit
description: 현재 git 변경사항을 검토하고 자동으로 커밋을 수행합니다.
---

# Git Commit Skill (Autonomous)

이 skill은 git 변경사항을 검토하고, 커밋 메시지를 작성하며, **사용자 확인 없이 자동으로** 커밋을 수행합니다.

## 실행 절차

### Step 1 — 변경사항 및 기록 수집

- `python3 ${GEMINI_SKILL_DIR}/scripts/get_git_info.py` 스크립트를 실행하여 다음 정보를 JSON 형식으로 수집합니다:
  - Staged 파일 목록 및 diff
  - Unstaged 파일 목록 및 diff
  - 최근 커밋 로그 (스타일 참조용)

### Step 2 — 변경사항 분석

- 스크립트가 수집한 정보를 분석하여 변경의 목적과 범위를 파악합니다.
- 하나의 논리적 단위로 커밋하는 것이 적절한지 판단합니다.

### Step 3 — 커밋 메시지 초안 작성

- 분석된 변경 내용을 바탕으로 Conventional Commits 명세에 따라 커밋 메시지 초안을 작성합니다.
  - **Type**: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore` 중에서 가장 적절한 것을 선택합니다.
  - **Scope**: 변경된 주요 모듈이나 기능을 소문자로 표기합니다. (선택)
  - **Subject**: 50자 미만의 명령형 문장으로 요약합니다.
  - **Body**: 변경의 이유와 방식을 설명합니다. (선택)
- 커밋 메시지 마지막 줄에 다음을 추가합니다:
  - `Co-authored-by: Gemini <gemini-author@google.com>`

### Step 4 — 자동 스테이징 및 커밋 실행

- Staged 파일 외에 Unstaged 상태의 모든 변경사항을 `git add .` 명령어로 스테이징합니다.
- `git commit` 명령어를 사용하여 Step 3에서 작성된 메시지로 즉시 커밋을 실행합니다.

### Step 5 — 결과 확인 및 보고

- `git status`와 `git log -n 1`을 실행하여 커밋이 성공적으로 완료되었는지 확인합니다.
- 최종 결과를 사용자에게 보고합니다.
  > **커밋 완료:**
  > - **커밋 해시:** `<commit_hash>`
  > - **커밋 메시지:**
  > ```
  > <commit_message>
  > ```

## 주의사항

- **자율 실행**: 이 skill은 **확인 절차 없이** 자동으로 커밋을 생성합니다. 원치 않는 변경이 커밋될 수 있으므로 주의가 필요합니다.
- **Push 금지**: 이 skill은 `git push`를 수행하지 않습니다. Push는 사용자가 별도로 명시적으로 요청해야 합니다.
- **Hook 실패**: Pre-commit hook 실패 시, 원인을 파악하고 문제를 해결하기 전에는 `--no-verify` 옵션을 사용하지 않습니다.
- **민감 정보**: 커밋에 민감한 정보(`.env` 파일, API 키, 비밀번호 등)가 포함되지 않도록 주의합니다.

## 지원 파일

- `scripts/get_git_info.py`: `git status`, `git diff`, `git log` 결과를 종합하여 JSON으로 출력하는 Python 스크립트.
