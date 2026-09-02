"""Integrity, derivation, contrast, and packaging tests for generated Light maps."""

from __future__ import annotations

import hashlib
import json
import math
import re
import subprocess
import sys
import unittest
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "Resources" / "map-themes" / "light" / "manifest.json"
SVG = "{http://www.w3.org/2000/svg}"
VISIBLE_TAGS = {"path", "image", "rect", "line", "polyline", "polygon", "circle", "ellipse", "text"}
HEX_COLOR = re.compile(r"#[0-9a-fA-F]{6}\b")


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def local_name(node: ET.Element) -> str:
    return node.tag.rsplit("}", 1)[-1]


def number(value: float) -> str:
    return f"{value:.12f}".rstrip("0").rstrip(".")


def formatted_matrix(values: list[float]) -> str:
    return "matrix(" + " ".join(number(value) for value in values) + ")"


def relative_luminance(color: str) -> float:
    channels = [int(color[index:index + 2], 16) / 255 for index in (1, 3, 5)]
    linear = [value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4 for value in channels]
    return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2]


def contrast(first: str, second: str) -> float:
    light, dark = sorted((relative_luminance(first), relative_luminance(second)), reverse=True)
    return (light + 0.05) / (dark + 0.05)


def svg_colors(root: ET.Element) -> set[str]:
    values: list[str] = []
    for node in root.iter():
        values.extend(node.attrib.values())
        if local_name(node) == "style" and node.text:
            values.append(node.text)
    return {match.group(0).lower() for value in values for match in HEX_COLOR.finditer(value)}


class LightMapTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        cls.assets = {asset["id"]: asset for asset in cls.manifest["assets"]}

    def output_path(self, asset: dict) -> Path:
        return ROOT / "Resources" / asset["light"]

    def test_generator_check_is_clean(self) -> None:
        result = subprocess.run(
            [sys.executable, str(ROOT / "tools" / "build-light-maps.py"), "--check"],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_schema_v2_has_complete_transform_and_quality_metadata(self) -> None:
        self.assertEqual(2, self.manifest["schemaVersion"])
        required = {
            "source", "canonical", "dark", "light",
            "scaleX", "scaleY", "offsetX", "offsetY", "rotation", "transform", "quality",
        }
        raster_keys = {"cropTop", "cropLeft", "cropWidth", "cropHeight"}
        for asset in self.manifest["assets"]:
            with self.subTest(asset=asset["id"]):
                expected = set(required)
                if asset["kind"] == "raster":
                    expected |= raster_keys
                self.assertTrue(expected.issubset(asset), expected - set(asset))
                self.assertEqual({"100", "150", "200"}, set(asset["quality"]))
                expected = [asset["scaleX"], 0.0, 0.0, asset["scaleY"], asset["offsetX"], asset["offsetY"]]
                self.assertEqual(expected, asset["transform"])
        # Alignment validation proved that the historical BW files are a
        # different layout. Norte/Nivel 3 therefore use the operational vector
        # as an exact-geometry Light presentation fallback, not raster stretching.
        for map_id in ("norte", "nivel3"):
            asset = self.assets[map_id]
            self.assertEqual("vector-remapped", asset["kind"])
            self.assertEqual("operational-vector-fallback-v1", asset["derivation"])
            self.assertEqual(asset["canonical"], asset["source"])
            self.assertEqual(0, asset["rotation"])
            self.assertEqual(asset["scaleX"], asset["scaleY"])
            self.assertEqual("vector", asset["quality"]["100"]["mode"])

    def test_locked_sources_canonicals_and_outputs_are_unchanged(self) -> None:
        for asset in self.manifest["assets"]:
            with self.subTest(asset=asset["id"]):
                self.assertEqual(asset["sourceSha256"], digest(ROOT / asset["source"]))
                self.assertEqual(asset["canonicalSha256"], digest(ROOT / asset["canonical"]))
                self.assertEqual(asset["darkSha256"], digest(ROOT / asset["canonical"]))
                self.assertEqual(asset["lightSha256"], digest(self.output_path(asset)))

    def test_viewboxes_transforms_and_rendered_content(self) -> None:
        for asset in self.manifest["assets"]:
            with self.subTest(asset=asset["id"]):
                root = ET.parse(self.output_path(asset)).getroot()
                expected_bounds = " ".join(number(float(value)) for value in asset["viewBox"])
                self.assertEqual(expected_bounds, root.attrib["viewBox"])
                direct_visible = [node for node in list(root) if local_name(node) in VISIBLE_TAGS]
                self.assertTrue(direct_visible)
                self.assertEqual("presentation-light-canvas", direct_visible[0].attrib.get("id"))
                rendered = [node for node in root.iter() if local_name(node) in VISIBLE_TAGS and node.attrib.get("id") != "presentation-light-canvas"]
                self.assertTrue(rendered, "Only the canvas would render.")
                if asset["kind"] == "raster":
                    inner_svg = next((node for node in root.iter() if local_name(node) == "svg"), None)
                    self.assertIsNotNone(inner_svg, "Raster asset has no inner crop svg")
                    self.assertTrue("viewBox" in inner_svg.attrib,
                                    "Raster inner svg has no viewBox")
                elif asset["kind"] == "vector-cleaned-remapped":
                    expected_transform = formatted_matrix(asset["transform"])
                    self.assertTrue(any(node.attrib.get("transform") == expected_transform for node in root.iter()))

    def test_sur_is_deterministically_clean_and_architectural(self) -> None:
        asset = self.assets["sur"]
        root = ET.parse(self.output_path(asset)).getroot()
        counts = {name: sum(1 for node in root.iter() if local_name(node) == name) for name in ("path", "use", "image", "text", "style")}
        self.assertEqual(17518, counts["path"])
        self.assertEqual(0, counts["use"])
        self.assertEqual(0, counts["image"])
        self.assertEqual(0, counts["text"])
        self.assertEqual(0, counts["style"])
        self.assertEqual("sur-architectural-direct-paths-v2", asset["derivation"])
        self.assertEqual({"#fafafa", "#374151", "#4b5563"}, svg_colors(root))
        self.assertGreaterEqual(min(float(node.attrib["stroke-width"]) for node in root.iter() if local_name(node) == "path"), 8.0)
        serialized = self.output_path(asset).read_text(encoding="utf-8").lower()
        for forbidden in ("glyph-", "source-", "rgb(", "xlink:href", "distribución", "julio 2026"):
            self.assertNotIn(forbidden, serialized)

    def test_id_and_qc_are_real_high_contrast_xml_remaps(self) -> None:
        for map_id in ("id", "qc"):
            with self.subTest(asset=map_id):
                asset = self.assets[map_id]
                source = (ROOT / asset["source"]).read_text(encoding="utf-8").lower()
                output_path = self.output_path(asset)
                output = output_path.read_text(encoding="utf-8").lower()
                root = ET.parse(output_path).getroot()
                self.assertNotEqual(source, output)
                self.assertTrue({"#374151", "#1f2937", "#4b5563"}.intersection(svg_colors(root)))
                self.assertNotIn("#989898", output)
                if map_id == "id":
                    self.assertNotIn("#808080", output)
                colors = svg_colors(root)
                self.assertGreater(len(colors - {"#fafafa"}), 0)
                for color in colors - {"#fafafa"}:
                    self.assertGreaterEqual(contrast(color, "#fafafa"), 3.0, color)
                self.assertEqual("vector-palette-and-stroke-remap-v2", root.attrib.get("data-light-derivation"))
                style_widths = [float(value) for value in re.findall(r"stroke-width\s*:\s*([0-9]*\.?[0-9]+)", output)]
                attribute_widths = [float(node.attrib["stroke-width"]) for node in root.iter() if "stroke-width" in node.attrib]
                self.assertTrue(style_widths or attribute_widths)
                self.assertGreaterEqual(min(style_widths + attribute_widths), 1.5)
                source_root = ET.parse(ROOT / asset["source"]).getroot()
                self.assertEqual(sum(1 for node in source_root.iter() if local_name(node) == "path"), sum(1 for node in root.iter() if local_name(node) == "path"))

    def test_no_active_or_remote_content(self) -> None:
        forbidden_tags = {"script", "foreignObject"}
        for asset in self.manifest["assets"]:
            with self.subTest(asset=asset["id"]):
                root = ET.parse(self.output_path(asset)).getroot()
                for node in root.iter():
                    self.assertNotIn(local_name(node), forbidden_tags)
                    for name, value in node.attrib.items():
                        simple_name = name.rsplit("}", 1)[-1].lower()
                        self.assertFalse(simple_name.startswith("on"))
                        if simple_name == "href":
                            self.assertFalse(value.lower().startswith(("http:", "https:", "file:", "javascript:")))

    def test_extraction_contract_has_exact_logical_path_and_harness(self) -> None:
        project = (ROOT / "PlanoOpenSpaceIT.Windows.csproj").read_text(encoding="utf-8")
        self.assertIn("$(RootNamespace).Resources.map-themes/light/%(Filename)%(Extension)", project)
        self.assertTrue((ROOT / "tests" / "PlanoOpenSpaceIT.Desktop.Tests" / "EmbeddedResourceExtractorTests.cs").exists())
        extractor = (ROOT / "src" / "Desktop" / "Resources" / "EmbeddedResourceExtractor.cs").read_text(encoding="utf-8")
        self.assertIn("NormalizeRelativePath(resource.RelativePath)", extractor)


if __name__ == "__main__":
    unittest.main()
