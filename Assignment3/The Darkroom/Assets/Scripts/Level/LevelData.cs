using UnityEngine;

namespace Darkroom
{
    // Plain-data definitions transcribed from spec section 11.
    // Rectangles are center + size in world units.

    public struct BoxDef
    {
        public string name;
        public ExposureObjectType type;
        public float cx, cy, w, h;
        public BoxDef(string name, ExposureObjectType type, float cx, float cy, float w, float h)
        { this.name = name; this.type = type; this.cx = cx; this.cy = cy; this.w = w; this.h = h; }
    }

    public struct EnemyDef
    {
        public string name;
        public float cx, cy, range, speed;
        public EnemyDef(string name, float cx, float cy, float range, float speed)
        { this.name = name; this.cx = cx; this.cy = cy; this.range = range; this.speed = speed; }
    }

    public struct SensorDef
    {
        public string name;
        public float cx, cy;
        public string doorId;
        public int mode;    // 0 = GlobalOverexposed (stand on it in OVER), 1 = LocalLux (deliver light)
        public float lux;   // LocalLux activation threshold
        public bool wash;   // a DOORLESS LocalLux meter that washes the print into colour
        public SensorDef(string name, float cx, float cy, string doorId)
        { this.name = name; this.cx = cx; this.cy = cy; this.doorId = doorId; this.mode = 0; this.lux = 0.6f; this.wash = false; }
        public SensorDef(string name, float cx, float cy, string doorId, int mode, float lux)
        { this.name = name; this.cx = cx; this.cy = cy; this.doorId = doorId; this.mode = mode; this.lux = lux; this.wash = false; }
        // wash tray: LocalLux, no door
        public SensorDef(string name, float cx, float cy, float lux, bool wash)
        { this.name = name; this.cx = cx; this.cy = cy; this.doorId = ""; this.mode = 1; this.lux = lux; this.wash = wash; }
    }

    public struct DoorDef
    {
        public string id;
        public float cx, cy, w, h;
        public DoorDef(string id, float cx, float cy, float w, float h)
        { this.id = id; this.cx = cx; this.cy = cy; this.w = w; this.h = h; }
    }

    public struct PickupDef
    {
        public Ability ability;
        public float cx, cy;
        public PickupDef(Ability ability, float cx, float cy)
        { this.ability = ability; this.cx = cx; this.cy = cy; }
    }

    public struct CheckpointDef
    {
        public string name;
        public float cx, cy;
        /// Contact-sheet margin note shown when this checkpoint develops
        /// (late-game only; the two "unexposed" notes plant the ending).
        public string caption;
        public CheckpointDef(string name, float cx, float cy)
        { this.name = name; this.cx = cx; this.cy = cy; this.caption = ""; }
        public CheckpointDef(string name, float cx, float cy, string caption)
        { this.name = name; this.cx = cx; this.cy = cy; this.caption = caption; }
    }

    public struct HintDef
    {
        public string text;
        public float cx, cy, w, h;
        public HintDef(string text, float cx, float cy, float w, float h)
        { this.text = text; this.cx = cx; this.cy = cy; this.w = w; this.h = h; }
    }

    public struct ExitDef
    {
        public float cx, cy, w, h;
        public ExitDef(float cx, float cy, float w, float h)
        { this.cx = cx; this.cy = cy; this.w = w; this.h = h; }
    }

    /// A wall of shadow (UmbralBarrier): solid until lit by a delivered stroke.
    public struct UmbralDef
    {
        public string name;
        public float cx, cy, w, h;
        public float threshold;
        public UmbralDef(string name, float cx, float cy, float w, float h, float threshold)
        { this.name = name; this.cx = cx; this.cy = cy; this.w = w; this.h = h; this.threshold = threshold; }
    }

    /// A shadow lift (ShadowLift): solid except in OVER; sinks in UNDER between
    /// topY and bottomY, holds in NORMAL. cx is fixed; it only moves vertically.
    public struct LiftDef
    {
        public string name;
        public float cx, topY, bottomY, w, h;
        public LiftDef(string name, float cx, float topY, float bottomY, float w, float h)
        { this.name = name; this.cx = cx; this.topY = topY; this.bottomY = bottomY; this.w = w; this.h = h; }
    }

    /// A white sheet that OVER burns through (BurnPaper): hold OVER nearby ~1.5s
    /// and it burns a permanent hole. Always solid until burnt.
    public struct BurnDef
    {
        public string name;
        public float cx, cy, w, h;
        public BurnDef(string name, float cx, float cy, float w, float h)
        { this.name = name; this.cx = cx; this.cy = cy; this.w = w; this.h = h; }
    }

    /// A bridge of light: solid only in OVER (the bright twin of a dark path).
    public struct BridgeDef
    {
        public string name;
        public float cx, cy, w, h;
        public BridgeDef(string name, float cx, float cy, float w, float h)
        { this.name = name; this.cx = cx; this.cy = cy; this.w = w; this.h = h; }
    }

