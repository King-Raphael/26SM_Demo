#!/usr/bin/env python3
"""Post-process the student's AI-generated darkroom art to drop straight into the game.

- BACKDROPS (bd_*): already dark + palette-fit; just feather the hard rectangle
  edges to alpha so they dissolve into the surrounding black on the parallax layer.
- PROPS (prop_*): generated on a flat ~#787878 gray field. Flood-fill chroma-key
  that gray to transparent (border-connected only, so interior greys survive),
  erode+feather the matte, desaturate + faint-cool tint, autocrop to content.

Raw generations are backed up to _aigen_raw/ before overwriting in StreamingAssets/art/.
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(__file__)
ART = os.path.normpath(os.path.join(HERE, "..", "Assets", "StreamingAssets", "art"))
RAW = os.path.join(HERE, "_aigen_raw")
os.makedirs(RAW, exist_ok=True)

BACKDROPS = ["bd_drying_line", "bd_wet_bench", "bd_enlarger_row",
             "bd_chem_shelf", "bd_window_shaft", "bd_safelight_corner"]
PROPS = ["prop_enlarger", "prop_bottles", "prop_hanging_prints",
         "prop_tray_stack", "prop_reel_tank", "prop_clock", "prop_cables"]


def backup(n):
    dst = os.path.join(RAW, n + ".png")
    if not os.path.exists(dst):
        Image.open(os.path.join(ART, n + ".png")).save(dst)


def feather_rect(h, w, margin=0.12):
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    dx = np.minimum(xx, w - 1 - xx) / (w * margin)
    dy = np.minimum(yy, h - 1 - yy) / (h * margin)
    return np.clip(np.minimum(dx, dy), 0, 1)  # 0 at edge -> 1 inside the margin


def process_backdrop(n, rng):
    backup(n)
    im = Image.open(os.path.join(RAW, n + ".png")).convert("RGB")   # pristine src -> idempotent re-runs
    arr = np.asarray(im).astype(float) / 255
    g = arr.mean(2, keepdims=True)
    arr = arr * 0.80 + g * 0.20                          # gentle desaturate (keep safelight warmth)
    arr = np.clip(arr + rng.standard_normal(arr.shape) * 0.006, 0, 1)  # grain
    h, w = arr.shape[:2]
    a = feather_rect(h, w, 0.12)
    Image.fromarray((np.dstack([arr, a]) * 255).astype("uint8"), "RGBA").save(
        os.path.join(ART, n + ".png"))
    print("  backdrop", n, (w, h))


def process_prop(n):
    backup(n)
    im = Image.open(os.path.join(ART, n + ".png")).convert("RGB")
    w, h = im.size
    ff = im.copy()
    SENT = (255, 0, 255)
    seeds = [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1),
             (w // 2, 0), (w // 2, h - 1), (0, h // 2), (w - 1, h // 2)]
    for s in seeds:
        ImageDraw.floodfill(ff, s, SENT, thresh=30)     # eat the connected gray field
    bg = np.all(np.asarray(ff) == np.array(SENT), axis=2)
    al = Image.fromarray(((~bg).astype(np.uint8) * 255), "L")
    al = al.filter(ImageFilter.MinFilter(3))            # erode 1px -> kill the gray fringe
    al = al.filter(ImageFilter.GaussianBlur(1.3))       # soft matte

    arr = np.asarray(im).astype(float) / 255
    g = arr.mean(2, keepdims=True)
    arr = arr * 0.60 + g * 0.40                          # desaturate ~40%
    arr = np.clip(arr * np.array([0.97, 0.99, 1.05]), 0, 1)  # faint cool

    rgba = np.dstack([arr, np.asarray(al).astype(float) / 255])
    out = Image.fromarray((rgba * 255).astype("uint8"), "RGBA")
    bbox = al.getbbox()                                  # crop to the matte, not the RGB
    if bbox:
        l, t, r, b = bbox
        pad = 10
        out = out.crop((max(0, l - pad), max(0, t - pad), min(w, r + pad), min(h, b + pad)))
    out.save(os.path.join(ART, n + ".png"))
    print("  prop    ", n, "->", out.size)


def process_mid(n):
    """Hanging mid-ground clutter: get an alpha matte (use existing transparency, else
    chroma-key the flat gray field), heavy-desaturate + DARKEN to a dim silhouette-with-detail
    (it lives on the dark mid-ground depth layer). Reads from the pristine _aigen_raw/ backup
    so re-runs are idempotent (no double-darkening)."""
    backup(n)
    raw = Image.open(os.path.join(RAW, n + ".png"))   # pristine source
    has_alpha = (raw.mode in ("RGBA", "LA")) and (np.asarray(raw.convert("RGBA"))[..., 3].min() < 250)
    im = raw.convert("RGB")
    w, h = im.size
    if has_alpha:
        al = Image.fromarray(np.asarray(raw.convert("RGBA"))[..., 3].copy(), "L")
    else:
        ff = im.copy()
        SENT = (255, 0, 255)
        for s in [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1),
                  (w // 2, 0), (w // 2, h - 1), (0, h // 2), (w - 1, h // 2)]:
            ImageDraw.floodfill(ff, s, SENT, thresh=30)
        bg = np.all(np.asarray(ff) == np.array(SENT), axis=2)
        al = Image.fromarray(((~bg).astype(np.uint8) * 255), "L")
    al = al.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(1.2))

    arr = np.asarray(im).astype(float) / 255
    g = arr.mean(2, keepdims=True)
    # dim + partly-desaturated, but keep enough tone/detail that the AI richness reads
    # (it's a mid-ground depth layer, not a flat silhouette). Darken factor is the main knob.
    arr = np.clip((arr * 0.45 + g * 0.55) * np.array([0.96, 0.98, 1.05]) * 0.64, 0, 1)  # desat+darken+cool

    rgba = np.dstack([arr, np.asarray(al).astype(float) / 255])
    out = Image.fromarray((rgba * 255).astype("uint8"), "RGBA")
    bbox = al.getbbox()
    if bbox:
        l, t, r, b = bbox
        pad = 8
        out = out.crop((max(0, l - pad), max(0, t - pad), min(w, r + pad), min(h, b + pad)))
    out.save(os.path.join(ART, n + ".png"))
    print("  mid     ", n, "->", out.size)


def montage():
    import glob
    BG = np.array([0x10, 0x10, 0x13]) / 255
    items = [os.path.splitext(os.path.basename(p))[0] for p in
             sorted(glob.glob(os.path.join(ART, "bd_*.png")) + glob.glob(os.path.join(ART, "mid_*.png")))]
    cols, cw, ch, pad, labh = 4, 300, 200, 8, 16
    import math
    R = math.ceil(len(items) / cols)
    sheet = Image.new("RGB", (cols * (cw + pad) + pad, R * (ch + labh + pad) + pad), (22, 22, 26))
    d = ImageDraw.Draw(sheet)
    for i, n in enumerate(items):
        im = Image.open(os.path.join(ART, n + ".png")).convert("RGBA")
        im.thumbnail((cw, ch))
        a = np.asarray(im).astype(float) / 255
        comp = a[..., :3] * a[..., 3:4] + BG * (1 - a[..., 3:4])
        t = Image.fromarray((comp * 255).astype("uint8"))
        r, c = divmod(i, cols)
        x, y = pad + c * (cw + pad), pad + r * (ch + labh + pad)
        sheet.paste(t, (x + (cw - t.width) // 2, y + labh + (ch - t.height) // 2))
        d.text((x, y + 2), n, fill=(205, 205, 215))
    sheet.save(os.path.join(HERE, "_preview_processed.png"))
    print("saved _preview_processed.png", sheet.size)


if __name__ == "__main__":
    import glob
    print("Processing AI art in", ART)
    rng = np.random.default_rng(13)
    # backdrops — only those still present (re-runs are idempotent: read from _aigen_raw/)
    for n in BACKDROPS:
        if os.path.exists(os.path.join(ART, n + ".png")):
            process_backdrop(n, rng)
    # mid-ground clutter — auto-detect whatever mid_*.png was dropped in
    for p in sorted(glob.glob(os.path.join(ART, "mid_*.png"))):
        process_mid(os.path.splitext(os.path.basename(p))[0])
    # (props were retired from the scene; process_prop() kept for reference only)
    montage()
