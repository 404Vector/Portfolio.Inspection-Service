#!/usr/bin/env python3
"""PR 리뷰 상태를 내부 폴링으로 감시합니다. 터미널 상태 도달 시 JSON을 출력하고 종료합니다.

Usage: python3 check_pr.py <pr_number> [poll_interval_seconds]

반환 status 값:
  merged            - PR이 이미 머지됨
  closed            - PR이 닫힘 (머지 없이)
  approved          - 리뷰 승인 완료 (머지 대기)
  changes_requested - 리뷰어가 변경 요청
  still_waiting     - 타임아웃(9분) 내 결정 없음 → 재호출 필요
"""
import subprocess
import json
import sys
import time


POLL_INTERVAL_DEFAULT = 30   # seconds
MAX_RUNTIME = 540            # 9분 (Bash 도구 10분 제한에 여유 포함)


def run_gh(pr_number):
    result = subprocess.run(
        ["gh", "pr", "view", str(pr_number),
         "--json", "state,reviewDecision,reviews,headRefName,url"],
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


def main():
    if len(sys.argv) < 2:
        print(json.dumps({"error": "PR 번호가 필요합니다. 사용법: check_pr.py <pr_number>"}, ensure_ascii=False))
        sys.exit(1)

    pr_number = sys.argv[1]
    poll_interval = int(sys.argv[2]) if len(sys.argv) > 2 else POLL_INTERVAL_DEFAULT

    start_time = time.time()
    attempt = 0

    while True:
        elapsed = time.time() - start_time
        attempt += 1

        if elapsed >= MAX_RUNTIME:
            print(json.dumps({
                "status": "still_waiting",
                "pr_number": pr_number,
                "elapsed_seconds": int(elapsed),
                "message": f"9분 타임아웃 초과. 재호출이 필요합니다."
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

        if review_decision == "APPROVED":
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
        print(
            f"[{int(elapsed)}s] PR #{pr_number} 대기 중 "
            f"(state={state}, review={review_decision or 'PENDING'}) "
            f"— {int(wait)}초 후 재확인...",
            file=sys.stderr
        )
        time.sleep(wait)


if __name__ == "__main__":
    main()
