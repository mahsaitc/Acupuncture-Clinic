"""Draws every chart with its library points on one HTML page, for checking positions by eye.

    python3 tools/charts/preview.py sheet.html

Uses the rendered photo charts (wwwroot/img/charts/*.png) when they are in use, the drawings otherwise.
Hover a marker for its code; positions are the ones generate.py writes into the app.
"""
import base64
import os
import sys

import generate
from figures import VIEWS as FIGURES
from points import VIEWS

CENTRE = {"front": 100, "back": 100, "head-front": 120}
SYMMETRIC = {v[0]: v[2] for v in VIEWS}


def marks(view):
    out = []
    for code, _, _, views in generate.POINTS:
        if view not in views:
            continue
        x, y = views[view]
        xs = [x] if not SYMMETRIC[view] else [CENTRE[view]] if x == 0 else [CENTRE[view] - x, CENTRE[view] + x]
        for xx in xs:
            out.append(f'<circle cx="{xx}" cy="{y}" r="2.4" fill="#c0392b" fill-opacity=".85"><title>{code}</title></circle>')
        out.append(f'<text x="{xs[-1] + 3}" y="{y + 1.5}" font-size="4.5" fill="#222">{code}</text>')
    return "".join(out)


def background(key, w, h, draw):
    if key in generate.PHOTO_VIEWS:
        with open(os.path.join(generate.CHARTS, f"{key}.png"), "rb") as f:
            data = base64.b64encode(f.read()).decode()
        return f'<image href="data:image/png;base64,{data}" width="{w}" height="{h}"/>'
    return draw()


def main(path):
    cells = []
    for key, (w, h, draw) in FIGURES.items():
        cells.append(f'<div><b>{key}</b><br><svg viewBox="0 0 {w} {h}" width="{w * 2.5}" height="{h * 2.5}" style="background:#f4f4ee">'
                     f"{background(key, w, h, draw)}{marks(key)}</svg></div>")
    with open(path, "w", encoding="utf-8") as f:
        f.write('<html><body style="display:flex;flex-wrap:wrap;gap:12px;font-family:sans-serif">' + "".join(cells) + "</body></html>")


if __name__ == "__main__":
    main(sys.argv[1])
