#!/usr/bin/env python3
"""Build deterministic derived Light map assets without modifying locked canonicals."""

from __future__ import annotations

import argparse
import base64
import copy
import hashlib
import json
import math
import re
import sys
from pathlib import Path
from typing import Any, TypeAlias
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
OUTPUT_DIR = ROOT / "Resources" / "map-themes" / "light"
MANIFEST_PATH = OUTPUT_DIR / "manifest.json"
SVG_NS = "http://www.w3.org/2000/svg"
XLINK_NS = "http://www.w3.org/1999/xlink"
ET.register_namespace("", SVG_NS)
ET.register_namespace("xlink", XLINK_NS)
ET.register_namespace("inkscape", "http://www.inkscape.org/namespaces/inkscape")
ET.register_namespace("sodipodi", "http://sodipodi.sourceforge.net/DTD/sodipodi-0.dtd")
CANVAS_ID = "presentation-light-canvas"
CANVAS_COLOR = "#fafafa"
TECHNICAL_STROKE = "#374151"
TECHNICAL_FILL = "#4b5563"

Spec: TypeAlias = dict[str, Any]

MAPS: tuple[Spec, ...] = (
    {
        "id": "norte",
        "kind": "vector-remapped",
        "source": "Resources/plano_norte_limpio.svg",
        "sourceSha256": "c8740b38110a552247f659c56b3f1f004aa56a2f5e5098ba9ced1ad6a917e55b",
        "sourceBounds": [0, 0, 1588, 1122.6667],
        "canonical": "Resources/plano_norte_limpio.svg",
        "canonicalSha256": "c8740b38110a552247f659c56b3f1f004aa56a2f5e5098ba9ced1ad6a917e55b",
        "dark": "plano_norte_limpio.svg",
        "light": "map-themes/light/plano_norte_limpio.svg",
        "viewBox": [0, 0, 1588, 1122.6667],
        "scaleX": 1,
        "scaleY": 1,
        "offsetX": 0,
        "offsetY": 0,
        "rotation": 0,
        "palette": {
            "#507005": "#374151", "#bababa": "#4b5563", "#ffffff": "#fafafa",
            "#808080": "#374151", "#000000": "#111827", "#545454": "#374151",
            "#989898": "#4b5563", "#c1e1c9": "#4b5563", "#767676": "#4b5563"
        },
        "derivation": "operational-vector-fallback-v1",
    },
    {
        "id": "nivel3",
        "kind": "vector-remapped",
        "source": "Resources/plano_nivel3_limpio.svg",
        "sourceSha256": "764a19c44ca9877b60fed58001bf27b0b0b12f725f52f269d5a956e2ae61a0af",
        "sourceBounds": [0, 0, 1588, 1122.6667],
        "canonical": "Resources/plano_nivel3_limpio.svg",
        "canonicalSha256": "764a19c44ca9877b60fed58001bf27b0b0b12f725f52f269d5a956e2ae61a0af",
        "dark": "plano_nivel3_limpio.svg",
        "light": "map-themes/light/plano_nivel3_limpio.svg",
        "viewBox": [0, 0, 1588, 1122.6667],
        "scaleX": 1,
        "scaleY": 1,
        "offsetX": 0,
        "offsetY": 0,
        "rotation": 0,
        "palette": {
            "#989898": "#374151", "#808080": "#4b5563", "#000000": "#111827",
            "#98fb98": "#4b5563", "#ffffff": "#fafafa"
        },
        "derivation": "operational-vector-fallback-v1",
    },
    {
        "id": "sur",
        "kind": "vector-cleaned-remapped",
        "source": "../Plano_Open_Space_Sur_Modular_CORREGIDO.svg",
        "sourceSha256": "177082fe3f4111fb01c295dc60ebde9a9f03f4b451ca62e3b76fcfe8e07bf26d",
        "sourceBounds": [0, 0, 842, 595],
        "canonical": "Resources/plano_sur_limpio.svg",
        "canonicalSha256": "297eaf02cae0854e24acb9a5bdb8569bf85783323a0c4dd3b7b654e1212a4ae8",
        "dark": "plano_sur_limpio.svg",
        "light": "map-themes/light/plano_sur_limpio.svg",
        "viewBox": [0, 0, 1122.6667, 793.33331],
        "cropTop": 0,
        "cropBottom": 0,
        "cropLeft": 0,
        "cropRight": 0,
        "scaleX": 1.3333333729216152,
        "scaleY": 1.333333294117647,
        "offsetX": 0,
        "offsetY": 0,
        "rotation": 0,
        "derivation": "sur-architectural-direct-paths-v2",
    },
    {
        "id": "id",
        "kind": "vector-remapped",
        "source": "Resources/plano_id.svg",
        "sourceSha256": "18a5430082eff76c6767b1fed1b3cb9054a43a8f76aa9ab54347281e8fa2bfb8",
        "sourceBounds": [0, 0, 1122.5601, 1587.36],
        "canonical": "Resources/plano_id.svg",
        "canonicalSha256": "18a5430082eff76c6767b1fed1b3cb9054a43a8f76aa9ab54347281e8fa2bfb8",
        "dark": "plano_id.svg",
        "light": "map-themes/light/plano_id.svg",
        "viewBox": [0, 0, 1122.5601, 1587.36],
        "cropTop": 0,
        "cropBottom": 0,
        "cropLeft": 0,
        "cropRight": 0,
        "scaleX": 1,
        "scaleY": 1,
        "offsetX": 0,
        "offsetY": 0,
        "rotation": 0,
        "palette": {
            "#808080": "#374151",
            "#989898": "#4b5563",
            "#636466": "#334155",
            "#000000": "#111827",
            "#ffffff": "#475569",
            "#d1d1d1": "#475569",
            "#f8d731": "#854d0e",
        },
    },
    {
        "id": "qc",
        "kind": "vector-remapped",
        "source": "Resources/plano_qc_limpio.svg",
        "sourceSha256": "1e065fa03004e78fecd293dbafcc0f1d7dceeeaa5d17ca35f19da806e62ea9d7",
        "sourceBounds": [0, 0, 793.33331, 1122.6667],
        "canonical": "Resources/plano_qc_limpio.svg",
        "canonicalSha256": "1e065fa03004e78fecd293dbafcc0f1d7dceeeaa5d17ca35f19da806e62ea9d7",
        "dark": "plano_qc_limpio.svg",
        "light": "map-themes/light/plano_qc_limpio.svg",
        "viewBox": [0, 0, 793.33331, 1122.6667],
        "cropTop": 0,
        "cropBottom": 0,
        "cropLeft": 0,
        "cropRight": 0,
        "scaleX": 1,
        "scaleY": 1,
        "offsetX": 0,
        "offsetY": 0,
        "rotation": 0,
        "palette": {
            "#989898": "#1f2937",
            "#505050": "#111827",
            "#eeeeee": "#4b5563",
        },
    },
)

