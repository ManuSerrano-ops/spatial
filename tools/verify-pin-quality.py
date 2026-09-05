"""Verify the quality halo, its legend, and interaction-layer separation."""

from __future__ import annotations

from copy import deepcopy
import json
import re
from typing import Any

from playwright.sync_api import sync_playwright

from frontend_harness import (
    launch_chromium,
    new_frontend_context,
    open_frontend_page,
    resource_server,
    runtime_initial_data,
)


RING_CASES = (
    ("selected", {"selected": "true"}, "--pin-selected"),
    ("multi-selected", {"multiSelected": "true"}, "--pin-warning"),
    ("search", {"searchHit": "true"}, "--pin-search"),
    ("problem-critical", {"problem": "critical"}, "--pin-critical"),
    ("problem-warning", {"problem": "warning"}, "--pin-warning"),
    ("problem-info", {"problem": "info"}, "--pin-info"),
    ("planner-source", {"planner": "source"}, "--pin-planner-source"),
    ("planner-destination", {"planner": "destination"}, "--pin-planner-destination"),
    ("planner-blocked", {"planner": "blocked"}, "--pin-planner-blocked"),
    ("problem-highlight", {"problemHighlight": "true"}, "transparent"),
)


def quality_initial_data() -> dict[str, Any]:
    """Derive a mixed-quality bridge response from the real read-only data."""
    data = deepcopy(runtime_initial_data())
    maps = data["maps"]["maps"]
    seat = maps[0]["seats"][0]
    seat["type"] = "occupied"
    for field in ("personId", "deviceId", "deviceName", "location", "roseta"):
        seat.pop(field, None)
    assignments = data["assignments"]["assignments"]
    data["assignments"]["assignments"] = [assignment for assignment in assignments if assignment.get("workstationId") != seat["id"]]
    data["assignments"]["assignments"].append({"workstationId": seat["id"], "status": "occupied"})
    return data


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


def inspect_quality(page: Any) -> dict[str, Any]:
    return page.evaluate(
        """() => {
          const resolve = (element, variable) => {
            const probe = document.createElement('span');
            probe.style.color = `var(${variable})`;
            element.append(probe);
            const value = getComputedStyle(probe).color;
            probe.remove();
            return value;
          };
          const pins = [...document.querySelectorAll('.pin')];
          const mismatches = pins.filter(pin => {
            const expected = pin.dataset.quality === 'complete' ? 'complete' : 'partial';
            return !pin.classList.contains(expected)
              || pin.classList.contains(expected === 'complete' ? 'partial' : 'complete');
          }).map(pin => ({ quality: pin.dataset.quality, classes: [...pin.classList] }));
          const counts = Object.fromEntries(['complete', 'incomplete'].map(quality => [quality, pins.filter(pin => pin.dataset.quality === quality).length]));
          const pin = pins[0];
          pin.style.transition = 'none';
          const root = document.documentElement;
          const sample = quality => {
            pin.classList.remove('complete', 'partial', 'problem-highlight');
            pin.classList.add(quality === 'complete' ? 'complete' : 'partial');
            pin.dataset.quality = quality;
            pin.dataset.selected = 'false';
            pin.dataset.multiSelected = 'false';
            pin.dataset.searchHit = 'false';
            pin.dataset.problem = 'none';
            pin.dataset.planner = 'none';
            return {
              quality,
              qualityColor: resolve(pin, '--pin-quality'),
              borderColor: getComputedStyle(pin).borderTopColor,
              mapCanvas: getComputedStyle(document.querySelector('#stage')).backgroundColor,
              boxShadow: getComputedStyle(pin).boxShadow,
              symbol: pin.querySelector('.quality-symbol')?.textContent,
              symbolDisplay: getComputedStyle(pin.querySelector('.quality-symbol')).display,
              legend: (() => {
                const marker = document.querySelector(`.legend-marker.quality-${quality === 'complete' ? 'complete' : 'partial'}`);
                return { qualityColor: resolve(marker, '--pin-quality'), boxShadow: getComputedStyle(marker).boxShadow };
              })()
            };
          };
          const qualities = { complete: sample('complete'), incomplete: sample('incomplete') };
          const interactions = Object.fromEntries(%s.map(([name, attributes, expected]) => {
            pin.classList.remove('complete', 'partial', 'problem-highlight');
            pin.classList.add('partial');
            pin.dataset.quality = 'incomplete';
            pin.dataset.selected = attributes.selected || 'false';
            pin.dataset.multiSelected = attributes.multiSelected || 'false';
            pin.dataset.searchHit = attributes.searchHit || 'false';
            pin.dataset.problem = attributes.problem || 'none';
            pin.dataset.planner = attributes.planner || 'none';
            if (attributes.problemHighlight) pin.classList.add('problem-highlight');
            return [name, {
              qualityColor: resolve(pin, '--pin-quality'),
              ringColor: expected === 'transparent' ? 'transparent' : resolve(pin, '--pin-ring'),
              expectedRingColor: expected === 'transparent' ? 'transparent' : resolve(root, expected),
              boxShadow: getComputedStyle(pin).boxShadow,
              outlineStyle: getComputedStyle(pin).outlineStyle
            }];
          }));
          const legendSymbol = document.querySelector('.legend-quality-symbol');
          return { mismatches, counts, qualities, interactions, legendSymbol: legendSymbol?.textContent, mapSymbol: pin.querySelector('.quality-symbol')?.textContent };
        }""" % json.dumps(RING_CASES)
    )


