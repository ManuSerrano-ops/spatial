"""Measure the proposed 900x460 minimum viewport in Chromium.

The harness serves the real frontend and injects a read-only WebView bridge. It
never writes runtime-data. Dynamic-dialog cases reach the real UI routes:
``#diff`` receives synthetic native results and mass editing selects real list
rows before opening ``#bulk-dialog``.
"""

from __future__ import annotations

from typing import Any

from playwright.sync_api import Browser, Page, sync_playwright

from frontend_harness import (
    launch_chromium,
    new_frontend_context,
    open_frontend_page,
    resource_server,
    runtime_initial_data,
)

VIEWPORT = {"width": 900, "height": 460}


def bulk_data(excluded_count: int) -> dict[str, Any]:
    """Create one eligible free seat followed by occupied exclusions.

    The real selection flow groups the exclusions by their reason, so both
    measurements should produce one list row ("Puesto ocupado."). This fixture
    tests whether the count itself can make ``#bulk-dialog`` grow.
    """
    seats = [{"id": "bulk-free", "name": "Medible libre", "x": 0.05, "y": 0.05}]
    assignments: list[dict[str, Any]] = []
    for index in range(excluded_count):
        seat_id = f"bulk-occupied-{index + 1:03d}"
        seats.append(
            {
                "id": seat_id,
                "name": f"Puesto ocupado {index + 1}",
                "x": 0.10 + (index % 10) * 0.08,
                "y": 0.10 + (index // 10) * 0.08,
            }
        )
        assignments.append(
            {
                "workstationId": seat_id,
                "personId": f"person-{index + 1}",
                "status": "confirmed",
            }
        )

    return {
        "maps": {
            "maps": [
                {
                    "id": "bulk-measurement",
                    "name": "Medición edición masiva",
                    "image": "plano_sur_limpio.svg",
                    "seats": seats,
                }
            ]
        },
        "assignments": {"assignments": assignments},
        "people": {"people": []},
        "devices": {"devices": []},
        "locations": {"locations": []},
        "managedAreas": {"areas": []},
        "grid": {"columns": 24, "rows": 18},
        "scenarios": [],
        "activeScenario": None,
        "readOnly": False,
    }


def diff_changes(count: int) -> list[dict[str, Any]]:
    return [
        {
            "id": f"viewport-change-{index + 1:03d}",
            "kind": "MODIFIED",
            "type": "MODIFIED",
            "entityType": "workspace",
            "entityId": f"viewport-seat-{index + 1:03d}",
            "seatId": f"viewport-seat-{index + 1:03d}",
            "mapId": "viewport-map",
            "mapName": "Plano de medición",
            "changedFields": [
                {
                    "field": "locationId",
                    "before": f"Ubicación anterior {index + 1}",
                    "after": f"Ubicación propuesta {index + 1}",
                }
            ],
        }
        for index in range(count)
    ]



def geometry_script() -> str:
    return """
    () => {
      const viewport = { width: innerWidth, height: innerHeight };
      const visible = element => {
        const style = getComputedStyle(element);
        return style.display !== 'none' && style.visibility !== 'hidden' && element.getClientRects().length > 0;
      };
      const rect = element => {
        const value = element.getBoundingClientRect();
        return { left: value.left, top: value.top, right: value.right, bottom: value.bottom, width: value.width, height: value.height };
      };
      const insideViewport = value => value.left >= 0 && value.top >= 0 && value.right <= viewport.width && value.bottom <= viewport.height;
      const topbarControls = [...document.querySelectorAll('.app-topbar button, .app-topbar select, .app-topbar input')]
        .filter(visible)
        .map(element => ({ id: element.id || element.name || element.type, rect: rect(element) }));
      const map = document.querySelector('#mapwrap');
      const toolbar = document.querySelector('.workspace-region .view-toolbar');
      return {
        documentOverflow: { width: document.documentElement.scrollWidth, height: document.documentElement.scrollHeight },
        viewport,
        topbarControls,
        escapedTopbarControls: topbarControls.filter(control => !insideViewport(control.rect)),
        toolbar: rect(toolbar),
        map: { ...rect(map), clientHeight: map.clientHeight, clientWidth: map.clientWidth },
      };
    }
    """


def dialog_measurement(page: Page, dialog_selector: str, list_selector: str) -> dict[str, Any]:
    return page.locator(dialog_selector).evaluate(
        """(dialog, listSelector) => {
          const rect = element => {
            if (!element) return null;
            const value = element.getBoundingClientRect();
            return { left: value.left, top: value.top, right: value.right, bottom: value.bottom, width: value.width, height: value.height };
          };
          const list = dialog.querySelector(listSelector);
          const actions = dialog.querySelector('.dialog-actions');
          const style = list ? getComputedStyle(list) : null;
          return {
            viewport: { width: innerWidth, height: innerHeight },
            dialog: rect(dialog),
            actions: rect(actions),
            list: list ? {
              itemCount: list.children.length,
              clientHeight: list.clientHeight,
              scrollHeight: list.scrollHeight,
              overflowY: style.overflowY,
            } : null,
          };
        }""",
        list_selector,
    )


def inside_viewport(rect: dict[str, float], viewport: dict[str, float]) -> bool:
    return rect["left"] >= 0 and rect["top"] >= 0 and rect["right"] <= viewport["width"] and rect["bottom"] <= viewport["height"]


def assert_geometry(measurement: dict[str, Any]) -> None:
    viewport = measurement["viewport"]
    overflow = measurement["documentOverflow"]
    if overflow["width"] > viewport["width"] or overflow["height"] > viewport["height"]:
        raise AssertionError(f"El documento desborda el viewport: {overflow} frente a {viewport}.")
    if measurement["escapedTopbarControls"]:
        raise AssertionError(f"Controles de barra fuera del viewport: {measurement['escapedTopbarControls']}")
    if measurement["map"]["clientHeight"] <= 0:
        raise AssertionError(f"El mapa no conserva área operativa: {measurement['map']}")


def assert_inside_viewport(rect: dict[str, float], label: str) -> None:
    if not inside_viewport(rect, VIEWPORT):
        raise AssertionError(f"{label} queda fuera del viewport: {rect}")


def open_page(
    browser: Browser,
    port: int,
    data: dict[str, Any],
    changes: list[dict[str, Any]] | None = None,
    viewport: dict[str, int] = VIEWPORT,
    theme: str = "high-contrast",
) -> Page:
    context = new_frontend_context(browser, viewport)
    return open_frontend_page(context, port, data, scenario_changes=changes, theme=theme)


def keyboard_reaches(page: Page, origin: str, target: str, tab_count: int, key: str = "Tab") -> bool:
    page.locator(origin).focus()
    for _ in range(tab_count):
        page.keyboard.press(key)
    return page.evaluate("target => document.activeElement?.id === target", target.removeprefix("#"))


def measure_diff(browser: Browser, port: int, count: int) -> dict[str, Any]:
    page = open_page(browser, port, runtime_initial_data(active_scenario=True), diff_changes(count))
    try:
        # At 900 px the responsive header puts Diff in the real "Más acciones" menu.
        page.locator("#more").click()
        page.locator("#diff").click()
        page.locator("#diff-dialog[open]").wait_for()
        page.locator("#diff-list .change-row").nth(count - 1).wait_for()
        measurement = dialog_measurement(page, "#diff-dialog", "#diff-list")
        # The two selection controls, one checkbox per diff unit, the closing
        # action, and finally Apply are all tabbable in this dialog.
        measurement["keyboardApplyReachable"] = keyboard_reaches(
            page, "#diff-dialog .dialog-close", "#apply-dialog", count + 4
        )
        measurement["focusedAction"] = page.evaluate("document.activeElement?.id || null")
        return measurement
    finally:
        page.close()


def select_bulk_range(page: Page, excluded_count: int) -> None:
    page.locator("#list-view").click()
    rows = page.locator("#list-table tbody tr")
    rows.nth(excluded_count).wait_for()
    rows.first.click()
    # The list's production handler owns range selection; dispatch Shift there
    # rather than mutating application state from the harness.
    rows.nth(excluded_count).dispatch_event("click", {"shiftKey": True})


def measure_bulk(browser: Browser, port: int, excluded_count: int) -> dict[str, Any]:
    page = open_page(browser, port, bulk_data(excluded_count))
    try:
        select_bulk_range(page, excluded_count)
        bulk_apply = page.locator("#bulk-apply")
        bulk_apply.focus()
        bulk_apply.press("Enter")
        page.locator("#bulk-dialog[open]").wait_for()
        measurement = dialog_measurement(page, "#bulk-dialog", "#bulk-exclusions")
        measurement["keyboardConfirmReachable"] = keyboard_reaches(
            page, "#bulk-dialog [data-close]", "#bulk-confirm", 1
        )
        measurement["focusedAction"] = page.evaluate("document.activeElement?.id || null")
        return measurement
    finally:
        page.close()


def measure_bulk_apply_overlap(
    browser: Browser, port: int, viewport: dict[str, int], theme: str
) -> dict[str, Any]:
    page = open_page(browser, port, bulk_data(5), viewport=viewport, theme=theme)
    try:
        select_bulk_range(page, 5)
        return page.evaluate(
            """() => {
              const rect = selector => {
                const value = document.querySelector(selector).getBoundingClientRect();
                return { left: value.left, top: value.top, right: value.right, bottom: value.bottom, width: value.width, height: value.height };
              };
              const bulkApply = rect('#bulk-apply');
              const detailHeader = rect('.detail-panel-header');
              const overlaps = bulkApply.left < detailHeader.right && bulkApply.right > detailHeader.left && bulkApply.top < detailHeader.bottom && bulkApply.bottom > detailHeader.top;
              return { viewport: { width: innerWidth, height: innerHeight }, bulkApply, detailHeader, overlaps };
            }"""
        )
    finally:
        page.close()


def validate_dynamic_measurements(diff: dict[int, dict[str, Any]], bulk: dict[int, dict[str, Any]]) -> list[str]:
    failures: list[str] = []
    for count, measurement in diff.items():
        if not inside_viewport(measurement["dialog"], measurement["viewport"]):
            failures.append(f"diff con {count} cambios deja el diálogo fuera: {measurement['dialog']}")
        if not inside_viewport(measurement["actions"], measurement["viewport"]):
            failures.append(f"diff con {count} cambios deja las acciones fuera: {measurement['actions']}")
        if measurement["list"]["overflowY"] not in ("auto", "scroll"):
            failures.append(f"diff con {count} cambios no tiene lista desplazable: {measurement['list']}")
        if not measurement["keyboardApplyReachable"]:
            failures.append(f"diff con {count} cambios no alcanza Aplicar por tabulación.")
    if abs(diff[50]["dialog"]["height"] - diff[500]["dialog"]["height"]) > 0.5:
        failures.append("El diálogo diff cambia de altura entre 50 y 500 cambios.")

    for count, measurement in bulk.items():
        if not inside_viewport(measurement["dialog"], measurement["viewport"]):
            failures.append(f"bulk con {count} exclusiones deja el diálogo fuera: {measurement['dialog']}")
        if not inside_viewport(measurement["actions"], measurement["viewport"]):
            failures.append(f"bulk con {count} exclusiones deja las acciones fuera: {measurement['actions']}")
        if not measurement["keyboardConfirmReachable"]:
            failures.append(f"bulk con {count} exclusiones no alcanza Aplicar por tabulación.")
    return failures


def main() -> None:
    with resource_server() as server:
        with sync_playwright() as playwright:
            browser = launch_chromium(playwright)
            normal_page = open_page(browser, server.server_port, runtime_initial_data(active_scenario=False))
            try:
                initial = normal_page.evaluate(geometry_script())
                assert_geometry(initial)
                normal_page.locator(".pin:not(:disabled)").first.click()
                normal_page.locator("#detail-panel:not(.hidden)").wait_for()
                detail = normal_page.locator("#detail-panel").evaluate(
                    """element => {
                      const rect = element.getBoundingClientRect();
                      return { left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom, width: rect.width, height: rect.height, clientHeight: element.clientHeight };
                    }"""
                )
                assert_inside_viewport(detail, "El panel de detalle (high-contrast)")
                normal_page.locator("#panel-history").click()
                normal_page.locator("#history-dialog[open]").wait_for()
                history = dialog_measurement(normal_page, "#history-dialog", "#events-list")
                assert_inside_viewport(history["dialog"], "El diálogo de historial (high-contrast)")
            finally:
                normal_page.close()

            diff = {count: measure_diff(browser, server.server_port, count) for count in (50, 500)}
            bulk = {count: measure_bulk(browser, server.server_port, count) for count in (5, 100)}
            overlap_cases = [
                ("900×460", {"width": 900, "height": 460}),
                ("1100×560", {"width": 1100, "height": 560}),
                ("1400×900", {"width": 1400, "height": 900}),
            ]
            overlap = {
                (label, theme): measure_bulk_apply_overlap(browser, server.server_port, viewport, theme)
                for label, viewport in overlap_cases
                for theme in ("professional-light", "high-contrast")
            }
            browser.close()

    failures = validate_dynamic_measurements(diff, bulk)
    print(f"Viewport comprobado: {VIEWPORT['width']}×{VIEWPORT['height']} CSS px; tema high-contrast.")
    print(f"Base: barra={len(initial['topbarControls'])} controles; toolbar={initial['toolbar']['width']:.0f}×{initial['toolbar']['height']:.0f}px; mapa={initial['map']['clientWidth']}×{initial['map']['clientHeight']}px; panel={detail['width']:.0f}×{detail['height']:.0f}px; historial={history['dialog']['width']:.0f}×{history['dialog']['height']:.0f}px.")
    for count, measurement in diff.items():
        print(f"Diff {count}: diálogo={measurement['dialog']['width']:.0f}×{measurement['dialog']['height']:.0f}px, bottom={measurement['dialog']['bottom']:.0f}/{measurement['viewport']['height']}; acciones bottom={measurement['actions']['bottom']:.0f}; lista={measurement['list']['clientHeight']}/{measurement['list']['scrollHeight']}px, overflow-y={measurement['list']['overflowY']}, Aplicar por teclado={measurement['keyboardApplyReachable']}.")
    for count, measurement in bulk.items():
        print(f"Bulk {count} exclusiones: diálogo={measurement['dialog']['width']:.0f}×{measurement['dialog']['height']:.0f}px, bottom={measurement['dialog']['bottom']:.0f}/{measurement['viewport']['height']}; acciones bottom={measurement['actions']['bottom']:.0f}; filas de exclusión={measurement['list']['itemCount']}; lista={measurement['list']['clientHeight']}/{measurement['list']['scrollHeight']}px, overflow-y={measurement['list']['overflowY']}, Aplicar por teclado={measurement['keyboardConfirmReachable']}.")
    for (label, theme), measurement in overlap.items():
        apply = measurement["bulkApply"]
        header = measurement["detailHeader"]
        print(f"Solapamiento {label}, {theme}: Aplicar=({apply['left']:.0f},{apply['top']:.0f})–({apply['right']:.0f},{apply['bottom']:.0f}); cabecera=({header['left']:.0f},{header['top']:.0f})–({header['right']:.0f},{header['bottom']:.0f}); solapan={measurement['overlaps']}.")
    if failures:
        raise AssertionError("\n".join(failures))


if __name__ == "__main__":
    main()
