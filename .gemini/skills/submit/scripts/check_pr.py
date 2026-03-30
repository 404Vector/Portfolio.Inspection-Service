#!/usr/bin/env python3
"""PR 리뷰 및 CI 상태를 내부 폴링으로 감시합니다. 터미널 상태 도달 시 JSON을 출력하고 종료합니다.

Usage: python3 check_pr.py <pr_number> [poll_interval_seconds]

반환 status 값:
  merged            - PR이 이미 머지됨
  closed            - PR이 닫힘 (머지 없이)
  approved          - 리뷰 승인 + CI 통과 (머지 준비 완료)
  changes_requested - 리뷰어가 변경 요청
  checks_failed     - CI 체크 실패
  still_waiting     - 타임아웃(180초) 내 결정 없음 → 재호출 필요
"""
import subprocess
import json
import sys
import time


POLL_INTERVAL_DEFAULT = 3    # seconds
MAX_RUNTIME = 180            # 3분

FAILED_CONCLUSIONS = {"FAILURE", "CANCELLED", "TIMED_OUT", "ACTION_REQUIRED"}


def run_gh(pr_number):
    result = subprocess.run(
        ["gh", "pr", "view", str(pr_number),
         "--json", "state,reviewDecision,reviews,headRefName,url,statusCheckRollup"],
        capture_output=True, text=True
    )
    if result.returncode != 0:
        return None, result.stderr.strip()
    return json.loads(result.stdout), None


def extract_review_comments(reviews):
    """CHANGES_REQUESTED 리뷰의 코멘트를 수집합니다."""
    comments = []
    for r in reviews:
        if r.get("state") == "CHANGES_REQUESTED":
            author = r.get("author", {}).get("login", "unknown")
            body = r.get("body", "").strip()
            if body:
                comments.append({"reviewer": author, "comment": body})
    return comments


def analyze_checks(checks):
    """CI 체크 결과를 분석합니다.

    Returns:
        (overall_status, failed_checks)
        overall_status: 'passed' | 'pending' | 'failed'
        failed_checks: 실패한 체크 이름 목록
    """
    if not checks:
        return "passed", []

    failed = []
    pending = []

    for check in checks:
        # CheckRun (GitHub Actions 등): conclusion 필드 존재
        if check.get("conclusion") is not None or check.get("status") == "COMPLETED":
            name = check.get("name", "unknown")
            conclusion = check.get("conclusion") or ""
            status = check.get("status", "")
            if conclusion in FAILED_CONCLUSIONS:
                failed.append(name)
            elif status != "COMPLETED":
                pending.append(name)
        # StatusContext (외부 CI 연동): state 필드로 판단
        elif "state" in check:
            name = check.get("context", "unknown")
            state = check.get("state", "")
            if state in ("FAILURE", "ERROR"):
                failed.append(name)
            elif state == "PENDING":
                pending.append(name)

    if failed:
        return "failed", failed
    if pending:
        return "pending", pending
    return "passed", []


def main():
    if len(sys.argv) < 2:
        print(json.dumps({"error": "PR 번호가 필요합니다. 사용법: check_pr.py <pr_number>"}, ensure_ascii=False))
        sys.exit(1)

    pr_number = sys.argv[1]
    poll_interval = int(sys.argv[2]) if len(sys.argv) > 2 else POLL_INTERVAL_DEFAULT

    start_time = time.time()

    while True:
        elapsed = time.time() - start_time

        if elapsed >= MAX_RUNTIME:
            print(json.dumps({
                "status": "still_waiting",
                "pr_number": pr_number,
                "elapsed_seconds": int(elapsed),
                "message": "180초 타임아웃 초과. 재호출이 필요합니다."
            }, ensure_ascii=False))
            sys.exit(0)

        pr_data, err = run_gh(pr_number)
        if err:
            print(json.dumps({"error": f"gh 명령 실패: {err}"}, ensure_ascii=False))
            sys.exit(1)

        state = pr_data.get("state", "")
        review_decision = pr_data.get("reviewDecision", "") or ""
        pr_url = pr_data.get("url", "")
        branch = pr_data.get("headRefName", "")
        checks = pr_data.get("statusCheckRollup", []) or []
        checks_status, affected_checks = analyze_checks(checks)

        # 터미널 상태 확인
        if state == "MERGED":
            print(json.dumps({
                "status": "merged",
                "pr_number": pr_number,
                "branch": branch,
                "url": pr_url,
                "elapsed_seconds": int(elapsed)
            }, ensure_ascii=False))
            sys.exit(0)

        if state == "CLOSED":
            print(json.dumps({
                "status": "closed",
                "pr_number": pr_number,
                "url": pr_url,
                "elapsed_seconds": int(elapsed)
            }, ensure_ascii=False))
            sys.exit(0)

        if review_decision == "CHANGES_REQUESTED":
            reviews = pr_data.get("reviews", [])
            print(json.dumps({
                "status": "changes_requested",
                "pr_number": pr_number,
                "url": pr_url,
                "review_comments": extract_review_comments(reviews),
                "elapsed_seconds": int(elapsed)
            }, ensure_ascii=False))
            sys.exit(0)

        if checks_status == "failed":
            print(json.dumps({
                "status": "checks_failed",
                "pr_number": pr_number,
                "url": pr_url,
                "failed_checks": affected_checks,
                "elapsed_seconds": int(elapsed)
            }, ensure_ascii=False))
            sys.exit(0)

        if review_decision == "APPROVED" and checks_status == "passed":
            print(json.dumps({
                "status": "approved",
                "pr_number": pr_number,
                "branch": branch,
                "url": pr_url,
                "elapsed_seconds": int(elapsed)
            }, ensure_ascii=False))
            sys.exit(0)

        # 아직 결정 없음 — 대기
        remaining = MAX_RUNTIME - elapsed
        wait = min(poll_interval, remaining)
        ci_info = f"CI={checks_status}({','.join(affected_checks) or '-'})" if checks else "CI=없음"
        print(
            f"[{int(elapsed)}s] PR #{pr_number} 대기 중 "
            f"(state={state}, review={review_decision or 'PENDING'}, {ci_info}) "
            f"— {int(wait)}초 후 재확인...",
            file=sys.stderr
        )
        time.sleep(wait)


if __name__ == "__main__":
    main()
