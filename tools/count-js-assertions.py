"""Report semantic assertion-call totals for JavaScript harness migration.

This is a migration aid, not a test: it has no expected total and is not
called by tools/verify.ps1. It counts assertion call sites per harness, while
intentionally excluding helper declarations. Local `equal(...)` helpers count
as one semantic assertion per call, even when their implementation compares a
compound value.

Examples:

    python tools/count-js-assertions.py
    python tools/count-js-assertions.py --write-baseline tests/js-assertions-pre-node-test.json
    python tools/count-js-assertions.py --compare tests/js-assertions-pre-node-test.json
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
HARNESS_GLOB = "tests/*-harness.js"
BARE_HELPERS = ("assert", "equal")


def mask_literals_and_comments(source: str) -> str:
    """Keep code structure while masking comments and JavaScript literals."""
    masked: list[str] = []
    index = 0
    state = "code"
    quote = ""

    while index < len(source):
        char = source[index]
        next_char = source[index + 1] if index + 1 < len(source) else ""

        if state == "code":
            if char == "/" and next_char == "/":
                masked.extend("  ")
                index += 2
                state = "line-comment"
            elif char == "/" and next_char == "*":
                masked.extend("  ")
                index += 2
                state = "block-comment"
            elif char in {"'", '"', "`"}:
                masked.append(" ")
                index += 1
                quote = char
                state = "literal"
            else:
                masked.append(char)
                index += 1
        elif state == "line-comment":
            masked.append("\n" if char == "\n" else " ")
            index += 1
            if char == "\n":
                state = "code"
        elif state == "block-comment":
            if char == "*" and next_char == "/":
                masked.extend("  ")
                index += 2
                state = "code"
            else:
                masked.append("\n" if char == "\n" else " ")
                index += 1
        else:
            if char == "\\":
                masked.extend(" " * min(2, len(source) - index))
                index += 2
            elif char == quote:
                masked.append(" ")
                index += 1
                state = "code"
            else:
                masked.append("\n" if char == "\n" else " ")
                index += 1

    return "".join(masked)


def count_source(source: str) -> Counter[str]:
    code = mask_literals_and_comments(source)
    counts: Counter[str] = Counter()

    for helper in BARE_HELPERS:
        calls = len(re.findall(rf"(?<![\w.$]){helper}\s*\(", code))
        declarations = len(
            re.findall(rf"\bfunction\s+{helper}\s*\(", code)
        )
        counts[helper] += calls - declarations

    counts["node-assert"] += len(
        re.findall(r"(?<![\w$])assert\.[A-Za-z_$][\w$]*\s*\(", code)
    )
    return counts


def report() -> dict[str, object]:
    files: dict[str, dict[str, object]] = {}
    total: Counter[str] = Counter()
    for path in sorted(ROOT.glob(HARNESS_GLOB)):
        counts = count_source(path.read_text(encoding="utf-8"))
        semantic_total = sum(counts.values())
        files[path.relative_to(ROOT).as_posix()] = {
            "total": semantic_total,
            "kinds": dict(sorted((name, count) for name, count in counts.items() if count)),
        }
        total.update(counts)

    return {
        "schemaVersion": 1,
        "description": "Semantic assertion calls; helper declarations are excluded.",
        "files": files,
        "total": sum(total.values()),
        "kinds": dict(sorted((name, count) for name, count in total.items() if count)),
    }


def print_report(result: dict[str, object]) -> None:
    files = result["files"]
    assert isinstance(files, dict)
    for path, details in files.items():
        assert isinstance(details, dict)
        print(f"{path}: {details['total']}")
    print(f"Total: {result['total']}")
    kinds = result["kinds"]
    assert isinstance(kinds, dict)
    for name, count in kinds.items():
        print(f"  {name}: {count}")


def compare(current: dict[str, object], baseline_path: Path) -> int:
    baseline = json.loads(baseline_path.read_text(encoding="utf-8"))
    current_files = current["files"]
    baseline_files = baseline["files"]
    assert isinstance(current_files, dict) and isinstance(baseline_files, dict)
    differences = []
    for path in sorted(set(current_files) | set(baseline_files)):
        before = baseline_files.get(path, {}).get("total") if isinstance(baseline_files.get(path, {}), dict) else None
        after = current_files.get(path, {}).get("total") if isinstance(current_files.get(path, {}), dict) else None
        if before != after:
            differences.append(f"{path}: {before} -> {after}")
    if differences:
        print("Diferencias por fichero:")
        print("\n".join(differences))
        return 1
    print("Recuento por fichero sin diferencias.")
    return 0


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write-baseline", type=Path)
    parser.add_argument("--compare", type=Path)
    args = parser.parse_args()

    result = report()
    print_report(result)
    if args.write_baseline:
        destination = (ROOT / args.write_baseline).resolve() if not args.write_baseline.is_absolute() else args.write_baseline
        destination.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"Baseline escrito: {destination.relative_to(ROOT)}")
    if args.compare:
        baseline = (ROOT / args.compare).resolve() if not args.compare.is_absolute() else args.compare
        raise SystemExit(compare(result, baseline))


if __name__ == "__main__":
    main()
