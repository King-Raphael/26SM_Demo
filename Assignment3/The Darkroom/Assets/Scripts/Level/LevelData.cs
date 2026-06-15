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
        public SensorDef(string name, float cx, float cy, string doorId)
        { this.name = name; this.cx = cx; this.cy = cy; this.doorId = doorId; this.mode = 0; this.lux = 0.6f; }
        public SensorDef(string name, float cx, float cy, string doorId, int mode, float lux)
        { this.name = name; this.cx = cx; this.cy = cy; this.doorId = doorId; this.mode = mode; this.lux = lux; }
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
    }

    public static class LevelData
    {
        /// Left edge of each room (index = room number); used by the HUD
        /// to show "ROOM N : TITLE" from the player's x position.
        public static readonly float[] RoomStarts =
            { -7.5f, 5.5f, 20f, 32.5f, 42f, 58.5f, 73f, 93.5f, 107.5f, 125f, 142.5f };

        public static int RoomIndexAt(float x)
        {
            int idx = 0;
            for (int i = 0; i < RoomStarts.Length; i++)
                if (x >= RoomStarts[i]) idx = i;
            return idx;
        }

        const ExposureObjectType SG = ExposureObjectType.StaticGround;
        const ExposureObjectType DP = ExposureObjectType.DarkPath;
        // BrightBarrier is retired from the levels — every white wall is now a
        // BurnPaper (hold OVER to burn through). The type/code still exists.

        public static readonly RoomDef[] Rooms = new RoomDef[]
        {
            // Room 0 — Calibration Strip (x ≈ −6.5 … 5.5)
            new RoomDef
            {
                name = "R0_CalibrationStrip",
                title = "THE CALIBRATION STRIP",
                objectives = new[] { "Collect the negative", "Stand on the dark, then let it go" },
                boxes = new[]
                {
                    new BoxDef("R0_LeftWall",   SG, -7f,   3f,   1f,    8f),
                    new BoxDef("R0_Floor",      SG, -0.5f, 0f,   12f,   1f),
                    new BoxDef("R0_DarkSample", DP,  1.0f, 1.6f, 1.4f,  0.35f),
                },
                // tutorial chain: move/jump -> negative (Under) -> flash (Over) -> shutter
                pickups = new[] { new PickupDef(Ability.Negative, -1.2f, 1.2f) },
                checkpoints = new[] { new CheckpointDef("CP_R0", -3f, 1.2f) },
                hints = new[]
                {
                    new HintDef("A/D or arrow keys to move. SPACE to jump.", -3.5f, 1.6f, 3f, 2f),
                    new HintDef("A film negative. Press 1: UNDEREXPOSED — see what hides in the dark.", -1.2f, 1.9f, 3f, 2f),
                    new HintDef("Stand on the dark platform. Then press 2. What is not lit, is not there.", 1.5f, 2.6f, 3.5f, 2f),
                },
            },

            // Room 1 — Invisible Steps (x ≈ 5.5 … 20)
            new RoomDef
            {
                name = "R1_InvisibleSteps",
                title = "INVISIBLE STEPS",
                objectives = new[] { "Cross the unlit path" },
                boxes = new[]
                {
                    // gap widened 4.5 -> 7.5 (a running jump covers ~5.6, with
                    // coyote ~6.3 — the spec gap was directly skippable without
                    // ever pressing 1); steps widened 1.0 -> 1.6 so the taught
                    // route is easier than before once revealed
                    new BoxDef("R1_Floor",    SG,  8f,     0f,   5f,   1f),
                    new BoxDef("R1_DarkStepA",DP,  12.0f,  0.2f, 1.6f, 0.4f),
                    new BoxDef("R1_DarkStepB",DP,  14.25f, 0.6f, 1.6f, 0.4f),
                    new BoxDef("R1_DarkStepC",DP,  16.5f,  0.2f, 1.6f, 0.4f),
                    new BoxDef("R1_Landing",  SG,  19f,    0f,   2f,   1f),
                },
                checkpoints = new[] { new CheckpointDef("CP_R1", 6.2f, 1.2f) },
                hints = new[]
                {
                    new HintDef("UNDER reveals what the light skipped — the path was never printed.", 8.5f, 1.6f, 4.5f, 2f),
                },
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
                burns = new[] { new BurnDef("R2_BurnWall", 24.5f, 1.75f, 0.5f, 2.5f) },
                pickups = new[] { new PickupDef(Ability.Flash, 22f, 1.2f) },
                enemies = new[] { new EnemyDef("R2_Enemy", 28f, 1.0f, 0f, 0f) },
                checkpoints = new[] { new CheckpointDef("CP_R2", 20.6f, 1.2f) },
                hints = new[]
                {
                    new HintDef("This white wall won't just vanish — hold OVEREXPOSED on it and it BURNS. Wait for the hole.", 22f, 2.3f, 4.2f, 2f),
                    new HintDef("Flash too long and the eyes come back red. Press 2 — in NORMAL they sleep. Every photograph has a subject.", 26.5f, 1.6f, 4f, 2f),
                },
            },

            // Room 3 — First Stroke (x ≈ 32.5 … 42) — SHUTTER
            new RoomDef
            {
                name = "R3_FirstStroke",
                title = "FIRST STROKE",
                objectives = new[] { "Collect the shutter", "Climb your own photograph" },
                boxes = new[]
                {
                    new BoxDef("R3_Floor",      SG, 36.25f, 0f,    7.5f, 1f),
                    new BoxDef("R3_ClimbBlock", SG, 41f,    1.75f, 2f,   3.5f),
                },
                pickups = new[] { new PickupDef(Ability.Shutter, 34.5f, 1.2f) },
                checkpoints = new[] { new CheckpointDef("CP_R3", 33.2f, 1.2f) },
                hints = new[]
                {
                    new HintDef("Hold SHIFT while moving in UNDER or OVER to draw light. RELEASE to fix it.", 36.5f, 1.8f, 5f, 2f),
                    new HintDef("Draw yourself a step. Fix it. Climb your own photograph.", 39f, 2.2f, 2.5f, 2f),
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
                riseLifts = new[] { new RiseDef("R4_RiseLift", 53f, 8.7f, 3.2f, 2f, 0.6f) },
                checkpoints = new[] { new CheckpointDef("CP_R4", 43f, 4.2f) },
                hints = new[]
                {
                    new HintDef("Press 3 (OVER): a slab of light rises from the right edge — ride it up. Let the light go mid-air and you fall.", 49f, 4.6f, 5f, 2f),
                },
            },

            // Room 5 — Blown Bridge (x ≈ 58.5 … 73)
            new RoomDef
            {
                name = "R5_BlownBridge",
                title = "BLOWN BRIDGE",
                objectives = new[] { "Bridge the gap in full light" },
                boxes = new[]
                {
                    new BoxDef("R5_GateCeiling", SG, 59.5f, 12.5f, 3f,   0.4f),
                    new BoxDef("R5_EnemyPerch",  SG, 64f,   6.5f,  1.6f, 0.5f),
                    new BoxDef("R5_FarLedge",    SG, 70.5f, 7f,    5f,   1f),
                },
                // the entry gate is white paper now: hold OVER to burn through —
                // and your light wakes their eyes while you do
                burns = new[] { new BurnDef("R5_BurnGate", 59.5f, 10.5f, 0.5f, 3f) },
                enemies = new[] { new EnemyDef("R5_PatrolEnemy", 64f, 7.3f, 0.6f, 1.0f) },
                checkpoints = new[] { new CheckpointDef("CP_R5", 57.5f, 9.7f) },
                hints = new[]
                {
                    new HintDef("Hold OVER on the white gate to BURN through — your light wakes their eyes. Then fix your bridge midair.", 57.5f, 10.1f, 3.2f, 2f),
                },
            },

            // Room 6 — Sensor Test (x ≈ 73 … 93.5)
            new RoomDef
            {
                name = "R6_SensorTest",
                title = "SENSOR TEST",
                objectives = new[] { "Stand on real ground", "Trip the photo sensor" },
                boxes = new[]
                {
                    new BoxDef("R6_StepDownA",    SG, 74.5f,  5.5f, 2f,   1f),
                    new BoxDef("R6_StepDownB",    SG, 77f,    4f,   2f,   1f),
                    new BoxDef("R6_Floor",        SG, 83.5f,  3f,   11f,  1f),
                    new BoxDef("R6_DarkShelfA",   DP, 83f,    4.6f, 1.2f, 0.35f),
                    new BoxDef("R6_DarkShelfB",   DP, 84.5f,  5.6f, 1.2f, 0.35f),
                    new BoxDef("R6_SensorAnchor", SG, 86.2f,  6.3f, 1.5f, 0.5f),
                    // raised + widened over the anchor: the old ceiling top (6.6) was
                    // flush with the anchor (6.55) — you could walk over the door
                    new BoxDef("R6_DoorCeiling",  SG, 87.5f,  8.4f, 5f,   0.4f),
                    new BoxDef("R6_PostFloor",    SG, 91.25f, 3f,   4.5f, 1f),
                },
                sensors = new[] { new SensorDef("R6_PhotoSensor", 86.2f, 7.2f, "Door_R6") },
                doors = new[] { new DoorDef("Door_R6", 88f, 5.75f, 0.6f, 4.5f) }, // y 3.5-8.0, meets the raised ceiling
                checkpoints = new[] { new CheckpointDef("CP_R6", 78.8f, 4.2f) },
                hints = new[]
                {
                    new HintDef("UNDER reveals the route up.", 80f, 4.4f, 4f, 2f),
                    new HintDef("Stand on real ground. Then OVEREXPOSE to trip the sensor.", 86f, 7.9f, 4f, 2f),
                },
            },

            // Room 7 — Still Life (x ≈ 93.5 … 107.5)
            new RoomDef
            {
                name = "R7_StillLife",
                title = "STILL LIFE",
                objectives = new[] { "Park a statue under the ledge" },
                boxes = new[]
                {
                    new BoxDef("R7_Floor",     SG, 99f,    3f, 11f, 1f),
                    new BoxDef("R7_HighLedge", SG, 105.5f, 6f, 4f,  1f),
                },
                enemies = new[] { new EnemyDef("R7_PatrolEnemy", 101f, 4.0f, 2.0f, 1.25f) },
                checkpoints = new[] { new CheckpointDef("CP_R7", 94.3f, 4.2f) },
                hints = new[]
                {
                    new HintDef("Light wakes them. Darkness turns them to stone. Stone bears weight.", 97.5f, 4.6f, 6f, 2f),
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
                enemies = new[] { new EnemyDef("R8_Enemy", 124f, 9.4f, 0f, 0f) },
                checkpoints = new[] { new CheckpointDef("CP_R8", 108.3f, 7.2f, "frame 9 — nothing carries over. nothing ever does.") },
                hints = new[]
                {
                    new HintDef("OVER for the bright bridge, then UNDER for the dark — the gaps are too wide to jump. Switch only on the anchor between them, or the bridge drops you.", 110.5f, 7.8f, 5.5f, 2f),
                },
            },

            // Room 9 — The Drop (upper x ≈ 125 … 130.5; corridor y ≈ −1.5)
            new RoomDef
            {
                name = "R9_TheDrop",
                title = "THE DROP",
                objectives = new[] { "Ride the shadow down", "Don't let the light in" },
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
                    new HintDef("In the light this shaft is empty — step in and you fall forever. Press 1 (UNDER): a shadow rises to catch you, and sinks you down. Keep it dark all the way.", 126f, 10.4f, 4f, 2.4f),
                    // greets the relight at the stairs instead of interrupting the
                    // dark (left edge 139.6: strictly past the relight at 138.5)
                    new HintDef("Developed downward. Keep going.", 141.6f, 0.2f, 4f, 2.6f),
                },
                // the descent: a shadow slab where the floor was. UNDER sinks it
                // (carrying her down the shaft), NORMAL holds, OVER burns it away.
                lifts = new[] { new LiftDef("R9_ShadowLift", 128f, 7f, -1.8f, 2.4f, 0.6f) },
            },

            // Room 10 — The Final Print (x ≈ 142.5 … 176)
            new RoomDef
            {
                name = "R10_FinalPrint",
                title = "THE FINAL PRINT",
                objectives = new[] { "Reveal, draw, anchor, burn", "Take the final photograph" },
                boxes = new[]
                {
                    new BoxDef("R10_StartPlatform", SG, 144.2f, 2.5f, 3.4f, 1f),
                    new BoxDef("R10_AnchorA",       SG, 150.8f, 4.6f, 1.5f, 0.6f),
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
                sensors = new[] { new SensorDef("R10_PhotoSensor", 155.3f, 5.4f, "Door_R10") },
                doors = new[] { new DoorDef("Door_R10", 157.4f, 5.75f, 0.6f, 2.5f) },
                enemies = new[] { new EnemyDef("R10_Guard", 171.5f, 7.4f, 1.0f, 1.25f) },
                checkpoints = new[]
                {
                    new CheckpointDef("CP_R10a", 143.2f, 3.7f, "frame 11 — unexposed."),
                    new CheckpointDef("CP_R10b", 158.9f, 5.2f, "frame 11 — still unexposed."),
                },
                hints = new[]
                {
                    new HintDef("The final print: reveal, draw, anchor, burn.", 145.5f, 3.8f, 4f, 2f),
                    new HintDef("A wall of shade. Your flash washes over it — lay a bright stroke at its foot and it recoils.", 159.0f, 4.9f, 3.6f, 1.8f),
                    new HintDef("The last frame is not empty. Its subject wakes with the light.", 162.5f, 5.6f, 3.2f, 2f),
                },
                // the opening "reveal" beat: a dark streak (only in UNDER)
                // bridging the start platform up to the first anchor over the
                // void — reveal it, then walk it (was two dark step-blocks)
                trails = new[]
                {
                    new TrailDef("R10_DarkTrail", new[]
                    {
                        new Vector2(145.9f, 3.0f), new Vector2(147.3f, 3.7f),
                        new Vector2(148.8f, 4.3f), new Vector2(150.0f, 4.9f),
                    }),
                },
                // both white barriers are paper now — burn each in OVER; the
                // final one burns right under the guard's nose
                burns = new[]
                {
                    new BurnDef("R10_BurnA",     152.3f, 6.4f, 0.5f, 3f),
                    new BurnDef("R10_BurnFinal", 169f,   7.8f, 0.5f, 3f),
                },
                // the closing beat: light you make yourself, not light you switch on,
                // clears the last shade before the final photograph
                umbrals = new[] { new UmbralDef("R10_PostUmbra", 160.0f, 6.0f, 0.5f, 3.0f, 0.3f) },
                exits = new[] { new ExitDef(174.2f, 7.5f, 1f, 2f) },
            },
        };
    }
}
