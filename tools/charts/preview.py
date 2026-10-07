"""Draws every chart with its library points on one HTML page, for checking positions by eye.

    python3 tools/charts/preview.py sheet.html
"""
import sys

from figures import VIEWS as FIGURES
from points import POINTS, VIEWS

CENTRE = {"front": 100, "back": 100, "head-front": 120}
SYMMETRIC = {v[0]: v[2] for v in VIEWS}


def marks(view):
    out = []
    for code, _, _, views in POINTS:
        if view not in views:
            continue
        x, y = views[view]
        xs = [x] if not SYMMETRIC[view] else [CENTRE[view]] if x == 0 else [CENTRE[view] - x, CENTRE[view] + x]
        for xx in xs:
            out.append(f'<circle cx="{xx}" cy="{y}" r="2.4" fill="#c0392b" fill-opacity=".85"/>')
        out.append(f'<text x="{xs[-1] + 3}" y="{y + 1.5}" font-size="4.5" fill="#222">{code}</text>')
    return "".join(out)


def main(path):
    cells = []
    for key, (w, h, draw) in FIGURES.items():
        cells.append(f'<div><b>{key}</b><br><svg viewBox="0 0 {w} {h}" width="{w * 2}" height="{h * 2}" style="background:#fff">'
                     f"{draw()}{marks(key)}</svg></div>")
    with open(path, "w", encoding="utf-8") as f:
        f.write('<html><body style="display:flex;flex-wrap:wrap;gap:12px;font-family:sans-serif">' + "".join(cells) + "</body></html>")


if __name__ == "__main__":
    main(sys.argv[1])
