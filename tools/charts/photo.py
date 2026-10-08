"""Moves the points of the schematic charts onto the photo charts rendered from Z-Anatomy.

The point library (points.py) was measured on the drawings. The photo charts are rendered by tools/blender with
the same chart size, so most of a chart keeps its place, but a real body differs from the drawing: the arms hang
about 8 cm higher, the trunk is wider, the head is longer. Each point is therefore moved by landmarks:

* vertically, a piecewise-linear map from a landmark on the drawing to the same landmark on the model
  (navel, knee, ankle, elbow crease, wrist crease, eyes, chin, ...), in metres taken from the Z-Anatomy region boxes;
* sideways, either a ratio around the midline (trunk, face), or the point's relative place between the left and
  right edge of the body at that height, measured on the old outline and on the rendered silhouette (limbs, side views).

The result is a first placement for review, not an atlas: open the preview page (preview.py) and correct what is off
with ADJUST below. Run by generate.py when tools/charts/photo/silhouettes.json exists.
"""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FRAMES_FILE = os.path.join(ROOT, "tools", "blender", "charts.json")
SILHOUETTES_FILE = os.path.join(os.path.dirname(__file__), "photo", "silhouettes.json")

# Charts drawn from the render. The ear chart stays a drawing.
VIEWS = ["front", "back", "side", "head-front", "head-side", "arm-inner", "arm-outer", "leg-inner", "leg-outer"]

# Per-point corrections after the automatic placement, in chart units: {(view, code): (dx_or_dx, dy)}.
# On symmetric charts (front, back, head-front) the first number is the distance from the midline.
ADJUST = {}

# Heights on the model, metres above the floor (Z-Anatomy region boxes).
Z = {
    "head_top": 1.709,  # the scalp, not the tips of the hair (1.743) "brow": 1.617, "eye": 1.5905, "nose_tip": 1.558, "mouth": 1.531, "chin": 1.486,
    "neck_base": 1.431, "notch": 1.403, "xiphoid": 1.222, "navel": 1.017, "pubis": 0.835,
    "shoulder": 1.42, "elbow": 1.125, "wrist": 0.8465, "knuckles": 0.769, "fingertip": 0.699,
    "hip": 0.92, "knee": 0.443, "ankle": 0.0765, "sole": 0.0,
}

# Trunk, legs and head on the front, back and side charts: (y on the drawing, landmark).
BODY_Y = [(12, "head_top"), (34, "brow"), (38.4, "eye"), (57.6, "mouth"), (71, "chin"), (89, "notch"), (144, "xiphoid"),
          (196, "navel"), (238, "pubis"), (344, "knee"), (441, "ankle"), (468, "sole")]
# The arm on the front and back charts hangs lower on the drawing than on the model.
BODY_ARM_Y = [(92, "shoulder"), (189, "elbow"), (262.5, "wrist"), (311, "fingertip")]
BODY_ARM_DX_SHIFT = 3.5
# The trunk on the drawing is narrower than the model: ratio of model to drawing half-width at a drawing height.
TRUNK_DX_RATIO = [(12, 1.05), (60, 1.05), (75, 1.3), (95, 1.2), (130, 1.19), (190, 1.44), (240, 1.25)]
LEGS_FROM_Y = 250  # below this the drawing has two separate legs

HEAD_FRONT_Y = [(18, "head_top"), (103, "brow"), (120, "eye"), (157, "nose_tip"), (193, "mouth"), (240, "chin"), (280, "neck_base")]
HEAD_SIDE_Y = [(18, "head_top"), (92, "brow"), (104, "eye"), (139, "nose_tip"), (166, "mouth"), (205, "chin"), (280, "neck_base")]
HEAD_FRONT_DX_RATIO = 0.86

ARM_Y = [(12, "shoulder"), (230, "elbow"), (400, "wrist"), (447, "knuckles"), (476.6, "fingertip")]
LEG_Y = [(10, "hip"), (238, "knee"), (422, "ankle"), (466, "sole")]

_frames = None
_silhouettes = None


def available():
    return os.path.exists(SILHOUETTES_FILE) and os.path.exists(FRAMES_FILE)


def _load():
    global _frames, _silhouettes
    if _frames is None:
        with open(FRAMES_FILE, encoding="utf-8") as fh:
            _frames = json.load(fh)["charts"]
        with open(SILHOUETTES_FILE, encoding="utf-8") as fh:
            _silhouettes = json.load(fh)
    return _frames, _silhouettes


def _new_y(view, z):
    """Chart y of a height on the model, for a chart whose frame is in charts.json (the views looking along x or y)."""
    frame = _load()[0][view]
    return frame["size"][1] / 2 - (z - frame["center"][2]) * frame["px_per_m"]


def _anchors(view, table):
    return [(old, _new_y(view, Z[name])) for old, name in table]


