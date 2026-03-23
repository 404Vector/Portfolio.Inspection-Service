import os
import json
import re
import urllib.request
import urllib.error
import sys

CONVENTIONAL_COMMIT_RE = re.compile(
    r"^(feat|fix|refactor|chore|docs|test|perf)(\(.+\))?: .{1,72}$"
)

FALLBACK_COMMIT_BODY_MAX_CHARS = 2000

# Globals set by main() so pure functions remain importable without env vars.
GITHUB_TOKEN = GEMINI_API_KEY = REPO = HEAD_SHA = HEAD_BRANCH = REQUIRED_CHECKS = None


# ---------------------------------------------------------------------------
# Pure helpers (testable without HTTP)
# ---------------------------------------------------------------------------

def strip_code_fences(text):
    text = re.sub(r"^```[^\n]*\n", "", text.strip())
    text = re.sub(r"\n```$", "", text.strip())
    return text.strip()


def parse_approval_from_reviews(reviews):
    """
    Determine approval status from a list of GitHub review objects.

    Only the latest review per reviewer is considered.
    Returns (approved: bool, reason: str).
    """
    latest_by_reviewer = {}
    for review in reviews:
        user = (review.get("user") or {}).get("login")
        if user:
            latest_by_reviewer[user] = review["state"]

    states = latest_by_reviewer.values()
    if "CHANGES_REQUESTED" in states:
        return False, "One or more reviewers have requested changes."
    if "APPROVED" not in states:
        return False, "No approved reviews found."
    return True, "Approved."


# ---------------------------------------------------------------------------
# GitHub API helpers
# ---------------------------------------------------------------------------

def github_get(path):
    url = f"https://api.github.com/repos/{REPO}{path}"
    req = urllib.request.Request(url, headers={
        "Authorization": f"Bearer {GITHUB_TOKEN}",
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
    })
    try:
        with urllib.request.urlopen(req) as resp:
            return json.loads(resp.read())
    except urllib.error.HTTPError as e:
        body = e.read().decode()
        print(f"GitHub API GET {path} failed [{e.code}]: {body}", file=sys.stderr)
        sys.exit(1)


def github_post(path, data):
    url = f"https://api.github.com/repos/{REPO}{path}"
    payload = json.dumps(data).encode()
    req = urllib.request.Request(url, data=payload, method="POST", headers={
        "Authorization": f"Bearer {GITHUB_TOKEN}",
        "Accept": "application/vnd.github+json",
        "Content-Type": "application/json",
        "X-GitHub-Api-Version": "2022-11-28",
    })
    try:
        with urllib.request.urlopen(req) as resp:
            return json.loads(resp.read())
    except urllib.error.HTTPError as e:
        body = e.read().decode()
        print(f"GitHub API POST {path} failed [{e.code}]: {body}", file=sys.stderr)
        sys.exit(1)


def github_put(path, data):
    """Raises urllib.error.HTTPError on failure (caller handles specific codes)."""
    url = f"https://api.github.com/repos/{REPO}{path}"
    payload = json.dumps(data).encode()
    req = urllib.request.Request(url, data=payload, method="PUT", headers={
        "Authorization": f"Bearer {GITHUB_TOKEN}",
        "Accept": "application/vnd.github+json",
        "Content-Type": "application/json",
        "X-GitHub-Api-Version": "2022-11-28",
    })
    with urllib.request.urlopen(req) as resp:
        return json.loads(resp.read())


def gemini_generate(prompt):
    url = (
        "https://generativelanguage.googleapis.com/v1beta/models"
        f"/gemini-flash-latest:generateContent?key={GEMINI_API_KEY}"
    )
    payload = json.dumps({
        "contents": [{"parts": [{"text": prompt}]}],
        "generationConfig": {"maxOutputTokens": 1024},
    }).encode()
    req = urllib.request.Request(
        url, data=payload, headers={"Content-Type": "application/json"}
    )
    try:
        with urllib.request.urlopen(req) as resp:
            data = json.loads(resp.read())
    except urllib.error.HTTPError as e:
        body = e.read().decode()
        print(f"Gemini API failed [{e.code}]: {body}", file=sys.stderr)
        sys.exit(1)

    candidates = data.get("candidates")
    if not candidates:
        print("Gemini returned no candidates (may have been blocked).", file=sys.stderr)
        sys.exit(1)

    parts = candidates[0].get("content", {}).get("parts")
    if not parts:
        print("Gemini response has no parts.", file=sys.stderr)
        sys.exit(1)

    return parts[0].get("text", "")


def get_passed_checks(sha):
    """Aggregate passed check names from both Check Runs and Commit Statuses APIs."""
    passed = set()

    check_runs = github_get(f"/commits/{sha}/check-runs?per_page=100")
    for run in check_runs.get("check_runs", []):
        if run.get("conclusion") == "success":
            passed.add(run["name"])

    status = github_get(f"/commits/{sha}/status")
    for s in status.get("statuses", []):
        if s.get("state") == "success":
            passed.add(s["context"])

    return passed


