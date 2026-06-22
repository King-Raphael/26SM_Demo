# The Darkroom

A 2D puzzle-platformer where you don't move through the world — you **develop** it like a photograph. Switch between three exposure states and draw long-exposure light strokes as temporary terrain.

## How to run

Open the project in Unity 6000.4 LTS, open `Assets/Scenes/Level01.unity` (any scene works), press **Play**. The entire level, player, camera and HUD are generated at runtime by `Bootstrap` — there is no manual scene wiring.

## Controls

| Action | Key | Notes |
|---|---|---|
| Move | A / D and ← / → | both always active |
| Jump | Space | grounded only; coyote time + jump buffering apply |
| Set Underexposed | 1 | first-class |
| Set Normal | 2 | first-class |
| Set Overexposed | 3 | first-class; locked until the Flash pickup |
| Cycle exposure | E forward / Q backward | secondary; skips locked/jamming states |
| Draw stroke | hold Shift (alt: hold L) | locked until the Shutter pickup; release to fix |
| Restart from checkpoint | R | on the win screen: full restart |
| Pause / resume | Esc | freezes time and audio; shows a controls recap |
| Mute | M | toggles all audio |

**Recommended hand layout:** right hand on arrow keys, left hand on 1/2/3 + Shift, thumb on Space. Switching exposure while moving must be physically comfortable — that's why 1/2/3 are primary.

## Exposure rules

| State | Look | Main function | Main cost |
|---|---|---|---|
| **Underexposed** (1) | near-black, cold glow | reveals DarkPath; DarkStrokes solid; enemies asleep | normal geometry hard to read; BrightStrokes gone |
| **Normal** (2) | clear mid-gray | safest, readable; enemies asleep | no strokes can be drawn; no special object solid |
| **Overexposed** (3) | blown-out white | passes BrightBarriers; BrightStrokes solid; activates sensors | DarkPaths and DarkStrokes gone; **enemies wake** |

### Solidity matrix

| Type | Underexposed | Normal | Overexposed |
|---|---|---|---|
| StaticGround | solid | solid | solid |
| DarkPath | **solid, glowing** | hidden | hidden |
| BrightBarrier | solid | solid | **passable, faded** |
| DarkStroke (fixed) | **solid** | faded, non-solid | gone |
| BrightStroke (fixed) | gone | faded, non-solid | **solid** |

- **Jam rule:** a switch that would solidify an object inside your body is refused — the film strip shakes and nothing changes.
- **Strokes:** hold Shift while moving in Under or Over to draw at your feet; release to **fix**. While drawing the stroke is intangible. Budget: 3 fixed strokes (the oldest blinks out, never while you stand on it). Max length 12 units. Drawing requires horizontal motion. All strokes clear on respawn.
- **Enemies:** asleep (any non-Over state) they are solid gray statues you can stand on, frozen wherever they were. In Over they wake — safelight red, deadly, patrolling. Time the switch to park a statue where you need it.
- **Sensors/doors:** stand on the sensor in Over to open its door permanently for the run (doors survive respawns; they reset only on full restart).
- **Death is cheap:** falling below the world, touching an awake enemy, or pressing R respawns you instantly at the last checkpoint in Normal exposure.

## Room-by-room intended solutions (spoilers)

