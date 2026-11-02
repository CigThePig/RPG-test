#!/usr/bin/env python3
"""Generate the PR #7 UI mockups (1280x800 logical canvas, Touch density).

Values mirror docs/ui/DESIGN_SYSTEM.md and the part guides. The guides are
authoritative; regenerate after changing them:  python3 docs/ui/mockups/build_mockups.py
"""
import math, random, pathlib

OUT = pathlib.Path(__file__).parent
W, H = 1280, 800

T = dict(
    scrim="#050A0A", s1="#101A1A", s2="#182726", s3="#22352F", sunken="#0A1111", plate="#0B1414",
    subtle="#2A3B38", frame="#7A8557", bright="#B0AF7E",
    text="#E4EBD0", text2="#AFBB9E", dis="#6B7766", onacc="#14201C",
    gold="#D9BC6E", moss="#8DB65A", ember="#E4793A", lumen="#9FE3E6", amber="#E9A23B",
    dtext="#F07A6E", dfill="#C9443A", hp_track="#2A1312", mp="#3C9BC0", mp_track="#0F2430", xp_track="#2B2617",
    attack="#795433", dodge="#355B56",
)
BODY = "Atkinson Hyperlegible, Verdana, sans-serif"
HEAD = "Alegreya SC, Georgia, serif"


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


