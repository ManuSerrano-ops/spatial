"""Report C# assertion-call totals for a legacy ref and the migrated xUnit tree.

This is a migration aid, not a test: it intentionally has no expected total and
is not called by tools/verify.ps1. Invoke it with the Git ref that precedes the
migration, for example:

    python tools/count-csharp-assertions.py HEAD
"""

from __future__ import annotations

import re
import subprocess
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LEGACY_HELPERS = (
    "Assert",
    "Equal",
    "EqualHashes",
    "EqualJson",
    "EqualDocumentContent",
    "SequenceEqual",
    "AssertRejectedWithoutWrites",
)


def git_files(ref: str) -> dict[str, str]:
    names = subprocess.check_output(
        ["git", "ls-tree", "-r", "--name-only", ref, "tests"], cwd=ROOT, text=True
    ).splitlines()
    return {
        name: subprocess.check_output(["git", "show", f"{ref}:{name}"], cwd=ROOT, text=True)
        for name in names
        if re.fullmatch(r"tests/[^/]+Harness/Program\.cs", name)
    }


def legacy_counts(files: dict[str, str]) -> Counter[str]:
    counts: Counter[str] = Counter()
    for source in files.values():
        for helper in LEGACY_HELPERS:
            calls = len(re.findall(rf"(?<![\w.]){helper}(?:<[^>]*>)?\s*\(", source))
            definitions = len(
                re.findall(
                    rf"^\s*(?:private\s+)?static\s+[\w<>,?\[\]\s]+\s+{helper}(?:<[^>]*>)?\s*\(",
                    source,
                    re.MULTILINE,
                )
            )
            counts[helper] += calls - definitions
    return counts


def migrated_counts() -> Counter[str]:
    counts: Counter[str] = Counter()
    for path in sorted(ROOT.glob("tests/PlanoOpenSpaceIT.*.Tests/**/*.cs")):
        if any(part in {"bin", "obj"} for part in path.parts):
            continue
        source = path.read_text(encoding="utf-8-sig")
        counts["xunit-direct"] += len(re.findall(r"\bAssert\.\w+\s*\(", source))
        counts["shared-helper"] += len(
            re.findall(
                r"\b(?:TestAssertions|DomainTestSupport)\.(?:EqualHashes|EqualJson|EqualDocumentContent|AssertRejectedWithoutWrites|SequenceEqual)\s*\(",
                source,
            )
        )
    return counts


def report(label: str, counts: Counter[str]) -> int:
    total = sum(counts.values())
    print(f"{label}: {total}")
    for name, count in sorted(counts.items()):
        if count:
            print(f"  {name}: {count}")
    return total


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("Uso: python tools/count-csharp-assertions.py <ref-pre-migracion>")

    legacy = report("Legacy", legacy_counts(git_files(sys.argv[1])))
    migrated = report("xUnit", migrated_counts())
    print(f"Diferencia: {migrated - legacy:+d}")


if __name__ == "__main__":
    main()
