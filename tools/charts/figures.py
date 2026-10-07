"""The schematic body charts. Each function returns the SVG content for one view."""
from geometry import smooth, mirror_half, mirror, fmt

OUT = "#7d8a60"
FILL = "#f8f1e3"
SHADE = "#ece2cc"
DET = "#ab9f80"
FAINT = "#cfc5a8"


def outline(d, width=1.6):
    return f'<path d="{d}" fill="{FILL}" stroke="{OUT}" stroke-width="{width}" stroke-linejoin="round"/>'


def shade(d):
    return f'<path d="{d}" fill="{SHADE}" stroke="none"/>'


def line(d, width=0.9, color=DET, dash=None):
    extra = f' stroke-dasharray="{dash}"' if dash else ""
    return f'<path d="{d}" fill="none" stroke="{color}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"{extra}/>'


def both(d_right, cx=100, **kw):
    """A detail line on the right of the midline and its mirror image."""
    return line(d_right, **kw) + line(flip_path(d_right, cx), **kw)


def flip_path(d, cx):
    out, num_index = [], 0
    for token in d.replace(",", " ").split():
        if token[0].isalpha():
            cmd = token[0]
            rest = token[1:]
            out.append(cmd)
            num_index = 0
            if rest:
                token = rest
            else:
                continue
        value = float(token)
        if num_index % 2 == 0:
            value = 2 * cx - value
        out.append(fmt(value))
        num_index += 1
    return " ".join(out)


def ellipse(cx, cy, rx, ry, fill=FILL, stroke=OUT, width=1.3, extra=""):
    return f'<ellipse cx="{fmt(cx)}" cy="{fmt(cy)}" rx="{fmt(rx)}" ry="{fmt(ry)}" fill="{fill}" stroke="{stroke}" stroke-width="{width}"{extra}/>'


def dot(cx, cy, r=1.3, color=DET):
    return f'<circle cx="{fmt(cx)}" cy="{fmt(cy)}" r="{fmt(r)}" fill="{color}"/>'


# ---------------------------------------------------------------- whole body, front and back

# Right half of the standing figure (viewer's right), from the top of the head to the crotch.
BODY_HALF = [
    (100, 12), (110, 13.5), (117.5, 19), (121, 29), (121.5, 40), (120, 51), (117, 60), (111.5, 67), (104, 71),
    (109, 73.5), (110.5, 80), (112.5, 85.5),
    (122, 88), (134, 91), (143, 95), (148.5, 101), (151.5, 110),
    (155, 125), (158, 146), (160.5, 168), (163, 188),
    (165.5, 206), (169, 228), (172, 248), (174, 262),
    (176.5, 268), (180, 275), (182.5, 282.5), (179, 285), (176.5, 291), (175.5, 300), (172.5, 309.5), (167.5, 313.5),
    (162, 311.5), (159, 303.5), (158, 292), (157.5, 280), (157.5, 270), (158, 263),
    (155.5, 248), (152, 228), (148, 207), (144.5, 190),
    (142, 171), (140, 151), (138.5, 136), (137, 129.5),
    (135, 138), (133, 152), (131, 167), (128.5, 181), (127.5, 191), (128.5, 201), (131.5, 213), (135.5, 225), (137.5, 237), (138, 250),
    (136.5, 266), (134.5, 286), (132.5, 308), (130.5, 327), (130, 339), (130.5, 351),
    (131.5, 366), (131.5, 382), (129.5, 400), (126.5, 418), (123.5, 433), (122.5, 441),
    (125, 449), (127, 457), (126.5, 464), (121, 468), (112, 468.5), (106.5, 466.5), (104.8, 459), (105.6, 450),
    (106.5, 441), (106, 428), (105.5, 410), (105.5, 392), (105.5, 374), (105.6, 360), (104.6, 348),
    (104, 334), (103.6, 316), (103, 296), (102, 276), (101, 258), (100.3, 250), (100, 248),
]


def body_outline():
    return outline(smooth(mirror_half(BODY_HALF, 100)))


def ears_front():
    return ellipse(122.5, 42, 3.6, 7.5) + ellipse(77.5, 42, 3.6, 7.5)


