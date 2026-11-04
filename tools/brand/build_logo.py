"""
Genera il logo vettoriale di CardExchange (palette del sito, senza oro).

Uscite:
  card-exchange-frontend/public/brand/*.svg      file da usare ovunque (sito, stampa, social)
  card-exchange-frontend/public/icon.svg         icona dell'app (favicon)
  card-exchange-frontend/src/components/brand/logoData.ts   markup per il componente React

La scritta è convertita in tracciati (font Inter Display Black Italic, lo stesso
del sito), quindi i file non dipendono da font installati.

Uso:  python3 tools/brand/build_logo.py [percorso-font]
"""
import math
import os
import sys

import uharfbuzz as hb
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FRONT = os.path.join(ROOT, "card-exchange-frontend")
FONT = sys.argv[1] if len(sys.argv) > 1 else "/usr/share/fonts/opentype/inter/InterDisplay-BlackItalic.otf"

# Palette del sito (index.css) + un azzurro luminoso derivato dal primary-light per l'accento
NAVY = "#1a3461"        # --color-primary
NAVY_DARK = "#122548"   # --color-primary-dark
BLUE = "#2a4d8f"        # --color-primary-light
AZURE = "#3d7be0"       # accento: primary-light schiarito e saturato
AZURE_LIGHT = "#8db8ff"
ICE = "#dde4ee"         # --color-border
WHITE = "#ffffff"


def fmt(v):
    return f"{v:.2f}".rstrip("0").rstrip(".")


def star(cx, cy, r, pinch=0.18):
    """Stella a 4 punte con lati concavi."""
    q = r * pinch
    return (f"M{fmt(cx)} {fmt(cy - r)}"
            f"Q{fmt(cx + q)} {fmt(cy - q)} {fmt(cx + r)} {fmt(cy)}"
            f"Q{fmt(cx + q)} {fmt(cy + q)} {fmt(cx)} {fmt(cy + r)}"
            f"Q{fmt(cx - q)} {fmt(cy + q)} {fmt(cx - r)} {fmt(cy)}"
            f"Q{fmt(cx - q)} {fmt(cy - q)} {fmt(cx)} {fmt(cy - r)}Z")


def crescent(cx, cy, rx, ry_outer, ry_inner, angle_deg, start_deg, end_deg, steps=48):
    """Nastro a mezzaluna (si assottiglia alle estremità) lungo un'ellisse ruotata."""
    a = math.radians(angle_deg)
    ca, sa = math.cos(a), math.sin(a)

    def pt(t, ry):
        x, y = rx * math.cos(t), ry * math.sin(t)
        return cx + x * ca - y * sa, cy + x * sa + y * ca

    ts = [math.radians(start_deg + (end_deg - start_deg) * i / steps) for i in range(steps + 1)]
    outer = [pt(t, ry_outer) for t in ts]
    inner = [pt(t, ry_inner) for t in reversed(ts)]
    pts = outer + inner
    return "M" + "L".join(f"{fmt(x)} {fmt(y)}" for x, y in pts) + "Z"


# ---------------------------------------------------------------- marchio (box 200x200)

