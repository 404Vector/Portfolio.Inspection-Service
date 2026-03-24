---
name: submit
description: 코드 제출 전체 워크플로우. branch 생성 → 커밋 → PR 생성 → 리뷰 대기 → 머지 → 브랜치 정리를 순서대로 처리합니다. "submit", "PR 만들어", "PR 올려", "pr 생성", "코드 제출" 키워드로 트리거.
argument-hint: ~
disable-model-invocation: false
user-invocable: true
allowed-tools: ~
model: ~
context: ~
agent: ~
hooks: ~
---

# Submit — 코드 제출 워크플로우

## 현재 Git 상태
`!`python3 /Users/4by4_khs/repos/Portfolio.Inspection-Service/.claude/skills/submit/scripts/git_status.py``

위 결과를 기반으로 아래 단계를 순서대로 실행합니다.

## 실행 단계

### Step 1 — Branch 확인
현재 branch가 `main`/`master`이면:
- 사용자에게 알리고 `/branch` skill을 호출해 새 branch 생성·전환을 요청합니다.
- **사용자 확인 필요.** 거절 시 워크플로우를 중단합니다.

### Step 2 — 미커밋 변경사항 처리
`git_status.py` 결과에 `uncommitted_changes`가 있으면:
- `/commit` skill을 호출해 커밋합니다.

### Step 3 — PR 생성
1. `git log main..HEAD`와 변경 파일 목록을 바탕으로 PR 제목·본문 초안을 작성합니다.
2. 초안을 사용자에게 보여줍니다. **사용자 확인 필요.**
3. 확인 후 `gh pr create`로 PR을 생성하고 PR URL과 번호를 출력합니다.

### Step 4 — 리뷰 대기
`check_pr.py`를 실행합니다. 스크립트가 내부에서 30초 간격으로 폴링합니다:

```bash
python3 /Users/4by4_khs/repos/Portfolio.Inspection-Service/.claude/skills/submit/scripts/check_pr.py <PR번호>
```

반환 결과에 따라 처리:
- `changes_requested` → 리뷰 코멘트를 사용자에게 전달하고 워크플로우를 **일시 중단**합니다.
  수정 완료 후 `/submit`을 재실행하면 Step 2부터 재개합니다.
- `approved` → Step 5로 진행합니다.
- `merged` → Step 5로 진행합니다 (이미 머지됨).
- `closed` → 사용자에게 알리고 중단합니다.
- `still_waiting` → 스크립트를 재호출합니다 (9분 타임아웃 초과 시).

### Step 5 — 머지 대기 및 브랜치 정리
`cleanup.py`를 실행합니다. 스크립트가 내부에서 머지 완료를 확인한 후 정리를 수행합니다:

```bash
python3 /Users/4by4_khs/repos/Portfolio.Inspection-Service/.claude/skills/submit/scripts/cleanup.py <PR번호> <branch명>
```

스크립트 내 처리 순서:
1. PR 상태가 `MERGED`가 될 때까지 대기 (10초 간격, 최대 30분)
2. 원격 브랜치 삭제 (`git push origin --delete <branch>`)
3. `git checkout main`
4. `git fetch origin main && git pull origin main`
5. 로컬 브랜치 삭제 (`git branch -d <branch>`)

완료 메시지를 출력합니다.

## 추가 리소스
- [scripts/git_status.py](scripts/git_status.py) — 현재 git 상태 수집
- [scripts/check_pr.py](scripts/check_pr.py) — PR 리뷰 상태 폴링 (blocking, 최대 9분)
- [scripts/cleanup.py](scripts/cleanup.py) — 머지 대기 + 브랜치 정리
