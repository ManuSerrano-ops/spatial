"""Measure nearest pin-center spacing from the real operational maps in Chromium.

The browser viewport matches MainWindow's configured 1400x900 DIP startup size.
This script is read-only: it serves Resources/ locally and reads runtime-data/data.
"""

from __future__ import annotations

import json
import math
import os
import threading
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from statistics import median
from typing import Any

from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
RESOURCES = ROOT / "Resources"
DATA = ROOT / "runtime-data" / "data"
WINDOW_WIDTH = 1400
WINDOW_HEIGHT = 900


class QuietResourceHandler(SimpleHTTPRequestHandler):
    def log_message(self, format: str, *args: object) -> None:
        pass


def load_json(name: str, default: object) -> object:
    path = DATA / name
    return json.loads(path.read_text(encoding="utf-8")) if path.exists() else default


def initial_data() -> dict[str, Any]:
    return {
        "maps": load_json("maps.json", {"maps": []}),
        "assignments": load_json("assignments.json", {"assignments": []}),
        "people": load_json("people.json", {"people": []}),
        "devices": load_json("devices.json", {"devices": []}),
        "locations": load_json("locations.json", {"locations": []}),
        "managedAreas": load_json("managed-areas.json", {"areas": []}),
        "grid": {"columns": 24, "rows": 18},
        "scenarios": [],
        "readOnly": False,
    }


def percent5(values: list[float]) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    index = max(0, math.ceil(len(ordered) * 0.05) - 1)
    return ordered[index]


def format_distance(value: float | None) -> str:
    return "—" if value is None else f"{value:.2f} px"


