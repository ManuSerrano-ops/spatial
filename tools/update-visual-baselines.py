"""Regenerate reviewed visual fixtures and baselines locally; never call from CI."""

from __future__ import annotations

import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "tests" / "PlanoOpenSpaceIT.Desktop.Tests" / "PlanoOpenSpaceIT.Desktop.Tests.csproj"
FIXTURES = ROOT / "tests" / "visual-fixtures"


def run(*command: str) -> None:
    subprocess.run(command, cwd=ROOT, check=True)


def main() -> None:
    run("dotnet", "build", str(PROJECT), "--no-restore")
    run("dotnet", "run", "--project", str(PROJECT), "--no-build", "--", "--export-visual-fixture", str(FIXTURES))
    run("python", "tools/visual-regression.py", "--update")
    print("Revisa expected, actual y diff antes de crear un commit exclusivo de baselines.")


if __name__ == "__main__":
    main()