def mark(theme, idp):
    """Due carte inclinate, orbita e scintille. theme: 'light' (su chiaro) | 'dark' (su blu)."""
    dark = theme == "dark"
    card_stroke = WHITE if dark else NAVY_DARK
    inset = AZURE_LIGHT if dark else AZURE
    orbit_a, orbit_b = (AZURE_LIGHT, WHITE) if dark else (BLUE, AZURE)
    spark = WHITE if dark else AZURE

    defs = f"""<defs>
<linearGradient id="{idp}card" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{BLUE}"/><stop offset="1" stop-color="{NAVY_DARK}"/></linearGradient>
<linearGradient id="{idp}card2" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#2f5aa6"/><stop offset="1" stop-color="{NAVY}"/></linearGradient>
<radialGradient id="{idp}star" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="{WHITE}"/><stop offset="1" stop-color="{AZURE_LIGHT}"/></radialGradient>
<linearGradient id="{idp}orbit" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="{orbit_a}" stop-opacity="0.9"/><stop offset="0.55" stop-color="{orbit_b}"/><stop offset="1" stop-color="{orbit_a}"/></linearGradient>
</defs>"""

    def card(tx, ty, rot, fill, deco):
        return (f'<g transform="translate({tx} {ty}) rotate({rot})">'
                f'<rect x="-36" y="-52" width="72" height="104" rx="9" fill="url(#{fill})" stroke="{card_stroke}" stroke-width="4"/>'
                f'<rect x="-29" y="-45" width="58" height="90" rx="5" fill="none" stroke="{inset}" stroke-width="2.2" opacity="0.9"/>'
                f'{deco}</g>')

    # Carta dietro: un arco decorativo appena accennato
    back_deco = f'<path d="M-14 -16a20 20 0 1 0 22 24" fill="none" stroke="{AZURE_LIGHT}" stroke-width="5" stroke-linecap="round" opacity="0.45"/>'
    # Carta davanti: stella luminosa con alone
    front_deco = (f'<circle r="21" fill="none" stroke="{AZURE_LIGHT}" stroke-width="1.6" opacity="0.5"/>'
                  f'<path d="{star(0, 0, 19, 0.16)}" fill="url(#{idp}star)"/>')

    # Orbita: parte posteriore (dietro le carte) sottile, parte anteriore piena
    orbit_back = crescent(100, 112, 92, 30, 27, -20, 190, 345)
    orbit_front = crescent(100, 112, 92, 30, 21, -20, 0, 175)

    return (defs
            + f'<path d="{orbit_back}" fill="url(#{idp}orbit)" opacity="0.55"/>'
            + card(70, 100, -17, f"{idp}card", back_deco)
            + card(126, 92, 12, f"{idp}card2", front_deco)
            + f'<path d="{orbit_front}" fill="url(#{idp}orbit)"/>'
            + f'<path d="{star(36, 40, 11)}" fill="{spark}"/>'
            + f'<path d="{star(170, 150, 8)}" fill="{spark}"/>'
            + f'<path d="{star(178, 52, 5)}" fill="{spark}" opacity="0.7"/>')


# ---------------------------------------------------------------- scritta in tracciati

def shape_text(text, font_path):
    blob = hb.Blob.from_file_path(font_path)
    face = hb.Face(blob)
    font = hb.Font(face)
    buf = hb.Buffer()
    buf.add_str(text)
    buf.guess_segment_properties()
    hb.shape(font, buf, {"kern": True, "liga": True})
    return buf.glyph_infos, buf.glyph_positions, face.upem


def text_paths(parts, font_path, size, tracking=-0.01):
    """parts: [(testo, chiave)] -> ({chiave: path}, larghezza, ascent, descent) a dimensione size, baseline y=0."""
    tt = TTFont(font_path)
    gs = tt.getGlyphSet()
    order = tt.getGlyphOrder()
    full = "".join(p for p, _ in parts)
    infos, poss, upem = shape_text(full, font_path)
    scale = size / upem
    x = 0.0
    out = {k: "" for _, k in parts}
    # mappa cluster (indice carattere) -> chiave
    owners = []
    for p, k in parts:
        owners += [k] * len(p)
    for info, pos in zip(infos, poss):
        name = order[info.codepoint]
        pen = SVGPathPen(gs, ntos=fmt)
        tpen = TransformPen(pen, (scale, 0, 0, -scale, x + pos.x_offset * scale, -pos.y_offset * scale))
        gs[name].draw(tpen)
        out[owners[info.cluster]] += pen.getCommands()
        x += pos.x_advance * scale + tracking * size
    os2 = tt["OS/2"]
    return out, x - tracking * size, os2.sCapHeight * scale, 0


def wordmark(theme, idp, size):
    dark = theme == "dark"
    paths, width, cap, _ = text_paths([("Card", "a"), ("Exchange", "b")], FONT, size)
    a_fill = WHITE if dark else NAVY
    g1, g2 = (AZURE_LIGHT, "#b9d4ff") if dark else (BLUE, AZURE)
    under = AZURE_LIGHT if dark else NAVY
    # sottolineatura a mezzaluna sotto la scritta
    u_cx, u_rx = width * 0.52, width * 0.44
    u = crescent(u_cx, size * 0.2, u_rx, size * 0.15, size * 0.065, 0, 20, 160)
    markup = (f'<defs><linearGradient id="{idp}word" x1="0" y1="0" x2="1" y2="0">'
              f'<stop offset="0" stop-color="{g1}"/><stop offset="1" stop-color="{g2}"/></linearGradient></defs>'
              f'<path d="{paths["a"]}" fill="{a_fill}"/>'
              f'<path d="{paths["b"]}" fill="url(#{idp}word)"/>'
              f'<path d="{u}" fill="{under}" opacity="{0.55 if dark else 0.9}"/>')
    return markup, width, cap


# ---------------------------------------------------------------- composizioni