def front():
    s = [ears_front(), body_outline()]
    # Face.
    s.append(line("M82 28 C88 18 112 18 118 28", color=FAINT))  # hairline
    s.append(both("M103.5 33.6 C106 32.4 110 32.4 113.5 33.8", width=1))  # brows
    s.append(both("M103.8 38.4 C106 36.8 109.5 36.8 112 38.4 C109.5 39.8 106 39.8 103.8 38.4 Z", width=0.8))  # eyes
    s.append(line("M100 39 L99 48.5 C99.8 49.6 100.2 49.6 101 49", width=0.8))
    s.append(both("M101.6 50.6 C102.8 50.9 103.8 50.4 104.2 49.4", width=0.8))
    s.append(line("M95 58 C98 59.8 102 59.8 105 58", width=1))
    s.append(line("M96 57.6 C98 56.6 102 56.6 104 57.6", width=0.6, color=FAINT))
    # Neck and chest.
    s.append(both("M109.5 74 C107 79 104.5 84 102.4 87.5", width=0.8, color=FAINT))
    s.append(both("M101.5 88 C110 86.5 122 89 141 95", width=1))  # clavicles
    s.append(line("M100 89 L100 139", width=0.8, color=FAINT))  # sternum
    s.append(line("M98.6 139 L100 144 L101.4 139", width=0.8))  # xiphoid
    s.append(both("M100.5 116 C108 131 124 135 136.5 126", width=0.9))  # pectoral border
    s.append(both("M100.6 141 C108 150 118 163 127.8 177", width=0.8))  # costal arch
    s.append(dot(118, 128, 1.5) + dot(82, 128, 1.5))  # nipples
    s.append(both("M147 101 C151 112 154 128 155 141", width=0.7, color=FAINT))  # deltoid
    s.append(both("M141 140 C144 154 147 170 150 182", width=0.6, color=FAINT))  # biceps
    # Abdomen.
    s.append(line("M100 146 L100 232", width=0.7, color=FAINT, dash="2 2"))  # linea alba
    s.append(both("M109.5 147 C111.5 170 111.5 200 107.5 231", width=0.6, color=FAINT))  # rectus edge
    s.append(ellipse(100, 196, 1.7, 2.3, fill=SHADE, stroke=DET, width=0.8))  # navel
    s.append(both("M132.5 220 C124 231 114 239 103.5 244", width=0.9))  # inguinal fold
    s.append(line("M95 238 C98 240 102 240 105 238", width=0.6, color=FAINT))
    # Arm creases and hand.
    s.append(both("M145.2 188.5 C151 190 157 190 162.6 188.5", width=0.8))  # elbow crease
    s.append(both("M158.4 262.5 C163 263.8 169 263.8 173.8 262.5", width=0.8))  # wrist crease
    s.append(both("M158.6 266 C163 267 169 267 173.4 266", width=0.6, color=FAINT))
    s.append(both("M174 271 C169.5 276 166.5 284 166 294", width=0.7, color=FAINT))  # thenar crease
    s.append(both("M162.4 297 L162.6 310.5 M167 297.5 L168.2 312 M171.3 296.5 L172.8 307.5", width=0.7))  # fingers
    s.append(both("M175.5 283 C175 287 175.6 291 176.3 293", width=0.6))  # thumb
    # Legs.
    s.append(ellipse(117.3, 334, 5.4, 7, fill="none", stroke=DET, width=0.9))  # patellae
    s.append(ellipse(82.7, 334, 5.4, 7, fill="none", stroke=DET, width=0.9))
    s.append(both("M117.6 347 C118.3 375 117.5 405 115.6 434", width=0.6, color=FAINT))  # tibial crest
    s.append(both("M124.6 437 C123.2 440 123.4 443 124.6 445", width=0.8))  # lateral malleolus
    s.append(both("M106.6 434 C108 437 108 440 106.8 442.5", width=0.8))  # medial malleolus
    s.append(both("M108.2 462.5 L108.6 467 M112.6 463.5 L112.8 468 M116.6 463.5 L116.6 468 M120.4 462.5 L120.2 467.2", width=0.6))  # toes
    return "".join(s)


