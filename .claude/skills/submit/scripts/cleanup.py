#!/usr/bin/env python3
"""PR 머지 완료를 대기한 후 브랜치를 정리합니다.

Usage: python3 cleanup.py <pr_number> <branch_name>

처리 순서:
  1. main/master 브랜치명 사전 확인
  2. PR 상태가 MERGED가 될 때까지 대기 (10초 간격, 최대 30분)
  3. 원격 브랜치 삭제
  4. main(또는 master)으로 checkout
  5. fetch + pull
  6. 로컬 브랜치 삭제
"""
import subprocess
import json
import sys
import time


POLL_INTERVAL = 10     # seconds
MAX_WAIT = 1800        # 30분


def run(cmd_list):
    result = subprocess.run(cmd_list, capture_output=True, text=True)
    return result.stdout.strip(), result.stderr.strip(), result.returncode


def detect_main_branch():
    """로컬/원격에서 main 또는 master 중 사용 중인 브랜치명을 반환합니다."""
    _, _, rc = run(["git", "show-ref", "--verify", "--quiet", "refs/remotes/origin/main"])
    if rc == 0:
        return "main"
    _, _, rc = run(["git", "show-ref", "--verify", "--quiet", "refs/remotes/origin/master"])
    if rc == 0:
        return "master"
    # 로컬 브랜치 확인
    _, _, rc = run(["git", "show-ref", "--verify", "--quiet", "refs/heads/main"])
    if rc == 0:
        return "main"
    return "master"


def get_pr_state(pr_number):
    out, err, rc = run(["gh", "pr", "view", str(pr_number), "--json", "state,mergedAt"])
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

    # Step 1 — main/master 브랜치명 사전 확인
    main_branch = detect_main_branch()
    print(f"기본 브랜치: {main_branch}", file=sys.stderr)

    # Step 2 — 머지 대기
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

    # Step 3 — 원격 브랜치 삭제
    _, err, rc = run(["git", "push", "origin", "--delete", branch])
    if rc != 0:
        if "remote ref does not exist" in err:
            print(f"원격 브랜치 삭제 스킵 (이미 없음)", file=sys.stderr)
        else:
            errors.append(f"원격 브랜치 삭제 실패: {err}")

    # Step 4 — main/master으로 전환
    _, err, rc = run(["git", "checkout", main_branch])
    if rc != 0:
        errors.append(f"{main_branch} checkout 실패: {err}")

    # Step 5 — 최신화
    _, err, rc = run(["git", "fetch", "origin", main_branch])
    if rc != 0:
        errors.append(f"fetch 실패: {err}")

    _, err, rc = run(["git", "pull", "origin", main_branch])
    if rc != 0:
        errors.append(f"pull 실패: {err}")

    # Step 6 — 로컬 브랜치 삭제
    _, err, rc = run(["git", "branch", "-d", branch])
    if rc != 0:
        _, err2, rc2 = run(["git", "branch", "-D", branch])
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
            "message": f"브랜치 '{branch}' 정리 완료. {main_branch} 브랜치가 최신 상태입니다.",
            "elapsed_seconds": int(elapsed)
        }, ensure_ascii=False))


if __name__ == "__main__":
    main()
