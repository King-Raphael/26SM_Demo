# The Darkroom — Illustrated Environment Art Direction

The goal: replace the flat code-box backdrop and the clashing red-brick photo wall with a
cohesive, illustrated darkroom environment. Everything must read as **one room, one light**.

## North star

A dim photographic darkroom, lit almost entirely by a few safelights and the light *you* make.
Cinematic, painterly, desaturated, chiaroscuro. The scene is mostly black; detail emerges from
shadow. Nothing saturated, nothing sunny, nothing photoreal that fights the stylization.

## Palette (locked — pulled from `VisualFactory`)

| Role | Hex | Use |
|---|---|---|
| Background base | `#0D0D0F` | the void behind everything |
| Warm-black | `#131010` | far-right "developing" drift |
| Neutral gray | `#6E6E6E` | static ground, mid props |
| Cold shadow blue | `#3A4A8C` → `#9FD8E6` | the **Underexposed** glow; cold rim light |
| Warm cream | `#FFF3D6` | highlights, bulbs, the **Overexposed** light |
| Safelight red | `#8B1A1A` | the only real "color" — safelight lamps, exit |
| Violet | `#2A2A30` | Umbra accents |

Rule of thumb: assets live in the **0.04–0.35 value band**, cool by default, with sparse warm-cream
and one safelight-red accent. The exposure system tints the whole frame per state (cold blue / neutral /
warm), so assets should be near-neutral and let the global grade color them.

## Depth layers (back → front)

| Layer | Parallax factor | Sort order | Content | Source track |
|---|---|---|---|---|
| **Backdrop-far** | ~0.15 | -20 | painterly hero vignettes (drying lines, wet bench, shelves, window shaft) | **AI-gen** |
| **Backdrop-near** | ~0.30 | -14 | illustrated props (enlarger, bottle shelf, hanging prints, tanks) | **AI-gen / code** |
| **Lamps** | 0 (world) | -5 | hanging cone lamps + real Light2D (already exists) | code (keep) |
| *(play space)* | 1.0 | 10–50 | gameplay — untouched | — |
| **Foreground** | ~1.3 | 60 | near-black out-of-focus framing (print edges, an enlarger arm, cables) | **code (silhouette)** |
| **Atmosphere** | varies | 55–65 | dust motes, light shafts, grain, safelight halos | **code** |

Gameplay elements, colliders, sizes and the solidity matrix are **byte-for-byte untouched**. This pass
is purely the non-colliding scenery layers (`BackdropBuilder`) + the wall *texture* swap (`PixelArt`).

## Sizing / PPU conventions

- **Wall materials** (tileable): loaded at **512 ppu** (a 1024px tile spans 2 world units), `wrapMode=Repeat`,
  drawn `Tiled`. Same as today's `bricks.jpg`/`concrete.jpg`. Source ≥1024², seamless.
- **Hero backdrops** (far): source ~**2048×1024**, placed at ppu ~100 → ~20×10 world units, very dark,
  detail concentrated center, edges fading to near-black so neighbors blend. Horizontal-tileable a plus.
- **Props** (cutouts, transparent PNG): source tall side ~**1024px**, placed at the prop's world height
  (enlarger ~3u, bottle shelf ~1.5u …). Generate centered on a flat uniform mid-gray `#808080` field for
  easy chroma-key, OR deliver a transparent PNG directly.

## Asset inventory & source track

### A. Wall / surface materials → **CC0 web** (research workflow running)
Replace `bricks.jpg` (reject — saturated red) and optionally recolor `concrete.jpg` cooler.
Targets: dark cool plaster, aged painted concrete, dark wood paneling, dark subway tile.
Drop seamless 1K diffuse JPGs into `Assets/StreamingAssets/`. Builder tints them dark + cool.

### B. Hero backdrops → **AI-gen** (prompts in `IMAGE_GEN_PROMPTS.md`)
`bd_drying_line`, `bd_wet_bench`, `bd_enlarger_row`, `bd_chem_shelf`, `bd_window_shaft`, `bd_safelight_corner`.

### C. Illustrated props → **AI-gen + code**
`prop_enlarger`, `prop_bottles`, `prop_hanging_prints`, `prop_tray_stack`, `prop_reel_tank`,
`prop_clock`, `prop_cables`. (Simpler ones — clock, cables, single bottles — fall back to code.)

### D. Foreground silhouettes → **code**
Near-black blurred print edges, an enlarger arm jutting in from a corner, drooping cables/wires.

### E. Atmosphere → **code**
Volumetric light shafts (cold from the window, warm from lamps), drifting dust + emulsion grain,
safelight halos. Extends existing `Drift` / `ProcGfx`.

## Integration plan (graceful fallback)

All new PNGs live in `Assets/StreamingAssets/art/` and load at runtime via the existing
`PixelArt.LoadExternal` pattern. Each illustrated layer checks for its file and, when absent, falls
back to the current code-box / procedural art — so the game always runs, and AI-gen images "slot in"
the moment they land in the folder. Document every external asset in `Assets/README.md` credits.