def back():
    s = [ears_front(), body_outline()]
    s.append(shade("M79.4 42 C78.8 18 121.2 18 120.6 42 C120 52 113 59 100 60 C87 59 80 52 79.4 42 Z"))  # hair
    s.append(line("M88 60 C95 62.5 105 62.5 112 60", width=0.7, color=FAINT))  # hairline at the nape
    s.append(line("M100 86 L100 214", width=0.8, color=FAINT))  # spine
    for y in [86] + [91 + i * 80 / 11 for i in range(12)] + [179, 187.5, 196, 204.5, 213]:
        s.append(line(f"M98.6 {fmt(y)} L101.4 {fmt(y)}", width=0.9, color=DET))
    s.append(dot(100, 86, 1.6))  # C7
    s.append(both("M114.5 104.5 L144 102.5", width=1))  # scapular spine
    s.append(both("M115 103 C116 118 118 136 124 151 C130 144 137 133 141.5 122 C143 114 143.8 108 144 102.5", width=0.8))  # scapula
    s.append(both("M137.5 129 C134 133 131 136 128 139", width=0.6, color=FAINT))  # posterior axillary fold
    s.append(both("M101 205 C113 202.5 126 205.5 135 214", width=0.9))  # iliac crest
    s.append(line("M92.5 214 L107.5 214 L100 243 Z", width=0.7, color=FAINT, dash="2 1.6"))  # sacrum
    s.append(line("M100 228 L100 251", width=0.9))  # gluteal cleft
    s.append(both("M101.2 262 C108 266 121 267 133.5 260", width=0.9))  # gluteal fold
    s.append(both("M106.5 341.5 C112 343 121 343 128.5 341", width=0.9))  # popliteal crease
    s.append(both("M107.6 352 C105.6 372 109 388 115.5 397 M128.6 352 C130.4 372 126 389 119.5 397", width=0.6, color=FAINT))  # calf
    s.append(both("M115 400 C115.6 414 115.8 426 116 436 M119.6 400 C119 414 118.6 426 118.6 436", width=0.6, color=FAINT))  # Achilles
    s.append(both("M111 458 C113 462 120 462 122.5 457", width=0.7))  # heel
    s.append(both("M151 181 C153 179 156 179 158 181.5", width=0.8))  # olecranon
    s.append(both("M158.4 262.5 C163 261.6 169 261.6 173.8 262.5", width=0.7))  # dorsal wrist crease
    s.append(both("M160.5 296 C161.5 294.5 163.5 294.5 164.5 296 M165.2 295 C166.3 293.4 168.4 293.4 169.4 295 M169.7 293.8 C170.8 292.3 172.8 292.3 173.6 293.8", width=0.6))  # knuckles
    s.append(both("M162.4 298 L162.6 310.5 M167 298.5 L168.2 312 M171.3 297.5 L172.8 307.5", width=0.7))  # fingers
    return "".join(s)


# ---------------------------------------------------------------- whole body, right side

SIDE = [
    (98, 12), (108, 13.5), (116, 19), (121, 28), (123, 37), (124, 42), (123.6, 45), (128, 51.5), (124.4, 55), (124.8, 58.2),
    (123.2, 61), (123.8, 64.2), (121, 69), (115, 71.4), (110, 74), (109.2, 80), (110.5, 86),
    (116, 94), (123.5, 107), (127, 121), (125.5, 133), (123, 146), (122, 160), (123, 176), (124.2, 192), (123, 208), (120, 221), (117, 231),
    (118.4, 240), (121, 256), (122, 276), (121, 298), (118.4, 321), (115.6, 336), (114.4, 349), (113, 364), (111.8, 384), (110, 404),
    (107.4, 424), (106.4, 436), (110, 444), (118, 451.5), (128, 458.5), (134, 462.5), (133, 467), (118, 468.2), (97, 468.2), (90.5, 464),
    (89.4, 456), (91, 446), (93, 434), (94, 420), (93, 404), (90.4, 388), (88.4, 372), (90.2, 356), (94, 346), (93.2, 336), (90.4, 320),
    (88.2, 300), (86.2, 280), (84.2, 262), (80.4, 250), (76.5, 240), (75.2, 226), (79, 212), (84, 200), (85, 186), (83.2, 170),
    (80.2, 150), (78.2, 130), (79.2, 112), (83, 98), (88, 88.5), (89, 78), (87, 70), (82, 62), (78, 50), (77, 38), (80, 26), (87, 17),
]


