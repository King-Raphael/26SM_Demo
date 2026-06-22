#!/usr/bin/env python3
"""Stylize public-domain darkroom photos/engravings into dim, cool, edge-feathered
backdrop vignettes that match The Darkroom palette.

Pipeline per source: grayscale -> normalize exposure -> dark tone curve ->
cool 3-stop duotone -> crush into the dim backdrop value band -> elliptical
alpha vignette (edges dissolve to nothing) -> grain -> RGBA PNG.

Sources are PD / CC0 (see README credits). Output -> _out/ for review, then the
keepers are copied to Assets/StreamingAssets/art/bd_*.png.
"""
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(__file__)
SRC = os.path.join(HERE, "_src")
OUT = os.path.join(HERE, "_out")
os.makedirs(OUT, exist_ok=True)

# cool, dark, desaturated duotone — shadow cool-black -> mid cool-blue -> muted highlight
STOPS_POS = np.array([0.0, 0.5, 1.0])
STOPS_COL = np.array([(0x0C, 0x0D, 0x14), (0x26, 0x31, 0x4C), (0x96, 0x9F, 0xB6)], float) / 255
HILIGHT = 0.60   # scale so even highlights stay dim (backdrop must recede)


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def duotone(g):
    flat = g.ravel()
    out = np.stack([np.interp(flat, STOPS_POS, STOPS_COL[:, c]) for c in range(3)], axis=1)
    return out.reshape(g.shape + (3,))


def vignette_alpha(h, w, hold=0.72, fade=1.12):
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    d = np.sqrt(((xx - w / 2) / (w * 0.5)) ** 2 + ((yy - h / 2) / (h * 0.5)) ** 2)
    return 1.0 - smoothstep(hold, fade, d)


def stylize(infile, outname, crop=None, gamma=1.35, rng_seed=1, max_w=1600):
    im = Image.open(os.path.join(SRC, infile)).convert("L")
    if crop:  # crop = (l,t,r,b) as fractions
        w, h = im.size
        im = im.crop((int(crop[0] * w), int(crop[1] * h), int(crop[2] * w), int(crop[3] * h)))
    if im.width > max_w:
        im = im.resize((max_w, int(im.height * max_w / im.width)))
    g = np.asarray(im).astype(float) / 255
    lo, hi = np.percentile(g, [2, 98])
    g = np.clip((g - lo) / max(hi - lo, 1e-3), 0, 1)
    g = g ** gamma                                   # push toward the shadows
    rgb = duotone(g) * HILIGHT
    rng = np.random.default_rng(rng_seed)
    rgb = np.clip(rgb + rng.standard_normal(rgb.shape) * 0.012, 0, 1)   # grain
    h, w = g.shape
    a = vignette_alpha(h, w)
    rgba = np.dstack([rgb, a])
    Image.fromarray((rgba * 255).astype("uint8"), "RGBA").save(os.path.join(OUT, outname))
    print("  wrote", outname, (w, h))
    return rgba


JOBS = [
    # infile, outname, crop(l,t,r,b), gamma, seed
    ("cornell_1917.jpg",   "bd_cornell.png",   (0.04, 0.06, 0.98, 0.92), 1.30, 3),
    ("sellwood_1956.jpg",  "bd_sellwood.png",  (0.02, 0.05, 0.98, 0.95), 1.25, 5),
    ("gartenlaube_1894.jpg", "bd_engraving.png", (0.05, 0.04, 0.95, 0.78), 1.20, 7),
    ("jeffcoat_loc.jpg",   "bd_jeffcoat.png",  (0.05, 0.05, 0.95, 0.95), 1.40, 9),
    ("wwii_iwm.jpg",       "bd_wwii.png",      (0.03, 0.04, 0.97, 0.96), 1.30, 11),
]

if __name__ == "__main__":
    print("Stylizing backdrops ->", OUT)
    outs = [(j[1], stylize(*j)) for j in JOBS]

    # preview montage (composited over the game bg, plus a brightened copy)
    from PIL import ImageDraw
    BG = np.array([0x0D, 0x0D, 0x0F]) / 255
    cw, ch, pad, labh = 360, 240, 12, 18
    rows = len(outs)
    sheet = Image.new("RGB", (cw * 2 + pad * 3, rows * (ch + labh + pad) + pad), (20, 20, 24))
    d = ImageDraw.Draw(sheet)

    def comp(rgba, bright=1.0):
        im = Image.fromarray((rgba * 255).astype("uint8"), "RGBA")
        im.thumbnail((cw, ch))
        arr = np.asarray(im).astype(float) / 255
        a = arr[..., 3:4]
        over = np.clip(arr[..., :3] * bright, 0, 1) * a + BG * (1 - a)
        return Image.fromarray((over * 255).astype("uint8"))

    for i, (name, rgba) in enumerate(outs):
        y = pad + i * (ch + labh + pad)
        for j, (lab, br) in enumerate([("over game bg", 1.0), ("brightened x2.4", 2.4)]):
            x = pad + j * (cw + pad)
            thumb = comp(rgba, br)
            sheet.paste(thumb, (x + (cw - thumb.width) // 2, y + labh + (ch - thumb.height) // 2))
            d.text((x, y + 3), f"{name if j == 0 else ''}  {lab}", fill=(205, 205, 215))
    sheet.save(os.path.join(HERE, "_preview_backdrops.png"))
    print("saved _preview_backdrops.png", sheet.size)
