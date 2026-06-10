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

## Implementation notes / documented deviations

- **URP 2D** template instead of the spec's built-in pipeline (project ships with URP; sprites + LineRenderer behave identically with the shared unlit material).
- **All art is procedural pixel art** (`PixelArt`): characters/pickups/exit are authored as in-code string maps, world surfaces are generated tile textures drawn in Tiled sprite mode — still zero external assets. The player has a 2-frame walk, jump pose, landing squash; enemies have closed/glowing eyes and a breathing idle while awake.
- **Atmosphere layer**: two-speed parallax backdrop of darkroom silhouettes (hanging photo lines, shelves with bottles, enlargers — `BackdropBuilder`), glow halos on dark paths / strokes / pickups / the exit (alpha-synced to the exposure matrix), a radial vignette while Underexposed, dim hanging-photo checkpoint markers that brighten once "developed", and a fading title card at boot.
- **Real URP 2D lighting** (`LightDirector`): the global light follows the exposure (Normal 1.0 neutral, Under 0.30 cold blue, Over 1.35 warm). World geometry and actors use the lit sprite material; light sources (dark paths, strokes, pickups, the safelight exit, sensor when tripped, awake enemies' red glow, the player's own faint lamp) are unlit and carry point lights whose intensity follows the solidity matrix. In Under the world goes genuinely dark and only the light you make remains readable — the screen overlay is now just a thin tint.
- **Everything is runtime-generated** (`Bootstrap` → `LevelBuilder` + code-built UGUI HUD). `Level01.unity` is an empty template scene; the bootstrap also works from any other scene.
- **Enemies are excluded from the jam check** — Room 7 requires freezing a statue while standing on it; physics depenetration handles the overlap.
- **The live stroke uses a disabled collider** rather than `Physics2D.IgnoreCollision` (equivalent semantics, avoids the <2-point EdgeCollider2D edge case).
- **The jam check tests strokes per segment**, not by whole-stroke AABB — an arc's bounding box covers space the line never touches and would refuse switches far away from the actual light. Stroke points are drawn 0.25 below the feet (more than the 0.07 edge radius) so a stroke drawn along the ground never protrudes above the floor, and a stroke drawn at the jump apex stays comfortably reachable by the next jump.
- **The player's Rigidbody2D never sleeps** — sensors and standing-on-a-waking-enemy kills depend on `OnTriggerStay2D`, which Unity stops delivering for sleeping bodies.
- **Full restart rebuilds the level in place** instead of reloading the scene (the bootstrap runs once per play session).
- **Audio is procedurally synthesized** (no external assets): shutter click on every switch, low hum in Under, bright hiss in Over, dull jam click, pickup chime, win shutter (`AudioDirector`).
- **All five spec stretch goals are implemented**: switch/state audio, sparkle particles along strokes while drawing, faint flickering film-grain overlay, statue "crackle" flicker when an enemy freezes, and a replay timer (hidden until the first win; final time shown on the win screen).
- Hint text says "arrow keys" instead of arrow glyphs (font glyph safety).
- Layers: 6=World, 7=Strokes, 8=Player, 9=Triggers; Strokes collides with Player only (set in code at boot).

## Verification

- `Darkroom.EditorTools.Validate` (batchmode `-executeMethod`) asserts the level data against the spec tables (rooms/checkpoints/enemies/sensors/doors/pickups/hints/ceilings), prints a floor-seam audit, and instantiates the full level to count objects.
