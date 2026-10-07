"""Small helpers for drawing the body charts as SVG."""


def smooth(points, closed=True, tension=0.5):
    """Catmull-Rom spline through the points, as an SVG path."""
    pts = list(points)
    n = len(pts)
    if n < 2:
        return ""

    def at(i):
        if closed:
            return pts[i % n]
        return pts[max(0, min(n - 1, i))]

    d = [f"M{fmt(pts[0][0])} {fmt(pts[0][1])}"]
    last = n if closed else n - 1
    for i in range(last):
        p0, p1, p2, p3 = at(i - 1), at(i), at(i + 1), at(i + 2)
        c1 = (p1[0] + (p2[0] - p0[0]) * tension / 3, p1[1] + (p2[1] - p0[1]) * tension / 3)
        c2 = (p2[0] - (p3[0] - p1[0]) * tension / 3, p2[1] - (p3[1] - p1[1]) * tension / 3)
        d.append(f"C{fmt(c1[0])} {fmt(c1[1])} {fmt(c2[0])} {fmt(c2[1])} {fmt(p2[0])} {fmt(p2[1])}")
    if closed:
        d.append("Z")
    return " ".join(d)


def mirror_half(half, cx):
    """A symmetric outline from its right half (top centre to bottom centre)."""
    left = [(2 * cx - x, y) for (x, y) in reversed(half[1:-1])]
    return half + left


def mirror(points, cx):
    return [(2 * cx - x, y) for (x, y) in points]


def lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def fmt(v):
    s = f"{v:.1f}"
    return s[:-2] if s.endswith(".0") else s