SUR_ARCHITECTURAL_STROKES = frozenset(
    {
        "rgb(50.19989%, 50.19989%, 50.19989%)",
        "rgb(58.399963%, 58.399963%, 28.999329%)",
        "rgb(0%, 0%, 0%)",
        "rgb(28.999329%, 58.399963%, 58.399963%)",
        "rgb(59.599304%, 59.599304%, 59.599304%)",
        "rgb(86.299133%, 86.299133%, 86.299133%)",
        "rgb(32.899475%, 32.899475%, 32.899475%)",
        "rgb(72.898865%, 72.898865%, 72.898865%)",
    }
)
ACTIVE_TAGS = {"script", "foreignObject"}
EXTERNAL_SCHEMES = ("http:", "https:", "file:", "javascript:")
HEX_COLOR = re.compile(r"#[0-9a-fA-F]{6}\b")
CSS_STROKE_WIDTH = re.compile(r"(stroke-width\s*:\s*)([0-9]*\.?[0-9]+)", re.IGNORECASE)
MIN_VECTOR_STROKE_WIDTH = 1.5


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def number(value: float) -> str:
    return f"{value:.12f}".rstrip("0").rstrip(".")


def view_box(bounds: list[float]) -> str:
    return " ".join(number(float(value)) for value in bounds)


def output_path(spec: Spec) -> Path:
    prefix = "map-themes/light/"
    light = str(spec["light"])
    if not light.startswith(prefix):
        raise ValueError(f'{spec["id"]}: Light path must start with {prefix}')
    return ROOT / "Resources" / light