def compose(layout, theme, idp="ce-"):
    if layout == "mark":
        return 200, 200, mark(theme, idp)
    if layout == "horizontal":
        size = 92
        wm, w, cap = wordmark(theme, idp, size)
        # marchio 200 alto, scritta centrata verticalmente sul marchio
        tx, ty = 214, 100 + cap / 2
        width = tx + w + 12
        return width, 200, (mark(theme, idp)
                            + f'<g transform="translate({fmt(tx)} {fmt(ty)})">{wm}</g>')
    if layout == "stacked":
        size = 96
        wm, w, cap = wordmark(theme, idp, size)
        k = 1.45  # marchio più grande sopra la scritta
        width = max(w + 40, 200 * k)
        mx = (width - 200 * k) / 2
        top = 200 * k
        return width, top + cap + 54, (f'<g transform="translate({fmt(mx)} 0) scale({k})">{mark(theme, idp)}</g>'
                                       + f'<g transform="translate({fmt((width - w) / 2)} {fmt(top + cap + 4)})">{wm}</g>')
    raise ValueError(layout)


def svg_doc(w, h, inner, title="CardExchange", bg=None):
    rect = f'<rect width="{fmt(w)}" height="{fmt(h)}" fill="{bg}"/>' if bg else ""
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {fmt(w)} {fmt(h)}" role="img" aria-label="{title}">'
            f'<title>{title}</title>{rect}{inner}</svg>\n')


def app_icon(maskable=False):
    """Icona dell'app: marchio chiaro su fondo blu (per favicon, PWA, home del telefono)."""
    pad = 0.66 if maskable else 0.86
    s = 512 * pad / 200
    off = (512 - 200 * s) / 2
    radius = 0 if maskable else 112
    bg = (f'<defs><linearGradient id="ce-bg" x1="0" y1="0" x2="1" y2="1">'
          f'<stop offset="0" stop-color="{BLUE}"/><stop offset="1" stop-color="{NAVY_DARK}"/></linearGradient></defs>'
          f'<rect width="512" height="512" rx="{radius}" fill="url(#ce-bg)"/>')
    return svg_doc(512, 512, bg + f'<g transform="translate({fmt(off)} {fmt(off + 6)}) scale({fmt(s)})">{mark("dark", "ce-")}</g>')


def main():
    out_dir = os.path.join(FRONT, "public", "brand")
    os.makedirs(out_dir, exist_ok=True)
    files = {
        "logo-mark.svg": ("mark", "light"),
        "logo-mark-inverse.svg": ("mark", "dark"),
        "logo-horizontal.svg": ("horizontal", "light"),
        "logo-horizontal-inverse.svg": ("horizontal", "dark"),
        "logo-stacked.svg": ("stacked", "light"),
        "logo-stacked-inverse.svg": ("stacked", "dark"),
    }
    for name, (layout, theme) in files.items():
        w, h, inner = compose(layout, theme)
        with open(os.path.join(out_dir, name), "w") as f:
            f.write(svg_doc(w, h, inner))
    with open(os.path.join(FRONT, "public", "icon.svg"), "w") as f:
        f.write(app_icon())
    with open(os.path.join(out_dir, "icon-maskable.svg"), "w") as f:
        f.write(app_icon(maskable=True))

    # Dati per il componente React: "__ID__" viene sostituito con un id univoco per istanza
    ts = ["// File generato da tools/brand/build_logo.py: non modificare a mano.",
          "export type LogoLayout = 'mark' | 'horizontal' | 'stacked';",
          "export type LogoTheme = 'light' | 'dark';",
          "export const LOGO_DATA: Record<LogoLayout, Record<LogoTheme, { w: number; h: number; svg: string }>> = {"]
    for layout in ("mark", "horizontal", "stacked"):
        ts.append(f"  {layout}: {{")
        for theme in ("light", "dark"):
            w, h, inner = compose(layout, theme, "__ID__")
            esc = inner.replace("\\", "\\\\").replace("`", "\\`")
            ts.append(f"    {theme}: {{ w: {fmt(w)}, h: {fmt(h)}, svg: `{esc}` }},")
        ts.append("  },")
    ts.append("};")
    comp_dir = os.path.join(FRONT, "src", "components", "brand")
    os.makedirs(comp_dir, exist_ok=True)
    with open(os.path.join(comp_dir, "logoData.ts"), "w") as f:
        f.write("\n".join(ts) + "\n")
    print("ok")


if __name__ == "__main__":
    main()
