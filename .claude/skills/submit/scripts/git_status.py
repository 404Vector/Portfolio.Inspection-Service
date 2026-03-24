#!/usr/bin/env python3
"""현재 git 상태를 수집하여 JSON으로 출력합니다."""
import subprocess
import json
import sys


def run(cmd_list):
    result = subprocess.run(cmd_list, capture_output=True, text=True)
    return result.stdout.strip(), result.returncode


def main():
    branch, rc = run(["git", "branch", "--show-current"])
    if rc != 0 or not branch:
        print(json.dumps({"error": "git 저장소가 아니거나 branch를 확인할 수 없습니다."}, ensure_ascii=False))
        sys.exit(1)

    is_main = branch in ("main", "master")

    # 미커밋 변경사항 (staged + unstaged + untracked)
    status_output, _ = run(["git", "status", "--porcelain"])
    uncommitted_changes = [line for line in status_output.splitlines() if line.strip()]

    # 원격 트래킹 브랜치 존재 여부 및 미푸시 커밋
    unpushed_output, rc = run(["git", "log", f"origin/{branch}..HEAD", "--oneline"])
    if rc != 0:
        remote_tracking_exists = False
        unpushed_commits = []
    else:
        remote_tracking_exists = True
        unpushed_commits = [line for line in unpushed_output.splitlines() if line.strip()]

    # main 대비 커밋 목록 (main 없으면 master 시도)
    commits_output, rc = run(["git", "log", "main..HEAD", "--oneline"])
    if rc != 0:
        commits_output, _ = run(["git", "log", "master..HEAD", "--oneline"])
    commits_vs_main = [line for line in commits_output.splitlines() if line.strip()]

    result = {
        "branch": branch,
        "is_main": is_main,
        "uncommitted_changes": uncommitted_changes,
        "unpushed_commits": unpushed_commits,
        "remote_tracking_exists": remote_tracking_exists,
        "commits_vs_main": commits_vs_main,
    }

    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