class Svg:
    def __init__(self, title):
        self.p = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
                  f'<title>{esc(title)}</title>',
                  '<defs><filter id="sh" x="-20%" y="-20%" width="140%" height="140%"><feDropShadow dx="0" dy="4" stdDeviation="6" flood-color="#000" flood-opacity=".45"/></filter>'
                  '<pattern id="dash" width="8" height="8" patternUnits="userSpaceOnUse"><path d="M0 8L8 0" stroke="#2A3B38" stroke-width="2"/></pattern></defs>']

    def add(self, s): self.p.append(s); return self

    def rect(self, x, y, w, h, fill="none", r=0, stroke=None, sw=2, op=1, shadow=False, dash=None):
        a = f' stroke="{stroke}" stroke-width="{sw}"' if stroke else ""
        if dash: a += f' stroke-dasharray="{dash}"'
        f = ' filter="url(#sh)"' if shadow else ""
        return self.add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" fill-opacity="{op}"{a}{f}/>')

    def circle(self, cx, cy, r, fill="none", stroke=None, sw=2, op=1, shadow=False, dash=None):
        a = f' stroke="{stroke}" stroke-width="{sw}"' if stroke else ""
        if dash: a += f' stroke-dasharray="{dash}"'
        f = ' filter="url(#sh)"' if shadow else ""
        return self.add(f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="{fill}" fill-opacity="{op}"{a}{f}/>')

    def text(self, x, y, s, size=26, fill=None, bold=False, font=BODY, anchor="start", op=1, outline=False):
        fill = fill or T["text"]
        st = ' stroke="#000" stroke-opacity=".7" stroke-width="3" paint-order="stroke"' if outline else ""
        return self.add(f'<text x="{x}" y="{y}" font-family="{font}" font-size="{size}" font-weight="{"700" if bold else "400"}" '
                        f'fill="{fill}" fill-opacity="{op}" text-anchor="{anchor}"{st}>{esc(s)}</text>')

    def arc(self, cx, cy, r, frac, color, sw):
        if frac <= 0: return self
        if frac >= 1: return self.circle(cx, cy, r, stroke=color, sw=sw)
        a = -math.pi / 2 + 2 * math.pi * frac
        x, y = cx + r * math.cos(a), cy + r * math.sin(a)
        large = 1 if frac > .5 else 0
        return self.add(f'<path d="M{cx} {cy - r} A{r} {r} 0 {large} 1 {x:.1f} {y:.1f}" fill="none" stroke="{color}" stroke-width="{sw}" stroke-linecap="round"/>')

    def wedge(self, cx, cy, r, frac, color, op):
        a = -math.pi / 2 + 2 * math.pi * frac
        x, y = cx + r * math.cos(a), cy + r * math.sin(a)
        large = 1 if frac > .5 else 0
        return self.add(f'<path d="M{cx} {cy} L{cx} {cy - r} A{r} {r} 0 {large} 1 {x:.1f} {y:.1f} Z" fill="{color}" fill-opacity="{op}"/>')

    def save(self, name):
        self.p.append("</svg>")
        (OUT / name).write_text("\n".join(self.p))


# ---------------------------------------------------------------- icons (24-grid, scaled)
ICONS = {
    "sword": "M19 3l2 2-9.5 9.5-2-2zM8.5 13.5l2 2-2.5 2.5 1.5 1.5-1.5 1.5-1.5-1.5L4 22l-2-2 2.5-2.5L3 16l1.5-1.5L6 16z",
    "flame": "M12 2c1 4 6 6 6 12a6 6 0 0 1-12 0c0-3 2-5 3-6 0 2 1 3 2 3 0-3-1-6 1-9z",
    "leaf": "M20 4C10 4 4 9 4 16c0 1 0 2 1 3 2-5 6-8 10-9-4 2-7 6-8 10 7 1 13-5 13-16z",
    "dash": "M3 7h10v3H3zM6 12h11v3H6zM3 17h8v3H3zM15 6l6 6-6 6v-3l3-3-3-3z",
    "flask": "M9 2h6v2h-1v5l6 10a2 2 0 0 1-2 3H6a2 2 0 0 1-2-3l6-10V4H9zM7.5 16l-1.5 3h12l-1.5-3z",
    "hand": "M8 11V5a1.5 1.5 0 0 1 3 0v5h1V3a1.5 1.5 0 0 1 3 0v7h1V5a1.5 1.5 0 0 1 3 0v9c0 5-3 8-7 8-3 0-5-2-7-5l-2-3c-1-1 0-3 2-2z",
    "chest": "M3 9a4 4 0 0 1 4-4h10a4 4 0 0 1 4 4v2H3zM3 12h18v8H3zM10 11h4v4h-4z",
    "bag": "M8 7V6a4 4 0 0 1 8 0v1h3l1 14H4L5 7zM10 7h4V6a2 2 0 0 0-4 0z",
    "menu": "M3 5h18v3H3zM3 11h18v3H3zM3 17h18v3H3z",
    "scroll": "M6 3h12a3 3 0 0 1 3 3v2h-4v11a3 3 0 0 1-3 3H5a3 3 0 0 1-3-3v-2h4V6a3 3 0 0 1 0 0zM9 8h6v2H9zM9 12h6v2H9z",
    "person": "M12 2a5 5 0 1 1 0 10 5 5 0 0 1 0-10zM3 22c0-5 4-8 9-8s9 3 9 8z",
    "map": "M2 5l6-2 8 3 6-2v15l-6 2-8-3-6 2zM8 6v12l8 3V9z",
    "close": "M5 3l7 7 7-7 2 2-7 7 7 7-2 2-7-7-7 7-2-2 7-7-7-7z",
    "coin": "M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18zm0 4a5 5 0 1 0 0 10 5 5 0 0 0 0-10z",
    "check": "M4 12l3-3 4 4 8-8 3 3-11 11z",
    "pin": "M9 2h6l-1 6 4 4v2h-5v7l-1 1-1-1v-7H6v-2l4-4z",
    "gear": "M10 2h4l1 3 3-1 2 3-2 2 1 3-3 1v3h-4l-1-3-3 1-2-3 2-2-1-3 3-1zM12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z",
    "key": "M7 8a5 5 0 1 1 4 7l-1 1H8v2H6v2H3v-3l6-6a5 5 0 0 1-2-3zm5-1a2 2 0 1 0 0 4 2 2 0 0 0 0-4z",
    "star": "M12 2l3 7 7 .5-5.5 4.5L18 21l-6-4-6 4 1.5-7L2 9.5 9 9z",
    "herb": "M12 22V10M12 12c-5 0-7-4-7-8 4 0 7 3 7 8zm0 2c4 0 7-3 7-7-4 0-7 3-7 7z",
    "ore": "M4 16l3-8 6-4 7 6-1 9-9 2z",
    "wood": "M3 15l12-12 6 6-12 12zM6 15l3 3",
    "coat": "M8 2h8l5 5-3 3v12H6V10L3 7z",
    "relic": "M12 2l6 6-6 14-6-14z",
    "chat": "M3 4h18v12H9l-5 4v-4H3z",
    "scale": "M11 2h2v3h6l3 7a4 4 0 0 1-7 0l3-6h-5v14h4v2H7v-2h4V6H6l3 6a4 4 0 0 1-7 0l3-7h6z",
    "back": "M15 3l3 3-6 6 6 6-3 3-9-9z",
    "target": "M12 2a10 10 0 1 1 0 20 10 10 0 0 1 0-20zm0 4a6 6 0 1 0 0 12 6 6 0 0 0 0-12zm0 3a3 3 0 1 1 0 6 3 3 0 0 1 0-6z",
    "plus": "M10 3h4v7h7v4h-7v7h-4v-7H3v-4h7z",
    "minus": "M3 10h18v4H3z",
    "lock": "M7 10V7a5 5 0 0 1 10 0v3h2v12H5V10zm3 0h4V7a2 2 0 0 0-4 0z",
    "lever": "M5 20h14v2H5zM10 18l6-14 2 1-5 13z",
    "brazier": "M6 12h12l-2 6H8zM9 20h6v2H9zM12 2c1 3 4 4 4 7H8c0-2 2-3 2-5 1 1 2 1 2-2z",
    "door": "M5 2h14v20H5zM14 11h2v2h-2z",
    "book": "M4 3h7a3 3 0 0 1 2 1 3 3 0 0 1 2-1h5v16h-6a2 2 0 0 0-2 2 2 2 0 0 0-2-2H4z",
    "save": "M4 3h13l3 3v15H4zM7 3v6h9V3zM7 14h10v5H7z",
    "sound": "M3 9h4l5-5v16l-5-5H3zM15 8a5 5 0 0 1 0 8M17 5a9 9 0 0 1 0 14",
    "eye": "M12 5c5 0 9 4 10 7-1 3-5 7-10 7S3 15 2 12c1-3 5-7 10-7zm0 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8z",
    "warning": "M12 2l11 20H1zM11 9v6h2V9zm0 8v2h2v-2z",
    "ward": "M12 2l8 3v6c0 5-3.5 9-8 11-4.5-2-8-6-8-11V5z",
    "next": "M9 3l-3 3 6 6-6 6 3 3 9-9z",
    "info": "M12 2a10 10 0 1 1 0 20 10 10 0 0 1 0-20zm-1 8v8h2v-8zm0-4v2h2V6z",
    "staff": "M17 2l3 3-2 2-1-1-11 14-2-2L18 7l-1-1z",
    "bug": "M9 4h6v3H9zM6 8h12v10a6 6 0 0 1-12 0zM2 10h4v2H2zM18 10h4v2h-4zM2 16h4v2H2zM18 16h4v2h-4z",
}


def icon(svg, name, x, y, size, color=None, op=1):
    color = color or T["text"]
    k = size / 24
    stroke_only = name in ("herb", "wood")
    d = ICONS[name]
    if stroke_only:
        svg.add(f'<path d="{d}" transform="translate({x},{y}) scale({k})" fill="none" stroke="{color}" stroke-opacity="{op}" stroke-width="2.4" stroke-linecap="round"/>')
    else:
        svg.add(f'<path d="{d}" transform="translate({x},{y}) scale({k})" fill="{color}" fill-opacity="{op}" fill-rule="evenodd"/>')


# ---------------------------------------------------------------- world backdrop
def world(svg, ruin=False, dim=0.0):
    rnd = random.Random(7 if not ruin else 11)
    tile = 32
    pal = ["#4E6B3A", "#557241", "#486434", "#6B5A3E", "#645339"] if not ruin else ["#3A3F3A", "#41463F", "#363B36", "#4A4A40"]
    for ty in range(0, H, tile):
        for tx in range(0, W, tile):
            c = rnd.choice(pal)
            svg.rect(tx, ty, tile, tile, fill=c)
    if not ruin:
        for (x, y, w, h) in [(140, 160, 220, 120), (900, 60, 320, 140), (430, 470, 520, 90), (40, 650, 200, 110)]:
            svg.rect(x, y, w, h, fill="#2E5560", r=4)
            svg.rect(x, y, w, h, fill="none", r=4, stroke="#B9C6A0", sw=2, op=0)
        for (x, y, w, h) in [(470, 250, 210, 64), (250, 300, 140, 28), (610, 30, 180, 120)]:
            svg.rect(x, y, w, h, fill="#7D8584", r=2)
            svg.rect(x, y + h - 10, w, 10, fill="#5D6564")
    else:
        for (x, y, w, h) in [(0, 0, W, 120), (0, 680, W, 120), (0, 0, 180, H), (1100, 0, 180, H), (520, 120, 60, 260)]:
            svg.rect(x, y, w, h, fill="#1E2422")
    # a few characters as blobs
    for (x, y, c) in [(560, 380, "#C77FA6"), (700, 360, "#3F5368"), (760, 410, "#8A5A3C"), (820, 380, "#7FAF3F")]:
        svg.add(f'<ellipse cx="{x}" cy="{y + 22}" rx="16" ry="6" fill="#000" fill-opacity=".3"/>')
        svg.rect(x - 12, y - 18, 24, 40, fill=c, r=8)
    if dim:
        svg.rect(0, 0, W, H, fill=T["scrim"], op=dim)


# ---------------------------------------------------------------- components
def statbar(svg, x, y, w, h, frac, fill, track, ghost=None):
    svg.rect(x, y, w, h, fill=track, r=h / 2)
    if ghost: svg.rect(x, y, w * ghost, h, fill="#F0C9A0", r=h / 2)
    svg.rect(x, y, w * frac, h, fill=fill, r=h / 2)
    svg.rect(x, y, w, h, fill="none", r=h / 2, stroke="#000", sw=2, op=0)
    svg.add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{h / 2}" fill="none" stroke="#000" stroke-opacity=".6" stroke-width="2"/>')


def button(svg, x, y, w, h, label, kind="secondary", ic=None, size=26, reason=None):
    if kind == "primary":
        svg.rect(x, y, w, h, fill=T["gold"], r=12, shadow=True); col = T["onacc"]
    elif kind == "ghost":
        col = T["text2"]
    elif kind == "danger":
        svg.rect(x, y, w, h, fill=T["dfill"], r=12); col = "#FFFFFF"
    elif kind == "disabled":
        svg.rect(x, y, w, h, fill=T["s2"], r=12, stroke=T["subtle"]); col = T["dis"]
    else:
        svg.rect(x, y, w, h, fill=T["s2"], r=12, stroke=T["bright"]); col = T["text"]
    cy = y + h / 2 + size * .35 - (10 if reason else 0)
    if ic:
        tw = len(label) * size * .52
        ix = x + w / 2 - (tw + size + 10) / 2
        icon(svg, ic, ix, y + h / 2 - size / 2 - (10 if reason else 0), size, col)
        svg.text(ix + size + 10, cy, label, size, col, bold=True)
    else:
        svg.text(x + w / 2, cy, label, size, col, bold=True, anchor="middle")
    if reason:
        svg.text(x + w / 2, y + h - 14, reason, 20, T["dtext"] if kind == "disabled" else T["text2"], anchor="middle")


def chip(svg, x, y, label, color, fill=None, size=20, ic=None):
    w = len(label) * size * .62 + 24 + (size + 6 if ic else 0)
    svg.rect(x, y, w, size + 12, fill=fill or T["s2"], r=6, stroke=color, sw=1.5)
    tx = x + 12
    if ic:
        icon(svg, ic, tx, y + 6, size, color); tx += size + 6
    svg.text(tx, y + size + 2, label, size, color, bold=True)
    return w


def tile(svg, x, y, ic, count=None, pips=0, equipped=False, selected=False, s=96, color=None):
    svg.rect(x, y, s, s, fill=T["s2"], r=12, stroke=T["gold"] if selected else ([T["subtle"], T["moss"], T["lumen"], T["gold"]][min(pips, 3)]), sw=3 if selected else 2)
    icon(svg, ic, x + s / 2 - 28, y + s / 2 - 28, 56, color or T["text"])
    for i in range(pips):
        svg.circle(x + 12 + i * 10, y + 12, 3.5, fill=[T["moss"], T["lumen"], T["gold"]][min(pips, 3) - 1])
    if equipped:
        svg.rect(x + s - 30, y + 6, 24, 24, fill=T["gold"], r=6); svg.text(x + s - 18, y + 25, "E", 18, T["onacc"], bold=True, anchor="middle")
    if count:
        svg.text(x + s - 8, y + s - 8, count, 24, T["text"], bold=True, anchor="end", outline=True)


def book_frame(svg, active, crowns="20"):
    skin_frame(svg, 16, 16, 1248, 768)
    svg.text(44, 74, "Adventurer's Book", 32, T["text"], bold=True, font=HEAD)
    tabs = [("Pack", "bag"), ("Hero", "person"), ("Journal", "scroll"), ("Map", "map")]
    x0, tw = 392, 140
    for i, (name, ic) in enumerate(tabs):
        x = x0 + i * tw
        on = name == active
        col = T["text"] if on else T["text2"]
        icon(svg, ic, x + 24, 42, 28, T["gold"] if on else T["text2"])
        svg.text(x + 60, 66, name, 26, col, bold=on)
        if on: svg.rect(x + 12, 100, tw - 24, 4, fill=T["gold"], r=2)
        if name == "Hero":
            svg.circle(x + 132, 42, 11, fill=T["gold"]); svg.text(x + 132, 49, "1", 18, T["onacc"], bold=True, anchor="middle")
    chip(svg, 966, 42, f"{crowns} crowns", T["gold"], ic="coin", size=22)
    svg.rect(1168, 24, 80, 80, fill=T["s2"], r=12, stroke=T["bright"])
    icon(svg, "close", 1192, 48, 32)
    svg.rect(16, 118, 1248, 2, fill=T["subtle"])


def segmented(svg, x, y, items, active, w_each=150, h=72, disabled=()):
    svg.rect(x, y, w_each * len(items), h, fill=T["sunken"], r=12, stroke=T["subtle"])
    for i, it in enumerate(items):
        sx = x + i * w_each
        if it == active:
            svg.rect(sx + 4, y + 4, w_each - 8, h - 8, fill=T["s3"], r=10, stroke=T["gold"], sw=2)
        col = T["gold"] if it == active else (T["dis"] if it in disabled else T["text2"])
        svg.text(sx + w_each / 2, y + h / 2 + 9, it, 24, col, bold=it == active, anchor="middle")


def listrow(svg, x, y, w, h, ic, title, sub=None, trailing=None, selected=False, icol=None, tcol=None):
    svg.rect(x, y, w, h, fill=T["s3"] if selected else T["s2"], r=12, stroke=T["subtle"], sw=1.5)
    if selected: svg.rect(x, y + 10, 5, h - 20, fill=T["gold"], r=2)
    svg.rect(x + 16, y + h / 2 - 28, 56, 56, fill=T["sunken"], r=10)
    icon(svg, ic, x + 28, y + h / 2 - 16, 32, icol or T["text"])
    ty = y + h / 2 + (-4 if sub else 9)
    svg.text(x + 88, ty, title, 26, tcol or T["text"], bold=True)
    if sub: svg.text(x + 88, ty + 30, sub, 22, T["text2"])
    if trailing: trailing(x + w, y + h / 2)


# ---------------------------------------------------------------- HUD pieces
def vitals(svg, x=24, y=20, hp=.84, ghost=.92, mp=.7, xp=.4, low=False):
    svg.rect(x - 8, y - 8, 448, 134, fill=T["plate"], r=14, op=.72)
    cx, cy = x + 38, y + 38
    svg.circle(cx, cy, 38, fill=T["s2"], stroke=T["frame"])
    svg.circle(cx, cy, 33, stroke=T["sunken"], sw=6)
    svg.arc(cx, cy, 33, xp, T["gold"], 6)
    svg.text(cx, cy + 10, "3", 28, T["text"], bold=True, anchor="middle", outline=True)
    svg.circle(cx + 30, cy - 30, 9, fill=T["gold"], stroke=T["s1"])
    bx = x + 92
    statbar(svg, bx, y + 4, 240, 26, hp, T["dfill"], T["hp_track"], ghost)
    if low: svg.rect(bx - 2, y + 2, 244, 30, fill="none", r=15, stroke=T["dfill"], sw=3)
    svg.text(bx + 252, y + 26, f"{int(hp * 100)}/100", 24, T["text"], bold=True, outline=True)
    statbar(svg, bx, y + 40, 240, 18, mp, T["mp"], T["mp_track"])
    svg.text(bx + 252, y + 58, f"{int(mp * 60)}/60", 22, T["lumen"], bold=True, outline=True)
    chip(svg, bx, y + 64, "Bark ward 6s", T["moss"], ic="ward", size=18)
    svg.text(x, y + 118, "The Reach · 14:32", 22, T["text2"], outline=True)


def toast(svg, x, y, w, ic, title, sub, accent, count=None):
    svg.rect(x, y, w, 72, fill=T["s1"], r=12, op=.92, shadow=True)
    svg.rect(x, y + 8, 4, 56, fill=accent, r=2)
    icon(svg, ic, x + 20, y + 16, 40, accent)
    svg.text(x + 74, y + (32 if sub else 44), title, 24, T["text"], bold=True)
    if sub: svg.text(x + 74, y + 60, sub, 20, T["text2"])
    if count: svg.text(x + w - 16, y + 44, count, 24, T["gold"], bold=True, anchor="end")


def util_buttons(svg, badge=True):
    for (x, ic, label) in [(1084, "bag", "Pack"), (1176, "menu", "Menu")]:
        svg.rect(x, 20, 80, 80, fill=T["plate"], r=14, op=.72, stroke=T["bright"])
        icon(svg, ic, x + 24, 30, 32)
        svg.text(x + 40, 88, label, 18, T["text"], bold=True, anchor="middle")
    if badge: svg.circle(1156, 26, 9, fill=T["gold"], stroke=T["s1"])


def tracker(svg, x=896, y=116, w=360):
    svg.rect(x, y, w, 112, fill=T["plate"], r=12, op=.8, stroke=T["frame"])
    icon(svg, "scroll", x + 14, y + 14, 24, T["gold"])
    svg.text(x + 48, y + 34, "The Buried Light", 22, T["text"], bold=True)
    svg.text(x + w - 14, y + 34, "2/4", 22, T["gold"], bold=True, anchor="end")
    svg.text(x + 16, y + 68, "Find keys, open sealed passages", 22, T["text2"])
    svg.text(x + 16, y + 96, "and recover the sanctuary relic.", 22, T["text2"])


def action(svg, cx, cy, r, fill, ic, label=None, chip_txt=None, chip_col=None, cool=None, isize=40, dim=False):
    svg.circle(cx, cy, r, fill=fill, stroke=T["bright"], sw=3 if r > 60 else 2, op=.92, shadow=True)
    oy = -8 if label and cool is None else 0
    icon(svg, ic, cx - isize / 2, cy - isize / 2 + oy, isize, T["text"], op=.35 if (dim or cool is not None) else 1)
    if label and cool is None: svg.text(cx, cy + r - 16, label, 18, T["text"], bold=True, anchor="middle")
    if cool is not None:
        svg.wedge(cx, cy, r - 2, cool, "#000", .55)
        svg.text(cx, cy + 10, "1", 30, T["text"], bold=True, anchor="middle", outline=True)
    if chip_txt:
        cw = len(chip_txt) * 11 + 14
        svg.rect(cx + r * .45, cy - r - 2, cw, 26, fill=chip_col or T["s1"], r=6, stroke=T["lumen"] if not chip_col else chip_col, sw=1.5)
        svg.text(cx + r * .45 + cw / 2, cy - r + 18, chip_txt, 18, T["lumen"] if not chip_col else "#FFFFFF", bold=True, anchor="middle")


def cluster(svg):
    R, B = 1280, 800
    def c(r, b, w, h): return (R - r + w / 2, B - b + h / 2, w / 2)
    x, y, r = c(172, 172, 136, 136); action(svg, x, y, r, T["attack"], "sword", isize=60)
    x, y, r = c(330, 150, 108, 108); action(svg, x, y, r, T["dodge"], "dash", "Dodge", isize=44)
    x, y, r = c(300, 282, 92, 92); action(svg, x, y, r, T["s2"], "flame", "Ember", "9", cool=.62)
    x, y, r = c(156, 330, 92, 92); action(svg, x, y, r, T["s2"], "leaf", "Mend", "12")
    x, y, r = c(446, 214, 88, 88); action(svg, x, y, r, T["s2"], "flask", "Draught", "×3", chip_col=T["s1"])
    x, y, r = c(260, 430, 88, 88); action(svg, x, y, r, T["s2"], "ward", "Ward", "8")
    # interact pill (contextual: Open chest)
    ix, iy = R - 480, B - 384
    svg.rect(ix, iy, 184, 80, fill=T["s2"], r=40, stroke=T["gold"], sw=3, op=.95, shadow=True)
    icon(svg, "chest", ix + 26, iy + 22, 36, T["gold"])
    svg.text(ix + 74, iy + 50, "Open", 24, T["text"], bold=True)


def stick(svg):
    svg.circle(140, 640, 94, fill="#142424", op=.65, stroke=T["frame"], sw=2)
    for a in range(0, 360, 45):
        rx, ry = 140 + 82 * math.cos(math.radians(a)), 640 + 82 * math.sin(math.radians(a))
        svg.circle(rx, ry, 3, fill=T["text2"], op=.6)
    svg.circle(140, 640, 30, fill="#A6BA80", op=.85)




# ---------------------------------------------------------------- creative layer helpers (C2 skin, C6 portraits)
CREATURES = {
    # 16x16 pixel bitmaps: . empty, o outline, a body, b shade, c accent, e eye
    "wayfarer": [
        "......oooo......", ".....oaaaao.....", "....oaaaaaao....", "....oaeaaeao....", "....oaaaaaao....", ".....obbbbo.....",
        "...ooccccccoo...", "..occccccccco...", "..oacccccccao...", "..oacccccccao...", "...occcccco.....", "....obbobbo.....",
        "....oaaoaao.....", "....oaaoaao.....", "...oooo.oooo....", "................"],
    "prowler": [
        "................", "................", "..oo........oo..", ".oaao......oaao.", ".oaaaoooooaaeao.", "..oaaaaaaaaaaao.", "..oaaaccccaaaoo.",
        ".oaaaccccccaao..", "oaaaacccccaao...", "obboaaaaaaabo...", ".ob.obboobbo....", ".o..ob.oo.bo....", "....oo..o..o....",
        "................", "................", "................"],
    "brute": [
        "................", "....oooooooo....", "...oaaaaaaaao...", "..oaaeaaaaeaao..", "..oaaaaaaaaaao..", ".ooaaccccccaaoo.", "oaaaccccccccaaao",
        "oaaacccccccccaao", "oaaaccccccccaaao", ".oobaaaaaaaaboo.", "...obbaaaabbo...", "...oaao..oaao...", "...oaao..oaao...",
        "..ooooo..ooooo..", "................", "................"],
}
PAL = {"wayfarer": ("#C77FA6", "#8E4F73", "#5B6B42"), "prowler": ("#6F8F3A", "#4A6427", "#A9C46A"), "brute": ("#3F5368", "#2A3847", "#7A8FA8"),
       "silhouette": ("#1E2A28", "#18211F", "#1E2A28")}


def creature(svg, kind, x, y, px, pal=None, eye="#F2E6B8"):
    a, b, c = PAL[pal or kind]
    cols = {"o": "#0A0F0E", "a": a, "b": b, "c": c, "e": eye if pal != "silhouette" else a}
    for j, row in enumerate(CREATURES[kind]):
        for i, ch in enumerate(row):
            if ch != ".": svg.rect(x + i * px, y + j * px, px, px, fill=cols[ch])


def skin_frame(svg, x, y, w, h, region="reach", fill=None, op=.97):
    """Approximates the C2 generated 9-slice frame (2 lp per texel)."""
    rim_l, rim_d = ("#E8CF8A", "#7A6232") if region == "reach" else ("#B7C2BE", "#3D4643")
    svg.rect(x, y, w, h, fill=fill or T["s1"], r=6, op=op, shadow=True)
    svg.rect(x, y, w, h, r=6, stroke="#0A0F0E", sw=4)
    svg.add(f'<path d="M{x + 4} {y + h - 6} V{y + 6} H{x + w - 6}" fill="none" stroke="{rim_l}" stroke-width="3"/>')
    svg.add(f'<path d="M{x + w - 4} {y + 6} V{y + h - 4} H{x + 6}" fill="none" stroke="{rim_d}" stroke-width="3"/>')
    for cx, cy in [(x + 4, y + 4), (x + w - 14, y + 4), (x + 4, y + h - 14), (x + w - 14, y + h - 14)]:
        svg.rect(cx, cy, 10, 10, fill=rim_l); svg.rect(cx + 3, cy + 3, 4, 4, fill="#0A0F0E")
    if region == "reach":
        for tx in (x + 22, x + 34, x + w - 46, x + w - 30):
            for dx, dy, col in [(0, -4, "#5E8A3A"), (2, -6, "#8DB65A"), (4, -4, "#5E8A3A"), (2, -2, "#5E8A3A"), (6, -2, "#8DB65A")]:
                svg.rect(tx + dx, y + dy, 2, 2, fill=col)
    else:
        for tx in (x + 30, x + w - 36):
            svg.rect(tx, y + 1, 4, 4, fill=T["lumen"])


def minimap(svg, x, y, size):
    svg.rect(x, y, size, size, fill=T["sunken"], r=14, op=.92)
    t = size / 24
    for j in range(24):
        for i in range(24):
            c = "#5B6B42"
            if (i - 5) ** 2 + (j - 6) ** 2 < 14 or (i - 18) ** 2 / 2 + (j - 18) ** 2 < 12: c = "#2E5560"
            if j in (0, 23) or i in (0, 23) or (14 <= i <= 17 and 3 <= j <= 9): c = "#26302D"
            if (i + 2 * j) % 11 == 0 and c == "#5B6B42": c = "#526239"
            svg.rect(x + i * t, y + j * t, t + .3, t + .3, fill=c)
    svg.rect(x, y, size, size, r=14, stroke=T["bright"], sw=3)
    svg.rect(x + size / 2 - 10, y - 4, 20, 14, fill=T["s1"], r=4); svg.text(x + size / 2, y + 8, "N", 12, T["gold"], bold=True, anchor="middle")
    cx, cy = x + size / 2, y + size / 2
    svg.add(f'<path d="M{cx} {cy - 9} L{cx + 7} {cy + 7} L{cx} {cy + 3} L{cx - 7} {cy + 7} Z" fill="{T["gold"]}" stroke="#000" stroke-width="1.5"/>')
    for (dx, dy) in [(40, -30), (52, -12), (-50, 40)]:
        svg.add(f'<path d="M{cx + dx} {cy + dy - 6} L{cx + dx + 6} {cy + dy + 5} L{cx + dx - 6} {cy + dy + 5} Z" fill="{T["dfill"]}" stroke="#000" stroke-width="1"/>')
    svg.circle(cx - 30, cy - 20, 5, fill=T["gold"], stroke="#000", sw=1)
    svg.circle(x + size - 10, cy + 10, 8, stroke=T["gold"], sw=3); svg.add(f'<path d="M{x + size - 2} {cy + 10} l-6 -5 v10 z" fill="{T["gold"]}"/>')


def float_text(svg, x, y, txt, size, fill, outline="#000"):
    svg.add(f'<text x="{x}" y="{y}" font-family="{BODY}" font-size="{size}" font-weight="700" fill="{fill}" text-anchor="middle" stroke="{outline}" stroke-width="4" paint-order="stroke">{esc(txt)}</text>')


def nameplate(svg, cx, y, name, frac):
    svg.text(cx, y, name, 16, T["text"], bold=True, anchor="middle", outline=True)
    svg.rect(cx - 24, y + 5, 48, 6, fill="#2A1312", r=3); svg.rect(cx - 24, y + 5, 48 * frac, 6, fill=T["dfill"], r=3)


# ================================================================ screens
def hud_mobile():
    s = Svg("HUD v3 — mobile (Touch)")
    world(s)
    # world: enemies with nameplates and combat text, chest with target highlight
    creature(s, "prowler", 690, 330, 3); nameplate(s, 714, 320, "Mire prowler", .45)
    float_text(s, 740, 300, "14", 30, T["text"], outline=T["ember"])
    float_text(s, 612, 352, "dodged", 18, T["lumen"])
    creature(s, "wayfarer", 560, 360, 3)
    s.rect(470, 300, 40, 30, fill="#7B5A33", r=4, stroke="#3A2A18"); s.add('<path d="M482 282l8 8 8-8" fill="none" stroke="#D9BC6E" stroke-width="4" stroke-linecap="round"/>')
    vitals(s)
    # tracker under vitals (C3)
    tracker(s, x=24, y=146)
    toast(s, 472, 20, 500, "herb", "Silverleaf", "Added to your pack", T["gold"], "×3")
    # minimap + stacked utilities
    minimap(s, 1080, 16, 176)
    for (yy, ic, label) in [(20, "bag", "Pack"), (112, "menu", "Menu")]:
        s.rect(988, yy, 80, 80, fill=T["plate"], r=14, op=.72, stroke=T["bright"]); icon(s, ic, 1012, yy + 10, 32)
        s.text(1028, yy + 68, label, 18, T["text"], bold=True, anchor="middle")
    s.circle(1060, 26, 9, fill=T["gold"], stroke=T["s1"])
    stick(s); cluster(s)
    s.text(24, 790, "01 · HUD v3 (Touch) — CREATIVE_DIRECTION C3/C4/C5; tall tier", 16, T["text2"], outline=True)
    s.save("01-hud-mobile.svg")


def book_pack():
    s = Svg("Book — Pack ▸ Bag")
    world(s, dim=.55); book_frame(s, "Pack")
    segmented(s, 40, 136, ["Bag", "Craft", "Trade"], "Bag")
    # equipped column
    s.text(40, 252, "Equipped", 28, T["text"], bold=True); s.rect(40, 262, 260, 2, fill=T["subtle"])
    s.text(40, 296, "Weapon", 22, T["text2"])
    tile(s, 40, 308, "sword", pips=2, equipped=True, s=80); s.text(132, 342, "Iron sword", 24, T["text"], bold=True); s.text(132, 372, "Damage 8", 22, T["text2"])
    s.text(40, 424, "Body", 22, T["text2"])
    s.rect(40, 436, 80, 80, fill="url(#dash)", r=12, stroke=T["subtle"], dash="6 6"); s.text(132, 484, "Empty", 24, T["text2"])
    s.text(40, 568, "Totals", 22, T["text2"]); s.text(40, 600, "Damage +8", 24, T["text"], bold=True); s.text(40, 632, "Defence +0", 24, T["text"], bold=True)
    # bag grid
    s.text(332, 252, "Bag · 8 items", 28, T["text"], bold=True); s.rect(332, 262, 512, 2, fill=T["subtle"])
    items = [("sword", None, 2, True, False), ("coat", None, 0, False, True), ("flask", "3", 0, False, False), ("herb", "4", 0, False, False),
             ("ore", "3", 0, False, False), ("wood", "1", 0, False, False), ("key", "2", 0, False, False), ("relic", None, 3, False, False)]
    for i, (ic, cnt, p, eq, sel) in enumerate(items):
        tile(s, 332 + (i % 4) * 108, 280 + (i // 4) * 108, ic, cnt, p, eq, sel)
    # details
    s.rect(868, 236, 372, 532, fill=T["s2"], r=14, stroke=T["subtle"])
    tile(s, 888, 256, "coat", s=96, selected=False)
    s.text(1000, 292, "Leather coat", 28, T["text"], bold=True); s.text(1000, 324, "Leather · Armour", 22, T["text2"])
    rows = [("Defence", "4", "▲ +4", T["moss"]), ("Mass", "1.0", "", None), ("Condition", "100%", "", None)]
    for i, (k, v, d, c) in enumerate(rows):
        y = 400 + i * 44
        s.text(888, y, k, 24, T["text2"]); s.text(1120, y, v, 24, T["text"], bold=True, anchor="end")
        if d: s.text(1220, y, d, 22, c, bold=True, anchor="end")
        s.rect(888, y + 14, 332, 1, fill=T["subtle"])
    s.text(888, 548, "Light armour worn over the body.", 22, T["text2"])
    button(s, 888, 664, 332, 88, "Equip", "primary")
    s.save("02-book-pack.svg")


def book_craft():
    s = Svg("Book — Pack ▸ Craft")
    world(s, dim=.55); book_frame(s, "Pack")
    segmented(s, 40, 136, ["Bag", "Craft", "Trade"], "Craft")
    def ready(x, y): chip(s, x - 120, y - 16, "Ready", T["moss"], ic="check", size=18)
    def missing(x, y): s.text(x - 20, y + 8, "Missing", 22, T["text2"], anchor="end")
    def quest_ready(x, y): chip(s, x - 222, y - 16, "Quest", T["gold"], ic="scroll", size=18); ready(x, y)
    listrow(s, 40, 236, 560, 96, "flask", "Restorative draught", "2 Silverleaf", quest_ready)
    listrow(s, 40, 344, 560, 96, "sword", "Iron sword", "3 Bog iron · 1 Seasoned wood", missing, selected=True)
    listrow(s, 40, 452, 560, 96, "staff" if "staff" in ICONS else "wood", "Oak staff", "3 Seasoned wood", missing)
    s.rect(624, 236, 616, 532, fill=T["s2"], r=14, stroke=T["subtle"])
    tile(s, 648, 256, "sword", pips=2, s=96)
    s.text(764, 292, "Iron sword  ×1", 28, T["text"], bold=True); s.text(764, 324, "Superior · Iron · Weapon", 22, T["text2"])
    s.text(648, 396, "Damage", 24, T["text2"]); s.text(1120, 396, "9.6", 24, T["text"], bold=True, anchor="end"); s.text(1216, 396, "▲ +1.6", 22, T["moss"], bold=True, anchor="end")
    s.text(648, 460, "Ingredients", 28, T["text"], bold=True); s.rect(648, 470, 568, 2, fill=T["subtle"])
    for i, (ic, name, have, need) in enumerate([("ore", "Bog iron", 3, 3), ("wood", "Seasoned wood", 0, 1)]):
        y = 482 + i * 76
        s.rect(648, y, 56, 56, fill=T["sunken"], r=10); icon(s, ic, 660, y + 12, 32)
        s.text(720, y + 38, name, 24, T["text"], bold=True)
        ok = have >= need
        s.text(1150, y + 38, f"{have} / {need}", 24, T["moss"] if ok else T["dtext"], bold=True, anchor="end")
        if ok: icon(s, "check", 1170, y + 16, 24, T["moss"])
        else: s.text(1216, y + 38, "", 22)
    s.text(648, 650, "Need 1 more Seasoned wood", 22, T["dtext"])
    button(s, 648, 664, 568, 88, "Craft", "disabled", ic="star", reason="Missing: 1 Seasoned wood")
    s.save("03-book-craft.svg")


def book_hero():
    s = Svg("Book — Hero (portrait + action bar)")
    world(s, dim=.55); book_frame(s, "Hero")
    # live portrait (C6)
    s.rect(40, 136, 340, 260, fill=T["sunken"], r=12, stroke=T["subtle"])
    s.add('<ellipse cx="210" cy="372" rx="70" ry="12" fill="#000" fill-opacity=".35"/>')
    creature(s, "wayfarer", 146, 196, 8)
    s.add('<path d="M70 372 q-14 -14 0 -28" fill="none" stroke="#AFBB9E" stroke-width="3"/><path d="M350 344 q14 14 0 28" fill="none" stroke="#AFBB9E" stroke-width="3"/>')
    s.circle(84, 178, 30, fill=T["s2"], stroke=T["gold"], sw=2); s.text(84, 189, "3", 30, T["text"], bold=True, anchor="middle")
    s.text(40, 432, "Wayfarer", 30, T["text"], bold=True, font=HEAD); s.text(370, 432, "Biped", 22, T["text2"], anchor="end")
    statbar(s, 40, 448, 340, 10, .4, T["gold"], T["xp_track"]); s.text(40, 484, "120 / 300 XP to level 4", 22, T["text2"])
    stats = [("Health", "84 / 110"), ("Strength", "10  (+2)"), ("Defence", "5"), ("Weapon", "+8")]
    for i, (k, v) in enumerate(stats):
        y = 528 + i * 40
        s.text(40, y, k, 24, T["text2"]); s.text(380, y, v, 24, T["text"], bold=True, anchor="end"); s.rect(40, y + 12, 340, 1, fill=T["subtle"])
    chip(s, 40, 704, "1 skill point to spend", T["gold"], ic="star", size=20)
    # growth: action bar segment
    segmented(s, 424, 136, ["Abilities", "Skills", "Action bar"], "Action bar", w_each=200)
    s.text(424, 258, "Tap a slot, then an ability", 22, T["text2"])
    slots = [("2", "flame", "Ember bolt", False), ("3", "leaf", "Mend", False), ("4", "ward", "Bark ward", True)]
    for i, (key, ic, name, sel) in enumerate(slots):
        x = 424 + i * 276
        s.rect(x, 276, 260, 150, fill=T["s3"] if sel else T["s2"], r=12, stroke=T["gold"] if sel else T["subtle"], sw=3 if sel else 1.5)
        s.circle(x + 60, 351, 44, fill=T["s1"], stroke=T["bright"]); icon(s, ic, x + 40, 331, 40, T["gold"] if sel else T["text"])
        s.text(x + 120, 340, name, 24, T["text"], bold=True); s.text(x + 120, 372, f"Slot {i + 1} · key {key}", 20, T["text2"])
    s.text(424, 478, "Known abilities", 26, T["text"], bold=True); s.rect(424, 488, 816, 2, fill=T["subtle"])
    def assign(lbl, on=False):
        def f(x, y): button(s, x - 200, y - 32, 180, 64, lbl, "primary" if on else "secondary", size=22)
        return f
    listrow(s, 424, 504, 816, 96, "ward", "Bark ward", "8 mana · 8 s cooldown · self", assign("In slot 3", True), selected=True, icol=T["gold"])
    listrow(s, 424, 612, 816, 96, "flame", "Ember bolt", "9 mana · 1.5 s · range 240", assign("Swap here"))
    s.save("04-book-hero.svg")


def book_journal():
    s = Svg("Book — Journal")
    world(s, dim=.55); book_frame(s, "Journal")
    segmented(s, 40, 136, ["Quests", "Lore"], "Quests", w_each=170)
    s.text(40, 252, "Active", 28, T["text"], bold=True); s.rect(40, 262, 420, 2, fill=T["subtle"])
    def prog(t, pin=False):
        def f(x, y):
            s.text(x - 20, y + 8, t, 24, T["gold"], bold=True, anchor="end")
            if pin: icon(s, "pin", x - 86, y - 12, 24, T["gold"])
        return f
    listrow(s, 40, 276, 420, 96, "scroll", "The Buried Light", "Recover the relic", prog("2/4", True), selected=True, icol=T["gold"])
    listrow(s, 40, 384, 420, 96, "scroll", "An apothecary's…", "Craft a draught", prog("0/1"))
    s.text(40, 528, "Completed", 28, T["text"], bold=True); s.rect(40, 538, 420, 2, fill=T["subtle"])
    listrow(s, 40, 552, 420, 96, "check", "Trouble in the Reach", "Completed", None, icol=T["moss"], tcol=T["text2"])
    # details
    s.rect(484, 236, 756, 532, fill=T["s2"], r=14, stroke=T["subtle"])
    s.text(508, 284, "The Buried Light", 32, T["text"], bold=True, font=HEAD)
    s.text(508, 320, "An old light lies beyond the sealed ruin.", 24, T["text2"])
    steps = [("done", "Enter the ruins east of Reach."), ("current", "Find keys, open sealed passages and")]
    for i, (st, txt) in enumerate(steps):
        y = 370 + i * 64
        if st == "done":
            icon(s, "check", 508, y, 28, T["moss"]); s.text(552, y + 22, txt, 24, T["text2"])
        else:
            s.rect(500, y - 8, 4, 80, fill=T["gold"], r=2); s.circle(522, y + 14, 10, fill=T["gold"]); s.text(552, y + 22, txt, 24, T["text"], bold=True)
    s.text(552, 488, "recover the sanctuary relic.", 24, T["text"], bold=True)
    s.text(552, 520, "Step 2 of 4", 22, T["text2"])
    s.text(508, 566, "Reward", 26, T["text"], bold=True)
    tile(s, 508, 580, "flask", "3", s=64); s.text(588, 620, "Restorative draught ×3", 24, T["text"])
    button(s, 508, 664, 340, 88, "Tracking", "primary", ic="pin")
    button(s, 868, 664, 348, 88, "Show on map", "secondary", ic="map")
    s.save("05-book-journal.svg")


def book_map():
    s = Svg("Book — Map (ruin)")
    world(s, dim=.55); book_frame(s, "Map")
    cx, cy, cw, ch = 40, 136, 880, 632
    s.rect(cx, cy, cw, ch, fill=T["sunken"], r=14, stroke=T["subtle"])
    t = 16
    rooms = [(3, 3, 9, 9), (20, 3, 9, 9), (3, 20, 9, 9), (20, 20, 11, 9), (37, 11, 9, 9)]
    halls = [(12, 6, 8, 3), (6, 12, 3, 8), (23, 12, 3, 8), (31, 23, 6, 3), (29, 14, 8, 3)]
    explored = {0, 1, 2, 3}
    for i, (x, y, w, h) in enumerate(rooms):
        if i in explored: s.rect(cx + 24 + x * t, cy + 24 + y * t, w * t, h * t, fill="#5B6B42")
    for (x, y, w, h) in halls[:4]:
        s.rect(cx + 24 + x * t, cy + 24 + y * t, w * t, h * t, fill="#5B6B42")
    def mk(px, py, ic, col, shape="circle"):
        X, Y = cx + 24 + px * t, cy + 24 + py * t
        s.circle(X, Y, 16, fill=T["s1"], stroke=col, sw=2.5); icon(s, ic, X - 10, Y - 10, 20, col)
    # gates (shape + colour)
    s.rect(cx + 24 + 12 * t, cy + 24 + 6 * t, 10, 3 * t, fill="#D4B66B"); mk(12.3, 4.6, "key", "#D4B66B")
    s.rect(cx + 24 + 23 * t, cy + 24 + 12 * t, 3 * t, 10, fill="url(#dash)", stroke="#A7B0B2", sw=2)
    s.rect(cx + 24 + 31 * t, cy + 24 + 23 * t, 10, 3 * t, fill="#9FE3E6", op=.35)
    mk(7.5, 7.5, "door", "#8FD18F"); mk(24.5, 7.5, "book", T["text"]); mk(7.5, 24.5, "lever", "#D4B66B"); mk(25.5, 24.5, "brazier", "#F2A443")
    # objective (transition) + player arrow
    ox, oy = cx + 24 + 34 * t, cy + 24 + 24.5 * t
    s.circle(ox, oy, 26, stroke=T["gold"], sw=3, dash="8 6"); icon(s, "target", ox - 12, oy - 12, 24, T["gold"])
    px, py = cx + 24 + 27 * t, cy + 24 + 26 * t
    s.add(f'<path d="M{px + 14} {py} L{px - 10} {py - 10} L{px - 4} {py} L{px - 10} {py + 10} Z" fill="{T["gold"]}" stroke="#000" stroke-width="2"/>')
    # zoom controls
    for i, ic in enumerate(["plus", "minus", "target"]):
        bx, by = cx + cw - 96, cy + ch - 288 + i * 92
        s.rect(bx, by, 80, 80, fill=T["s2"], r=12, stroke=T["bright"]); icon(s, ic, bx + 24, by + 24, 32)
    # sidebar
    sx = 944
    s.text(sx, 168, "The Sealed Ruin", 32, T["text"], bold=True, font=HEAD); s.text(sx, 204, "Explored 46%", 22, T["text2"])
    s.rect(sx, 222, 296, 120, fill=T["s2"], r=12, stroke=T["gold"])
    icon(s, "target", sx + 14, 238, 24, T["gold"]); s.text(sx + 48, 258, "Recover the relic", 22, T["text"], bold=True)
    s.text(sx + 16, 292, "Location unknown —", 22, T["text2"]); s.text(sx + 16, 322, "explore further east.", 22, T["text2"])
    s.text(sx, 384, "Legend", 26, T["text"], bold=True)
    legend = [("door", "#8FD18F", "Entrance"), ("key", "#D4B66B", "Locked gate"), ("lever", "#D4B66B", "Lever"), ("brazier", "#F2A443", "Brazier ring"), ("book", T["text"], "Forgotten archive"), ("target", T["gold"], "Objective")]
    for i, (ic, col, name) in enumerate(legend):
        y = 404 + i * 54
        s.circle(sx + 18, y + 18, 16, fill=T["s1"], stroke=col, sw=2.5); icon(s, ic, sx + 8, y + 8, 20, col)
        s.text(sx + 48, y + 26, name, 22, T["text"])
    s.save("06-book-map.svg")


def dialogue():
    s = Svg("Dialogue — bottom sheet")
    world(s)
    vitals(s); toast(s, 472, 20, 408, "scroll", "Quest accepted", "Trouble in the Reach", T["gold"])
    s.rect(24, 324, 1232, 452, fill=T["s1"], r=18, stroke=T["frame"], op=.97, shadow=True)
    s.rect(56, 300, 200, 52, fill=T["gold"], r=10); s.text(156, 336, "Warden", 30, T["onacc"], bold=True, font=HEAD, anchor="middle")
    s.circle(112, 432, 56, fill=T["s2"], stroke=T["gold"], sw=3); creature(s, "wayfarer", 80, 396, 4)
    s.text(196, 400, "The old light kept these roads safe. Something in the", 26, T["text"])
    s.text(196, 436, "ruins has stirred.", 26, T["text"])
    rows = [("scroll", "I can help with the prowlers.", T["gold"], True), ("scroll", "I want to learn alchemy.", T["gold"], True),
            ("scale", "Show me your supplies.", T["text"], False), ("back", "Goodbye.", T["text2"], False)]
    for i, (ic, txt, col, bang) in enumerate(rows):
        y = 476 + i * 72
        ghost = txt == "Goodbye."
        if not ghost: s.rect(196, y, 1028, 64, fill=T["s2"], r=12, stroke=T["subtle"])
        icon(s, ic, 220, y + 16, 32, col)
        if bang: s.circle(250, y + 16, 9, fill=T["gold"]); s.text(250, y + 22, "!", 16, T["onacc"], bold=True, anchor="middle")
        s.text(272, y + 42, txt, 26, T["text"] if not ghost else T["text2"], bold=not ghost)
    s.text(24, 795, "07 · Dialogue — choices use target.row; 250 ms double-tap guard; tags pick icons", 16, T["text2"], outline=True)
    s.save("07-dialogue.svg")


def pause():
    s = Svg("Pause")
    world(s, dim=.6)
    x, y, w = 320, 24, 640
    s.rect(x, y, w, 752, fill=T["s1"], r=18, stroke=T["frame"], op=.97, shadow=True)
    s.text(640, 82, "Mosslight", 44, T["text"], bold=True, font=HEAD, anchor="middle")
    s.text(640, 114, "Wayfarer · Level 3 · The Sealed Ruin", 22, T["text2"], anchor="middle")
    icon(s, "scroll", 392, 128, 22, T["gold"]); s.text(422, 146, "Recover the relic · saved 3 min ago", 22, T["text2"])
    button(s, x + 32, 164, w - 64, 96, "Resume", "primary", size=28)
    labels = [("bag", "Pack"), ("scroll", "Journal"), ("person", "Character"), ("map", "Map")]
    bw = (w - 64 - 36) / 4
    for i, (ic, lab) in enumerate(labels):
        bx = x + 32 + i * (bw + 12)
        s.rect(bx, 272, bw, 96, fill=T["s2"], r=12, stroke=T["bright"]); icon(s, ic, bx + bw / 2 - 16, 286, 32)
        s.text(bx + bw / 2, 352, lab, 20, T["text"], bold=True, anchor="middle")
    for i, (ic, lab) in enumerate([("save", "Save game"), ("book", "Load game"), ("gear", "Settings")]):
        yy = 380 + i * 100
        s.rect(x + 32, yy, w - 64, 88, fill=T["s2"], r=12, stroke=T["subtle"]); icon(s, ic, x + 56, yy + 28, 32)
        s.text(x + 104, yy + 54, lab, 26, T["text"], bold=True); icon(s, "next", x + w - 72, yy + 32, 24, T["text2"])
    gw = (w - 64 - 24) / 3
    for i, (ic, lab) in enumerate([("star", "New game"), ("bug", "Developer"), ("close", "Quit")]):
        bx = x + 32 + i * (gw + 12)
        icon(s, ic, bx + 14, 708, 22, T["text2"]); s.text(bx + 44, 727, lab, 24, T["text2"])
    s.rect(x + 32, 686, w - 64, 1, fill=T["subtle"])
    s.save("08-pause.svg")


def settings():
    s = Svg("Settings — Display")
    world(s, dim=.6)
    s.rect(16, 16, 1248, 768, fill=T["s1"], r=18, stroke=T["frame"], op=.97, shadow=True)
    s.rect(32, 24, 80, 80, fill=T["s2"], r=12, stroke=T["bright"]); icon(s, "back", 56, 48, 32)
    s.text(136, 76, "Settings", 32, T["text"], bold=True, font=HEAD)
    segmented(s, 40, 128, ["Display", "Audio", "Controls", "Access", "About"], "Display", w_each=180)
    s.add('<clipPath id="list"><rect x="40" y="220" width="1200" height="548"/></clipPath><g clip-path="url(#list)">')
    y = 228
    s.rect(40, y, 1180, 112, fill=T["s2"], r=12, stroke=T["subtle"]); s.text(64, y + 48, "Text size", 26, T["text"], bold=True); s.text(64, y + 82, "Larger text wraps; lists scroll", 22, T["text2"])
    segmented(s, 740, y + 20, ["100%", "115%", "130%"], "115%", w_each=150)
    y += 128
    for lab, val, f in [("HUD opacity", "85%", .7), ("Camera zoom", "1.50×", .2)]:
        s.rect(40, y, 1180, 120, fill=T["s2"], r=12, stroke=T["subtle"]); s.text(64, y + 44, lab, 26, T["text"], bold=True); s.text(1196, y + 44, val, 24, T["gold"], bold=True, anchor="end")
        s.rect(64, y + 78, 1132, 8, fill=T["sunken"], r=4); s.rect(64, y + 78, 1132 * f, 8, fill=T["gold"], r=4); s.circle(64 + 1132 * f, y + 82, 20, fill=T["text"], stroke=T["gold"], sw=3)
        y += 136
    for lab, on in [("Show quest tracker", True), ("Enemy health bars", False)]:
        s.rect(40, y, 1180, 88, fill=T["s2"], r=12, stroke=T["subtle"]); s.text(64, y + 54, lab, 26, T["text"], bold=True)
        s.rect(1132, y + 26, 64, 36, fill=T["gold"] if on else T["sunken"], r=18, stroke=T["bright"]); s.circle(1178 if on else 1150, y + 44, 14, fill=T["onacc"] if on else T["text2"])
        y += 100
    s.add("</g>")
    s.rect(1228, 228, 8, 300, fill=T["frame"], r=4)
    s.save("09-settings.svg")


def hud_desktop():
    s = Svg("HUD — desktop")
    world(s)
    vitals(s); tracker(s, x=24, y=146); minimap(s, 1104, 76, 152)
    for i, (ic, key) in enumerate([("bag", "I"), ("person", "C"), ("scroll", "J"), ("map", "M"), ("menu", "Esc")]):
        bx = 984 + i * 56
        s.rect(bx, 20, 48, 48, fill=T["plate"], r=10, op=.8, stroke=T["bright"]); icon(s, ic, bx + 12, 32, 24)
    s.text(1256, 16, "", 10)
    slots = [("sword", "1", T["attack"]), ("flame", "2", T["s2"]), ("leaf", "3", T["s2"]), ("ward", "4", T["s2"]), ("flask", "Q", T["s2"]), ("dash", "Space", T["dodge"])]
    total = len(slots) * 72 + 160
    x0 = 640 - total / 2
    for i, (ic, key, fill) in enumerate(slots):
        bx = x0 + i * 72
        s.rect(bx, 712, 64, 64, fill=fill, r=12, stroke=T["bright"], op=.95)
        icon(s, ic, bx + 16, 728, 32)
        kw = len(key) * 9 + 10
        s.rect(bx - 4, 704, kw, 22, fill=T["s1"], r=5, stroke=T["subtle"]); s.text(bx - 4 + kw / 2, 720, key, 14, T["text"], bold=True, anchor="middle")
        if i == 1: s.wedge(bx + 32, 744, 30, .5, "#000", .55)
    ix = x0 + len(slots) * 72
    s.rect(ix, 712, 152, 64, fill=T["s2"], r=32, stroke=T["gold"], sw=3); icon(s, "chat", ix + 18, 730, 28, T["gold"]); s.text(ix + 58, 752, "Talk", 22, T["text"], bold=True)
    s.rect(ix - 4, 704, 20, 22, fill=T["s1"], r=5, stroke=T["subtle"]); s.text(ix + 6, 720, "E", 14, T["text"], bold=True, anchor="middle")
    toast(s, 472, 20, 400, "star", "Level 3", "+1 skill point · open Hero (C)", T["gold"])
    s.text(24, 795, "10 · Desktop HUD — slots 64 lp with keycaps; Desktop type ramp applies in-engine", 16, T["text2"], outline=True)
    s.save("10-hud-desktop.svg")


def title():
    s = Svg("Title — The Lantern Gate")
    world(s)
    s.add('<defs><radialGradient id="vig" cx="62%" cy="45%" r="75%"><stop offset="0" stop-color="#050A0A" stop-opacity="0"/><stop offset="1" stop-color="#050A0A" stop-opacity=".88"/></radialGradient>'
          '<radialGradient id="glow" cx="50%" cy="50%" r="50%"><stop offset="0" stop-color="#9FE3E6" stop-opacity=".35"/><stop offset="1" stop-color="#9FE3E6" stop-opacity="0"/></radialGradient></defs>')
    creature(s, "wayfarer", 820, 380, 4); creature(s, "brute", 960, 360, 4)
    s.rect(0, 0, W, H, fill="url(#vig)")
    s.rect(0, 0, 620, H, fill="#050A0A", op=.45)
    s.add('<ellipse cx="290" cy="150" rx="260" ry="90" fill="url(#glow)"/>')
    s.add(f'<text x="80" y="176" font-family="{HEAD}" font-size="76" font-weight="700" fill="{T["gold"]}" stroke="#0A0F0E" stroke-width="4" paint-order="stroke">Mosslight</text>')
    s.text(84, 214, "A light lies buried beneath the Reach.", 24, T["text2"])
    skin_frame(s, 80, 260, 460, 132, fill=T["s2"])
    s.rect(96, 276, 100, 100, fill=T["sunken"], r=8); creature(s, "wayfarer", 106, 284, 5)
    s.text(212, 306, "Continue", 30, T["gold"], bold=True)
    s.text(212, 338, "Wayfarer · Level 3 · The Sealed Ruin", 20, T["text"])
    s.text(212, 366, "Recover the relic · 2 Nov, 14:32", 20, T["text2"])
    for i, (ic, lab) in enumerate([("star", "New adventure"), ("book", "Load game"), ("gear", "Settings"), ("close", "Quit")]):
        y = 408 + i * 96
        s.rect(80, y, 460, 84, fill=T["s2"], r=12, stroke=T["subtle"], op=.9); icon(s, ic, 104, y + 26, 32); s.text(152, y + 52, lab, 26, T["text"], bold=True)
    s.text(1256, 788, "build 3f9c2ab", 16, T["text2"], anchor="end")
    s.save("11-title.svg")


def codex():
    s = Svg("Book — Journal ▸ Codex")
    world(s, dim=.55); book_frame(s, "Journal")
    segmented(s, 40, 136, ["Quests", "Codex", "Lore"], "Codex", w_each=170)
    x = 580
    for i, (lab, on) in enumerate([("Creatures 3/5", True), ("Places 4", False), ("Items 9", False)]):
        x += chip(s, x, 156, lab, T["gold"] if on else T["text2"], size=20) + 10
    cards = [("prowler", "Mire prowler", "Defeated ×6", None), ("brute", "Ruin brute", "Defeated ×2", None), ("wayfarer", "Reach warden", "Met in Reach", None),
             ("brute", "???", "Not yet encountered", "silhouette"), ("prowler", "???", "Not yet encountered", "silhouette")]
    for i, (kind, name, sub, pal) in enumerate(cards):
        cx, cy = 40 + (i % 3) * 176, 236 + (i // 3) * 264
        sel = i == 1
        s.rect(cx, cy, 164, 248, fill=T["s3"] if sel else T["s2"], r=12, stroke=T["gold"] if sel else T["subtle"], sw=3 if sel else 1.5)
        s.rect(cx + 10, cy + 10, 144, 144, fill=T["sunken"], r=8)
        creature(s, kind, cx + 18, cy + 18, 8, pal)
        s.text(cx + 82, cy + 190, name, 22, T["text"] if pal is None else T["dis"], bold=True, anchor="middle")
        s.text(cx + 82, cy + 222, sub, 18, T["text2"], anchor="middle")
    s.rect(580, 236, 660, 532, fill=T["s2"], r=14, stroke=T["subtle"])
    s.rect(604, 260, 200, 200, fill=T["sunken"], r=10); creature(s, "brute", 612, 268, 11)
    s.text(828, 300, "Ruin brute", 32, T["text"], bold=True, font=HEAD)
    s.text(828, 336, "The Sealed Ruin · deep halls", 22, T["text2"])
    s.text(828, 372, "Defeated ×2 · first seen day 1", 22, T["text2"])
    s.text(604, 512, "Known attacks", 26, T["text"], bold=True); s.rect(604, 522, 612, 2, fill=T["subtle"])
    s.rect(604, 540, 612, 112, fill=T["s1"], r=12, stroke=T["amber"])
    icon(s, "warning", 624, 560, 28, T["amber"]); s.text(668, 582, "Crushing sweep", 24, T["text"], bold=True)
    s.text(668, 614, "Brutes commit to the sweep — step out of the", 22, T["text2"])
    s.text(668, 640, "cone or dodge through it.", 22, T["text2"])
    s.text(604, 700, "Bite · Acid droplet — not yet observed", 22, T["dis"])
    s.save("12-codex.svg")


def trade():
    s = Svg("Book — Pack ▸ Trade (buy and sell)")
    world(s, dim=.55); book_frame(s, "Pack")
    segmented(s, 40, 136, ["Bag", "Craft", "Trade"], "Trade")
    chip(s, 560, 154, "Friendly · 5% off", T["moss"], ic="star", size=20)
    s.text(1240, 182, "You 20 · Warden 80", 24, T["gold"], bold=True, anchor="end")
    def price(p, col=None):
        def f(x, y): chip(s, x - 130, y - 16, f"{p}", col or T["gold"], ic="coin", size=20)
        return f
    s.text(40, 252, "Warden's supplies", 26, T["text"], bold=True); s.rect(40, 262, 590, 2, fill=T["subtle"])
    listrow(s, 40, 276, 590, 88, "coat", "Leather coat", "Defence 4", price(15), selected=True)
    listrow(s, 40, 372, 590, 88, "flask", "Restorative draught", "×5 in stock", price(6))
    listrow(s, 40, 468, 590, 88, "ore", "Bog iron  · Sold by you", "Buy back at sell price", price(1, T["moss"]))
    s.text(650, 252, "Your pack", 26, T["text"], bold=True); s.rect(650, 262, 590, 2, fill=T["subtle"])
    listrow(s, 650, 276, 590, 88, "staff" if "staff" in ICONS else "wood", "Oak staff", "Sell for", price(7))
    listrow(s, 650, 372, 590, 88, "herb", "Silverleaf ×4", "Sell for", price(1))
    listrow(s, 650, 468, 590, 88, "relic", "Reach relic", "Quest item — can't be sold", None, tcol=T["text2"])
    # details strip
    s.rect(40, 572, 1200, 196, fill=T["s2"], r=14, stroke=T["subtle"])
    s.rect(60, 592, 156, 156, fill=T["sunken"], r=10); creature(s, "wayfarer", 74, 606, 8)
    chip(s, 66, 598, "Preview", T["lumen"], size=16)
    s.text(240, 630, "Leather coat", 28, T["text"], bold=True); s.text(240, 662, "Leather · Armour · Common", 22, T["text2"])
    s.text(240, 708, "Defence", 24, T["text2"]); s.text(420, 708, "4", 24, T["text"], bold=True, anchor="end"); s.text(500, 708, "▲ +4", 22, T["moss"], bold=True, anchor="end")
    button(s, 900, 640, 316, 96, "Buy · 15 crowns", "primary", ic="coin")
    s.save("13-trade.svg")


def defeat():
    s = Svg("Fallen — death recap")
    world(s, ruin=True)
    s.rect(0, 0, W, H, fill="#2A2A2A", op=.55)
    skin_frame(s, 240, 60, 800, 680, region="ruin")
    s.text(640, 140, "You have fallen", 48, T["dtext"], bold=True, font=HEAD, anchor="middle")
    s.rect(280, 176, 180, 180, fill=T["sunken"], r=10); creature(s, "brute", 290, 186, 10)
    s.text(488, 222, "Slain by a Ruin brute", 28, T["text"], bold=True)
    s.text(488, 258, "Crushing sweep · 23 damage", 22, T["text2"])
    s.rect(488, 280, 512, 76, fill=T["s1"], r=12, stroke=T["amber"])
    icon(s, "info", 504, 300, 26, T["amber"]); s.text(544, 314, "Brutes commit to the sweep. Step out", 21, T["text"])
    s.text(544, 342, "of the cone or dodge through it.", 21, T["text"])
    for i, (k, v) in enumerate([("Foes defeated", "5"), ("Rooms explored", "7"), ("Time this life", "12 min")]):
        x = 280 + i * 250
        s.text(x, 410, v, 32, T["gold"], bold=True); s.text(x, 440, k, 20, T["text2"])
    button(s, 280, 480, 720, 96, "Load last save · slot 2, 4 min ago", "primary", ic="book")
    button(s, 280, 592, 352, 88, "Recover autosave", "secondary")
    button(s, 648, 592, 352, 88, "New adventure", "secondary")
    s.text(640, 716, "Return to title", 22, T["text2"], anchor="middle")
    s.save("14-defeat.svg")


def quest_complete():
    s = Svg("Ceremony — quest complete")
    world(s)
    vitals(s); minimap(s, 1080, 16, 176)
    for (yy, ic) in [(20, "bag"), (112, "menu")]:
        s.rect(988, yy, 80, 80, fill=T["plate"], r=14, op=.72, stroke=T["bright"]); icon(s, ic, 1012, yy + 18, 40)
    s.circle(1060, 26, 9, fill=T["gold"], stroke=T["s1"])
    skin_frame(s, 380, 150, 520, 230, fill=T["s1"])
    s.text(640, 214, "Quest complete", 40, T["gold"], bold=True, font=HEAD, anchor="middle")
    s.text(640, 252, "The Buried Light", 26, T["text"], bold=True, anchor="middle")
    tile(s, 516, 276, "flask", "3", s=72); tile(s, 604, 276, "coin", "15", s=72, color=T["gold"]); tile(s, 692, 276, "star", None, s=72, color=T["gold"])
    s.text(728, 368, "+5 Wardens", 18, T["moss"], bold=True, anchor="middle")
    # flight arc to the pack button
    s.add(f'<path d="M904 300 Q 960 150 1024 64" fill="none" stroke="{T["gold"]}" stroke-width="3" stroke-dasharray="4 8" stroke-linecap="round"/>')
    for (fx, fy, op) in [(926, 238, .5), (958, 160, .75), (996, 100, 1)]:
        s.circle(fx, fy, 18, fill=T["s2"], stroke=T["gold"], op=op); icon(s, "flask", fx - 12, fy - 12, 24, T["text"], op=op)
    creature(s, "wayfarer", 600, 470, 4)
    s.text(24, 790, "15 · C10 ceremony — non-blocking; rewards fly to the Pack button after 1.8 s", 16, T["text2"], outline=True)
    s.save("15-quest-complete.svg")


if __name__ == "__main__":
    for f in (hud_mobile, book_pack, book_craft, book_hero, book_journal, book_map, dialogue, pause, settings, hud_desktop,
              title, codex, trade, defeat, quest_complete):
        f()
    print("mockups written to", OUT)
