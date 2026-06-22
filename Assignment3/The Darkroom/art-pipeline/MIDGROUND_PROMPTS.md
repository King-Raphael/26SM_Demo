# The Darkroom — Mid-ground Clutter Prompt Pack

Hanging/overhead darkroom clutter that sits on the **`MidGround`** layer (a dark depth layer
between the photo backdrops and the play space). Generate these, drop the PNGs in
`Assets/StreamingAssets/art/` named exactly `mid_*.png`, and the game auto-loads them (the
code-drawn silhouettes become the fallback). I darken + desaturate them on import so they stay
a *dark mid-ground* — don't worry about exact tone.

> Same model + the SAME STYLE block for a cohesive set. Generate each **isolated on a flat,
> uniform mid-gray `#808080` background** (no floor, no scene) so I can chroma-key the alpha —
> OR deliver transparent PNGs directly. **Compose each so its mount/attachment is at the TOP of
> the frame and it hangs/extends downward** (the engine pins the top to the ceiling and lets it
> hang). Tall portrait framing for hanging items.

## SHARED — STYLE block (prepend to every prompt)
> cinematic darkroom prop, hanging from above, vintage 1970s photographic darkroom, dark and
> desaturated, deep shadow, moody, painterly illustration, matte, almost monochrome, subtle cool
> rim light, high detail in shadow, isolated on a plain flat neutral gray background, no floor,
> no scene, soft top-down lighting

## SHARED — NEGATIVE block
> bright, sunny, saturated, colorful, neon, text, watermark, logo, people, hands, floor, ground
> shadow, modern digital screens, cartoon, glossy studio render

---

### `mid_drying_prints.png`  (`--ar 3:4`)
> [STYLE], a sagging wire strung across the top with several photographic PRINTS hanging by
> wooden clothespins, the prints curl slightly and show faint ghostly grey images, varied sizes

### `mid_drying_negatives.png`  (`--ar 2:3`)
> [STYLE], a wire with long strips of 35mm FILM NEGATIVES and negatives in glassine sleeves
> hanging by clips, faint sprocket holes, translucent, dangling straight down

### `mid_pipes.png`  (`--ar 4:3`)
> [STYLE], overhead industrial PIPES and electrical conduit running horizontally near the
> ceiling, a junction, a valve wheel, one elbow dropping down, dark painted metal

### `mid_vent.png`  (`--ar 4:3`)
> [STYLE], an overhead ventilation DUCT with an exhaust fan grille and a flexible hose, dusty
> sheet metal, mounted to the ceiling

### `mid_safelight.png`  (`--ar 2:3`)
> [STYLE], a hanging red SAFELIGHT fixture — a dark metal housing with a deep red glass dome
> casting a dim red glow downward, short cord from the top, the only colour is the dull red

### `mid_reels.png`  (`--ar 2:3`)
> [STYLE], a cluster of film developing REELS, stainless spirals, a developing tank, and round
> film canisters hanging from hooks by their handles, dark matte metal

### `mid_cables.png`  (`--ar 2:3`)
> [STYLE], coiled rubber CABLES and a looped hose hanging from a nail/hook at the top, a power
> strip dangling, tangled, dark

### `mid_shelf.png`  (`--ar 4:3`)
> [STYLE], a high wall SHELF crowded with dark amber chemistry bottles, graduated cylinders and
> jars, a bracket holding it up, mounted high on the wall, dim

### `mid_tongs_rack.png`  (`--ar 3:2`)
> [STYLE], a small rack near the ceiling with print TONGS, squeegees, clips and clothespins
> hanging in a row, dark

### `mid_clock.png`  (`--ar 1:1`)
> [STYLE], an old round darkroom interval TIMER / clock mounted high on the wall, dark face,
> faint luminous hands, worn metal bezel, a bracket above it

---

## After you generate
1. Name each file exactly as the heading, drop in `Assets/StreamingAssets/art/`.
2. Gray-bg or transparent both fine — tell me if gray and I'll chroma-key + darken via
   `process_aigen.py` (mid_* branch).
3. ⌘R → ⌘P; they auto-hang on the mid-ground layer. Ping me to tune size/height/tone/density.

Generate as many or as few as you like — each new `mid_*.png` adds variety; the code silhouettes
fill in for any you skip.
