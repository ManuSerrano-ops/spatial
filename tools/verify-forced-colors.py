"""Verify Windows forced-colors pin semantics in Chromium.

This verification serves the real frontend with the same read-only runtime data
used by the pin-spacing measurement. It deliberately emulates forced-colors
rather than inferring behavior from CSS declarations.
"""

from __future__ import annotations

import json
import os
import threading
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any

from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
RESOURCES = ROOT / "Resources"
DATA = ROOT / "runtime-data" / "data"
THEMES = ("professional-light", "penpot-dark", "high-contrast", "projector")
MAP_APPEARANCES = ("dark", "light")


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


def pin_style_script() -> str:
    return """
      ([theme, appearance]) => {
        document.documentElement.dataset.theme = theme;
        document.documentElement.dataset.mapAppearance = appearance;
        const properties = ['backgroundColor', 'borderColor', 'borderStyle', 'borderRadius', 'borderWidth'];
        const styleFor = element => Object.fromEntries(properties.map(property => [property, getComputedStyle(element)[property]]));
        const legendFor = state => document.querySelector(`.legend-marker.state-${state}`);
        const states = ['free', 'occupied', 'reserved'];
        const candidates = [...document.querySelectorAll('.pin')];
        const used = new Set();
        states.forEach(state => {
          const pin = candidates.find(candidate => candidate.dataset.state === state && !used.has(candidate))
            || candidates.find(candidate => !used.has(candidate));
          used.add(pin);
          pin.dataset.forcedColorsSample = state;
          pin.dataset.state = state;
        });
        const pinFor = state => document.querySelector(`.pin[data-forced-colors-sample="${state}"]`);
        const pins = Object.fromEntries(states.map(state => [state, styleFor(pinFor(state))]));
        const legend = Object.fromEntries(states.map(state => [state, styleFor(legendFor(state))]));
        const occupied = pinFor('occupied');
        document.activeElement?.blur();
        const beforeFocus = {
          outlineColor: getComputedStyle(occupied).outlineColor,
          outlineStyle: getComputedStyle(occupied).outlineStyle,
          outlineWidth: getComputedStyle(occupied).outlineWidth,
          outlineOffset: getComputedStyle(occupied).outlineOffset
        };
        occupied.focus();
        const focused = {
          isFocused: document.activeElement === occupied,
          outlineColor: getComputedStyle(occupied).outlineColor,
          outlineStyle: getComputedStyle(occupied).outlineStyle,
          outlineWidth: getComputedStyle(occupied).outlineWidth,
          outlineOffset: getComputedStyle(occupied).outlineOffset
        };
        const resolveSystemColor = (property, value) => {
          const probe = document.createElement('span');
          probe.style.position = 'absolute';
          probe.style[property] = value;
          document.body.append(probe);
          const result = getComputedStyle(probe)[property];
          probe.remove();
          return result;
        };
        const systemColors = {
          Canvas: resolveSystemColor('backgroundColor', 'Canvas'),
          CanvasText: resolveSystemColor('color', 'CanvasText'),
          ButtonFace: resolveSystemColor('backgroundColor', 'ButtonFace'),
          SelectedItem: resolveSystemColor('backgroundColor', 'SelectedItem'),
          SelectedItemText: resolveSystemColor('color', 'SelectedItemText'),
          Highlight: resolveSystemColor('backgroundColor', 'Highlight')
        };
        return { pins, legend, beforeFocus, focused, systemColors };
      }
    """


def assert_distinct_states(styles: dict[str, dict[str, str]], context: str) -> None:
    tuples = {state: tuple(style.values()) for state, style in styles.items()}
    if len(set(tuples.values())) != len(tuples):
        raise AssertionError(f"Los estados de pin no son distinguibles en {context}: {tuples}")


def assert_legend_matches(result: dict[str, Any], context: str) -> None:
    for state in ("free", "occupied", "reserved"):
        if result["pins"][state] != result["legend"][state]:
            raise AssertionError(
                f"La leyenda {state} no coincide con el pin en {context}: "
                f"{result['legend'][state]} != {result['pins'][state]}"
            )


def assert_focus_visible(result: dict[str, Any], context: str) -> None:
    before = result["beforeFocus"]
    focused = result["focused"]
    if not focused["isFocused"]:
        raise AssertionError(f"El pin ocupado no recibió foco en {context}.")
    if focused["outlineStyle"] == "none" or focused["outlineWidth"] == "0px":
        raise AssertionError(f"El foco no tiene contorno visible en {context}: {focused}")
    if all(before[key] == focused[key] for key in ("outlineColor", "outlineStyle", "outlineWidth", "outlineOffset")):
        raise AssertionError(f"El pin ocupado con foco no difiere del no enfocado en {context}: {focused}")
    if focused["outlineColor"] == result["pins"]["occupied"]["backgroundColor"]:
        raise AssertionError(f"El foco comparte color con el estado ocupado en {context}: {focused}")


def compact(style: dict[str, str]) -> str:
    return "; ".join(f"{key}={value}" for key, value in style.items())


def main() -> None:
    server = ThreadingHTTPServer(("127.0.0.1", 0), QuietResourceHandler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    original_directory = Path.cwd()
    os.chdir(RESOURCES)
    thread.start()

    try:
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch(
                headless=True,
                executable_path=r"C:\Program Files\Google\Chrome\Application\chrome.exe",
            )
            context = browser.new_context(
                viewport={"width": 1400, "height": 900},
                device_scale_factor=1,
                forced_colors="active",
            )
            page = context.new_page()
            payload = json.dumps(initial_data(), ensure_ascii=False)
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
                """ % payload
            )
            page.goto(f"http://127.0.0.1:{server.server_port}/index.html", wait_until="networkidle")
            page.locator(".pin").first.wait_for()
            if page.locator(".pin").count() < 3:
                raise AssertionError("Se necesitan tres pines reales para comprobar los tres estados.")

            results: list[tuple[str, str, dict[str, Any]]] = []
            for theme in THEMES:
                for appearance in MAP_APPEARANCES:
                    result = page.evaluate(pin_style_script(), [theme, appearance])
                    label = f"{theme} / mapa {appearance}"
                    assert_distinct_states(result["pins"], label)
                    assert_legend_matches(result, label)
                    assert_focus_visible(result, label)
                    results.append((theme, appearance, result))
            browser.close()
    finally:
        server.shutdown()
        server.server_close()
        os.chdir(original_directory)

    print("forced-colors=active en Chromium; 4 temas × 2 apariencias de plano.")
    print("| Tema | Plano | Libre | Ocupado | Reservado | Foco en ocupado |")
    print("|---|---|---|---|---|---|")
    for theme, appearance, result in results:
        focus = compact({key: value for key, value in result["focused"].items() if key != "isFocused"})
        print(
            f"| {theme} | {appearance} | {compact(result['pins']['free'])} | "
            f"{compact(result['pins']['occupied'])} | {compact(result['pins']['reserved'])} | {focus} |"
        )
    print()
    print("Colores del sistema que Chromium resolvió en la primera combinación:")
    print(json.dumps(results[0][2]["systemColors"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
