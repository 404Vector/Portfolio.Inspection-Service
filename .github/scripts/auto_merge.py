import os
import json
import re
import urllib.request
import urllib.error
import sys

GITHUB_TOKEN = os.environ["GITHUB_TOKEN"]
GEMINI_API_KEY = os.environ["GEMINI_API_KEY"]
REPO = os.environ["REPO"]
HEAD_SHA = os.environ["HEAD_SHA"]
HEAD_BRANCH = os.environ["HEAD_BRANCH"]
REQUIRED_CHECKS = set(os.environ["REQUIRED_CHECKS"].split(","))


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


def github_put(path, data):
    url = f"https://api.github.com/repos/{REPO}{path}"
    payload = json.dumps(data).encode()
    req = urllib.request.Request(url, data=payload, method="PUT", headers={
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
        print(f"GitHub API PUT {path} failed [{e.code}]: {body}", file=sys.stderr)
        sys.exit(1)


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
            return data["candidates"][0]["content"]["parts"][0]["text"]
    except urllib.error.HTTPError as e:
        body = e.read().decode()
        print(f"Gemini API failed [{e.code}]: {body}", file=sys.stderr)
        sys.exit(1)


def strip_code_fences(text):
    # Remove ```lang ... ``` or ``` ... ``` wrappers that LLMs often add
    text = re.sub(r"^```[^\n]*\n", "", text.strip())
    text = re.sub(r"\n```$", "", text.strip())
    return text.strip()


# 1. Find open PR for this branch
owner = REPO.split("/")[0]
pulls = github_get(f"/pulls?head={owner}:{HEAD_BRANCH}&state=open")
if not pulls:
    print(f"No open PR found for branch '{HEAD_BRANCH}'. Skipping.")
    sys.exit(0)

pr = pulls[0]
pr_number = pr["number"]
pr_title = pr["title"]
# Truncate PR body to limit prompt injection surface
pr_body = (pr.get("body") or "")[:500]
print(f"Found PR #{pr_number}: {pr_title}")

# 2. Verify all required checks have passed
check_runs = github_get(f"/commits/{HEAD_SHA}/check-runs?per_page=100")
passed = {
    run["name"]
    for run in check_runs["check_runs"]
    if run["conclusion"] == "success"
}
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
prompt = (
    "Generate a concise git squash commit message in Conventional Commits format.\n\n"
    "Rules:\n"
    "- First line: `<type>(<optional scope>): <short summary>` (max 72 chars)\n"
    "- Blank line after the first line\n"
    "- Bullet points summarizing the key changes\n"
    "- Use one of these types: feat, fix, refactor, chore, docs, test, perf\n"
    "- Output ONLY the raw commit message text, no markdown code fences\n\n"
    f"PR title: {pr_title}\n\n"
    f"PR description:\n{pr_body}\n\n"
    f"Commits:\n{commit_messages}"
)

raw = gemini_generate(prompt)
commit_message = strip_code_fences(raw)
lines = commit_message.splitlines()
commit_title = lines[0]
commit_body = "\n".join(lines[1:]).strip()

print(f"Generated commit message:\n{commit_message}")

# 5. Squash merge
result = github_put(f"/pulls/{pr_number}/merge", {
    "merge_method": "squash",
    "commit_title": commit_title,
    "commit_message": commit_body,
})

print(f"Merged PR #{pr_number}: {result.get('message', 'success')}")