def find_pr_by_sha(sha):
    """
    Find an open PR by commit SHA.

    Uses the commits/pulls endpoint rather than head={owner}:{branch} so that
    PRs from forks are found correctly regardless of the fork owner.
    """
    pulls = github_get(f"/commits/{sha}/pulls?state=open")
    if not pulls:
        return None
    return pulls[0]


def check_human_approval(pr_number):
    """Return (approved: bool, reason: str) based on the PR's review state."""
    reviews = github_get(f"/pulls/{pr_number}/reviews")
    return parse_approval_from_reviews(reviews)


def post_pr_comment(pr_number, body):
    github_post(f"/issues/{pr_number}/comments", {"body": body})


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    global GITHUB_TOKEN, GEMINI_API_KEY, REPO, HEAD_SHA, HEAD_BRANCH, REQUIRED_CHECKS
    GITHUB_TOKEN = os.environ["GITHUB_TOKEN"]
    GEMINI_API_KEY = os.environ["GEMINI_API_KEY"]
    REPO = os.environ["REPO"]
    HEAD_SHA = os.environ["HEAD_SHA"]
    HEAD_BRANCH = os.environ["HEAD_BRANCH"]
    REQUIRED_CHECKS = {x.strip() for x in os.environ["REQUIRED_CHECKS"].split(",")}

    # 1. Find open PR by commit SHA (works for forks and same-repo branches)
    pr = find_pr_by_sha(HEAD_SHA)
    if pr is None:
        print(f"No open PR found for SHA '{HEAD_SHA}'. Skipping.")
        sys.exit(0)

    pr_number = pr["number"]
    pr_title = pr["title"]
    pr_body = (pr.get("body") or "")[:500]
    print(f"Found PR #{pr_number}: {pr_title}")

    # 2. Verify all required CI checks have passed
    passed = get_passed_checks(HEAD_SHA)
    missing = REQUIRED_CHECKS - passed
    if missing:
        print(f"Required checks not yet passed: {missing}. Skipping merge.")
        sys.exit(0)

    print(f"All required checks passed: {passed}")

    # 3. Collect commit messages
    commits = github_get(f"/pulls/{pr_number}/commits")
    commit_messages = "\n".join(
        f"- {c['commit']['message'].splitlines()[0]}"
        for c in commits
    )

    # 4. Generate squash commit message with Gemini
    with open(".github/ai/system/squash_commit.md") as f:
        template = f.read()

    prompt = (
        template
        + f"\n\nPR title: {pr_title}\n\n"
        f"PR description:\n{pr_body}\n\n"
        f"Commits:\n{commit_messages}"
    )

    raw = gemini_generate(prompt)
    commit_message = strip_code_fences(raw)
    lines = commit_message.splitlines()
    commit_title = lines[0]
    commit_body = "\n".join(lines[1:]).strip()

    # 5. Validate commit title; fall back to PR title if Gemini output is invalid
    if not CONVENTIONAL_COMMIT_RE.match(commit_title):
        print(
            f"WARNING: Gemini output '{commit_title}' does not match Conventional Commits format. "
            "Falling back to PR title.",
            file=sys.stderr,
        )
        commit_title = pr_title
        commit_body = commit_messages[:FALLBACK_COMMIT_BODY_MAX_CHARS]

    print(f"Commit title: {commit_title}")
    print(f"Commit body:\n{commit_body}")

    # 6. Squash merge — handle conflict/method-not-allowed gracefully
    try:
        result = github_put(f"/pulls/{pr_number}/merge", {
            "merge_method": "squash",
            "commit_title": commit_title,
            "commit_message": commit_body,
        })
        print(f"Merged PR #{pr_number}: {result.get('message', 'success')}")
    except urllib.error.HTTPError as e:
        raw_body = e.read().decode()
        error_data = {}
        try:
            error_data = json.loads(raw_body)
        except json.JSONDecodeError:
            pass
        message = error_data.get("message", raw_body)

        if e.code in (405, 409):
            comment = (
                f"Auto-merge failed for PR #{pr_number}.\n\n"
                f"**Reason:** {message}\n\n"
                "Please resolve any merge conflicts or blocking conditions and re-trigger the workflow."
            )
            post_pr_comment(pr_number, comment)
            print(f"Merge blocked (HTTP {e.code}): {message}", file=sys.stderr)
        else:
            print(f"GitHub API PUT /pulls/{pr_number}/merge failed [{e.code}]: {raw_body}", file=sys.stderr)

        sys.exit(1)


if __name__ == "__main__":
    main()