def side():
    s = [outline(smooth(SIDE))]
    s.append(shade("M78 46 C76 22 96 10 112 15 C102 20 96 30 94 36 C90 40 86 48 86 58 C82 56 79 52 78 46 Z"))  # hair
    s.append(ellipse(95.5, 45, 4.6, 8.4, fill=FILL, stroke=OUT, width=1.1))  # ear
    s.append(line("M95.2 40 C97.5 41.5 97.5 47 95.2 49.5", width=0.6))
    s.append(line("M113.6 35.4 C116.5 34 119.5 34.2 121.5 35.6", width=1))  # brow
    s.append(line("M115.8 39 C117.5 38 119.8 38.2 121 39.4", width=0.8))  # eye
    s.append(line("M121.6 54.6 C122.6 55.2 123.6 55 124.2 54.4", width=0.7))  # nostril
    s.append(line("M120.5 59.4 L124 59.6", width=0.8))  # mouth
    s.append(line("M118 62.5 C112 64 104 62 99 54", width=0.7, color=FAINT))  # jaw
    s.append(line("M105 75 C102 80 101 85 102 90", width=0.6, color=FAINT))  # neck muscle
    s.append(line("M88 96 C97 90 109 92 114 102 C114 112 110 120 104 124", width=0.8, color=DET, dash="2.2 1.6"))  # arm removed
    s.append(line("M100 126 C110 128 120 128 126 124", width=0.6, color=FAINT))  # breast fold
    s.append(line("M86 198 C94 200 104 202 113 208", width=0.8))  # iliac crest
    s.append(dot(100, 238, 1.8))  # greater trochanter
    s.append(line("M115 325 C119 330 119 342 115 346", width=0.8))  # patella
    s.append(dot(97.8, 352, 1.5))  # head of fibula
    s.append(line("M93.6 343 L97 344", width=0.8))  # popliteal crease
    s.append(ellipse(100, 440, 3, 3.4, fill="none", stroke=DET, width=0.8))  # lateral malleolus
    s.append(line("M104 452 C112 456 122 460 132 463", width=0.6, color=FAINT))  # foot side
    s.append(line("M128 460.5 L128.4 466.5", width=0.6))
    return "".join(s)


# ---------------------------------------------------------------- head, front and side

HEAD_HALF = [
    (120, 18), (146, 21), (168, 33), (183.5, 54), (190.5, 82), (191.5, 112), (189, 140), (184, 166), (176, 191), (165, 212),
    (153, 229), (147, 240), (146, 256), (148, 280), (120, 280),
]


def head_front():
    s = []
    for sign in (1, -1):
        x = 120 + sign * 74
        s.append(ellipse(x + sign * 6, 136, 10.5, 25, width=1.4))
        s.append(line(f"M{fmt(x + sign * 6)} 116 C{fmt(x + sign * 13)} 124 {fmt(x + sign * 12)} 150 {fmt(x + sign * 4)} 156", width=0.7))
    s.append(outline(smooth(mirror_half(HEAD_HALF, 120)), width=1.8))
    s.append(shade("M58 70 C56 28 184 28 182 70 C170 54 150 50 120 56 C90 50 70 54 58 70 Z"))  # hair
    s.append(line("M58 70 C70 54 90 50 120 56 C150 50 170 54 182 70", width=0.8, color=FAINT))  # hairline
    s.append(both("M132 103 C142 98 158 98 170 104", 120, width=1.6))  # brows
    s.append(both("M136 120 C142 114 156 114 163 120 C156 125 142 125 136 120 Z", 120, width=1))  # eyes
    s.append(ellipse(149.5, 119.8, 3.2, 3.2, fill=DET, stroke="none") + ellipse(90.5, 119.8, 3.2, 3.2, fill=DET, stroke="none"))
    s.append(line("M120 114 L117.5 154 C119 157 121 157 122.5 154", width=1))  # nose
    s.append(both("M124 162 C130 163 135 160 136 155 C134 150 131 150 129 152", 120, width=1))  # nostrils
    s.append(line("M115.6 166 L116.6 180 M124.4 166 L123.4 180", width=0.7, color=FAINT))  # philtrum
    s.append(line("M98 194 C106 191 114 190 120 192 C126 190 134 191 142 194", width=1.1))
    s.append(line("M98 194 C108 200 132 200 142 194", width=1))  # lips
    s.append(line("M106 212 C112 216 128 216 134 212", width=0.7, color=FAINT))  # chin fold
    s.append(both("M150 160 C155 176 156 196 152 214", 120, width=0.6, color=FAINT))  # cheek
    s.append(both("M146 246 C140 252 130 256 120 257", 120, width=0.8, color=FAINT))  # chin and jaw
    return "".join(s)


