"""Verify selected accessible controls through CDP node-to-AX correlation.

The checks intentionally target observable controls instead of AX-tree counts:
controls present on load, controls exposed after a real opener interaction, and
the known unreachable cluster-shape dialog. Closed dialogs are never altered to
make them measurable.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from playwright.sync_api import Browser, BrowserContext, CDPSession, Page, sync_playwright

from frontend_harness import launch_chromium, new_frontend_context, open_frontend_page, resource_server

ROOT = Path(__file__).resolve().parents[1]
FIXTURE = ROOT / "tests" / "visual-fixtures" / "general.json"


def load_fixture() -> dict[str, Any]:
    return json.loads(FIXTURE.read_text(encoding="utf-8"))


def fixture_bridge_results(fixture: dict[str, Any]) -> dict[str, Any]:
    return {
        "runValidation": fixture["runValidationResult"],
        "runSpatialAnalytics": fixture["runSpatialAnalyticsResult"],
        "reportPlanResourceDiagnostic": {"logged": True},
        "getEvents": {"events": []},
    }


def ax_node(session: CDPSession, selector: str) -> dict[str, Any] | None:
    root = session.send("DOM.getDocument", {"depth": 1})["root"]["nodeId"]
    node_id = session.send("DOM.querySelector", {"nodeId": root, "selector": selector})["nodeId"]
    if not node_id:
        raise AssertionError(f"No existe el nodo DOM {selector}.")
    backend_node_id = session.send("DOM.describeNode", {"nodeId": node_id})["node"].get("backendNodeId")
    nodes = session.send("Accessibility.getPartialAXTree", {"nodeId": node_id, "fetchRelatives": False})["nodes"]
    return next((node for node in nodes if node.get("backendDOMNodeId") == backend_node_id), None)


def ax_value(node: dict[str, Any], property_name: str) -> str | None:
    value = node.get(property_name, {}).get("value")
    return str(value) if value is not None else None


def assert_exposed(session: CDPSession, selector: str, role: str, name: str) -> None:
    node = ax_node(session, selector)
    if node is None or node.get("ignored"):
        raise AssertionError(f"{selector} no está expuesto en el árbol AX: {node}")
    actual_role = ax_value(node, "role")
    actual_name = ax_value(node, "name")
    if actual_role != role or actual_name != name:
        raise AssertionError(f"{selector} AX esperado {role!r}/{name!r}, obtenido {actual_role!r}/{actual_name!r}.")


def assert_not_exposed(session: CDPSession, selector: str) -> None:
    node = ax_node(session, selector)
    if node is not None and not node.get("ignored"):
        raise AssertionError(f"{selector} es alcanzable en AX aunque no tiene ruta de apertura: {node}")


def active_element_is(page: Page, selector: str) -> None:
    if not page.locator(selector).evaluate("element => document.activeElement === element"):
        active = page.evaluate("() => document.activeElement?.id || document.activeElement?.getAttribute('data-dialog') || null")
        raise AssertionError(f"El abridor {selector} no tenía foco; foco actual: {active!r}.")


def main() -> None:
    fixture = load_fixture()
    with resource_server() as server:
        with sync_playwright() as playwright:
            browser: Browser = launch_chromium(playwright)
            context: BrowserContext = new_frontend_context(browser, {"width": 1400, "height": 900})
            try:
                page = open_frontend_page(
                    context,
                    server.server_port,
                    fixture["loadInitialDataResult"],
                    bridge_results=fixture_bridge_results(fixture),
                    theme="professional-light",
                )
                session = context.new_cdp_session(page)

                # Category 1: visible controls are represented with their real role and name.
                assert_exposed(session, "#map-select", "combobox", "Plano o zona")
                assert_exposed(session, "#add-seat", "button", "Añadir puesto")

                # Category 2: focus the actual opener before opening its container.
                opener = page.locator('[data-dialog="history-dialog"]')
                opener.focus()
                active_element_is(page, '[data-dialog="history-dialog"]')
                opener.press("Enter")
                page.locator("#history-dialog[open]").wait_for()
                assert_exposed(session, "#history-dialog .dialog-close", "button", "Cerrar")
                page.keyboard.press("Escape")
                if page.locator("#history-dialog").evaluate("dialog => dialog.open"):
                    raise AssertionError("Escape no cerró el diálogo abierto por el abridor real.")
                active_element_is(page, '[data-dialog="history-dialog"]')

                # Category 3: keep the unreachable dialog closed and prove AX ignores it.
                assert_not_exposed(session, "#cluster-shape-dialog")
            finally:
                context.close()
                browser.close()

    print("AX por nodo: controles cargados, diálogo abierto y diálogo inalcanzable verificados.")


if __name__ == "__main__":
    main()
