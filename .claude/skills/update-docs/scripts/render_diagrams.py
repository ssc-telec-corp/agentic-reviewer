#!/usr/bin/env python3
"""Render every Mermaid block in the given Markdown files to PNG.

Usage: render_diagrams.py [--out DIR] [FILE.md ...]
Defaults to all Markdown files under docs/. Prints one line per diagram
(source file, line, PNG path) and exits 1 if any diagram fails to render.
"""
import argparse
import pathlib
import re
import subprocess
import sys
import tempfile

MERMAID_CLI = "@mermaid-js/mermaid-cli@11.4.2"
BLOCK = re.compile(r"^```mermaid\n(.*?)^```", re.S | re.M)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("files", nargs="*", type=pathlib.Path)
    parser.add_argument("--out", type=pathlib.Path)
    args = parser.parse_args()

    files = args.files or sorted(pathlib.Path("docs").rglob("*.md"))
    out = args.out or pathlib.Path(tempfile.mkdtemp(prefix="diagrams-"))
    out.mkdir(parents=True, exist_ok=True)

    failures = 0
    for md in files:
        text = md.read_text(encoding="utf-8")
        for index, match in enumerate(BLOCK.finditer(text)):
            line = text.count("\n", 0, match.start()) + 1
            stem = f"{md.stem}-{index}"
            source = out / f"{stem}.mmd"
            png = out / f"{stem}.png"
            source.write_text(match.group(1), encoding="utf-8")
            result = subprocess.run(
                ["npx", "-y", MERMAID_CLI, "-i", str(source), "-o", str(png), "-w", "1200"],
                capture_output=True, text=True,
            )
            if result.returncode == 0:
                print(f"ok    {md}:{line} -> {png}")
            else:
                failures += 1
                print(f"FAIL  {md}:{line}\n{result.stderr.strip()[-800:]}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