def assert_quality_semantics(result: dict[str, Any]) -> None:
    if result["mismatches"]:
        raise AssertionError(f"Las clases de calidad no reflejan data-quality: {result['mismatches']}")
    if not all(result["counts"].values()):
        raise AssertionError(f"La muestra real necesita pines completos e incompletos: {result['counts']}")
    if result["legendSymbol"] != "!" or result["mapSymbol"] != "!":
        raise AssertionError(f"El glifo de calidad difiere entre plano y leyenda: {result['mapSymbol']!r} / {result['legendSymbol']!r}")


def assert_quality_layers(result: dict[str, Any]) -> None:
    for quality, sample in result["qualities"].items():
        if sample["qualityColor"] != sample["legend"]["qualityColor"]:
            raise AssertionError(f"La leyenda {quality} no usa el halo del pin: {sample}")
        if sample["qualityColor"] not in sample["boxShadow"] or sample["qualityColor"] not in sample["legend"]["boxShadow"]:
            raise AssertionError(f"El halo {quality} no está presente tanto en pin como leyenda: {sample}")
        if quality == "incomplete" and sample["symbolDisplay"] == "none":
            raise AssertionError("El pin incompleto perdió el símbolo de forma '!'.")
        if quality == "complete" and sample["symbolDisplay"] != "none":
            raise AssertionError("El pin completo muestra indebidamente el símbolo de incompleto.")
        for surface_name, surface in (("borde", sample["borderColor"]), ("lienzo", sample["mapCanvas"])):
            ratio = contrast_ratio(sample["qualityColor"], surface)
            if ratio < 3:
                raise AssertionError(f"El halo {quality} no alcanza 3:1 contra {surface_name}: {ratio:.2f}:1")


def assert_interaction_layers(result: dict[str, Any]) -> dict[str, float]:
    ratios: dict[str, float] = {}
    partial_color = result["qualities"]["incomplete"]["qualityColor"]
    for name, sample in result["interactions"].items():
        if sample["qualityColor"] != partial_color or partial_color not in sample["boxShadow"]:
            raise AssertionError(f"La interacción {name} alteró o ocultó el halo de calidad: {sample}")
        if sample["ringColor"] != sample["expectedRingColor"]:
            raise AssertionError(f"La interacción {name} cambió el anillo contextual: {sample}")
        if sample["ringColor"] != "transparent":
            ratios[name] = contrast_ratio(partial_color, sample["ringColor"])
    return ratios


def main() -> None:
    with resource_server() as server:
        with sync_playwright() as playwright:
            browser = launch_chromium(playwright)
            context = new_frontend_context(browser, {"width": 1400, "height": 900})
            try:
                page = open_frontend_page(context, server.server_port, quality_initial_data(), theme="professional-light")
                page.locator(".pin").first.wait_for()
                result = inspect_quality(page)
                assert_quality_semantics(result)
                assert_quality_layers(result)
                ratios = assert_interaction_layers(result)
            finally:
                context.close()
                browser.close()

    print("Calidad de pines: clases, halo, leyenda y símbolo '!' verificados en el frontend real.")
    print("Contraste del halo incompleto frente a anillos contextuales (informativo; el estado contextual es prioritario):")
    for name, ratio in ratios.items():
        print(f"- {name}: {ratio:.2f}:1")


if __name__ == "__main__":
    main()
