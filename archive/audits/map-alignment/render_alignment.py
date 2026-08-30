from __future__ import annotations

import json
import subprocess
from pathlib import Path
from urllib.parse import quote

import numpy as np
from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = Path(__file__).resolve().parent / "alignment-final"
OUT.mkdir(exist_ok=True)
CHROME = Path(r"C:/Program Files/Google/Chrome/Application/chrome.exe")
SIZE = (2400, 1696)  # 1588 : 1122.6667 logical contract
MAPS = {
    "norte": ("Resources/plano_norte_limpio.svg", "Resources/map-themes/light/plano_norte_limpio.svg"),
    "nivel3": ("Resources/plano_nivel3_limpio.svg", "Resources/map-themes/light/plano_nivel3_limpio.svg"),
    "sur": ("Resources/plano_sur_limpio.svg", "Resources/map-themes/light/plano_sur_limpio.svg"),
}

def uri(path: Path) -> str:
    return "file:///" + quote(path.resolve().as_posix())

def render(name: str, svg: Path) -> Path:
    html = OUT / f"{name}.html"
    png = OUT / f"{name}.png"
    html.write_text(
        "<!doctype html><meta charset=utf-8><style>html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#fafafa}img{display:block;width:100vw;height:100vh;object-fit:fill}</style>"
        f"<img src='{uri(svg)}'>", encoding="utf-8")
    command = [str(CHROME), "--headless=new", "--disable-gpu", "--hide-scrollbars", f"--user-data-dir={OUT / 'chrome-profile'}", f"--screenshot={png}", f"--window-size={SIZE[0]},{SIZE[1]}", uri(html)]
    result = subprocess.run(command, capture_output=True, text=True, timeout=60)
    if result.returncode != 0 or not png.exists():
        raise RuntimeError(f"render failed for {name}: {result.stderr}\n{result.stdout}")
    return png

def dark_mask(image: Image.Image) -> np.ndarray:
    rgb = np.asarray(image.convert("RGB"), dtype=np.int16)
    # Architecture ink for either the dark presentation or technical Light presentation.
    lum = rgb.mean(axis=2)
    chroma = rgb.max(axis=2) - rgb.min(axis=2)
    return (lum < 145) | ((lum < 185) & (chroma > 38))

def edges(mask: np.ndarray) -> np.ndarray:
    # Morphological boundary without extra dependencies.
    padded = np.pad(mask, 1)
    inside = mask.copy()
    for dy, dx in ((0,1),(0,-1),(1,0),(-1,0),(1,1),(1,-1),(-1,1),(-1,-1)):
        inside &= padded[1+dy:1+dy+mask.shape[0], 1+dx:1+dx+mask.shape[1]]
    return mask & ~inside

def bbox(mask: np.ndarray) -> tuple[int,int,int,int]:
    ys, xs = np.where(mask)
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())

def overlap(a: np.ndarray, b: np.ndarray) -> tuple[float, float]:
    # tolerance around Light edges: ±3 px; compares all real architectural segments.
    bpad = np.pad(b, 3)
    dilated = np.zeros_like(b)
    for dy in range(7):
        for dx in range(7):
            dilated |= bpad[dy:dy+b.shape[0], dx:dx+b.shape[1]]
    match = (a & dilated).sum() / max(1, a.sum())
    union = (a | b).sum()
    iou = (a & b).sum() / max(1, union)
    return float(match), float(iou)

def map_seats(map_id: str) -> list[tuple[float,float,str]]:
    data = json.loads((ROOT / "qa-runtime-data/data/maps.json").read_text(encoding="utf-8"))
    # map records are identified in order and by their common id/name fields.
    for record in data["maps"]:
        identity = " ".join(str(record.get(k, "")).lower() for k in ("id", "mapId", "name", "title"))
        if map_id in identity or (map_id == "nivel3" and ("nivel 3" in identity or "nivel3" in identity)):
            return [(float(s["x"]), float(s["y"]), str(s["id"])) for s in record["seats"]]
    # QA data is stable in Norte, Nivel 3, Sur order.
    order = {"norte": 0, "nivel3": 1, "sur": 2}[map_id]
    return [(float(s["x"]), float(s["y"]), str(s["id"])) for s in data["maps"][order]["seats"]]

def annotate(name: str, image: Image.Image, seats: list[tuple[float,float,str]]) -> Image.Image:
    out = image.convert("RGBA").copy()
    draw = ImageDraw.Draw(out)
    # distributed deterministic sample: all seats if <=20, otherwise 20 quantiles by source order.
    indices = list(range(len(seats))) if len(seats) <= 20 else [round(i * (len(seats)-1) / 19) for i in range(20)]
    for idx in indices:
        x, y, label = seats[idx]
        px, py = round(x * (SIZE[0]-1)), round(y * (SIZE[1]-1))
        draw.ellipse((px-8, py-8, px+8, py+8), outline=(235, 20, 20, 255), width=3)
        draw.text((px+10, py-9), label, fill=(235, 20, 20, 255), stroke_width=1, stroke_fill=(255,255,255,255))
    return out

summary = {}
for map_id, (dark_rel, light_rel) in MAPS.items():
    dark_png = render(f"{map_id}-dark", ROOT / dark_rel)
    light_png = render(f"{map_id}-light", ROOT / light_rel)
    dark = Image.open(dark_png).convert("RGBA")
    light = Image.open(light_png).convert("RGBA")
    # Red=Dark, cyan=Light: aligned edges appear pale/white; split colored traces expose residuals.
    dr, dg, db, _ = dark.split()
    lr, lg, lb, _ = light.split()
    overlay = Image.merge("RGBA", (dr, lg, lb, Image.new("L", SIZE, 255)))
    overlay.save(OUT / f"{map_id}-overlay.png")
    seats = map_seats(map_id)
    dark_ann = annotate(map_id, dark, seats)
    light_ann = annotate(map_id, light, seats)
    side = Image.new("RGBA", (SIZE[0]*2, SIZE[1]), (250,250,250,255))
    side.paste(dark_ann, (0,0)); side.paste(light_ann, (SIZE[0],0))
    side.save(OUT / f"{map_id}-seats-side-by-side.png")
    dm, lm = dark_mask(dark), dark_mask(light)
    de, le = edges(dm), edges(lm)
    match_dl, iou = overlap(de, le)
    match_ld, _ = overlap(le, de)
    summary[map_id] = {
        "renderPixels": SIZE,
        "darkInkBBox": bbox(dm), "lightInkBBox": bbox(lm),
        "darkEdgePixels": int(de.sum()), "lightEdgePixels": int(le.sum()),
        "darkToLightEdgeMatchWithin3px": round(match_dl, 5),
        "lightToDarkEdgeMatchWithin3px": round(match_ld, 5),
        "rawEdgeIoU": round(iou, 5),
        "workspaceSample": min(len(seats),20),
        "workspaceCoordinatesIdenticalByLogicalContract": True,
    }
(OUT / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
print(json.dumps(summary, indent=2))
