import os
import json
import urllib.request
import sys

with open("pr_diff.txt") as f:
    diff = f.read()

if not diff.strip():
    result = {"approved": True, "body": "No code changes detected — automatically approved."}
    with open("review.json", "w") as f:
        json.dump(result, f)
    sys.exit(0)

policy = ""
try:
    with open(".github/REVIEW_POLICY.md") as f:
        policy = f.read().strip()
except FileNotFoundError:
    pass

policy_section = (
    f"## Project Review Policy\n\n{policy}\n\n"
    if policy else ""
)

prompt = (
    "You are a senior code reviewer for this specific project.\n\n"
    "Before evaluating, read the Project Review Policy section carefully.\n"
    "Treat every item listed under 'Accepted Patterns' as an intentional design\n"
    "decision that has already been approved — do not flag these as issues.\n"
    "Only mark something as FAIL if it is an unintentional bug, an unrecognized\n"
    "security flaw, or a clear deviation from what the change is trying to achieve.\n\n"
    + policy_section
    + "Evaluate each item below and mark it as PASS or FAIL with a brief reason:\n\n"
    "- Correctness: Logic is sound, no obvious bugs\n"
    "- Security: No injection, auth bypass, secrets in code, or other OWASP Top 10 issues\n"
    "- Performance: No unnecessary N+1 queries, blocking calls, or memory issues\n"
    "- Maintainability: Code is readable, follows existing patterns, no dead code\n"
    "- Test coverage: New functionality has corresponding tests\n"
    "- Breaking changes: No unintended breaking changes to APIs or contracts\n\n"
    "Then provide:\n"
    "1. A summary of any issues found\n"
    "2. A final verdict of exactly one of: APPROVED or CHANGES_REQUESTED\n\n"
    "Use this exact format:\n\n"
    "## Checklist\n\n"
    "- [PASS/FAIL] **Correctness** - <reason>\n"
    "- [PASS/FAIL] **Security** - <reason>\n"
    "- [PASS/FAIL] **Performance** - <reason>\n"
    "- [PASS/FAIL] **Maintainability** - <reason>\n"
    "- [PASS/FAIL] **Test coverage** - <reason>\n"
    "- [PASS/FAIL] **Breaking changes** - <reason>\n\n"
    "## Comments\n\n"
    "<detailed feedback, or 'No issues found.' if all items pass>\n\n"
    "## Verdict\n\n"
    "APPROVED or CHANGES_REQUESTED\n\n"
    "Git diff:\n\n"
    + diff
)

api_key = os.environ["GEMINI_API_KEY"]
model = "gemini-flash-latest"
url = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={api_key}"

payload = json.dumps({
    "contents": [{"parts": [{"text": prompt}]}],
    "generationConfig": {"maxOutputTokens": 4096},
}).encode()

req = urllib.request.Request(
    url,
    data=payload,
    headers={"Content-Type": "application/json"},
)

try:
    with urllib.request.urlopen(req) as resp:
        data = json.loads(resp.read())
        review_text = data["candidates"][0]["content"]["parts"][0]["text"]
except Exception as e:
    print(f"Gemini API error: {e}", file=sys.stderr)
    sys.exit(1)

# Parse verdict from "## Verdict" section to avoid false positives
verdict_section = ""
if "## Verdict" in review_text:
    verdict_section = review_text.split("## Verdict")[-1]

approved = (
    "APPROVED" in verdict_section
    and "CHANGES_REQUESTED" not in verdict_section
)

icon = "✅" if approved else "❌"
verdict_label = "Approved" if approved else "Changes Requested"

body = (
    f"## {icon} Gemini Code Review — {verdict_label}\n\n"
    + review_text
    + "\n\n---\n*Automated review by Gemini AI*"
)

result = {"approved": approved, "body": body}
with open("review.json", "w") as f:
    json.dump(result, f)

print(f"Verdict: {verdict_label}")
