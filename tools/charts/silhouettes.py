"""Rebuilds tools/charts/photo/silhouettes.json from the rendered chart PNGs (needs Pillow and numpy).

    python3 tools/charts/silhouettes.py

For each chart, per row in chart units, the runs [x0, x1] where the picture is opaque. photo.py uses them to place points
relative to the edges of the body. The PNGs are rendered at a whole number of pixels per chart unit (3 by default).
"""
import json
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CHARTS = os.path.join(ROOT, "src", "Clinic.Web", "wwwroot", "img", "charts")
OUT = os.path.join(os.path.dirname(__file__), "photo", "silhouettes.json")
FRAMES = os.path.join(ROOT, "tools", "blender", "charts.json")


def runs(row, k):
    cols = np.flatnonzero(row)
    out = []
    if len(cols):
        start = prev = cols[0]
        for c in cols[1:]:
            if c - prev > k:  # a gap wider than one chart unit starts a new run
                out.append([round(float(start) / k, 2), round(float(prev + 1) / k, 2)])
                start = c
            prev = c
        out.append([round(float(start) / k, 2), round(float(prev + 1) / k, 2)])
    return out


def main():
    with open(FRAMES, encoding="utf-8") as f:
        charts = json.load(f)["charts"]
    result = {}
    for key, spec in charts.items():
        path = os.path.join(CHARTS, f"{key}.png")
        if not os.path.exists(path):
            continue
        w, h = spec["size"]
        alpha = np.asarray(Image.open(path).convert("RGBA"))[..., 3] > 127
        k = alpha.shape[1] // w
        assert alpha.shape == (h * k, w * k), f"{key}: {alpha.shape} is not {w}x{h} times a whole scale"
        rows = [runs(alpha[r * k:(r + 1) * k].any(axis=0), k) for r in range(h)]
        result[key] = {"width": w, "height": h, "rows": rows}
        print(key, f"scale {k}")
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(result, f, separators=(",", ":"))


if __name__ == "__main__":
    main()