def transform(spec: Spec) -> list[float]:
    angle = math.radians(float(spec["rotation"]))
    cosine = math.cos(angle)
    sine = math.sin(angle)
    scale_x = float(spec["scaleX"])
    scale_y = float(spec["scaleY"])
    values = [
        scale_x * cosine,
        scale_x * sine,
        -scale_y * sine,
        scale_y * cosine,
        float(spec["offsetX"]),
        float(spec["offsetY"]),
    ]
    return [0.0 if abs(value) < 1e-15 else value for value in values]


def matrix(values: list[float]) -> str:
    return "matrix(" + " ".join(number(value) for value in values) + ")"


def canvas() -> ET.Element:
    return ET.Element(
        f"{{{SVG_NS}}}rect",
        {"id": CANVAS_ID, "x": "0", "y": "0", "width": "100%", "height": "100%", "fill": CANVAS_COLOR},
    )


def serialize(root: ET.Element) -> bytes:
    ET.indent(root, space="  ")
    return ET.tostring(root, encoding="utf-8", xml_declaration=True, short_empty_elements=True)


def local_name(node: ET.Element) -> str:
    return node.tag.rsplit("}", 1)[-1] if isinstance(node.tag, str) else ""


def sanitize_tree(root: ET.Element) -> None:
    for parent in list(root.iter()):
        for child in list(parent):
            if local_name(child) in ACTIVE_TAGS:
                parent.remove(child)
        for name in list(parent.attrib):
            simple_name = name.rsplit("}", 1)[-1].lower()
            value = parent.attrib[name].strip().lower()
            if simple_name.startswith("on"):
                del parent.attrib[name]
            elif simple_name == "href" and value.startswith(EXTERNAL_SCHEMES):
                del parent.attrib[name]


def build_raster(spec: Spec, source: bytes) -> bytes:
    """Render a raster source within the operational viewBox using uniform scale.

    The source image may contain margins. The spec must declare cropTop/cropLeft
    and cropWidth/cropHeight that bound the architectural content. The output
    uses an inner ``<svg>`` element whose viewBox isolates the crop, so the
    raster is not stretched non-uniformly."""
    target = spec["viewBox"]
    root = ET.Element(
        f"{{{SVG_NS}}}svg",
        {
            "viewBox": view_box(target),
            "width": number(float(target[2])),
            "height": number(float(target[3])),
        },
    )
    root.append(canvas())

    crop_left = float(spec.get("cropLeft", 0))
    crop_top = float(spec.get("cropTop", 0))
    crop_width = float(spec.get("cropWidth", spec["sourceBounds"][2]))
    crop_height = float(spec.get("cropHeight", spec["sourceBounds"][3]))
    content_ar = crop_width / max(1, crop_height)
    target_ar = float(target[2]) / max(1, float(target[3]))

    if content_ar > target_ar:
        fitted_width = float(target[2])
        fitted_height = float(target[2]) / content_ar
    else:
        fitted_height = float(target[3])
        fitted_width = float(target[3]) * content_ar

    offset_x = (float(target[2]) - fitted_width) / 2.0
    offset_y = (float(target[3]) - fitted_height) / 2.0

    inner = ET.SubElement(
        root,
        f"{{{SVG_NS}}}svg",
        {
            "x": number(offset_x),
            "y": number(offset_y),
            "width": number(fitted_width),
            "height": number(fitted_height),
            "viewBox": f"{number(crop_left)} {number(crop_top)} {number(crop_width)} {number(crop_height)}",
        },
    )
    inner.set("preserveAspectRatio", "none")
    image = ET.SubElement(
        inner,
        f"{{{SVG_NS}}}image",
        {
            "id": "canonical-map",
            "width": number(float(spec["sourceBounds"][2])),
            "height": number(float(spec["sourceBounds"][3])),
            "href": "data:image/png;base64," + base64.b64encode(source).decode("ascii"),
        },
    )
    image.set("data-quality", "locked-raster")
    return serialize(root)


def remap_text(value: str | None, palette: dict[str, str]) -> str | None:
    if value is None:
        return None
    normalized = {key.lower(): replacement for key, replacement in palette.items()}
    return HEX_COLOR.sub(lambda match: normalized.get(match.group(0).lower(), match.group(0)), value)


def strengthen_css_strokes(value: str | None) -> str | None:
    if value is None:
        return None
    return CSS_STROKE_WIDTH.sub(
        lambda match: match.group(1) + number(max(MIN_VECTOR_STROKE_WIDTH, float(match.group(2)))),
        value,
    )