    /// A light lift (RiseLift): real only in OVER, rises bottomY → topY.
    public struct RiseDef
    {
        public string name;
        public float cx, topY, bottomY, w, h;
        public RiseDef(string name, float cx, float topY, float bottomY, float w, float h)
        { this.name = name; this.cx = cx; this.topY = topY; this.bottomY = bottomY; this.w = w; this.h = h; }
    }

    /// A pre-authored dark light-streak (DarkTrail): a glowing curve, solid +
    /// visible only in UNDER. World-space polyline points.
    public struct TrailDef
    {
        public string name;
        public Vector2[] points;
        public TrailDef(string name, Vector2[] points) { this.name = name; this.points = points; }
    }

    /// A latent platform (FixPlatform): a faint cool ghost until you flash OVER
    /// beside it (not inside it), which PRINTS it permanently solid — footing you
    /// PLAN, not a state you hold. Passable until printed, then always solid.
    public struct FixDef
    {
        public string name;
        public float cx, cy, w, h;
        public FixDef(string name, float cx, float cy, float w, float h)
        { this.name = name; this.cx = cx; this.cy = cy; this.w = w; this.h = h; }
    }

    /// A LOST FRAME (LostFrame): an undeveloped negative hidden in a verb-gated
    /// pocket. Develops into a SEPARATE gallery, never the sacred eleven.
    public struct LostDef
    {
        public string name;
        public float cx, cy, w, h;
        public LostDef(string name, float cx, float cy, float w, float h)
        { this.name = name; this.cx = cx; this.cy = cy; this.w = w; this.h = h; }
    }

    public class RoomDef
    {
        public string name;
        public string title = "";
        public string[] objectives = new string[0];
        public BoxDef[] boxes = new BoxDef[0];
        public EnemyDef[] enemies = new EnemyDef[0];
        public SensorDef[] sensors = new SensorDef[0];
        public DoorDef[] doors = new DoorDef[0];
        public PickupDef[] pickups = new PickupDef[0];
        public CheckpointDef[] checkpoints = new CheckpointDef[0];
        public HintDef[] hints = new HintDef[0];
        public ExitDef[] exits = new ExitDef[0];
        public UmbralDef[] umbrals = new UmbralDef[0];
        public LiftDef[] lifts = new LiftDef[0];
        public BurnDef[] burns = new BurnDef[0];
        public BridgeDef[] bridges = new BridgeDef[0];
        public RiseDef[] riseLifts = new RiseDef[0];
        public TrailDef[] trails = new TrailDef[0];
        public FixDef[] fixPlats = new FixDef[0];
        public LostDef[] lostFrames = new LostDef[0];
    }

    public static class LevelData
    {
        /// Left edge of each room (index = room number); used by the HUD
        /// to show "ROOM N : TITLE" from the player's x position.
        public static readonly float[] RoomStarts =
            { -49f, 5.5f, 20f, 32.5f, 42f, 58.5f, 73f, 93.5f, 107.5f, 125f, 142.5f };

        public static int RoomIndexAt(float x)
        {
            int idx = 0;
            for (int i = 0; i < RoomStarts.Length; i++)
                if (x >= RoomStarts[i]) idx = i;
            return idx;
        }

        /// Total hidden lost frames across the roll (for the "N / total" readout).
        public static int LostFrameTotal
        {
            get { int n = 0; foreach (var r in Rooms) n += r.lostFrames.Length; return n; }
        }

        const ExposureObjectType SG = ExposureObjectType.StaticGround;
        const ExposureObjectType DP = ExposureObjectType.DarkPath;
        // BrightBarrier is retired from the levels — every white wall is now a
        // BurnPaper (hold OVER to burn through). The type/code still exists.