HEAD_SIDE = [
    (118, 18), (140, 20), (160, 28), (175, 42), (183, 60), (186, 80), (185, 96), (183, 104), (184.5, 111), (196, 139), (194, 146),
    (186, 148.5), (188.5, 158), (186, 166), (187.5, 172), (182, 180), (183.5, 190), (178, 204), (166, 210), (151, 213), (140, 218),
    (136.5, 236), (138, 280), (76, 280), (77.5, 250), (75.5, 228), (69.5, 214), (61, 200), (55.5, 178), (52, 150), (52, 120), (56, 90),
    (66, 60), (80, 40), (98, 25),
]


def head_side():
    s = [outline(smooth(HEAD_SIDE), width=1.8)]
    s.append(shade("M54 132 C46 60 96 14 150 24 C170 30 178 44 180 58 C166 50 150 50 136 60 C126 70 120 84 118 98 C104 100 92 110 88 132 C82 150 78 168 70 182 C60 176 54 156 54 132 Z"))  # hair
    # Ear.
    s.append('<path d="M112 98 C100 98 98 112 99 124 C100 140 104 156 110 166 C116 172 124 170 126 160 C127 150 124 146 126 138 C128 128 130 116 126 106 C123 100 118 98 112 98 Z" '
             f'fill="{FILL}" stroke="{OUT}" stroke-width="1.5"/>')
    s.append(line("M110 106 C104 112 104 126 106 138 C108 148 112 154 116 152", width=0.9))  # antihelix
    s.append(line("M118 120 C122 126 122 134 118 140 C114 144 112 140 113 134", width=0.8))  # concha
    s.append(line("M127 128 C130 130 130 136 127 139", width=1))  # tragus
    s.append(line("M164 92 C170 89 178 90 183 94", width=1.6))  # brow
    s.append(line("M168 104 C172 100 178 100 181 104 C178 107 172 107 168 104 Z", width=1))  # eye
    s.append(line("M186 146 C188 142 191 142 192 145", width=0.9))  # nostril
    s.append(line("M180 166 L187.5 166.5", width=1))  # mouth
    s.append(line("M128 122 C140 120 152 122 162 128", width=0.8, color=FAINT))  # zygomatic arch
    s.append(line("M120 168 C124 182 128 192 134 198 C148 204 164 206 176 204", width=0.9, color=DET))  # jaw
    s.append(line("M104 176 C116 200 128 220 136 240", width=0.7, color=FAINT))  # sternocleidomastoid
    s.append(line("M82 196 C80 210 78 226 78 240", width=0.6, color=FAINT))
    return "".join(s)


# ---------------------------------------------------------------- arm and hand

