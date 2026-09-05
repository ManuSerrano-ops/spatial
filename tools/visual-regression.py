"""Capture deterministic frontend states and compare them with reviewed PNG baselines.

This command only verifies screenshots. Baseline replacement is deliberately
owned by tools/update-visual-baselines.py and must never be invoked by CI.
"""

from __future__ import annotations

import argparse
import json
import shutil
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable

from PIL import Image, ImageChops
from playwright.sync_api import Browser, BrowserContext, Page, sync_playwright

from frontend_harness import (
    launch_chromium,
    new_frontend_context,
    open_frontend_page,
    resource_server,
)

ROOT = Path(__file__).resolve().parents[1]
FIXTURES = ROOT / "tests" / "visual-fixtures"
BASELINES = ROOT / "tests" / "visual-baselines"
MAX_ANTIALIASING_PIXELS = 5


@dataclass(frozen=True)
class VisualCase:
    name: str
    fixture: str
    viewport: dict[str, int]
    theme: str
    prepare: Callable[[Page], None]
    forced_colors: str | None = None


def no_op(_: Page) -> None:
    pass


def open_detail(page: Page) -> None:
    page.locator(".pin:not(:disabled)").first.click()
    page.locator("#detail-panel:not(.hidden)").wait_for()


def show_keyboard_cursor(page: Page) -> None:
    open_detail(page)
    page.locator("#move-seat").press("Enter")
    page.keyboard.press("ArrowRight")
    page.locator("#grid-cursor:not(.hidden)").wait_for()


def show_long_diff(page: Page) -> None:
    page.select_option("#scenario-mode", "fixture-general-diff")
    page.wait_for_function("() => !document.querySelector('#diff').disabled")
    page.locator("#more").click()
    page.locator("#diff").click()
    page.locator("#diff-dialog[open]").wait_for()
    page.locator("#diff-list .change-row").nth(49).wait_for()


def show_dense_south(page: Page) -> None:
    page.select_option("#map-select", "sur")
    page.wait_for_function("() => document.querySelector('#map-select').value === 'sur'")
    page.locator(".pin").nth(5).wait_for()


def show_compact_bulk_selection(page: Page) -> None:
    page.locator("#list-view").click()
    rows = page.locator("#list-table tbody tr")
    rows.nth(2).wait_for()
    rows.first.click()
    rows.nth(2).dispatch_event("click", {"shiftKey": True})
    page.locator("#bulk-apply").wait_for()
    page.locator("#detail-panel.selection-review-mode:not(.hidden)").wait_for()


CASES = (
    VisualCase("general-light-map", "general", {"width": 1400, "height": 900}, "professional-light", no_op),
    VisualCase("general-high-contrast", "general", {"width": 1400, "height": 900}, "high-contrast", no_op, "active"),
    VisualCase("general-detail", "general", {"width": 1400, "height": 900}, "professional-light", open_detail),
    VisualCase("general-keyboard-cursor", "general", {"width": 1400, "height": 900}, "professional-light", show_keyboard_cursor),
    VisualCase("general-long-diff", "general", {"width": 1400, "height": 900}, "professional-light", show_long_diff),
    VisualCase("general-compact-high-contrast", "general", {"width": 900, "height": 460}, "high-contrast", show_compact_bulk_selection, "active"),
    VisualCase("sur-denso", "sur-denso", {"width": 1400, "height": 900}, "professional-light", show_dense_south),
)


def load_fixture(name: str) -> dict[str, Any]:
    return json.loads((FIXTURES / f"{name}.json").read_text(encoding="utf-8"))


def bridge_results(fixture: dict[str, Any]) -> dict[str, Any]:
    results = {
        "runValidation": fixture["runValidationResult"],
        "runSpatialAnalytics": fixture["runSpatialAnalyticsResult"],
        "reportPlanResourceDiagnostic": {"logged": True},
        "getEvents": {"events": []},
    }
    if "getScenarioDiffResult" in fixture:
        results.update(
            {
                "reloadData": fixture["loadScenarioDataResult"],
                "getScenarioDiff": fixture["getScenarioDiffResult"],
                "runValidation": fixture["runScenarioValidationResult"],
                "runSpatialAnalytics": fixture["runScenarioSpatialAnalyticsResult"],
            }
        )
    return results


