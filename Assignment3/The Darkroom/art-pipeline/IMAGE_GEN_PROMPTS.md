# The Darkroom — Image-Gen Prompt Pack

Run these in Midjourney / Stable Diffusion (SDXL) / DALL·E. Drop results into
`Assets/StreamingAssets/art/` with the exact filename in each heading; the game auto-loads them.
**Generate the whole set with the SAME model + the shared STYLE block** so they read as one room.

> Tip: Midjourney → append the params shown. SDXL → put STYLE in the positive prompt, NEGATIVE in the
> negative prompt, sampler DPM++ 2M Karras, ~30 steps, CFG 5–7. DALL·E → paste STYLE + subject as one
> paragraph (it ignores `--params`; bake the aspect ratio into the request).

---

## SHARED — STYLE block (prepend to every prompt)

> cinematic painterly digital illustration of a dim 1970s photographic darkroom, deep chiaroscuro,
> almost monochrome, heavily desaturated cool palette, near-black shadows, lit only by a dim red
> safelight and a faint cold blue glow, soft volumetric haze, film grain, muted, moody, atmospheric,
> matte-painting quality, subtle warm-cream highlights, painterly brushwork, high detail in shadow,
> low-key lighting

## SHARED — NEGATIVE block (use in every prompt)

> bright, sunny, daylight, saturated colors, vivid, neon, colorful, red brick wall, modern digital
> equipment, computers, monitors, screens, text, watermark, signature, logo, people, faces, hands,
> cartoon, cute, glossy, clean studio render, HDR, lens flare overload, oversharpened

---

# B. HERO BACKDROPS  (far parallax — wide, dark, edges fade to black)

Aspect **2:1**, place detail in the center third, let left/right edges fall to near-black so tiles blend.
`--ar 2:1` (MJ) / 2048×1024 (SDXL).

### `bd_drying_line.png`
> [STYLE], a back wall strung with several horizontal wires, dozens of photographic prints hanging by
> wooden clothespins, the prints are faint ghostly grey images, gentle sag in the wires, dust in the air,
> a dim red safelight glow from the upper left, wide establishing shot, mostly empty dark wall around them

### `bd_wet_bench.png`
> [STYLE], a long wet processing bench seen from the front, three shallow developing trays with faint
> liquid sheen, a pair of print tongs, a row of dark glass chemical bottles, a single red safelight lamp
> glowing above the bench, faint reflections on the wet surface, everything dissolving into shadow at the edges

### `bd_enlarger_row.png`
> [STYLE], a row of three vintage photographic enlargers on a long bench, tall vertical columns with
> angular enlarger heads and bellows, baseboards below, receding slightly into darkness, cold faint blue
> rim light on the metal edges, deep shadow between the stations

### `bd_chem_shelf.png`
> [STYLE], a tall wall of wooden shelves crowded with amber and brown glass chemical bottles, beakers,
> measuring cylinders and labeled jars, dust and grime, a faint warm highlight catching a few bottle
> shoulders, the rest sinking into black, cluttered apothecary feel

### `bd_window_shaft.png`
> [STYLE], a blacked-out window covered by a heavy dark curtain with a thin gap, a single narrow shaft of
> cold pale-blue light cutting diagonally into the dark room, dust drifting in the beam, a stool and coiled
> cables silhouetted, the cold beam is the only bright element, strong volumetric light

### `bd_safelight_corner.png`
> [STYLE], a cozy dark corner of the darkroom, a red safelight lamp mounted on the wall casting a warm-red
> pool, an old round wall clock, a wooden stool, a coat on a hook, a small shelf, intimate and shadowy,
> the red glow falling off quickly into black

---

# C. ILLUSTRATED PROPS  (near parallax — CUTOUTS)

**Generate centered on a FLAT, UNIFORM mid-gray `#808080` background** (say "isolated on a plain flat
neutral gray background, no scene, no floor, no shadow on the ground") so I can chroma-key the alpha —
OR deliver a transparent PNG directly. Aspect **1:1** unless noted. `--ar 1:1`.

### `prop_enlarger.png`  (hero prop, tall → `--ar 2:3`)
> [STYLE], a single vintage photographic enlarger, full object: heavy flat baseboard, a tall vertical
> metal column, an angled enlarger head with lamp housing and bellows and a lens at the bottom, a focus
> knob, dark painted metal with faint cold rim light, isolated on a plain flat neutral gray background

### `prop_bottles.png`
> [STYLE], a tight cluster of vintage darkroom chemistry: three or four amber and dark-brown glass bottles
> of different heights, a glass beaker, faded paper labels, faint warm highlight on the glass shoulders,
> isolated on a plain flat neutral gray background

### `prop_hanging_prints.png`  (wide → `--ar 3:2`)
> [STYLE], a short section of wire with three or four photographic prints hanging from wooden clothespins,
> the prints are faint ghostly grey images curling slightly, isolated on a plain flat neutral gray background

### `prop_tray_stack.png`
> [STYLE], a stack of three shallow rectangular developing trays at a slight angle, a pair of print tongs
> resting on top, scratched enamel, isolated on a plain flat neutral gray background

### `prop_reel_tank.png`
> [STYLE], a film developing tank with its lid beside it and a spiral stainless film reel, dark matte metal,
> faint cold rim light, isolated on a plain flat neutral gray background

### `prop_clock.png`  (simple — code fallback exists)
> [STYLE], an old round darkroom interval timer clock with a dark face and faint luminous hands, worn metal
> bezel, isolated on a plain flat neutral gray background

### `prop_cables.png`  (simple — code fallback exists)
> [STYLE], a tangle of old coiled electrical cables and a power strip hanging on a nail, dark rubber,
> isolated on a plain flat neutral gray background

---

## After you generate

1. Name each file exactly as the heading and drop it in `Assets/StreamingAssets/art/`.
2. If a prop is on gray (not transparent), that's fine — tell me and I'll chroma-key the alpha + tint it
   to palette in the pipeline.
3. Ping me; I wire them into the parallax layers and tune placement/scale/tint in-engine.

Don't worry about perfect color — I desaturate + tint everything to the locked palette on import, so even
a slightly-too-warm generation will be corrected to match the room.