def arm(inner):
    """The arm hanging straight, palm towards us (inner) or back of the hand towards us (outer).
    In the inner view the thumb is on the right; the outer view is its mirror image."""
    arm_outline = [
        (58, 12), (104, 12), (105.5, 60), (104.5, 120), (104.5, 180), (106, 230), (103.5, 270), (100.5, 320), (97.5, 364), (95, 398),
        (97.5, 408), (102, 414), (108.5, 418), (115.5, 426), (120, 436), (119.5, 444), (114, 444), (106.5, 437), (99, 432), (98, 444),
        (98.6, 458), (96.2, 466), (91.8, 465.8), (90.8, 458), (90, 470), (86.8, 476.4), (82, 476.6), (79.6, 470), (78.4, 474.6), (73.8, 474.2),
        (71.8, 466), (70.4, 467), (66.4, 465.6), (65.4, 456), (64.6, 440), (65.2, 420), (66.6, 404), (66, 398), (63, 364), (59.6, 320),
        (56.4, 270), (54, 230), (55.6, 180), (55.8, 120), (56.6, 60),
    ]
    s = []
    pts = arm_outline if inner else mirror(arm_outline, 80)
    s.append(outline(smooth(pts), width=1.6))
    m = (lambda d: d) if inner else (lambda d: flip_path(d, 80))
    if inner:
        s.append(line(m("M56 230 C68 233 92 233 106 230"), width=0.9))  # elbow crease
        s.append(line(m("M84 160 C86 190 88 212 88 228"), width=0.6, color=FAINT))  # biceps tendon
        s.append(line(m("M66.4 400 C76 402.5 88 402.5 95.4 400"), width=0.9))  # wrist creases
        s.append(line(m("M66.4 405 C76 407 88 407 96 405"), width=0.6, color=FAINT))
        s.append(line(m("M97 412 C88 420 82 432 82 448"), width=0.7, color=FAINT))  # thenar crease
        s.append(line(m("M66 428 C76 426 86 428 94 434"), width=0.6, color=FAINT))  # heart line
        s.append(line(m("M70.6 452 L71.6 466 M79.4 452 L79.6 470 M88.6 452 L89.6 465.6"), width=0.7))  # fingers
        s.append(line(m("M75.4 408 L75.4 360 M84.4 408 L84.4 360"), width=0.5, color=FAINT))  # tendons
    else:
        s.append(line(m("M86 218 C82 214 76 214 72 219"), width=0.9))  # olecranon
        s.append(line(m("M66.4 400 C76 398 88 398 95.4 400"), width=0.8))  # dorsal wrist crease
        s.append(line(m("M100 410 C104 412 108 412 110 416"), width=0.6, color=FAINT))
        for x in (69, 77.4, 85.6, 93.6):
            s.append(line(m(f"M{fmt(x - 3.4)} 447 C{fmt(x - 2)} 444.6 {fmt(x + 2)} 444.6 {fmt(x + 3.4)} 447"), width=0.7))  # knuckles
        s.append(line(m("M70.6 452 L71.6 466 M79.4 452 L79.6 470 M88.6 452 L89.6 465.6"), width=0.7))
        s.append(line(m("M72 404 L69 446 M78 404 L77.4 446 M84 404 L85.6 446 M90 404 L93.6 446"), width=0.45, color=FAINT))  # metacarpals
        s.append(line(m("M60 120 C64 140 70 158 74 166"), width=0.6, color=FAINT))  # deltoid insertion
    s.append(line(m("M56 14 L104 14"), width=0.8, color=FAINT, dash="3 2"))  # cut at the shoulder
    return "".join(s)


def arm_inner():
    return arm(True)


def arm_outer():
    return arm(False)


# ---------------------------------------------------------------- leg and foot

def leg(inner):
    """The leg from the hip down with the foot in profile. Inner view: toes to the right."""
    pts = [
        (38, 10), (110, 10), (108, 60), (104, 120), (98, 180), (94, 214), (93.5, 232), (95, 250), (95.5, 280), (92, 320), (86, 360),
        (81, 398), (82, 418), (88, 428), (100, 434), (114, 440), (128, 446), (140, 450), (144, 455), (143, 461), (134, 464),
        (110, 466), (80, 466), (62, 464), (52, 458), (51, 448), (54, 432), (56, 414), (54, 396), (46, 360), (41, 320),
        (40, 280), (43, 250), (45, 232), (44, 214), (40, 180), (36, 120), (35, 60),
    ]
    if not inner:
        pts = mirror(pts, 80)
    m = (lambda d: d) if inner else (lambda d: flip_path(d, 80))
    s = [outline(smooth(pts), width=1.6)]
    s.append(line(m("M38 12 L110 12"), width=0.8, color=FAINT, dash="3 2"))
    s.append(line(m("M92 200 C99 208 100 230 94 242"), width=0.8))  # patella
    s.append(line(m("M46 232 C50 230 54 230 57 232"), width=0.8))  # back of the knee
    s.append(line(m("M88 260 C85 300 82 350 80 400"), width=0.6, color=FAINT))  # shin
    if inner:
        s.append(ellipse(70, 422, 3.6, 4, fill="none", stroke=DET, width=0.8))  # medial malleolus
        s.append(line("M60 400 C60 410 62 418 64 424", width=0.6, color=FAINT))  # Achilles
        s.append(line("M86 448 C100 444 116 446 130 452", width=0.7, color=FAINT))  # arch
        s.append(line("M136 452 C138 454 138 458 136 461", width=0.7))  # big toe
        s.append(line("M50 160 C56 190 60 210 62 226", width=0.6, color=FAINT))  # hamstring edge
    else:
        s.append(ellipse(m_x(70, inner), 420, 3.8, 4.2, fill="none", stroke=DET, width=0.8))  # lateral malleolus
        s.append(dot(m_x(60, inner), 244, 1.8))  # head of fibula
        s.append(line(m("M60 248 C64 300 66 360 68 414"), width=0.6, color=FAINT))  # fibula
        s.append(line(m("M98 450 L132 456"), width=0.6, color=FAINT))
        for x in (128, 132, 136, 140):
            s.append(line(m(f"M{x} 452 L{x - 1} 463"), width=0.6))  # toes
        s.append(line(m("M66 10 C68 80 70 160 72 220"), width=0.6, color=FAINT, dash="2 2"))  # iliotibial band
    return "".join(s)


