#!/usr/bin/env python3
"""Check docs against the repo's writing rules.

Usage: lint_docs.py [FILE.md ...]
Defaults to all Markdown files under docs/. Prints file:line: problem and
exits 1 if any problem is found. Checks only what a script can check reliably;
tone, prose-vs-table and "true of any project" still need a human read.
"""
import pathlib
import re
import sys

BANNED = [
    "robust", "seamless", "seamlessly", "comprehensive", "leverage", "leverages",
    "leveraging", "cutting-edge", "streamline", "streamlines", "empower",
    "empowers", "holistic", "it's important to note", "in today's landscape",
]
BANNED_RE = re.compile(r"\b(" + "|".join(re.escape(w) for w in BANNED) + r")\b", re.I)
EMOJI_RE = re.compile("[\U0001F300-\U0001FAFF☀-➿]")
STUB_RE = re.compile(r"\b(TODO|TBD|FIXME)\b")
ADR_SECTIONS = ["## Context", "## Considered options", "## Decision", "## Consequences"]


def lint(path: pathlib.Path) -> list[str]:
    problems = []
    text = path.read_text(encoding="utf-8")
    in_code = False
    for number, line in enumerate(text.splitlines(), 1):
        if line.startswith("```"):
            in_code = not in_code
            continue
        if in_code:
            continue
        for match in BANNED_RE.finditer(line):
            problems.append(f"{path}:{number}: banned word '{match.group(0)}'")
        if EMOJI_RE.search(line):
            problems.append(f"{path}:{number}: emoji")
        if STUB_RE.search(line):
            problems.append(f"{path}:{number}: stub marker; write 'Unknown' or '[decide: ...]'")

    if path.parent.name == "adr" and re.match(r"\d{4}-", path.name):
        for section in ADR_SECTIONS:
            if section not in text:
                problems.append(f"{path}: ADR missing section '{section}'")
        if "- Status: " not in text:
            problems.append(f"{path}: ADR missing '- Status:' line")
        for target in re.findall(r"\]\((\d{4}-[^)#]+\.md)", text):
            if not (path.parent / target).exists():
                problems.append(f"{path}: broken ADR link {target}")
    return problems


def main() -> int:
    files = [pathlib.Path(a) for a in sys.argv[1:]] or sorted(pathlib.Path("docs").rglob("*.md"))
    problems = [p for f in files for p in lint(f)]
    print("\n".join(problems) if problems else f"ok: {len(files)} files")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