def _interp(table, v):
    """Piecewise linear through (x, y) pairs, extrapolating along the first and last segment."""
    if v <= table[0][0]:
        (x0, y0), (x1, y1) = table[0], table[1]
    elif v >= table[-1][0]:
        (x0, y0), (x1, y1) = table[-2], table[-1]
    else:
        for (x0, y0), (x1, y1) in zip(table, table[1:]):
            if x0 <= v <= x1:
                break
    return y0 + (v - x0) * (y1 - y0) / (x1 - x0)


def _scan(poly, y):
    """The runs [x0, x1] where the horizontal line at y is inside the polygon."""
    xs = []
    for (x1, y1), (x2, y2) in zip(poly, poly[1:] + poly[:1]):
        if y1 != y2 and min(y1, y2) <= y < max(y1, y2):
            xs.append(x1 + (y - y1) * (x2 - x1) / (y2 - y1))
    xs.sort()
    return [(xs[i], xs[i + 1]) for i in range(0, len(xs) - 1, 2)]


def _old_runs(poly, y):
    for d in range(0, 8):
        for yy in (y - d, y + d):
            runs = _scan(poly, yy)
            if runs:
                return runs
    return []


def _row_runs(row):
    """A row of the silhouette file: a list of [x0, x1] runs, or (older files) just [left, right]."""
    if not row:
        return []
    if isinstance(row[0], (int, float)):
        return [(row[0], row[1])]
    return [tuple(run) for run in row]


def _new_runs(view, y):
    rows = _load()[1][view]["rows"]
    r = min(max(int(y), 0), len(rows) - 1)
    for d in range(0, 16):
        for rr in (r - d, r + d):
            if 0 <= rr < len(rows) and rows[rr]:
                return _row_runs(rows[rr])
    return []


def _across(old_edges, new_edges, x):
    """x between the old left/right edges becomes the same relative place between the new edges."""
    (a0, b0), (a1, b1) = old_edges, new_edges
    if b0 - a0 < 2:
        return x - a0 + a1
    return a1 + (x - a0) * (b1 - a1) / (b0 - a0)


def _extent(runs):
    return runs[0][0], runs[-1][1]


def _single_run(view, old_poly, x, y_old, y_new):
    old, new = _old_runs(old_poly, y_old), _new_runs(view, y_new)
    if not old or not new:
        return x
    return _across(_extent(old), _extent(new), x)


def _first_half_run(view, y_new, mid):
    """First run on the right half of a symmetric chart, as distances from the midline."""
    for x0, x1 in _new_runs(view, y_new):
        if x1 > mid:
            return max(x0, mid) - mid, x1 - mid
    return None


def _safe_dx(old, new):
    """Keep a point off the midline when it was off it: the chart tells the sides apart by more than 2 units."""
    return 0.0 if old == 0 else max(new, 2.2)


def _front_back(view, dx, y, figures):
    in_arm = y >= 95 and dx >= 44
    if in_arm:
        z_table = [(old, Z[name]) for old, name in BODY_ARM_Y]
        y_new = _new_y(view, _interp(z_table, y))
        return dx + BODY_ARM_DX_SHIFT, y_new
    y_new = _interp(_anchors(view, BODY_Y), y)
    if y < LEGS_FROM_Y:
        return _safe_dx(dx, dx * _interp(TRUNK_DX_RATIO, y)), y_new
    old = _old_runs(figures.BODY_HALF, y)
    new = _first_half_run(view, y_new, _load()[0][view]["size"][0] / 2)
    if not old or not new:
        return dx, y_new
    return _safe_dx(dx, _across((old[0][0] - 100, old[0][1] - 100), new, dx)), y_new


def remap(view, x, y, code=None):
    """New chart position of a point drawn at (x, y) on the schematic chart `view`. Symmetric charts take and return
    the distance from the midline in x."""
    import figures

    if view == "front" or view == "back":
        nx, ny = _front_back(view, x, y, figures)
    elif view == "side":
        ny = _interp(_anchors(view, BODY_Y), y)
        nx = _single_run(view, figures.SIDE, x, y, ny)
    elif view == "head-front":
        ny = _interp(_anchors(view, HEAD_FRONT_Y), y)
        nx = _safe_dx(x, x * HEAD_FRONT_DX_RATIO)
    elif view == "head-side":
        ny = _interp(_anchors(view, HEAD_SIDE_Y), y)
        nx = _single_run(view, figures.HEAD_SIDE, x, y, ny)
    elif view in ("arm-inner", "arm-outer"):
        ny = _interp(_anchors(view, ARM_Y), y)
        poly = figures.ARM_OUTLINE if view == "arm-inner" else figures.mirror(figures.ARM_OUTLINE, 80)
        nx = _single_run(view, poly, x, y, ny)
    elif view in ("leg-inner", "leg-outer"):
        ny = _interp(_anchors(view, LEG_Y), y)
        poly = figures.LEG_OUTLINE if view == "leg-inner" else figures.mirror(figures.LEG_OUTLINE, 80)
        nx = _single_run(view, poly, x, y, ny)
    else:
        raise ValueError(f"{view} is not a photo chart")
    ax, ay = ADJUST.get((view, code), (0, 0))
    return round(nx + ax, 1), round(ny + ay, 1)