def build_vector_remapped(spec: Spec, source: bytes) -> bytes:
    root = ET.fromstring(source)
    sanitize_tree(root)
    palette = spec["palette"]
    for node in root.iter():
        for name, value in list(node.attrib.items()):
            remapped = remap_text(value, palette) or ""
            node.attrib[name] = strengthen_css_strokes(remapped) if name == "style" else remapped
        if "stroke-width" in node.attrib:
            try:
                node.attrib["stroke-width"] = number(max(MIN_VECTOR_STROKE_WIDTH, float(node.attrib["stroke-width"])))
            except ValueError:
                pass
        if local_name(node) == "style" and node.text:
            node.text = strengthen_css_strokes(remap_text(node.text, palette))
    root.attrib["data-light-derivation"] = "vector-palette-and-stroke-remap-v2"
    root.insert(0, canvas())
    return serialize(root)


def keep_sur_path(node: ET.Element) -> bool:
    if local_name(node) != "path":
        return False
    stroke = node.attrib.get("stroke")
    fill = node.attrib.get("fill", "none")
    try:
        stroke_width = float(node.attrib.get("stroke-width", "0"))
    except ValueError:
        return False
    return stroke in SUR_ARCHITECTURAL_STROKES and stroke_width >= 1.9 and fill in {"none", stroke}


def build_sur_cleaned(spec: Spec, source: bytes) -> bytes:
    source_root = ET.fromstring(source)
    original_layer = next((node for node in source_root if node.attrib.get("id") == "capa-plano-original"), None)
    if original_layer is None:
        raise ValueError("sur: locked source has no capa-plano-original")

    retained = [copy.deepcopy(node) for node in original_layer if keep_sur_path(node)]
    if len(retained) != 17518:
        raise ValueError(f"sur: architectural selection drifted; expected 17518 paths, got {len(retained)}")

    target = spec["viewBox"]
    root = ET.Element(
        f"{{{SVG_NS}}}svg",
        {
            "viewBox": view_box(target),
            "width": number(float(target[2])),
            "height": number(float(target[3])),
        },
    )
    root.append(canvas())
    architectural = ET.SubElement(
        root,
        f"{{{SVG_NS}}}g",
        {"id": "canonical-map", "transform": matrix(transform(spec)), "data-derivation": spec["derivation"]},
    )
    for node in retained:
        if node.attrib.get("fill", "none") != "none":
            node.attrib["fill"] = TECHNICAL_FILL
        node.attrib["stroke"] = TECHNICAL_STROKE
        node.attrib["stroke-width"] = number(max(8.0, float(node.attrib["stroke-width"])))
        architectural.append(node)
    return serialize(root)


def build_asset(spec: Spec, source: bytes) -> bytes:
    if spec["kind"] == "raster":
        return build_raster(spec, source)
    if spec["kind"] == "vector-cleaned-remapped":
        return build_sur_cleaned(spec, source)
    if spec["kind"] == "vector-remapped":
        return build_vector_remapped(spec, source)
    raise ValueError(f'{spec["id"]}: unsupported kind {spec["kind"]}')


def visible_nodes(root: ET.Element) -> list[ET.Element]:
    visible_tags = {"path", "image", "rect", "line", "polyline", "polygon", "circle", "ellipse", "text"}
    return [node for node in root.iter() if local_name(node) in visible_tags]


def validate_svg(spec: Spec, output: bytes) -> None:
    root = ET.fromstring(output)
    if root.attrib.get("viewBox") != view_box(spec["viewBox"]):
        raise ValueError(f'{spec["light"]}: unexpected viewBox')
    direct_visible = [node for node in list(root) if local_name(node) in {"path", "image", "rect", "line", "polyline", "polygon", "circle", "ellipse", "text"}]
    if not direct_visible or direct_visible[0].attrib.get("id") != CANVAS_ID:
        raise ValueError(f'{spec["light"]}: Light canvas is not the first direct visible element')
    if len(visible_nodes(root)) < 2:
        raise ValueError(f'{spec["light"]}: no visible map content')
    expected_transform = matrix(transform(spec))
    if spec["kind"] == "vector-cleaned-remapped" and not any(
        node.attrib.get("transform") == expected_transform for node in root.iter()
    ):
        raise ValueError(f'{spec["light"]}: normalization transform is missing')
    if spec["kind"] == "raster":
        inner_svg = next((node for node in root.iter() if local_name(node) == "svg"), None)
        if inner_svg is None or "viewBox" not in inner_svg.attrib:
            raise ValueError(f'{spec["light"]}: raster missing inner svg with crop viewBox')
    for node in root.iter():
        if local_name(node) in ACTIVE_TAGS:
            raise ValueError(f'{spec["light"]}: active SVG content remains')
        for name, value in node.attrib.items():
            simple_name = name.rsplit("}", 1)[-1].lower()
            if simple_name.startswith("on"):
                raise ValueError(f'{spec["light"]}: event attribute remains')
            if simple_name == "href" and value.strip().lower().startswith(EXTERNAL_SCHEMES):
                raise ValueError(f'{spec["light"]}: external reference remains')