        public static readonly RoomDef[] Rooms = new RoomDef[]
        {
            // Room 0 — PROLOGUE: THE UNPRINTED FRAME (x ≈ −48 … −5, an ISOLATED
            // pocket far to the left of Frame 1). The opening is a long, quiet
            // corridor: she walks a while in the dark, then the drying roll appears
            // overhead — ten frames printed, the eleventh always blank. The lit way
            // forward ends at a WIDE gap; only by lowering the room to safelight
            // (press 1) does the missing footing DEVELOP — a long, gentle arc of
            // light across the dark. She climbs it, raises the work light to confirm
            // the real ground, and steps into the blank-paper door → the enter-photo
            // cinematic, which TELEPORTS her across the empty gap into Frame 1 (so the
            // door never reveals the path ahead; the cut reads as entering elsewhere).
            // The far ground sits +2.3 above the entrance across a WIDE 14u gap, with
            // the paper-door towering high on top — clearly un-jumpable. Pressing 1
            // DEVELOPS the missing footing: three UNDER-only dark STEP-LEDGES (DarkPath,
            // gone in work light) climbing up to it — a CLIMB, distinct from Frame 1's
            // horizontal light-trail crossing. Non-lethal: a mis-switch mid-climb drops
            // onto R0_CatchLedge, a stumble not a death. Under is granted silently at
            // boot — no pickup.
            new RoomDef
            {
                name = "R0_Prologue",
                title = "THE UNPRINTED FRAME",
                objectives = new[] { "A/D — move", "SPACE — jump" },
                boxes = new[]
                {
                    new BoxDef("R0_LeftWall",   SG, -48f,   3f,    1f,    9f),   // back wall
                    new BoxDef("R0_FloorA",     SG, -35.25f,0f,    24.5f, 1f),   // x -47.5..-23, top 0.5 (long entrance walk)
                    // a non-lethal catch below the wide gap — a mis-switch on the
                    // bridge is a short drop + climb back, never a death (and low
                    // enough that you can't jump from it onto the far ground)
                    new BoxDef("R0_CatchLedge", SG, -15f,  -1.5f, 16f,    1f),   // x -23..-7, top -1.0
                    // the far real ground: a HIGH ledge, +2.3 above the entrance across
                    // a wide 14u gap → un-jumpable; the UNDER dark steps are the way up
                    new BoxDef("R0_FarGround",  SG,  -7f,   2.3f,  4f,    1f),   // x -9..-5, top 2.8
                    // the missing footing DEVELOPS under the safelight: three dark
                    // step-ledges (DarkPath — solid only in UNDER) climbing the gap to
                    // the high ledge. Small hops; switch to work light and they vanish.
                    new BoxDef("R0_Step1",      DP, -19.5f, 0.825f, 3f,   0.5f),  // top 1.08
                    new BoxDef("R0_Step2",      DP, -15.5f, 1.4f,   3f,   0.5f),  // top 1.65
                    new BoxDef("R0_Step3",      DP, -11.5f, 1.975f, 3f,   0.5f),  // top 2.23
                },
                checkpoints = new[] { new CheckpointDef("CP_R0", -46f, 1.2f) },
                // the blank-paper door: a tall blank sheet perched high on the far ledge;
                // walking in begins the enter-photo cinematic
                exits = new[] { new ExitDef(-6f, 4.4f, 1.2f, 3.2f) },
                hints = new[]
                {
                    // shown at the drying line, under the blank eleventh frame
                    new HintDef("Frame 11 is blank.", -25f, 2.3f, 2.4f, 1.8f),
                    new HintDef("The print needs darkness — press 1, then climb what develops.", -22.5f, 1.9f, 2.2f, 1.8f),
                    new HintDef("Bring back the room.", -7.5f, 3.3f, 2f, 2f),
                },
            },

            // Room 1 — The Long Exposure (x ≈ 5.5 … 20)
            // First SIGHTING of a light-trail: a pre-authored glowing streak the
            // dark prints across the gap. No skill — press 1 and walk it. (The
            // "draw your own" verb is held back to Room 5; this only plants the
            // idea that light-trails exist.)
            new RoomDef
            {
                name = "R1_LongExposure",
                title = "THE LONG EXPOSURE",
                objectives = new[] { "Walk the path the dark reveals" },
                boxes = new[]
                {
                    // a 7.5-wide gap (un-jumpable: a running jump covers ~5.6) — the
                    // only crossing is the dark trail revealed in UNDER
                    new BoxDef("R1_Floor",   SG,  8f,  0f, 5f, 1f),   // x5.5-10.5, top 0.5
                    new BoxDef("R1_Landing", SG,  19f, 0f, 2f, 1f),   // x18-20,   top 0.5
                },
                checkpoints = new[] { new CheckpointDef("CP_R1", 6.2f, 1.2f) },
                // the pre-authored streak: a flat lead-in that OVERLAPS the floor at
                // its surface (endpoints dropped by the 0.07 edgeRadius so the streak's
                // top is flush with the 0.5 ground — no lip to catch the feet), a gentle
                // bow, then a flat lead-out overlapping the landing. Solid only in UNDER.
                trails = new[]
                {
                    new TrailDef("R1_SeeTrail", new[]
                    {
                        new Vector2(10.0f, 0.43f), new Vector2(10.5f, 0.43f),
                        new Vector2(12.6f, 1.25f), new Vector2(14.25f, 1.5f),
                        new Vector2(15.9f, 1.25f),
                        new Vector2(18.0f, 0.43f), new Vector2(18.5f, 0.43f),
                    }),
                },
                hints = new[]
                {
                    new HintDef("A long exposure draws the whole crossing — walk its length.", 9.5f, 1.8f, 4.5f, 2f),
                },
                // a lost frame above the dark trail — only reachable in Under (the verb)
                lostFrames = new[] { new LostDef("R1_Lost", 14.25f, 2.6f, 0.8f, 0.8f) },
            },

            // Room 2 — The White Wall (x ≈ 20 … 32.5) — FLASH
            new RoomDef
            {
                name = "R2_WhiteWall",
                title = "THE WHITE WALL",
                objectives = new[] { "Collect the flash", "Burn through the barrier" },
                boxes = new[]
                {
                    new BoxDef("R2_Floor",      SG, 26.25f, 0f,    12.5f, 1f),
                },
                // the white wall no longer just vanishes in OVER — you must hold
                // the flash on it until it burns through (and the eyes wake while
                // you do). A committed, world-altering act.
                // top 3.5 (was 2.5, exactly the 2.489 apex — un-jumpable by only 0.011u);
                // raised for a comfortable can't-jump-it margin so burning stays forced
                burns = new[] { new BurnDef("R2_BurnWall", 24.5f, 2.0f, 0.5f, 3.0f) },
                pickups = new[] { new PickupDef(Ability.Flash, 22f, 1.2f) },
                enemies = new[] { new EnemyDef("R2_Enemy", 28f, 1.0f, 0f, 0f) },
                checkpoints = new[] { new CheckpointDef("CP_R2", 20.6f, 1.2f) },
                hints = new[]
                {
                    // burn + sleep merged into one operational line (R2 still allows op)
                    new HintDef("Hold OVER to burn the wall — it wakes the eyes; press 2 to pass.", 23.5f, 2.0f, 4.4f, 2f),
                },
            },

            // Room 3 — The Latent Image (x ≈ 32.5 … 42) — FixPlatform debut.
            // The player's first PRINTED footing. Two latent grain-slabs hang over a
            // gap to the Contact-Sheet ledge, intangible until you flash OVER (3)
            // beside one — then it DEVELOPS solid and STAYS, even back in Normal.
            // Footing you PLAN, not a state you hold; completes the commit-verb trio
            // (R2 burns to commit, R3 PRINTS, R5 draws). The R4 floor sits +3.0 over the
            // ~2.49 jump apex so printing is mandatory; slab A is placed so A ALONE can't
            // reach R4 (un-jumpable), forcing B as a mandatory SECOND print — the rule
            // reads as a rule, not a one-off. A low catch makes a mistimed flash a
            // climb-back, never a death — and the prints persist, so you never redo them.
            new RoomDef
            {
                name = "R3_TheLatentImage",
                title = "THE LATENT IMAGE",
                objectives = new[] { "Print the latent footing, then climb" },
                boxes = new[]
                {
                    new BoxDef("R3_Floor",  SG, 36.25f, 0f,    7.5f, 1f),  // x32.5-40, top 0.5
                    // non-lethal catch under the gap: a mistimed jump drops here, then
                    // climb back onto the floor (left of the prints) and retry
                    new BoxDef("R3_Catch",  SG, 40f,   -1.5f,  6f,   1f),  // x37-43, top -1.0
                },
                // two latent slabs = a FORCED two-print climb to R4's floor (top 3.5).
                // A (right edge x37.0) sits 5.0u from R4's near edge (x42) at +1.5 dy —
                // past the 4.54u reach — so printing A alone can't reach R4. B is therefore
                // the mandatory second print: floor → print A → climb A → B → hop to R4.
                // Both prints are used; the print rule reads as a rule, not a one-off.
                fixPlats = new[]
                {
                    new FixDef("R3_LatentA", 36.2f, 1.8f, 1.6f, 0.4f),  // x35.4-37.0, top 2.0 (A→R4 un-jumpable)
                    new FixDef("R3_LatentB", 39.0f, 3.3f, 1.6f, 0.4f),  // x38.2-39.8, top 3.5 → hop to R4
                },
                checkpoints = new[] { new CheckpointDef("CP_R3", 33.2f, 1.2f) },
                hints = new[]
                {
                    new HintDef("Flash the ghost slab (3) and it prints — solid for good. Climb.", 36.5f, 1.8f, 4.5f, 2f),
                },
            },

            // Room 4 — Contact Sheet (x ≈ 42 … 58.5)
            new RoomDef
            {
                name = "R4_ContactSheet",
                title = "CONTACT SHEET",
                objectives = new[] { "Ride the light to the top" },
                boxes = new[]
                {
                    new BoxDef("R4_Floor",     SG, 47f, 3f, 10f, 1f),
                    new BoxDef("R4_HighLedge", SG, 56f, 8.5f, 5f, 1f),
                },
                // a light lift rises from the floor's right edge over a deadly
                // drop — OVER summons it, ride it up to the ledge (the mirror of
                // R9's sinking shadow). Let the light go mid-air and you fall.
                // Sized to FILL the gap exactly (w1.5 @ cx52.75 → spans [52, 53.5]):
                // left flush with the floor's right edge (52), right flush with the
                // HighLedge's left edge (53.5). A wider slab overlapped the ledge and
                // pinched the player against its left edge at the top of the ride.
                riseLifts = new[] { new RiseDef("R4_RiseLift", 52.75f, 8.7f, 3.2f, 1.5f, 0.6f) },
                checkpoints = new[] { new CheckpointDef("CP_R4", 43f, 4.2f) },
                hints = new[]
                {
                    new HintDef("A slab of light rises — ride it up; lose the light and you fall.", 49f, 4.6f, 5f, 2f),
                },
            },

            // Room 5 — First Stroke (x ≈ 58.5 … 73) — SHUTTER
            // The halfway point and the player's FIRST drawing. Burn the gate
            // (revision of R2), drop onto the wide pad and grab the Shutter — then
            // the far ledge sits a full +3 above the pad, past the ~2.5 jump
            // ceiling (JumpForce 12.5 / gravity 3.2), so it CANNOT be jumped. One
            // drawn step in the air breaks the climb, then hop up. The watcher
            // waits on the ledge, after the stroke. Checkpoint is on the pad so a
            // failed stroke retries instantly (no re-burning the gate).
            new RoomDef
            {
                name = "R5_FirstStroke",
                title = "FIRST STROKE",
                objectives = new[] { "Draw a step, then climb to the ledge" },
                boxes = new[]
                {
                    new BoxDef("R5_GateCeiling", SG, 59.5f, 12.5f, 3f,  0.4f),
                    new BoxDef("R5_Pad",         SG, 63f,   4.25f, 6f,  0.5f),  // top 4.5, x60-66
                    new BoxDef("R5_FarLedge",    SG, 70.5f, 7f,    5f,  1f),     // top 7.5, +3 above the pad
                },
                // entry gate is white paper: hold OVER to burn through (revision)
                burns = new[] { new BurnDef("R5_BurnGate", 59.5f, 10.5f, 0.5f, 3f) },
                // the Shutter: grabbed on the pad, used at once on the climb
                pickups = new[] { new PickupDef(Ability.Shutter, 64f, 5.4f) },
                // a lost frame high above the pad — only reachable by DRAWING a step
                lostFrames = new[] { new LostDef("R5_Lost", 62f, 7.8f, 0.8f, 0.8f) },
                // moved onto the far ledge — a guard met AFTER the stroke, not during
                enemies = new[] { new EnemyDef("R5_PatrolEnemy", 71.5f, 7.9f, 0.8f, 1.0f) },
                // checkpoint on the pad (past the gate) so draw-retries are instant
                checkpoints = new[] { new CheckpointDef("CP_R5", 62f, 5.4f) },
                hints = new[]
                {
                    new HintDef("Too high to jump — draw a step in the air, then climb.", 64.5f, 6f, 3.6f, 2.5f),
                },
            },

            // Room 6 — Sensor Test (x ≈ 73 … 93.5)
            // A diptych of the two ways to "activate": deliver your BODY (the photo
            // sensor — stand on it in OVER) then deliver LIGHT (the light-meter at the
            // exit). The meter beat is the dual-use, rebuilt as a STEP, not a wall-climb:
            // the exit ledge sits +3 over the launch pad (top 6.5 > 3.5 + 2.49 apex), so
            // it cannot be jumped — you draw ONE bright step at the jump's apex (the same
            // motion as R5) to reach it, and that step's light (a BrightStroke is a
            // LightField emitter) wakes the meter on its near lip and opens the gate. One
            // natural stroke moves you AND activates. In UNDER a dark step lifts you the
            // exact same way but feeds the meter NOTHING (a safelight does not expose a
            // light meter — see the Over-gated player glow in PlayerController), so it
            // stays cold: the "why can't I do it in Under?" question answers itself.
            // Soft-lock-proof: CP on the launch pad, a 2-wide exit ledge to redraw from,
            // ~0.6 lux from the step vs 0.4 threshold, Door_R6L opens for the run. Mirrors
            // R5's proven geometry (pad → +3 ledge over a 2-wide gap, "draw a step").
            new RoomDef
            {
                name = "R6_SensorTest",
                title = "SENSOR TEST",
                objectives = new[] { "Trip the photo sensor with your body", "Then draw light to the meter" },
                boxes = new[]
                {
                    new BoxDef("R6_StepDownA",    SG, 74.5f,  5.5f, 2f,   1f),
                    new BoxDef("R6_StepDownB",    SG, 77f,    4f,   2f,   1f),
                    // body-sensor floor, shrunk to x78-86 to free the finale's room
                    new BoxDef("R6_Floor",        SG, 82f,    3f,   8f,   1f),     // x78-86, top 3.5
                    new BoxDef("R6_DarkShelfA",   DP, 82f,    4.6f, 1.2f, 0.35f),
                    new BoxDef("R6_DarkShelfB",   DP, 83.5f,  5.6f, 1.2f, 0.35f),
                    new BoxDef("R6_SensorAnchor", SG, 84.5f,  6.3f, 1.5f, 0.5f),   // top 6.55
                    new BoxDef("R6_DoorCeiling",  SG, 84.5f,  8.4f, 5f,   0.4f),   // anti-skip over Door_R6
                    // the launch pad past Door_R6 — runway to build speed for the draw
                    new BoxDef("R6_LaunchPad",    SG, 87.75f, 3f,   3.5f, 1f),     // x86-89.5, top 3.5
                    // the exit ledge: +3 over the launch (un-jumpable), reached by a
                    // drawn bright step at the apex; the meter rides its near lip
                    new BoxDef("R6_ExitLedge",    SG, 92.5f,  6f,   2f,   1f),     // x91.5-93.5, top 6.5
                },
                // the body sensor (Door_R6) and the light-meter (Door_R6L) at the exit.
                sensors = new[]
                {
                    new SensorDef("R6_PhotoSensor", 84.5f, 7.2f, "Door_R6"),
                    new SensorDef("R6_LightMeter",  91.5f, 6.2f, "Door_R6L", 1, 0.4f), // LocalLux at the exit lip
                },
                doors = new[]
                {
                    new DoorDef("Door_R6",  86f,   5.75f, 0.6f, 4.5f),  // y3.5-8.0, meets the ceiling
                    new DoorDef("Door_R6L", 93.4f, 8f,    0.4f, 4f),    // y6.0-10.0 — off the exit ledge, too tall to jump
                },
                checkpoints = new[]
                {
                    new CheckpointDef("CP_R6",  78.8f, 4.2f),
                    new CheckpointDef("CP_R6b", 87.5f, 4.2f), // on the launch pad → instant step retries
                },
                hints = new[]
                {
                    new HintDef("Stand on real ground, then OVER to trip the sensor.", 84.5f, 7.9f, 4f, 2f),
                    new HintDef("Too high to jump — draw a bright step in OVER; its light wakes the meter and opens the gate.", 88f, 5.0f, 4.2f, 2f),
                },
            },

            // Room 7 — Still Life (x ≈ 93.5 … 107.5)
            // The first room that asks for TWO verbs at once. The ledge is raised
            // to top 7.5 — un-jumpable from the floor (apex 5.99 << 7.5, so the
            // statue is needed as a base) AND un-jumpable off the frozen statue
            // (statue top 4.4 + 2.49 apex = 6.89 < 7.5, so one drawn step is
            // needed on top of that). Park the statue at the ledge's left lip
            // (x≈103), freeze in UNDER, climb on, draw one dark step up its left
            // face, hop onto the ledge. statue + draw, held together in UNDER.
            new RoomDef
            {
                name = "R7_StillLife",
                title = "STILL LIFE",
                objectives = new[] { "Freeze a statue at the ledge", "Draw the last step up" },
                boxes = new[]
                {
                    new BoxDef("R7_Floor",     SG, 99f,    3f, 11f, 1f),     // top 3.5
                    // raised top 6.5 -> 7.5: past both the floor-jump (5.99) and
                    // the statue-jump (6.89), so it cannot be reached without a
                    // drawn step taken from atop the parked statue
                    new BoxDef("R7_HighLedge", SG, 105.5f, 7f, 4f,  1f),     // top 7.5
                },
                // ALTERNATE route (player expression — the back third opens to combinations):
                // a latent slab you can PRINT to climb instead of drawing off the frozen
                // statue. Print from R7_Floor (top 3.5, ~2.2u < 3.2 range), then hop printed
                // slab (5.8) → ledge (7.5, +1.7). A ghost until printed, so a verb is still
                // mandatory; freeze the patroller first to print in peace — freeze + print.
                fixPlats = new[]
                {
                    new FixDef("R7_LatentLedge", 102.9f, 5.3f, 1.6f, 0.4f),  // x102.1-103.7, top 5.5 (hand-tuned in editor)
                },
                enemies = new[] { new EnemyDef("R7_PatrolEnemy", 101f, 4.0f, 2.0f, 1.25f) },
                checkpoints = new[] { new CheckpointDef("CP_R7", 94.3f, 4.2f) },
                hints = new[]
                {
                    // R7+: darkroom-narrative voice, one line. Carries the statue
                    // lesson AND the draw nudge that A1 needs, without a tutorial tone.
                    new HintDef("Light wakes them, dark makes them stone — stand on her, draw the rest.", 99f, 4.8f, 5f, 2f),
                },
            },

            // Room 8 — Negative Transfer (x ≈ 107.5 … 125)
            new RoomDef
            {
                name = "R8_NegativeTransfer",
                title = "NEGATIVE TRANSFER",
                objectives = new[] { "Land on real ground before you switch" },
                // two WIDE bridges over a deadly drop, each spanning a gap far
                // too wide to jump — a light bridge (OVER) then a dark bridge
                // (UNDER), with one real anchor between. You can't skip either,
                // so you MUST switch exposure; and you can only switch on the
                // anchor (switch on a bridge and it vanishes beneath you). You
                // arrive on the dark side, so the watcher stays stone as you land.
                boxes = new[]
                {
                    new BoxDef("R8_Start",     SG, 108.5f, 6f,   2f,   1f),    // x107.5-109.5
                    new BoxDef("R8_Anchor1",   SG, 116.2f, 7.5f, 1.4f, 0.5f),  // x115.5-116.9 — the only switch point
                    new BoxDef("R8_DarkBridge",DP, 119.9f, 8.0f, 6f,   0.4f),  // x116.9-122.9, UNDER-only
                    new BoxDef("R8_MidLedge",  SG, 123.95f,8.5f, 2.1f, 0.6f),  // x122.9-125
                },
                bridges = new[] { new BridgeDef("R8_LightBridge", 112.5f, 7.0f, 6f, 0.4f) }, // x109.5-115.5, OVER-only
                // ALTERNATE (printer's handhold, light-bridge side ONLY — the dark bridge is
                // UNDER-only, so you can't be in OVER to print on it). Print from R8_Start
                // (top 6.5, already OVER for the light bridge, ~2.6u < 3.2) for a permanent
                // rung if you'd rather not trust the OVER-only bridge. Does NOT bypass the
                // anchor switch or the dark bridge — the "switch only on real ground" rule holds.
                fixPlats = new[]
                {
                    new FixDef("R8_LatentStep", 112.4f, 8.2f, 1.6f, 0.4f),  // x111.6-113.2, top 8.4 (hand-tuned in editor)
                },
                // a lost frame above the light bridge — only reachable in OVER (the verb)
                lostFrames = new[] { new LostDef("R8_Lost", 112f, 9.0f, 0.8f, 0.8f) },
                enemies = new[] { new EnemyDef("R8_Enemy", 124f, 9.4f, 0f, 0f) },
                checkpoints = new[] { new CheckpointDef("CP_R8", 108.3f, 7.2f, "frame 9 — nothing carries over. nothing ever does.") },
                hints = new[]
                {
                    new HintDef("Each bridge lives in one light — change only where the ground is real.", 110.5f, 7.8f, 5.5f, 2f),
                },
            },

            // Room 9 — The Drop (upper x ≈ 125 … 130.5; corridor y ≈ −1.5)
            new RoomDef
            {
                name = "R9_TheDrop",
                title = "THE DROP",
                objectives = new[] { "Find a way down the shaft" },
                boxes = new[]
                {
                    new BoxDef("R9_ArrivalLedge",  SG, 125.9f,  9f,    1.8f, 1f),
                    // the old DarkPath floor-hatch is gone — the descent is now a
                    // shadow lift (see lifts[] below)
                    new BoxDef("R9_SealWall",      SG, 130f,    11.5f, 1f,   9f),
                    new BoxDef("R9_CapCeiling",    SG, 127.25f, 12.5f, 5.5f, 0.5f),
                    new BoxDef("R9_ShaftLeftWall", SG, 126.3f,  3.5f,  1f,   10f),
                    // corridor left edge pulled to 129.2 so the shaft (126.8-129.2)
                    // drops into a deadly void — only the shadow lift crosses it
                    new BoxDef("R9_CorridorFloor", SG, 135.1f, -2f,    11.8f, 1f),
                    new BoxDef("R9_StairsUpA",     SG, 139.5f, -0.5f,  2f,   1f),
                    new BoxDef("R9_StairsUpB",     SG, 141.75f, 1f,    2.5f, 1f),
                },
                checkpoints = new[]
                {
                    new CheckpointDef("CP_R9a", 125.6f, 10.2f, "frame 10 — a dead end, printed."),
                    new CheckpointDef("CP_R9b", 131f, -0.7f, "frame 10 — the floor was a photograph too."),
                },
                hints = new[]
                {
                    // cryptic on purpose: the descent must be DISCOVERED (go dark,
                    // like every hidden footing since R1), not handed over
                    new HintDef("The light prints no way down.", 126f, 10.4f, 4f, 2.4f),
                    // greets the relight at the stairs instead of interrupting the
                    // dark (left edge 139.6: strictly past the relight at 138.5)
                    new HintDef("Developed downward. Keep going.", 141.6f, 0.2f, 4f, 2.6f),
                    // (the UmbralBarrier "curtain" teach that used to sit here at the
                    // stairs is gone — it diluted the drop. THE DROP now ends as a
                    // pure, quiet set-piece: trust-fall, dark walk, relight, stairs,
                    // straight into the finale. The curtain debuts in R10 instead.)
                },
                // the descent: a shadow slab, real only in UNDER (invisible +
                // intangible otherwise). Rests LOW (topY 2.5) — the camera clamps
                // its centre at y9, so the frame bottom is ~y3.5 and the slab sits
                // just under it: you can't see it from the ledge edge. You commit
                // to the dark drop and the shadow rises to catch you partway down.
                lifts = new[] { new LiftDef("R9_ShadowLift", 128f, 2.5f, -1.8f, 2.4f, 0.6f) },
            },

            // Room 10 — The Final Print (x ≈ 142.5 … 176)
            new RoomDef
            {
                name = "R10_FinalPrint",
                title = "THE FINAL PRINT",
                objectives = new[] { "Draw your bridge, anchor, burn", "Take the final photograph" },
                boxes = new[]
                {
                    new BoxDef("R10_StartPlatform", SG, 144.2f, 2.5f, 3.4f, 1f),
                    // first anchor — raised to +2.7 above the start platform (past
                    // the ~2.5 jump ceiling) so the opening gap MUST be drawn, not
                    // jumped: the player draws their own bridge here (the capstone)
                    new BoxDef("R10_AnchorA",       SG, 150.8f, 5.4f, 1.5f, 0.6f),
                    new BoxDef("R10_CeilingA",      SG, 152.3f, 8.1f, 3f,   0.4f),
                    new BoxDef("R10_SensorFloor",   SG, 155.8f, 4f,   5f,   1f),
                    new BoxDef("R10_DoorCeiling",   SG, 157.4f, 7.4f, 3f,   0.4f),
                    new BoxDef("R10_PostFloor",     SG, 159.8f, 4f,   3f,   1f),
                    new BoxDef("R10_DarkClueStep",  DP, 163f,   5.2f, 1f,   0.3f),
                    new BoxDef("R10_AnchorB",       SG, 167f,   6f,   1.5f, 0.6f),
                    new BoxDef("R10_FinalCeiling",  SG, 169f,   9.5f, 3f,   0.4f),
                    new BoxDef("R10_ExitPlatform",  SG, 172.5f, 6f,   5f,   1f),
                    new BoxDef("R10_RightWall",     SG, 176f,   8f,   1f,   6f),
                },
                // ALTERNATE opening route (player expression): the crossing to
                // AnchorA can be DRAWN (the intended capstone) OR PRINTED — one
                // latent slab you flash solid, then hop across. Neither is required
                // and the exit is never gated on it; it's drawer-vs-printer, your call.
                // Still un-crossable with no verb (the slab is a ghost until printed).
                fixPlats = new[]
                {
                    new FixDef("R10_LatentSpan", 147.5f, 4.3f, 1.6f, 0.4f),  // x146.7-148.3, top 4.5
                },
                // a lost frame above the latent span — only reachable by PRINTING it
                lostFrames = new[] { new LostDef("R10_Lost", 147.5f, 6.6f, 0.8f, 0.8f) },
                sensors = new[]
                {
                    new SensorDef("R10_PhotoSensor", 155.3f, 5.4f, "Door_R10"),
                    // the FIXER WASH: a doorless light-meter by the exit. Lay a bright
                    // stroke on it to DEVELOP the final print — colour floods the world
                    // and the exit unseals. Colour is the last verb she spends. Threshold
                    // 0.4 (> the player's own glow) so only a drawn stroke trips it.
                    new SensorDef("R10_WashTray", 173f, 6.9f, 0.4f, true),
                },
                doors = new[] { new DoorDef("Door_R10", 157.4f, 5.75f, 0.6f, 2.5f) },
                enemies = new[] { new EnemyDef("R10_Guard", 171.5f, 7.4f, 1.0f, 1.25f) },
                checkpoints = new[]
                {
                    new CheckpointDef("CP_R10a", 143.2f, 3.7f, "frame 11 — unexposed."),
                    new CheckpointDef("CP_R10b", 158.9f, 5.2f, "frame 11 — still unexposed."),
                },
                hints = new[]
                {
                    new HintDef("The last print is yours alone — cross by your own hand.", 145.5f, 3.8f, 4f, 2f),
                    new HintDef("A blackout curtain — flood it in Over, or draw a bright stroke across it, and pass.", 159.0f, 4.9f, 3.6f, 1.8f),
                    new HintDef("The last frame is not empty — its subject wakes with the light.", 162.5f, 5.6f, 3.2f, 2f),
                    new HintDef("Develop the last print — lay your light on the tray.", 172f, 8f, 3.4f, 1.8f),
                },
                // the opening beat is now a PLAYER-DRAWN bridge, not a pre-authored
                // streak: R10_AnchorA is raised past jump height (above), so the
                // player must draw their own way across — the drawing capstone.
                // (the old pre-authored R10_DarkTrail is gone; "see a trail" lives
                // in R1, "draw a simple one" in R5, "draw complex" here)
                // both white barriers are paper now — burn each in OVER; the
                // final one burns right under the guard's nose
                burns = new[]
                {
                    new BurnDef("R10_BurnA",     152.3f, 6.4f, 0.5f, 3f),
                    new BurnDef("R10_BurnFinal", 169f,   7.8f, 0.5f, 3f),
                },
                // the blackout curtain before the final print — flood it with light
                // (Over) and step through. The curtain's DEBUT now that R9's drop
                // stays a pure set-piece: it meets the player once, in the finale.
                umbrals = new[] { new UmbralDef("R10_PostUmbra", 160.0f, 6.0f, 0.5f, 3.0f, 0.3f) },
                exits = new[] { new ExitDef(174.2f, 7.5f, 1f, 2f) },
            },
        };
    }
}
