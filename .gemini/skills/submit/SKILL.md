# Submit — 코드 제출 워크플로우

이 스킬은 코드 제출 전체 워크플로우를 자동화합니다. branch 생성, 커밋, PR 생성, 리뷰 대기, 머지, 그리고 브랜치 정리까지의 과정을 순서대로 처리합니다.

## 실행 단계

### Step 1 — Branch 확인
현재 git 브랜치를 확인합니다. 만약 현재 브랜치가 `main` 또는 `master`인 경우, 새로운 브랜치를 생성해야 합니다.
- 새로운 브랜치를 생성해야 한다면, `branch` 스킬을 호출하여 자동으로 새 브랜치를 생성합니다.

### Step 2 — 미커밋 변경사항 처리
`git status`를 실행하여 미커밋된 변경사항이 있는지 확인합니다. 만약 있다면, `commit` 스킬을 호출하여 변경사항을 커밋합니다.

### Step 3 — PR 생성
1. `git log main..HEAD` 와 변경된 파일 목록을 기반으로 PR의 제목과 본문 초안을 작성합니다.
2. `--auto` 플래그가 없으면, 생성된 초안을 사용자에게 보여주고 확인을 받습니다.
3. `--auto` 플래그가 있으면, 확인 절차 없이 즉시 `gh pr create` 명령을 실행하여 PR을 생성합니다.
4. 생성된 PR의 URL과 번호를 출력합니다.

### Step 4 — 리뷰 대기
`check_pr.py` 스크립트를 실행하여 PR의 리뷰 및 CI 상태를 주기적으로 확인합니다. 이 스크립트는 3초 간격으로 상태를 폴링합니다.

```bash
python3 .gemini/skills/submit/scripts/check_pr.py <PR번호>
```

스크립트의 반환 결과에 따라 다음과 같이 처리합니다:
- `changes_requested`: 리뷰어가 변경을 요청한 경우, 리뷰 코멘트를 사용자에게 전달하고 워크플로우를 **일시 중단**합니다. 사용자가 코드를 수정한 후 `/submit`을 다시 실행하면 Step 2부터 워크플로우를 재개합니다.
- `checks_failed`: CI 체크가 실패한 경우, 실패한 체크 목록을 사용자에게 전달하고 워크플로우를 **일시 중단**합니다.
- `approved`: 리뷰가 승인되고 CI가 통과되면 Step 5로 진행합니다.
- `merged`: PR이 이미 머지된 경우 Step 5로 진행합니다.
- `closed`: PR이 머지 없이 닫힌 경우, 사용자에게 알리고 워크플로우를 중단합니다.
- `still_waiting`: 180초의 타임아웃 시간 동안 상태가 결정되지 않은 경우, 스크립트를 재호출하여 확인을 계속합니다.

### Step 5 — 머지 대기 및 브랜치 정리
`cleanup.py` 스크립트를 실행합니다. 이 스크립트는 PR이 머지될 때까지 기다린 후, 관련 브랜치를 정리하는 작업을 수행합니다.

```bash
python3 .gemini/skills/submit/scripts/cleanup.py <PR번호> <branch명>
```

스크립트 내 처리 순서:
1. `main` 또는 `master` 브랜치명을 확인합니다.
2. PR 상태가 `MERGED`가 될 때까지 10초 간격으로 최대 30분 동안 대기합니다.
3. 원격 브랜치를 삭제합니다.
4. `main`(또는 `master`) 브랜치로 체크아웃합니다.
5. `git fetch`와 `git pull`을 실행하여 로컬 저장소를 최신 상태로 업데이트합니다.
6. 로컬 브랜치를 삭제합니다.
7. 모든 과정이 완료되면 완료 메시지를 출력합니다.

## 추가 리소스
- [scripts/git_status.py](scripts/git_status.py) — 현재 git 상태를 수집합니다.
- [scripts/check_pr.py](scripts/check_pr.py) — PR의 리뷰 및 CI 상태를 폴링합니다 (3초 간격, 최대 180초).
- [scripts/cleanup.py](scripts/cleanup.py) — PR 머지를 대기하고 브랜치를 정리합니다.