def main() -> None:
    maps = initial_data()["maps"]["maps"]
    server = ThreadingHTTPServer(("127.0.0.1", 0), QuietResourceHandler)
    server_thread = threading.Thread(target=server.serve_forever, daemon=True)
    original_directory = Path.cwd()
    os.chdir(RESOURCES)
    server_thread.start()

    try:
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch(
                headless=True,
                executable_path=r"C:\Program Files\Google\Chrome\Application\chrome.exe",
            )
            page = browser.new_page(viewport={"width": WINDOW_WIDTH, "height": WINDOW_HEIGHT}, device_scale_factor=1)
            data = json.dumps(initial_data(), ensure_ascii=False)
            page.add_init_script(
                """
                const initialData = %s;
                window.chrome = window.chrome || {};
                window.chrome.webview = {
                  addEventListener() {},
                  postMessage(message) {
                    let response = null;
                    if (message.action === 'getUserPreferences') {
                      response = { action: 'getUserPreferencesResult', success: true,
                        data: { theme: 'professional-light', singleKeyShortcutsEnabled: true } };
                    } else if (message.action === 'loadInitialData') {
                      response = { action: 'loadInitialDataResult', success: true, data: initialData };
                    } else if (message.action === 'runValidation') {
                      response = { action: 'runValidationResult', success: true,
                        data: { results: [], summary: { total: 0, critical: 0, warning: 0, info: 0 } } };
                    } else if (message.action === 'runSpatialAnalytics') {
                      response = { action: 'runSpatialAnalyticsResult', success: true, data: { result: {} } };
                    }
                    if (response) setTimeout(() => window.receiveFromNative?.(response), 0);
                  }
                };
                """ % data
            )
            page.goto(f"http://127.0.0.1:{server.server_port}/index.html", wait_until="networkidle")
            page.wait_for_function("document.querySelectorAll('#map-select option').length === 5")

            measurements = []
            for map_data in maps:
                map_id = map_data["id"]
                expected_pins = len(map_data.get("seats", []))
                page.select_option("#map-select", map_id)
                page.wait_for_function(
                    "([expected, mapId, resource]) => { "
                    "const plan = document.querySelector('#plan'); "
                    "return document.querySelectorAll('.pin').length === expected && "
                    "document.querySelector('#map-select').value === mapId && "
                    "plan.complete && plan.naturalWidth > 0 && plan.src.endsWith(resource); "
                    "}",
                    arg=[expected_pins, map_id, map_data["image"]],
                )
                page.wait_for_timeout(100)
                measurements.append(page.evaluate(
                    """
                    () => {
                      const centers = [...document.querySelectorAll('.pin')].map(pin => {
                        const rect = pin.getBoundingClientRect();
                        return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
                      });
                      const nearest = centers.map((center, index) => Math.min(...centers
                        .filter((_, otherIndex) => otherIndex !== index)
                        .map(other => Math.hypot(center.x - other.x, center.y - other.y))));
                      const pairDistances = centers.flatMap((center, index) => centers
                        .slice(index + 1)
                        .map(other => Math.hypot(center.x - other.x, center.y - other.y)));
                      const stage = document.querySelector('#stage');
                      const plan = document.querySelector('#plan');
                      const transform = getComputedStyle(stage).transform;
                      const match = transform.match(/^matrix\\(([^,]+)/);
                      return {
                        pinCount: centers.length,
                        minimum: nearest.length ? Math.min(...nearest) : null,
                        nearest,
                        pairDistancesBelow20: pairDistances.filter(distance => distance < 20).length,
                        zoom: match ? Number(match[1]) : 1,
                        viewport: [document.querySelector('#mapwrap').clientWidth, document.querySelector('#mapwrap').clientHeight],
                        plan: [plan.getBoundingClientRect().width, plan.getBoundingClientRect().height]
                      };
                    }
                    """
                ))

            active_map = next(map_data for map_data in maps if map_data["seats"])
            page.select_option("#map-select", active_map["id"])
            page.locator("#map-to-list").focus()
            page.keyboard.press("Enter")
            page.wait_for_function(
                "([mapId, expected]) => document.querySelector('#filter-zone').value === mapId && "
                "document.querySelectorAll('#list-table tbody tr').length === expected && "
                "!document.querySelector('#listview').classList.contains('hidden')",
                arg=[active_map["id"], len(active_map["seats"])],
            )
            page.locator("#list-table tbody tr").first.focus()
            page.keyboard.press("Enter")
            page.wait_for_function("!document.querySelector('#detail-panel').classList.contains('hidden')")
            page.locator("#edit-seat").focus()
            page.keyboard.press("Enter")
            assert page.evaluate("document.activeElement?.id") == "seat-name"
            page.locator("#move-seat").focus()
            page.keyboard.press("Enter")
            assert not page.locator("#mapwrap").evaluate("element => element.classList.contains('hidden')")
            page.locator("#map-to-list").focus()
            page.keyboard.press("Enter")
            page.locator("#list-table tbody tr").first.focus()
            page.keyboard.press("Enter")
            page.locator("#panel-history").focus()
            page.keyboard.press("Enter")
            page.wait_for_function("document.querySelector('#history-dialog').open")
            page.locator("#history-dialog").evaluate("dialog => dialog.close()")
            keyboard_equivalence_verified = True
            browser.close()
    finally:
        server.shutdown()
        server.server_close()
        os.chdir(original_directory)

    print(f"Viewport de medición: {WINDOW_WIDTH}×{WINDOW_HEIGHT} CSS px (escala de dispositivo 1).")
    print("P5 = percentil 5 de la distancia al vecino más próximo de cada pin.")
    print(f"Ruta teclado mapa → lista → inspector verificada: {'sí' if keyboard_equivalence_verified else 'no'}." )
    print()
    print("| Plano | Pines | Zoom inicial | Área visible | Plano renderizado | Mínimo | P5 | P50/mediana | Pares <20 px |")
    print("|---|---:|---:|---:|---:|---:|---:|---:|---:|")
    for map_data, result in zip(maps, measurements, strict=True):
        viewport = f"{result['viewport'][0]}×{result['viewport'][1]}"
        plan = f"{result['plan'][0]:.0f}×{result['plan'][1]:.0f}"
        print(
            f"| {map_data['name']} ({map_data['id']}) | {result['pinCount']} | "
            f"{result['zoom'] * 100:.2f} % | {viewport} | {plan} | "
            f"{format_distance(result['minimum'])} | {format_distance(percent5(result['nearest']))} | "
            f"{format_distance(median(result['nearest']) if result['nearest'] else None)} | "
            f"{result['pairDistancesBelow20']} |"
        )


if __name__ == "__main__":
    main()
