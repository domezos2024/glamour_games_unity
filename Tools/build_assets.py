#!/usr/bin/env python3
"""Erzeugt die Laufzeit-Atlanten fuer Glamour Games (Unity).

  * font_atlas.png / font_atlas.txt  - SDF-Schriftatlas (Selawik, Selawik Bold, PT Serif Bold Italic,
                                       Noto Sans Symbols 2 als Fallback fuer Spielkarten-/Pfeilsymbole)
  * img_atlas.png  / img_atlas.txt   - alle Spielgrafiken aus SourceArt in einem Atlas (mit Alpha-Bleeding)

Die Dateien landen als .bytes in Assets/Resources/Glamour, damit Unity sie unveraendert als TextAsset
importiert (keine Kompression, keine Farbraum-Konvertierung). Aufruf aus dem Repo-Wurzelverzeichnis:

    pip install pillow numpy scipy
    python3 Tools/build_assets.py

Wer neue Sonderzeichen in Texten verwendet, ergaenzt sie in EXTRA und laesst das Skript erneut laufen.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SRC = os.path.join(ROOT, "SourceArt")
OUT = os.path.join(ROOT, "Assets", "Resources", "Glamour")

BASE = 56          # Atlas-Glyphgroesse in Pixeln (em)
SPREAD = 14        # SDF-Reichweite in Atlas-Pixeln (= Glow-Reichweite)
SS = 4             # Supersampling fuer die Distanzberechnung
FONTS = [
    ("Selawik-Regular.ttf", 0),
    ("Selawik-Bold.ttf", 1),
    ("PTSerif-BoldItalic.ttf", 2),
    ("NotoSansSymbols2.ttf", 3),
]
EXTRA = "€–—…„“”‚‘’•×÷½¼¾°±·∞▲▶▼◀►◄★☆♠♣♥♦♤♧♡♢⬆⬇←→↑↓✓✔✗✘♪♫●○■□◆◇♛♚⚡☀☾❤✦✧⚑⚓☺☹"


def charset():
    cs = [chr(c) for c in range(32, 127)] + [chr(c) for c in range(160, 256)]
    for ch in EXTRA:
        if ch not in cs:
            cs.append(ch)
    return cs


def has_glyph(font, ch):
    try:
        mask = font.getmask(ch)
        if mask.size[0] == 0 and ch.strip():
            return False
        # Pillow liefert fuer fehlende Glyphen das .notdef-Rechteck; Vergleich mit garantiert fehlendem Zeichen
        return True
    except Exception:
        return False


def notdef_signature(font):
    m = font.getmask("\U000F0000")
    return (m.size, bytes(m))


def sdf_glyph(font_hi, ch):
    """Rendert eine Glyphe hochaufgeloest und liefert (sdf uint8, bx, by, adv) in Basis-Pixeln."""
    pad_hi = SPREAD * SS
    asc, desc = font_hi.getmetrics()
    left, top, right, bottom = font_hi.getbbox(ch, anchor="ls")
    adv = font_hi.getlength(ch) / SS
    if right <= left or bottom <= top:
        return None, 0, 0, adv
    # Ausrichtung am Basis-Raster: Glyph-Box auf Vielfache von SS erweitern
    left = int(np.floor(left / SS)) * SS
    top = int(np.floor(top / SS)) * SS
    right = int(np.ceil(right / SS)) * SS
    bottom = int(np.ceil(bottom / SS)) * SS
    w = right - left + 2 * pad_hi
    h = bottom - top + 2 * pad_hi
    img = Image.new("L", (w, h), 0)
    ImageDraw.Draw(img).text((pad_hi - left, pad_hi - top), ch, font=font_hi, fill=255, anchor="ls")
    a = np.asarray(img).astype(np.float32) / 255.0
    inside = a > 0.5
    d_out = ndimage.distance_transform_edt(~inside)
    d_in = ndimage.distance_transform_edt(inside)
    sd = (d_out - d_in) / SS  # positiv = aussen, in Basis-Pixeln
    hb, wb = h // SS, w // SS
    sd = sd[: hb * SS, : wb * SS].reshape(hb, SS, wb, SS).mean(axis=(1, 3))
    v = np.clip(0.5 - sd / (2.0 * SPREAD), 0.0, 1.0)
    bx = left / SS - SPREAD
    by = top / SS - SPREAD
    return (v * 255 + 0.5).astype(np.uint8), bx, by, adv


def pack(items, width):
    """Einfaches Shelf-Packing. items: Liste (key, w, h). Liefert dict key->(x,y) und Hoehe."""
    order = sorted(items, key=lambda it: -it[2])
    x = y = shelf = 0
    pos = {}
    for key, w, h in order:
        if x + w > width:
            x = 0
            y += shelf + 1
            shelf = 0
        pos[key] = (x, y)
        x += w + 1
        shelf = max(shelf, h)
    return pos, y + shelf + 1


def build_font():
    chars = charset()
    glyphs = []  # (fi, cp, arr, bx, by, adv)
    metrics = []
    primary_have = {}
    for fi, (fname, _) in enumerate(FONTS):
        path = os.path.join(SRC, "Fonts", fname)
        f_hi = ImageFont.truetype(path, BASE * SS)
        asc, desc = f_hi.getmetrics()
        metrics.append((fi, asc / SS, desc / SS))
        nd = notdef_signature(f_hi)
        for ch in chars:
            m = f_hi.getmask(ch)
            if (m.size, bytes(m)) == nd and ch != " ":
                continue
            if fi == 3:
                # Symbolschrift nur fuer Zeichen, die in keiner Textschrift existieren
                if any(ch in primary_have.get(k, set()) for k in range(3)):
                    continue
            arr, bx, by, adv = sdf_glyph(f_hi, ch)
            primary_have.setdefault(fi, set()).add(ch)
            glyphs.append((fi, ord(ch), arr, bx, by, adv))
    items = [(i, (g[2].shape[1] if g[2] is not None else 0), (g[2].shape[0] if g[2] is not None else 0)) for i, g in enumerate(glyphs)]
    width = 2048
    pos, height = pack([it for it in items if it[1] > 0], width)
    height = 1 << int(np.ceil(np.log2(max(height, 64))))
    atlas = np.zeros((height, width), np.uint8)
    lines = [f"atlas {width} {height} {BASE} {SPREAD}"]
    for fi, asc, desc in metrics:
        lines.append(f"font {fi} {asc:.3f} {desc:.3f}")
    for i, (fi, cp, arr, bx, by, adv) in enumerate(glyphs):
        if arr is None:
            lines.append(f"g {fi} {cp} {adv:.3f} 0 0 0 0 0 0")
            continue
        x, y = pos[i]
        h, w = arr.shape
        atlas[y:y + h, x:x + w] = arr
        lines.append(f"g {fi} {cp} {adv:.3f} {x} {y} {w} {h} {bx:.3f} {by:.3f}")
    Image.fromarray(atlas, "L").save(os.path.join(OUT, "font_atlas.png.bytes"), "PNG", optimize=True)
    with open(os.path.join(OUT, "font_atlas.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print(f"font atlas {width}x{height}, {len(glyphs)} glyphs")


def bleed(im):
    a = np.asarray(im).copy()
    alpha = a[:, :, 3]
    if alpha.min() == 255:
        return im
    solid = alpha > 8
    if not solid.any():
        return im
    _, (iy, ix) = ndimage.distance_transform_edt(~solid, return_indices=True)
    rgb = a[:, :, :3][iy, ix]
    out = a.copy()
    mask = ~solid
    out[:, :, :3][mask] = rgb[mask]
    return Image.fromarray(out, "RGBA")


def build_images():
    names = []
    for fn in sorted(os.listdir(SRC)):
        base, ext = os.path.splitext(fn)
        if ext.lower() not in (".png", ".jpg", ".webp") or base == "icon":
            continue
        names.append((base, os.path.join(SRC, fn)))
    imgs = {}
    for base, path in names:
        im = Image.open(path).convert("RGBA")
        if max(im.size) > 512:
            im.thumbnail((512, 512), Image.LANCZOS)
        imgs[base] = bleed(im)
    PAD = 4
    items = [(k, im.size[0] + 2 * PAD, im.size[1] + 2 * PAD) for k, im in imgs.items()]
    width = 2048
    pos, height = pack(items, width)
    height = 1 << int(np.ceil(np.log2(max(height, 64))))
    atlas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    lines = [f"atlas {width} {height}"]
    for k, im in imgs.items():
        x, y = pos[k]
        w, h = im.size
        # Rand extrudieren, damit bilineares Filtern keine Nachbarn einblutet
        ext = Image.new("RGBA", (w + 2 * PAD, h + 2 * PAD))
        ext.paste(im.resize((w + 2 * PAD, h + 2 * PAD), Image.NEAREST), (0, 0))
        ext.paste(im, (PAD, PAD))
        atlas.paste(ext, (x, y))
        lines.append(f"img {k} {x + PAD} {y + PAD} {w} {h}")
    atlas.save(os.path.join(OUT, "img_atlas.png.bytes"), "PNG", optimize=True)
    with open(os.path.join(OUT, "img_atlas.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print(f"image atlas {width}x{height}, {len(imgs)} images")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    build_font()
    build_images()
    sys.exit(0)
