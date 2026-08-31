"""Verify Windows forced-colors pin semantics in Chromium.

This verification serves the real frontend with the same read-only runtime data
used by the pin-spacing measurement. It deliberately emulates forced-colors
rather than inferring behavior from CSS declarations.
"""

from __future__ import annotations

import json
import os
import re
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
        const properties = ['backgroundColor', 'borderColor', 'borderStyle', 'borderRadius', 'borderWidth', 'color'];
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
          outlineOffset: getComputedStyle(occupied).outlineOffset,
          innerRingColor: getComputedStyle(occupied, '::before').borderColor,
          innerRingStyle: getComputedStyle(occupied, '::before').borderStyle,
          innerRingWidth: getComputedStyle(occupied, '::before').borderWidth,
          innerRingInset: getComputedStyle(occupied, '::before').inset,
          innerRingContent: getComputedStyle(occupied, '::before').content
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
    if focused["innerRingColor"] != result["systemColors"]["CanvasText"] or focused["innerRingStyle"] == "none" or focused["innerRingWidth"] == "0px":
        raise AssertionError(f"El foco no conserva el anillo interior CanvasText en {context}: {focused}")
    assert_focus_contrast(result, context)


def parse_rgb(value: str) -> tuple[int, int, int]:
    match = re.fullmatch(r"rgb\((\d+),\s*(\d+),\s*(\d+)\)", value)
    if not match:
        raise AssertionError(f"Color computado no RGB: {value}")
    return tuple(int(channel) for channel in match.groups())


def relative_luminance(value: str) -> float:
    channels = []
    for channel in parse_rgb(value):
        normalized = channel / 255
        channels.append(normalized / 12.92 if normalized <= 0.04045 else ((normalized + 0.055) / 1.055) ** 2.4)
    return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2]


def contrast_ratio(foreground: str, background: str) -> float:
    lighter, darker = sorted((relative_luminance(foreground), relative_luminance(background)), reverse=True)
    return (lighter + 0.05) / (darker + 0.05)


def focus_contrasts(result: dict[str, Any]) -> dict[str, float]:
    canvas_text = result["systemColors"]["CanvasText"]
    return {
        state: contrast_ratio(canvas_text, result["pins"][state]["backgroundColor"])
        for state in ("free", "occupied", "reserved")
    }


def assert_focus_contrast(result: dict[str, Any], context: str) -> None:
    contrasts = focus_contrasts(result)
    failures = {state: value for state, value in contrasts.items() if value < 3}
    if failures:
        raise AssertionError(f"El anillo interior de foco no alcanza 3:1 en {context}: {failures}")


def assert_combination_equivalence(results: list[tuple[str, str, dict[str, Any]]]) -> None:
    baseline = results[0][2]
    for theme, appearance, result in results:
        context = f"{theme} / mapa {appearance}"
        for family in ("pins", "legend"):
            for state in ("free", "occupied", "reserved"):
                for property_name, expected in baseline[family][state].items():
                    known_dark_selection = theme == "penpot-dark" and state == "occupied" and property_name in {"backgroundColor", "color"}
                    if not known_dark_selection and result[family][state][property_name] != expected:
                        raise AssertionError(
                            f"{family} {state}.{property_name} difiere del baseline fuera de la variación "
                            f"SelectedItem conocida en {context}."
                        )
        for property_name, expected in baseline["focused"].items():
            if result["focused"][property_name] != expected:
                raise AssertionError(f"El foco difiere del baseline en {context}: {property_name}")


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
            assert_combination_equivalence(results)
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
    print("El anillo interior CanvasText contrasta contra todos los rellenos de pin:")
    print("| Tema | Plano | Libre | Ocupado | Reservado |")
    print("|---|---|---:|---:|---:|")
    for theme, appearance, result in results:
        contrasts = focus_contrasts(result)
        print(f"| {theme} | {appearance} | {contrasts['free']:.2f}:1 | {contrasts['occupied']:.2f}:1 | {contrasts['reserved']:.2f}:1 |")
    print()
    print("Invariante de equivalencia: las ocho combinaciones coinciden salvo SelectedItem/SelectedItemText en penpot-dark.")
    print("Colores del sistema que Chromium resolvió en la primera combinación:")
    print(json.dumps(results[0][2]["systemColors"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
