#!/usr/bin/env python3
"""
validate.py — SKILL.md for Gemini structure validator

Usage:
    python3 validate.py <path-to-SKILL.md>

Exit Codes:
    0 = Validation passed
    1 = Validation failed (errors printed)
"""

import re
import sys
from pathlib import Path

# --- Constants ---

REQUIRED_FIELDS = [
    "name",
    "description",
]

NAME_PATTERN = re.compile(r"^[a-z0-9-]{1,64}$")

MAX_LINES = 500

# --- Parser ---

def parse_frontmatter(text: str) -> tuple[dict, int]:
    """
    Parses YAML frontmatter from SKILL.md text.
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

# --- Validation Rules ---

def check_required_fields(fields: dict) -> list[str]:
    errors = []
    for f in REQUIRED_FIELDS:
        if f not in fields:
            errors.append(f"[MISSING] Missing field: `{f}`")
    return errors


def check_name(fields: dict) -> list[str]:
    name = fields.get("name")
    if name and name != "~":
        if not NAME_PATTERN.match(name):
            return [f"[INVALID] `name: {name}` — Must be lowercase, numbers, hyphens, max 64 chars"]
    return []


def check_line_count(path: Path, total_lines: int) -> list[str]:
    if total_lines > MAX_LINES:
        return [
            f"[WARN] SKILL.md is {total_lines} lines — recommended max is {MAX_LINES}"
        ]
    return []


def strip_code_blocks(text: str) -> str:
    """Replaces fenced code blocks (``` ... ```) with blank lines."""
    return re.sub(r"```.*?```", lambda m: "\n" * m.group().count("\n"), text, flags=re.DOTALL)


def check_referenced_files(path: Path, text: str) -> list[str]:
    """
    Checks if local files referenced as [text](file) in the SKILL.md body exist.
    Ignores links inside code blocks.
    """
    errors = []
    skill_dir = path.parent
    body = strip_code_blocks(text)
    # [label](relative/path) pattern, excluding http(s) links
    for match in re.finditer(r"\[([^\]]+)\]\(([^)]+)\)", body):
        ref = match.group(2)
        if ref.startswith("http://") or ref.startswith("https://"):
            continue
        ref_path = skill_dir / ref
        if not ref_path.exists():
            errors.append(f"[MISSING] Referenced file not found: `{ref}`")
    return errors

# --- Main ---

def validate(skill_path: Path) -> list[str]:
    if not skill_path.exists():
        return [f"[ERROR] File not found: {skill_path}"]

    text = skill_path.read_text(encoding="utf-8")
    lines = text.splitlines()

    fields, _ = parse_frontmatter(text)

    errors: list[str] = []
    errors += check_required_fields(fields)
    errors += check_name(fields)
    errors += check_line_count(skill_path, len(lines))
    errors += check_referenced_files(skill_path, text)

    return errors


def main() -> None:
    if len(sys.argv) < 2:
        print("Usage: python3 validate.py <path-to-SKILL.md>", file=sys.stderr)
        sys.exit(1)

    path = Path(sys.argv[1])
    errors = validate(path)

    if not errors:
        print(f"✓ {path} — Validation passed")
        sys.exit(0)
    else:
        print(f"✗ {path} — Found {len(errors)} issues\n")
        for e in errors:
            print(f"  {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()
