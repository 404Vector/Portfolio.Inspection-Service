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

with open(".github/ai/system/code_review.md") as f:
    template = f.read()

policy = ""
try:
    with open(".github/ai/system/review_policy.md") as f:
        policy = f.read().strip()
except FileNotFoundError:
    pass

policy_section = f"## Project Review Policy\n\n{policy}\n\n" if policy else ""

prompt = template.replace("{policy}", policy_section) + "\n\nGit diff:\n\n" + diff

api_key = os.environ["GEMINI_API_KEY"]
model = "gemini-flash-latest"
url = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={api_key}"

payload = json.dumps({
    "contents": [{"parts": [{"text": prompt}]}],
    "generationConfig": {"maxOutputTokens": 8192},
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
