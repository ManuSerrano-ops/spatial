"""Measure nearest pin-center spacing from the real operational maps in Chromium.

The browser viewport matches MainWindow's configured 1400x900 DIP startup size.
This script is read-only: it serves Resources/ locally and reads runtime-data/data.
"""

from __future__ import annotations

import math
from statistics import median

from playwright.sync_api import sync_playwright

from frontend_harness import (
    launch_chromium,
    new_frontend_context,
    open_frontend_page,
    resource_server,
    runtime_initial_data,
)

WINDOW_WIDTH = 1400
WINDOW_HEIGHT = 900


def percent5(values: list[float]) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    index = max(0, math.ceil(len(ordered) * 0.05) - 1)
    return ordered[index]


def format_distance(value: float | None) -> str:
    return "—" if value is None else f"{value:.2f} px"


def main() -> None:
    initial_data = runtime_initial_data()
    maps = initial_data["maps"]["maps"]
    with resource_server() as server:
        with sync_playwright() as playwright:
            browser = launch_chromium(playwright)
            context = new_frontend_context(browser, {"width": WINDOW_WIDTH, "height": WINDOW_HEIGHT})
            page = open_frontend_page(context, server.server_port, initial_data)
            page.wait_for_function("() => document.querySelectorAll('#map-select option').length === 5")

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
            page.wait_for_function("() => !document.querySelector('#detail-panel').classList.contains('hidden')")
            page.locator("#edit-seat").focus()
            page.keyboard.press("Enter")
            assert page.evaluate("document.activeElement?.id") == "seat-name"
            page.locator("#move-seat").focus()
            page.keyboard.press("Enter")
            assert not page.locator("#mapwrap").evaluate("element => element.classList.contains('hidden')")
            # 3.6 makes Enter inside #mapwrap confirm the active grid cursor;
            # cancel the mode before exercising the independent map→list route.
            page.keyboard.press("Escape")
            page.locator("#map-to-list").focus()
            page.keyboard.press("Enter")
            page.locator("#list-table tbody tr").first.focus()
            page.keyboard.press("Enter")
            history = page.locator("#panel-history")
            history.focus()
            page.keyboard.press("Enter")
            page.locator("#history-dialog[open]").wait_for()
            page.locator("#history-dialog").evaluate("dialog => dialog.close()")
            keyboard_equivalence_verified = True
            browser.close()

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