0. **Calibration Strip** — jump onto the dark platform in Under, press 2, drop harmlessly: what is not lit, is not there.
1. **Invisible Steps** — press 1; cross the three dark steps.
2. **The White Wall** — take the **Flash**; press 3 to burn through the barrier; the enemy ahead wakes in Over — press 2 and walk past the sleeping statue.
3. **First Stroke** — take the **Shutter**; in Under, draw while jumping beside the block and release at the apex; climb your own photograph.
4. **Contact Sheet** — two draw-fix steps (~5.5 then ~7.4) up to the high ledge.
5. **Blown Bridge** — pass the gate in Over; draw-fix one or two bright arcs over the patrolling enemy. Don't land on its perch.
6. **Sensor Test** — climb the dark shelves in Under, land on the **static** anchor, switch to Over (the shelves ghost away below you — you're safe), trip the sensor, door opens.
7. **Still Life** — press 3, let the enemy patrol under the ledge lip, press 2 to freeze it into a statue, jump on its back and up. (A stroke step also works.)
8. **Negative Transfer** — Under → dark step → land on Anchor A → Over (the step dies behind you) → through the barrier → bright-stroke arc over the threat perch.
9. **The Drop** — the wall is a dead end. Stand on the dark hatch in Under and press 2: the floor you trusted ceases to exist. Fall. That's the way forward.
10. **The Final Print** — reveal (Under), anchor, burn (Over), sensor, door, checkpoint; then dark-stroke to Anchor B, switch to Over (stroke dies, barrier opens, guard wakes), time the guard, enter the exit.

## External assets (credits)

| Asset | Source | License |
|---|---|---|
| `Assets/Resources/Fonts/Mono.ttf` (JetBrains Mono Regular) | github.com/JetBrains/JetBrainsMono | OFL-1.1 |
| `Assets/StreamingAssets/concrete.jpg` (concrete_wall_004, 1K diffuse) | polyhaven.com | CC0 |
| `Assets/StreamingAssets/bricks.jpg` (red_brick_03, 1K diffuse) — *superseded by the authored wall, kept only as a deep fallback* | polyhaven.com | CC0 |

**Illustrated backdrop vignettes** (`Assets/StreamingAssets/art/bd_*.png`) — public-domain darkroom photographs / engravings, restyled to the game palette (desaturate → cool duotone → dim → edge-feather; `art-pipeline/gen_backdrops.py`):

| File | Source image | License |
|---|---|---|
| `bd_cornell.png` | "Cornell University — one of the dark rooms", NARA 26425820 (1917) | Public Domain (PD-USGov) |
| `bd_sellwood.png` | "Print processing darkroom, Sellwood Lab, Portland" (1956) | Public Domain (PD-USGov-USDA) |
| `bd_engraving.png` | "In der Dunkelkammer", Die Gartenlaube p.605 (1894) | Public Domain (PD-old) |
| `bd_jeffcoat.png` | Jeffcoat Photography Studio darkroom, Abilene KS — Carol M. Highsmith, Library of Congress | Public Domain (no known restrictions) |

**Authored (original) art** — `Assets/StreamingAssets/art/wall_{plaster,concrete,panel}.png`: seamless cool-dark darkroom wall textures generated by `art-pipeline/gen_walls.py` (FFT periodic noise). The plaster wall replaces the old red brick, which clashed with the monochrome palette.

**AI-generated art** (student-generated from the prompts in `art-pipeline/IMAGE_GEN_PROMPTS.md`, then post-processed by `art-pipeline/process_aigen.py`): six hero backdrops `bd_{drying_line,wet_bench,enlarger_row,chem_shelf,window_shaft,safelight_corner}.png` — edge-feathered and used as cohesive background patches on `Layer_Scenes`. Seven prop cutouts `prop_*.png` were also generated and chroma-keyed, but **deliberately not used in-scene**: a crisp standalone prop reads as an interactive object in a game whose whole logic is "lit = solid, interact", so non-interactive prop sprites were misleading. The backdrops already depict enlargers / bottles / trays in context, so the darkroom objects live there instead. The processed props are archived in `art-pipeline/_props_removed/` (raw generations in `art-pipeline/_aigen_raw/`).

Ten **mid-ground clutter** cutouts `mid_{drying_prints,drying_negatives,pipes,vent,safelight,reels,cables,shelf,tongs_rack,clock}.png` (prompts in `art-pipeline/MIDGROUND_PROMPTS.md`) are generated on a flat-gray field, then `process_aigen.py` chroma-keys + desaturates + darkens them into dim hanging silhouettes-with-detail. Unlike the floor props these are safe to use in-scene because they hang high on the `MidGround` depth layer behind gameplay (clearly background, never read as interactable). `PixelArt.MidgroundClutter` auto-discovers them; `BackdropBuilder.BuildMidground` hangs them from the ceiling band (top-centre pivot), with the code-drawn silhouettes as fallback/filler.

All `art/` images load at runtime via `PixelArt.LoadExternal` / `LoadExternalFirst` / `BackdropScenes`. The loader is prioritised — authored `art/…` → legacy photo → procedural — and the backdrop builder no-ops gracefully when a file is absent, so the game always renders and new (CC0 / AI-generated) PNGs dropped into `art/` slot in with no code change. The full art pipeline, palette spec and image-gen prompt pack live in `art-pipeline/` (outside `Assets/`). Everything else remains generated in code.

## Implementation notes / documented deviations

- **URP 2D** template instead of the spec's built-in pipeline (project ships with URP; sprites + LineRenderer behave identically with the shared unlit material).
- **Per-exposure background colour (M12)**: each exposure now has its own BACKGROUND tone rather than a full-screen tint over the foreground, so elements keep their own colour and read against it — Under = deep blue, Normal = near-black, Over = warm. Driven two ways: `LightDirector` lerps the camera clear colour per state, and `BackdropTint` (on `_Backdrop`) lerps the parallax backdrop toward the state tint (the backdrop occludes the camera clear, so it carries the visible background). The OVER post grade was correspondingly de-warmed (the background carries the warmth, not a screen filter) and OVER's "overexposed" read comes from the bright warm background + moderate light + highlight bloom, keeping shadows/foreground legible. NB: live-tuning requires ⌘R (Asset Refresh) before Play — Unity does not auto-recompile script edits on focus/Play here.
- **Gameplay-element redesign (M12)**: the puzzle elements used to be flat coloured boxes; they are now layered, animated objects — metaphor-first (darkroom/photography), abstract light/shadow, industrial where it fits. The exposure **colour-coding is preserved** (cold-blue Under / warm-cream Over / violet Umbra) and colliders, sizes and the solidity matrix are byte-for-byte unchanged — only the form changed. New shared toolkit `Core/ProcGfx.cs` (a supersampled anti-aliased drawing `Canvas` + cached element-texture generators: film strip, photo paper, char scar, roiling emulsion, latent grain) and reusable animation drivers `World/Drift.cs` (pooled motes) + `World/FrameCycle.cs` (turbulence frames). Per element: **DarkPath** = a film-negative strip with sprocket rails, a developing core and drifting cold motes; **LightBridge** = a projected beam with a caustic top edge + dust; **BrightBarrier** = a framed frosted print; **BurnPaper** = real photo paper that browns, chars and burns a hole from a point (ember swell + growing char scar + spark); **UmbralBarrier** = roiling undeveloped emulsion (cycled turbulence frames) with a violet rim + foot light; **FixPlatform** = a latent grain veil that *develops in* when printed; **Shadow/Rise lifts** = slabs with HDR catch-light edges, a breathing underside glow and always-visible shaft rails; **SensorDoor** = a darkroom light-lock with a breathing safelight lamp that turns warm the instant it opens; **Checkpoint** develops with a spark; the **Exit** gains drifting motes. Child detail fades in lockstep via `ExposureObject.OnAlphaApplied` or new additive hooks on the behaviour scripts (`onAlpha` on UmbralBarrier/ShadowLift/RiseLift, `OnCharProgress`/`OnBurned` on BurnPaper, `OnOpen` on SensorDoor, a develop coroutine in FixPlatform) — gameplay logic untouched.
- **Cinematic post-processing & uplift pass (M11)**: the URP post stack used to be dormant — every effect was present in the volume profile but at intensity 0, and nothing enabled post-processing on the runtime camera. `PostFXDirector` now builds a global `Volume` in code (ACES tonemapping + Bloom + Vignette + ColorAdjustments + FilmGrain) and lerps it per exposure state (Under cold & deep, Normal balanced filmic, Over warm bloom-out), mirroring `LightDirector`; the finale gets a warm grade override. The camera enables post-processing + SMAA, and the URP asset gets MSAA 4 + HDR colour grading, with full-resolution 2D lights (`Renderer2D.LightRenderTextureScale` 0.5→1). Procedural edges are anti-aliased (`SilhouetteArt` supersamples then box-downsamples; all generated sprites switched `FilterMode.Point`→`Bilinear`; the door tile re-rendered at 4× density), and bulbs / the exit doorway emit HDR-bright colour so they bloom. The HUD's hand-rolled film-grain + vignette overlays were retired (the post stack owns them now). Narrative beats (title, banners, checkpoint/death notes, hints, "DEVELOPED") use an elegant display face (`FontLoader.Display`: bundle `Resources/Fonts/Display.ttf`, else an OS face such as Optima/Didot, else Mono) while the camera-viewfinder readouts stay JetBrains Mono. Cinematic flow: boot fade-up under a brief letterbox, a "FRAME N" beat on entering each frame, a softer checkpoint develop-flash, fading hint bubbles, and a widescreen letterbox + warm grade for the finale. **Gameplay, physics and level data are unchanged** (the visual pass is mechanically neutral).
- **Cinematic monochrome restyle (M9, per the student's concept art)**: soft silhouette girl with glowing eyes (`SilhouetteArt`, bilinear shape-drawn — no longer pixel art), shadow-blob enemies, dark concrete/brick noise textures with catch-light edges, hanging cone lamps with light pools, fog-glass barriers, lens-panel doors, a glowing white exit doorway. Camera-viewfinder UI: exposure slider + state badge (replaces the film-strip HUD), room title + objectives top-left, progressive control hints that retire after the tutorial, a tutorial-only exposure card, world-anchored hint bubbles, and viewfinder corners + blinking REC that appear **only while the shutter is open** (drawing).
- **Tutorial chain extended**: the **Negative** pickup (Room 0) now unlocks Underexposed itself — abilities arrive one at a time: move/jump → negative (Under) → flash (Over) → shutter (draw).
- **Room 6 door raised** (door y 3.5–8.0, ceiling moved to 8.2–8.6 spanning the anchor): the spec ceiling top (6.6) was flush with the sensor anchor (6.55), so the door could be walked over. Extreme 3-stroke stair-stacking can still clear it — accepted as speedrun tech.
- **Atmosphere layer**: two-speed parallax backdrop of darkroom silhouettes (hanging photo lines, shelves with bottles, enlargers — `BackdropBuilder`), glow halos on dark paths / strokes / pickups / the exit (alpha-synced to the exposure matrix), a radial vignette while Underexposed, dim hanging-photo checkpoint markers that brighten once "developed", and a fading title card at boot.
- **Real URP 2D lighting** (`LightDirector`): the global light follows the exposure (Normal 1.0 neutral, Under 0.30 cold blue, Over 1.35 warm). World geometry and actors use the lit sprite material; light sources (dark paths, strokes, pickups, the safelight exit, sensor when tripped, awake enemies' red glow, the player's own faint lamp) are unlit and carry point lights whose intensity follows the solidity matrix. In Under the world goes genuinely dark and only the light you make remains readable — the screen overlay is now just a thin tint.
- **Everything is runtime-generated** (`Bootstrap` → `LevelBuilder` + code-built UGUI HUD). `Level01.unity` is an empty template scene; the bootstrap also works from any other scene.
- **Enemies are excluded from the jam check** — Room 7 requires freezing a statue while standing on it; physics depenetration handles the overlap.
- **The live stroke uses a disabled collider** rather than `Physics2D.IgnoreCollision` (equivalent semantics, avoids the <2-point EdgeCollider2D edge case).
- **The jam check tests strokes per segment**, not by whole-stroke AABB — an arc's bounding box covers space the line never touches and would refuse switches far away from the actual light. Stroke points are drawn 0.25 below the feet (more than the 0.07 edge radius) so a stroke drawn along the ground never protrudes above the floor, and a stroke drawn at the jump apex stays comfortably reachable by the next jump.
- **The player's Rigidbody2D never sleeps** — sensors and standing-on-a-waking-enemy kills depend on `OnTriggerStay2D`, which Unity stops delivering for sleeping bodies.
- **Full restart rebuilds the level in place** instead of reloading the scene (the bootstrap runs once per play session).
- **Audio is procedurally synthesized** (no external assets, `AudioDirector`): shutter click on every switch, low hum in Under, bright hiss in Over, dull jam click, pickup chime, win shutter — plus footsteps, jump/land, death rip, develop-in swell, checkpoint notes, door rumble, a crackle loop while drawing, and a barely-audible room tone.
- **Esc pause / M mute** were added after the Definition of Done passed (the spec's "no menus" rule applied until then); the pause is a single overlay with a controls recap, not a menu system.
- **Death/respawn are themed**: the image "burns" into a grain burst on death and re-develops (alpha/scale ease-in) at the checkpoint.
- **Room 4 ledge raised to top 9.0** (spec said 7.5): with the spec's jump math a single stroke (max launch ~5.8, apex ~8.3) cleared 7.5, so the room's intended *two* fixes were never required. Room 5's entry (gate, gate ceiling, checkpoint, hint) moved up 1.5 with it; seams stay flush and Room 5 remains solvable as intended.
- **Room 1 gap widened to 7.5** (spec: 4.5) and the three dark steps widened to 1.6: a running jump covers ~5.6 units (~6.3 with coyote time), so the spec's gap was directly jumpable and the room's one lesson — press 1 — could be skipped entirely. The revealed route is now easier than spec (step gaps 0.25–0.7), the skip is impossible.
- **All five spec stretch goals are implemented**: switch/state audio, sparkle particles along strokes while drawing, faint flickering film-grain overlay, statue "crackle" flicker when an enemy freezes, and a replay timer (hidden until the first win; final time shown on the win screen).
- Hint text says "arrow keys" instead of arrow glyphs (font glyph safety).
- Layers: 6=World, 7=Strokes, 8=Player, 9=Triggers; Strokes collides with Player only (set in code at boot).

## Verification

- `Darkroom.EditorTools.Validate` (batchmode `-executeMethod`) asserts the level data against the spec tables (rooms/checkpoints/enemies/sensors/doors/pickups/hints/ceilings), prints a floor-seam audit, and instantiates the full level to count objects.
