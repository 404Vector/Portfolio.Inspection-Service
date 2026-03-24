#!/usr/bin/env python3
"""PR 머지 완료를 대기한 후 브랜치를 정리합니다.

Usage: python3 cleanup.py <pr_number> <branch_name>

처리 순서:
  1. PR 상태가 MERGED가 될 때까지 대기 (10초 간격, 최대 30분)
  2. 원격 브랜치 삭제
  3. git checkout main
  4. git fetch origin main && git pull origin main
  5. 로컬 브랜치 삭제
"""
import subprocess
import json
import sys
import time


POLL_INTERVAL = 10     # seconds
MAX_WAIT = 1800        # 30분


def run(cmd, check=False):
    result = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    return result.stdout.strip(), result.stderr.strip(), result.returncode


def get_pr_state(pr_number):
    out, err, rc = run(f"gh pr view {pr_number} --json state,mergedAt")
    if rc != 0:
        return None, err
    data = json.loads(out)
    return data.get("state", ""), None


def main():
    if len(sys.argv) < 3:
        print(json.dumps({"error": "사용법: cleanup.py <pr_number> <branch_name>"}, ensure_ascii=False))
        sys.exit(1)

    pr_number = sys.argv[1]
    branch = sys.argv[2]
    start_time = time.time()

    # Step 1 — 머지 대기
    print(f"PR #{pr_number} 머지 완료를 대기합니다...", file=sys.stderr)
    while True:
        elapsed = time.time() - start_time

        if elapsed >= MAX_WAIT:
            print(json.dumps({
                "status": "timeout",
                "message": f"30분 내에 머지가 확인되지 않았습니다. PR #{pr_number}를 직접 확인해주세요.",
                "elapsed_seconds": int(elapsed)
            }, ensure_ascii=False))
            sys.exit(1)

        state, err = get_pr_state(pr_number)
        if err:
            print(json.dumps({"error": f"PR 상태 확인 실패: {err}"}, ensure_ascii=False))
            sys.exit(1)

        if state == "MERGED":
            print(f"[{int(elapsed)}s] 머지 확인됨. 브랜치 정리를 시작합니다.", file=sys.stderr)
            break

        print(f"[{int(elapsed)}s] 아직 머지 대기 중 (state={state}) — {POLL_INTERVAL}초 후 재확인...", file=sys.stderr)
        time.sleep(POLL_INTERVAL)

    errors = []

    # Step 2 — 원격 브랜치 삭제
    _, err, rc = run(f"git push origin --delete {branch}")
    if rc != 0:
        # 이미 삭제된 경우 무시
        if "remote ref does not exist" in err or "error: unable to delete" not in err:
            print(f"원격 브랜치 삭제 스킵 (이미 없음): {err}", file=sys.stderr)
        else:
            errors.append(f"원격 브랜치 삭제 실패: {err}")

    # Step 3 — main으로 전환
    _, err, rc = run("git checkout main")
    if rc != 0:
        _, err2, rc2 = run("git checkout master")
        if rc2 != 0:
            errors.append(f"main/master checkout 실패: {err}")

    # Step 4 — main 최신화
    _, err, rc = run("git fetch origin main")
    if rc != 0:
        run("git fetch origin master")

    _, err, rc = run("git pull origin main")
    if rc != 0:
        _, err2, rc2 = run("git pull origin master")
        if rc2 != 0:
            errors.append(f"main pull 실패: {err}")

    # Step 5 — 로컬 브랜치 삭제
    _, err, rc = run(f"git branch -d {branch}")
    if rc != 0:
        # 강제 삭제 시도
        _, err2, rc2 = run(f"git branch -D {branch}")
        if rc2 != 0:
            errors.append(f"로컬 브랜치 삭제 실패: {err2}")

    elapsed = time.time() - start_time

    if errors:
        print(json.dumps({
            "status": "completed_with_errors",
            "branch": branch,
            "pr_number": pr_number,
            "errors": errors,
            "elapsed_seconds": int(elapsed)
        }, ensure_ascii=False))
    else:
        print(json.dumps({
            "status": "success",
            "branch": branch,
            "pr_number": pr_number,
            "message": f"브랜치 '{branch}' 정리 완료. main 브랜치가 최신 상태입니다.",
            "elapsed_seconds": int(elapsed)
        }, ensure_ascii=False))


if __name__ == "__main__":
    main()