def m_x(x, inner):
    return x if inner else 160 - x


def leg_inner():
    return leg(True)


def leg_outer():
    return leg(False)


# ---------------------------------------------------------------- ear

def ear():
    """A left ear seen from the side (face on the left), with the zones used for auricular points."""
    s = []
    contour = [(96, 18), (124, 20), (148, 34), (163, 58), (168, 90), (165, 124), (158, 158), (150, 190), (143, 215), (138, 240),
               (132, 264), (120, 284), (102, 290), (86, 282), (78, 264), (80, 244), (86, 230), (82, 222), (72, 210), (66, 194),
               (66, 174), (70, 158), (76, 148), (72, 138), (66, 120), (62, 96), (66, 62), (78, 36)]
    s.append(outline(smooth(contour), width=2))
    # Depth: the concha (cymba above the helix crus, cavum below) and the triangular fossa.
    s.append(shade(smooth([(84, 128), (112, 130), (126, 140), (135, 160), (131, 184), (121, 204), (104, 212), (90, 212), (84, 196), (80, 170), (80, 146)])))
    s.append(shade(smooth([(84, 99), (124, 99), (128, 110), (118, 120), (92, 116), (84, 108)])))
    s.append(shade(smooth([(84, 86), (104, 79), (121, 84), (114, 64), (104, 48), (90, 56), (82, 72)])))
    # Helix rim and its fold.
    s.append(line(smooth([(72, 76), (84, 46), (110, 32), (138, 40), (154, 62), (158, 96), (154, 140), (146, 180), (138, 214)], closed=False), width=1.2))
    # Antihelix with its two crura.
    s.append(line(smooth([(141, 214), (151, 178), (153, 138), (147, 102), (133, 80), (124, 58), (114, 40)], closed=False), width=1.1))
    s.append(line(smooth([(124, 206), (134, 178), (138, 140), (134, 108), (126, 96), (106, 92), (82, 96)], closed=False), width=1.1))
    s.append(line(smooth([(104, 46), (112, 64), (121, 84), (104, 79), (80, 84)], closed=False), width=1))
    # Helix crus running into the concha.
    s.append(f'<path d="{smooth([(68, 116), (90, 113), (114, 119), (123, 127), (112, 128), (90, 125), (69, 126)])}" fill="{SHADE}" stroke="{DET}" stroke-width="1"/>')
    # Tragus, antitragus and the notch between them.
    s.append(line(smooth([(76, 148), (83, 160), (85, 180), (81, 198), (76, 207)], closed=False), width=1.1))
    s.append(line(smooth([(96, 220), (110, 212), (126, 209), (136, 216)], closed=False), width=1.2))
    s.append(line("M80 210 C84 218 90 221 96 220", width=0.9))
    # The lobe in its usual nine zones.
    s.append(line("M84 248 C100 251 120 251 135 248 M82 266 C98 269 116 269 130 266", width=0.6, color=FAINT, dash="2 2"))
    s.append(line("M100 236 L98 288 M118 236 L117 286", width=0.6, color=FAINT, dash="2 2"))
    return "".join(s)


VIEWS = {
    "front": (200, 480, front),
    "back": (200, 480, back),
    "side": (200, 480, side),
    "head-front": (240, 280, head_front),
    "head-side": (240, 280, head_side),
    "arm-inner": (160, 480, arm_inner),
    "arm-outer": (160, 480, arm_outer),
    "leg-inner": (160, 480, leg_inner),
    "leg-outer": (160, 480, leg_outer),
    "ear": (200, 300, ear),
}
