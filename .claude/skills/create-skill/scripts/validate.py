#!/usr/bin/env python3
"""
validate.py — SKILL.md 구조 검증기

사용법:
    python3 validate.py <path-to-SKILL.md>

종료 코드:
    0 = 검증 통과
    1 = 검증 실패 (오류 내용 출력)
"""

import re
import sys
from pathlib import Path

# ── 상수 ──────────────────────────────────────────────────────────────────────

REQUIRED_FIELDS = [
    "name",
    "description",
    "argument-hint",
    "disable-model-invocation",
    "user-invocable",
    "allowed-tools",
    "model",
    "context",
    "agent",
    "hooks",
]

BOOL_FIELDS = {"disable-model-invocation", "user-invocable"}
VALID_BOOL_VALUES = {"true", "false", "~"}

NAME_PATTERN = re.compile(r"^[a-z0-9-]{1,64}$")
CONTEXT_VALID = {"fork", "~", None}
AGENT_VALID = {"Explore", "Plan", "general-purpose", "~", None}

MAX_LINES = 500

# ── 파서 ──────────────────────────────────────────────────────────────────────

def parse_frontmatter(text: str) -> tuple[dict, int]:
    """
    SKILL.md 텍스트에서 YAML frontmatter를 파싱합니다.
    Returns (fields: dict, body_start_line: int)
    """
    lines = text.splitlines()
    if not lines or lines[0].strip() != "---":
        return {}, 0

    end = None
    for i, line in enumerate(lines[1:], start=1):
        if line.strip() == "---":
            end = i
            break

    if end is None:
        return {}, 0

    fields: dict = {}
    for line in lines[1:end]:
        if ":" in line:
            key, _, val = line.partition(":")
            fields[key.strip()] = val.strip() or None

    return fields, end + 1

# ── 검증 규칙 ─────────────────────────────────────────────────────────────────

def check_required_fields(fields: dict) -> list[str]:
    errors = []
    for f in REQUIRED_FIELDS:
        if f not in fields:
            errors.append(f"[MISSING] 필드 누락: `{f}`")
    return errors


def check_name(fields: dict) -> list[str]:
    name = fields.get("name")
    if name and name != "~":
        if not NAME_PATTERN.match(name):
            return [f"[INVALID] `name: {name}` — 소문자·숫자·하이픈만 허용, 최대 64자"]
    return []


def check_bool_fields(fields: dict) -> list[str]:
    errors = []
    for f in BOOL_FIELDS:
        val = fields.get(f)
        if val is not None and str(val).lower() not in VALID_BOOL_VALUES:
            errors.append(
                f"[INVALID] `{f}: {val}` — true / false / ~ 중 하나여야 합니다"
            )
    return errors


def check_context_agent(fields: dict) -> list[str]:
    errors = []
    ctx = fields.get("context")
    agent = fields.get("agent")

    if ctx not in CONTEXT_VALID:
        errors.append(f"[INVALID] `context: {ctx}` — fork 또는 ~ 여야 합니다")

    if agent not in AGENT_VALID:
        errors.append(
            f"[INVALID] `agent: {agent}` — Explore / Plan / general-purpose / ~ 여야 합니다"
        )

    if ctx != "fork" and agent and agent != "~":
        errors.append(
            f"[WARN] `agent: {agent}` 는 `context: fork` 일 때만 의미 있습니다"
        )
    return errors


def check_line_count(path: Path, total_lines: int) -> list[str]:
    if total_lines > MAX_LINES:
        return [
            f"[WARN] SKILL.md가 {total_lines}줄입니다 — {MAX_LINES}줄 이하를 권장합니다"
        ]
    return []


def strip_code_blocks(text: str) -> str:
    """펜스 코드 블록(``` ... ```) 내용을 빈 줄로 치환합니다."""
    return re.sub(r"```.*?```", lambda m: "\n" * m.group().count("\n"), text, flags=re.DOTALL)


def check_referenced_files(path: Path, text: str) -> list[str]:
    """
    SKILL.md 본문에서 마크다운 링크 [text](file) 형태로 참조된
    로컬 파일이 실제로 존재하는지 확인합니다.
    코드 블록 내부의 링크는 무시합니다.
    """
    errors = []
    skill_dir = path.parent
    body = strip_code_blocks(text)
    # [label](relative/path) 패턴, http(s) 링크는 제외
    for match in re.finditer(r"\[([^\]]+)\]\(([^)]+)\)", body):
        ref = match.group(2)
        if ref.startswith("http://") or ref.startswith("https://"):
            continue
        ref_path = skill_dir / ref
        if not ref_path.exists():
            errors.append(f"[MISSING] 참조 파일 없음: `{ref}`")
    return errors

# ── 메인 ──────────────────────────────────────────────────────────────────────

def validate(skill_path: Path) -> list[str]:
    if not skill_path.exists():
        return [f"[ERROR] 파일을 찾을 수 없습니다: {skill_path}"]

    text = skill_path.read_text(encoding="utf-8")
    lines = text.splitlines()

    fields, _ = parse_frontmatter(text)

    errors: list[str] = []
    errors += check_required_fields(fields)
    errors += check_name(fields)
    errors += check_bool_fields(fields)
    errors += check_context_agent(fields)
    errors += check_line_count(skill_path, len(lines))
    errors += check_referenced_files(skill_path, text)

    return errors


def main() -> None:
    if len(sys.argv) < 2:
        print("사용법: python3 validate.py <path-to-SKILL.md>", file=sys.stderr)
        sys.exit(1)

    path = Path(sys.argv[1])
    errors = validate(path)

    if not errors:
        print(f"✓ {path} — 검증 통과")
        sys.exit(0)
    else:
        print(f"✗ {path} — {len(errors)}개 문제 발견\n")
        for e in errors:
            print(f"  {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()