def quality_metadata(spec: Spec) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for percent in (100, 150, 200):
        if spec["kind"] == "raster":
            source = spec["sourceBounds"]
            target = spec["viewBox"]
            ratio = min(float(source[2]) / float(target[2]), float(source[3]) / float(target[3])) / (percent / 100)
            result[str(percent)] = {
                "mode": "raster",
                "minSourcePixelsPerDisplayPixel": ratio,
                "status": "pass" if ratio >= 1 else "degraded",
            }
        else:
            result[str(percent)] = {"mode": "vector", "status": "pass"}
    return result


def manifest_entry(spec: Spec, output: bytes) -> Spec:
    public_keys = (
        "id", "kind", "source", "sourceSha256", "sourceBounds",
        "canonical", "canonicalSha256", "dark", "light", "viewBox",
        "cropTop", "cropLeft", "cropWidth", "cropHeight",
        "scaleX", "scaleY", "offsetX", "offsetY", "rotation",
        "uniformStrategy",
    )
    item = {key: spec[key] for key in public_keys if key in spec}
    if spec.get("uniformStrategy") or spec["kind"] == "vector-cleaned-remapped":
        item["transform"] = transform(spec)
    else:
        item["transform"] = transform(spec) if spec["kind"] != "raster" else [1, 0, 0, 1, 0, 0]
    item["darkSha256"] = spec["canonicalSha256"]
    item["transform"] = transform(spec)
    item["quality"] = quality_metadata(spec)
    if "derivation" in spec:
        item["derivation"] = spec["derivation"]
    item["lightSha256"] = sha256(output)
    return item


def validate_locked_inputs(spec: Spec, source: bytes) -> None:
    actual_source_hash = sha256(source)
    if actual_source_hash != spec["sourceSha256"]:
        raise ValueError(f'{spec["source"]}: locked SHA-256 mismatch: {actual_source_hash}')
    canonical_path = ROOT / spec["canonical"]
    canonical_hash = sha256(canonical_path.read_bytes())
    if canonical_hash != spec["canonicalSha256"]:
        raise ValueError(f'{spec["canonical"]}: locked canonical SHA-256 mismatch: {canonical_hash}')


def generated_manifest(assets: list[tuple[Spec, bytes]]) -> bytes:
    document = {
        "schemaVersion": 2,
        "generator": "tools/build-light-maps.py",
        "canvas": CANVAS_COLOR,
        "assets": [manifest_entry(spec, output) for spec, output in assets],
    }
    return (json.dumps(document, indent=2, ensure_ascii=False) + "\n").encode("utf-8")


def run(check: bool) -> int:
    built: list[tuple[Spec, bytes]] = []
    for spec in MAPS:
        source = (ROOT / spec["source"]).read_bytes()
        validate_locked_inputs(spec, source)
        output = build_asset(spec, source)
        validate_svg(spec, output)
        built.append((spec, output))

    manifest = generated_manifest(built)
    expected = [(output_path(spec), output) for spec, output in built] + [(MANIFEST_PATH, manifest)]
    stale = [str(path.relative_to(ROOT)) for path, data in expected if not path.exists() or path.read_bytes() != data]
    if check:
        if stale:
            print("Stale generated Light assets: " + ", ".join(stale), file=sys.stderr)
            return 1
        print("Light map assets are reproducible and valid.")
        return 0

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for path, data in expected:
        path.write_bytes(data)
        print(f"Wrote {path.relative_to(ROOT)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="validate that generated files are current")
    args = parser.parse_args()
    try:
        return run(args.check)
    except (OSError, UnicodeError, ValueError, ET.ParseError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