def capture(browser: Browser, port: int, case: VisualCase, destination: Path) -> None:
    fixture = load_fixture(case.fixture)
    context: BrowserContext = new_frontend_context(browser, case.viewport, forced_colors=case.forced_colors)
    try:
        page = open_frontend_page(
            context,
            port,
            fixture["loadInitialDataResult"],
            bridge_results=bridge_results(fixture),
            theme=case.theme,
        )
        case.prepare(page)
        page.wait_for_function("() => { const plan = document.querySelector('#plan'); return plan.complete && plan.naturalWidth > 0; }")
        page.wait_for_timeout(150)
        page.screenshot(path=str(destination), animations="disabled")
    except Exception as error:
        raise AssertionError(f"{case.name}: no se pudo preparar la captura visual: {error}") from error
    finally:
        context.close()


def diff_images(expected: Path, actual: Path, destination: Path) -> int:
    with Image.open(expected).convert("RGBA") as expected_image, Image.open(actual).convert("RGBA") as actual_image:
        if expected_image.size != actual_image.size:
            raise AssertionError(f"Dimensiones distintas: esperado {expected_image.size}, actual {actual_image.size}.")
        difference = ImageChops.difference(expected_image, actual_image)
        # Chromium can vary a few alpha-rounded antialiasing values by one unit;
        # differences of at least 8 in one RGBA channel remain visible and fail.
        changed = sum(1 for pixel in difference.getdata() if max(pixel) >= 8)
        if changed > MAX_ANTIALIASING_PIXELS:
            destination.parent.mkdir(parents=True, exist_ok=True)
            difference.point(lambda value: min(255, value * 8)).save(destination)
        return max(0, changed - MAX_ANTIALIASING_PIXELS)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--update", action="store_true", help="Regenera baselines locales; no usar en CI.")
    parser.add_argument("--artifacts", type=Path, default=ROOT / "tests" / "visual-artifacts")
    return parser.parse_args()


def main() -> None:
    arguments = parse_arguments()
    artifacts = arguments.artifacts.resolve()
    actual = artifacts / "actual"
    diffs = artifacts / "diff"
    if artifacts.exists():
        shutil.rmtree(artifacts)
    actual.mkdir(parents=True)

    failures: list[str] = []
    metadata: list[dict[str, Any]] = []
    with resource_server() as server:
        with sync_playwright() as playwright:
            browser = launch_chromium(playwright)
            try:
                for case in CASES:
                    actual_path = actual / f"{case.name}.png"
                    capture(browser, server.server_port, case, actual_path)
                    baseline = BASELINES / f"{case.name}.png"
                    case_metadata = {
                        "case": case.name,
                        "fixture": case.fixture,
                        "viewport": case.viewport,
                        "theme": case.theme,
                        "forcedColors": case.forced_colors,
                        "baseline": str(baseline.relative_to(ROOT)),
                        "actual": str(actual_path.relative_to(ROOT)),
                    }
                    if arguments.update:
                        BASELINES.mkdir(parents=True, exist_ok=True)
                        shutil.copy2(actual_path, baseline)
                        case_metadata["status"] = "updated"
                    elif not baseline.exists():
                        failures.append(f"{case.name}: no existe baseline {baseline.relative_to(ROOT)}")
                        case_metadata["status"] = "missing-baseline"
                    else:
                        changed = diff_images(baseline, actual_path, diffs / f"{case.name}.png")
                        case_metadata["changedPixels"] = changed
                        case_metadata["status"] = "different" if changed else "equal"
                        if changed:
                            expected = artifacts / "expected" / baseline.name
                            expected.parent.mkdir(parents=True, exist_ok=True)
                            shutil.copy2(baseline, expected)
                            case_metadata["expected"] = str(expected.relative_to(ROOT))
                            case_metadata["diff"] = str((diffs / baseline.name).relative_to(ROOT))
                            failures.append(f"{case.name}: {changed} píxeles distintos")
                    metadata.append(case_metadata)
            finally:
                browser.close()

    (artifacts / "metadata.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")
    if failures:
        raise AssertionError("Regresión visual:\n" + "\n".join(failures))
    action = "regenerados" if arguments.update else "verificados"
    print(f"Baselines visuales {action}: {len(CASES)} casos.")


if __name__ == "__main__":
    main()
