#!/usr/bin/env python3
"""Reset a chart's `artifacthub.io/changes` annotation to only the image tag update entry.

Run after a release, so the next release's changelog starts from scratch. The entry that is kept is
the one carrying the `# x-release-please-version` marker, i.e. the "updated recruIT image tags"
line that release-please bumps on every release. Works line by line instead of round-tripping the
YAML, so that marker (which is part of the annotation's string value) and the rest of the file's
formatting and comments stay untouched.

Usage: reset-chart-changes.py <path to Chart.yaml>
"""

import re
import sys

KEY_PATTERN = re.compile(r"^(?P<indent>\s*)artifacthub\.io/changes:\s*\|-?\s*$")
VERSION_MARKER = "# x-release-please-version"


def indent_of(line: str) -> int:
    return len(line) - len(line.lstrip(" "))


def main(chart_path: str) -> None:
    with open(chart_path, encoding="utf-8") as f:
        lines = f.read().splitlines(keepends=True)

    key_index = next(
        (i for i, line in enumerate(lines) if KEY_PATTERN.match(line)), None
    )
    if key_index is None:
        sys.exit(f"No 'artifacthub.io/changes' block annotation found in {chart_path}")

    key_indent = indent_of(lines[key_index])

    # the annotation's block scalar spans every following line indented deeper than its key
    block_end = key_index + 1
    while block_end < len(lines) and (
        not lines[block_end].strip() or indent_of(lines[block_end]) > key_indent
    ):
        block_end += 1
    block = lines[key_index + 1 : block_end]

    # split the block into its list entries, each starting at a "- " line at the list's own indent
    # (deeper "- " lines belong to nested lists inside an entry, e.g. its `links`)
    entry_indent = next(
        (indent_of(line) for line in block if line.lstrip().startswith("- ")), None
    )
    entries: list[list[str]] = []
    for line in block:
        if line.lstrip().startswith("- ") and indent_of(line) == entry_indent:
            entries.append([])
        if entries:
            entries[-1].append(line)

    kept = [entry for entry in entries if any(VERSION_MARKER in line for line in entry)]
    if len(kept) != 1:
        sys.exit(
            f"Expected exactly one changes entry marked with '{VERSION_MARKER}', found {len(kept)}"
        )

    # block scalars use strip chomping (`|-`), so don't leave trailing blank lines behind
    entry = kept[0]
    while entry and not entry[-1].strip():
        entry.pop()

    lines[key_index + 1 : block_end] = entry

    with open(chart_path, "w", encoding="utf-8") as f:
        f.writelines(lines)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
