#!/usr/bin/env python3
"""Generate seamless, palette-fit dark wall textures for The Darkroom.

Replaces the clashing red-brick photo with cool, desaturated darkroom-wall
materials. All textures are SEAMLESS (periodic FFT noise) so they tile cleanly
at 512 ppu (1024px -> 2 world units), matching the existing loader.

Output: Assets/StreamingAssets/art/wall_*.png
"""
import os
import numpy as np
from PIL import Image

OUT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..",
                                    "Assets", "StreamingAssets", "art"))
os.makedirs(OUT, exist_ok=True)
RES = 1024


def periodic_blur(x, sigma):
    """Isotropic Gaussian blur with wrap-around (keeps the result seamless)."""
    f = np.fft.fftfreq(x.shape[0])
    fx, fy = np.meshgrid(f, f)
    h = np.exp(-2.0 * (np.pi * sigma) ** 2 * (fx ** 2 + fy ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(x) * h))


def norm(x):
    x = x - x.min()
    m = x.max()
    return x / m if m > 0 else x


def seamless(rng, res, sigma):
    return norm(periodic_blur(rng.standard_normal((res, res)), sigma))


def fbm(rng, res, sigmas, weights):
    acc = np.zeros((res, res))
    for s, w in zip(sigmas, weights):
        acc += w * seamless(rng, res, s)
    return norm(acc)


def tint(v, color, grain=0.012, rng=None):
    """value field (HxW, 0..1) -> RGB float with a cool tint + fine grain."""
    rgb = v[..., None] * np.array(color)[None, None, :]
    if rng is not None and grain > 0:
        rgb = rgb + (rng.standard_normal(rgb.shape) * grain)
    return np.clip(rgb, 0, 1)


def save(arr, name):
    Image.fromarray((np.clip(arr, 0, 1) * 255).astype("uint8")).save(
        os.path.join(OUT, name))
    print("  wrote", name)


def wall_plaster():
    rng = np.random.default_rng(7)
    base = fbm(rng, RES, [44, 16, 6, 2.5], [0.50, 0.26, 0.15, 0.09])
    blotch = seamless(rng, RES, 70)                 # large soft tonal drift
    v = 0.085 + base * 0.085 + (blotch - 0.5) * 0.045
    # faint vertical grime runs
    streak = norm(periodic_blur(rng.standard_normal((RES, RES)), 3.0))
    streak_mask = norm(periodic_blur(rng.standard_normal((1, RES)).repeat(RES, 0), 6))
    v = v - (streak * streak_mask) * 0.025
    rgb = tint(np.clip(v, 0, 1), (0.90, 0.96, 1.08), rng=rng)   # cool plaster
    save(rgb, "wall_plaster.png")


def wall_concrete():
    rng = np.random.default_rng(23)
    base = fbm(rng, RES, [40, 14, 5, 2.0], [0.46, 0.27, 0.17, 0.10])
    v = 0.10 + base * 0.075
    # horizontal pour seams every ~256px (wraps cleanly: 1024/256 = 4)
    y = np.arange(RES)[:, None].repeat(RES, 1)
    seam = np.exp(-((y % 256) - 4) ** 2 / 9.0) + np.exp(-((y % 256) - 252) ** 2 / 9.0)
    v = v - seam * 0.035
    rgb = tint(np.clip(v, 0, 1), (0.95, 0.99, 1.04), rng=rng)   # near-neutral, faint cool
    save(rgb, "wall_concrete.png")


def wall_panel():
    rng = np.random.default_rng(51)
    # vertical wood grain: low horizontal freq, high vertical freq
    grain = norm(periodic_blur(rng.standard_normal((RES, RES)), 1.2))
    grain = norm(grain + 0.6 * norm(periodic_blur(rng.standard_normal((RES, RES)), 5)))
    v = 0.075 + grain * 0.07
    # plank seams every 128px (8 planks) — dark grooves + a thin highlight edge
    x = np.arange(RES)[None, :].repeat(RES, 0)
    groove = np.exp(-((x % 128) - 2) ** 2 / 4.0)
    edge = np.exp(-((x % 128) - 6) ** 2 / 6.0)
    v = v - groove * 0.06 + edge * 0.02
    rgb = tint(np.clip(v, 0, 1), (1.02, 0.98, 0.95), rng=rng)   # faintly warm dark wood
    save(rgb, "wall_panel.png")


if __name__ == "__main__":
    print("Generating walls ->", OUT)
    wall_plaster()
    wall_concrete()
    wall_panel()
    print("done.")
