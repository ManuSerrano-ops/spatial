"""Shared Playwright infrastructure for read-only frontend harnesses.

The bridge intentionally only answers native requests. It never reads or writes
runtime data from the browser and lets each harness keep its own interactions
and assertions.
"""

from __future__ import annotations

import json
import threading
from contextlib import contextmanager
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any, Iterator

from playwright.sync_api import Browser, BrowserContext, Page, Playwright

ROOT = Path(__file__).resolve().parents[1]
RESOURCES = ROOT / "Resources"
RUNTIME_DATA = ROOT / "runtime-data" / "data"


class _QuietResourceHandler(SimpleHTTPRequestHandler):
    def log_message(self, format: str, *args: object) -> None:
        pass


@contextmanager
def resource_server() -> Iterator[ThreadingHTTPServer]:
    """Serve the real frontend on a loopback port without changing cwd."""
    handler = partial(_QuietResourceHandler, directory=str(RESOURCES))
    server = ThreadingHTTPServer(("127.0.0.1", 0), handler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        yield server
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=5)


def load_runtime_json(name: str, default: object) -> object:
    """Load a read-only operational document for legacy measurement harnesses."""
    path = RUNTIME_DATA / name
    return json.loads(path.read_text(encoding="utf-8")) if path.exists() else default


def runtime_initial_data(active_scenario: bool | None = None) -> dict[str, Any]:
    """Build the existing frontend load payload from the read-only runtime files.

    ``None`` preserves legacy harnesses that did not send ``activeScenario``;
    explicit ``True`` or ``False`` includes it for scenario-aware harnesses.
    """
    scenario = {"id": "viewport-scenario", "name": "Medición de viewport", "undoCount": 0}
    result: dict[str, Any] = {
        "maps": load_runtime_json("maps.json", {"maps": []}),
        "assignments": load_runtime_json("assignments.json", {"assignments": []}),
        "people": load_runtime_json("people.json", {"people": []}),
        "devices": load_runtime_json("devices.json", {"devices": []}),
        "locations": load_runtime_json("locations.json", {"locations": []}),
        "managedAreas": load_runtime_json("managed-areas.json", {"areas": []}),
        "grid": {"columns": 24, "rows": 18},
        "scenarios": [scenario] if active_scenario else [],
        "readOnly": False,
    }
    if active_scenario is not None:
        result["activeScenario"] = scenario if active_scenario else None
    return result


def readonly_bridge_script(initial_data: dict[str, Any], scenario_changes: list[dict[str, Any]] | None = None) -> str:
    """Return an init script that emulates only the native WebView bridge contract."""
    return """
    const initialData = %s;
    const scenarioChanges = %s;
    window.chrome = window.chrome || {};
    window.chrome.webview = {
      addEventListener() {},
      postMessage(message) {
        let response = null;
        if (message.action === 'getUserPreferences') {
          response = { action: 'getUserPreferencesResult', success: true,
            data: { theme: 'professional-light', singleKeyShortcutsEnabled: true } };
        } else if (message.action === 'loadInitialData' || message.action === 'reloadData') {
          response = { action: message.action === 'loadInitialData' ? 'loadInitialDataResult' : 'reloadDataResult', success: true, data: initialData };
        } else if (message.action === 'getScenarioDiff') {
          response = { action: 'getScenarioDiffResult', success: true,
            data: { changes: scenarioChanges } };
        } else if (message.action === 'runValidation') {
          response = { action: 'runValidationResult', success: true,
            data: { results: [], summary: { total: 0, critical: 0, warning: 0, info: 0 } } };
        } else if (message.action === 'runSpatialAnalytics') {
          response = { action: 'runSpatialAnalyticsResult', success: true, data: { result: {} } };
        }
        if (response) setTimeout(() => window.receiveFromNative?.(response), 0);
      }
    };
    """ % (json.dumps(initial_data, ensure_ascii=False), json.dumps(scenario_changes or [], ensure_ascii=False))


def launch_chromium(playwright: Playwright) -> Browser:
    """Launch Playwright-managed Chromium, including on CI without local Chrome."""
    return playwright.chromium.launch(headless=True)


def new_frontend_context(
    browser: Browser,
    viewport: dict[str, int],
    *,
    forced_colors: str | None = None,
) -> BrowserContext:
    return browser.new_context(viewport=viewport, device_scale_factor=1, forced_colors=forced_colors)


def open_frontend_page(
    context: BrowserContext,
    port: int,
    initial_data: dict[str, Any],
    *,
    scenario_changes: list[dict[str, Any]] | None = None,
    theme: str | None = None,
    ready_selector: str = ".pin",
) -> Page:
    """Inject the bridge before the frontend loads and wait for its real UI."""
    page = context.new_page()
    page.add_init_script(readonly_bridge_script(initial_data, scenario_changes))
    page.goto(f"http://127.0.0.1:{port}/index.html", wait_until="networkidle")
    page.locator(ready_selector).first.wait_for()
    if theme is not None:
        page.evaluate("theme => document.documentElement.dataset.theme = theme", theme)
        page.wait_for_timeout(50)
    return page
