"""Fail when the versioned visual fixtures diverge from the real bridge output."""

from __future__ import annotations

import filecmp
import shutil
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FIXTURES = ROOT / "tests" / "visual-fixtures"
TEST_PROJECT = ROOT / "tests" / "PlanoOpenSpaceIT.Desktop.Tests" / "PlanoOpenSpaceIT.Desktop.Tests.csproj"


def main() -> None:
    with tempfile.TemporaryDirectory(prefix="plano-visual-fixtures-") as directory:
        generated = Path(directory)
        subprocess.run(
            [
                "dotnet", "run", "--project", str(TEST_PROJECT), "--no-build", "--",
                "--export-visual-fixture", str(generated),
            ],
            cwd=ROOT,
            check=True,
        )
        expected_names = ("general.json", "sur-denso.json")
        mismatches = [
            name for name in expected_names
            if not (FIXTURES / name).is_file()
            or not (generated / name).is_file()
            or not filecmp.cmp(FIXTURES / name, generated / name, shallow=False)
        ]
        extras = sorted(path.name for path in generated.glob("*.json") if path.name not in expected_names)
        if mismatches or extras:
            detail = ", ".join(mismatches + extras)
            raise AssertionError(
                f"Las fixtures versionadas no coinciden con WebViewBridge: {detail}. "
                "Regénéralas con tools/update-visual-baselines.py y revisa el commit resultante."
            )
    print("Fixtures visuales: origen WebViewBridge verificado (general, sur-denso).")


if __name__ == "__main__":
    main()
