using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkroom
{
    /// Constructs the entire level under a "_Level" root from LevelData.
    /// No manual scene wiring anywhere.
    public static class LevelBuilder
    {
        static Transform _root;

        public static GameObject Build(int throughRoom)
        {
            var rootGO = new GameObject("_Level");
            _root = rootGO.transform;
            var rooms = LevelData.Rooms;
            int last = Mathf.Min(throughRoom, rooms.Length - 1);

            // pass 1: everything except sensors (sensors reference doors by id)
            var doorMap = new Dictionary<string, SensorDoor>();
            for (int r = 0; r <= last; r++)
            {
                var room = rooms[r];
                foreach (var b in room.boxes)
                    Box(b.name, b.type, new Vector2(b.cx, b.cy), new Vector2(b.w, b.h));
                foreach (var u in room.umbrals)
                    Umbral(u.name, new Vector2(u.cx, u.cy), new Vector2(u.w, u.h), u.threshold);
                foreach (var l in room.lifts)
                    Lift(l.name, l.cx, l.topY, l.bottomY, new Vector2(l.w, l.h));
                foreach (var bn in room.burns)
                    BurnWall(bn.name, new Vector2(bn.cx, bn.cy), new Vector2(bn.w, bn.h));
                foreach (var br in room.bridges)
                    LightBridge(br.name, new Vector2(br.cx, br.cy), new Vector2(br.w, br.h));
                foreach (var rl in room.riseLifts)
                    LightLift(rl.name, rl.cx, rl.topY, rl.bottomY, new Vector2(rl.w, rl.h));
                foreach (var t in room.trails)
                    Trail(t.name, t.points);
                foreach (var e in room.enemies)
                    Enemy(e.name, new Vector2(e.cx, e.cy), e.range, e.speed);
                foreach (var d in room.doors)
                    doorMap[d.id] = Door(d.id, new Vector2(d.cx, d.cy), new Vector2(d.w, d.h));
                foreach (var p in room.pickups)
                    Pickup(p.ability, new Vector2(p.cx, p.cy));
                foreach (var c in room.checkpoints)
                    CheckpointAt(c.name, new Vector2(c.cx, c.cy), c.caption, r);
                foreach (var h in room.hints)
                    Hint(h.text, new Vector2(h.cx, h.cy), new Vector2(h.w, h.h));
                foreach (var x in room.exits)
                    Exit(new Vector2(x.cx, x.cy), new Vector2(x.w, x.h));
            }

            // pass 2: sensors, wired to their doors
            for (int r = 0; r <= last; r++)
            {
                foreach (var s in rooms[r].sensors)
                {
                    doorMap.TryGetValue(s.doorId, out var door);
                    if (door == null)
                        Debug.LogError("[LevelBuilder] Sensor " + s.name + " references missing door " + s.doorId);
                    Sensor(s.name, new Vector2(s.cx, s.cy), door, s.mode, s.lux);
                }
            }

            // Room 9 set piece: the corridor blackout (rebuilt with the level)
            if (last >= 9)
            {
                var blackout = new GameObject("R9_Blackout");
                blackout.transform.SetParent(_root, false);
                blackout.AddComponent<ScriptedBlackout>();
            }

            // demo truncation: containment wall + notice
            if (last < rooms.Length - 1)
            {
                Box("DemoWall", ExposureObjectType.StaticGround, new Vector2(33f, 3f), new Vector2(1f, 8f));
                Hint("END OF DEMO — the next build continues from here.", new Vector2(31f, 1.6f), new Vector2(3f, 2f));
                Debug.Log("[LevelBuilder] Demo build: rooms 0-" + last + " only.");
            }

            // dev mechanic sandbox (only while the dev warp is enabled)
            if (GameManager.DevWarpEnabled) BuildDevSandbox();

            return rootGO;
        }

        /// A development-only sandbox far to the right (x ~392+), reached with
        /// the lab warp key. Five labeled stations for the new exposure verbs.
        /// Not part of the 11-frame game; never built when DevWarpEnabled=false.
        static void BuildDevSandbox()
        {
            const ExposureObjectType SG = ExposureObjectType.StaticGround;

            // platforms at y3.5 with GAPS the new verbs must cross. A fall is
            // not scenery here — it drops to the void and respawns you at the
            // lab start (P also re-warps here any time).
            Box("Lab_Start", SG, new Vector2(394f, 3f), new Vector2(8f, 1f)); // x390-398
            Hint("DEV LAB — try each verb left → right. Fall = back to start · [ to leave.", new Vector2(394f, 4.7f), new Vector2(4f, 2f));

            // 1) DARK TRAIL — a streak bridges the gap, solid only in UNDER.
            // Starts flush with the floor so you just WALK across (slopes work).
            Box("Lab_T_R", SG, new Vector2(404f, 3f), new Vector2(4f, 1f)); // x402-406
            Trail("Lab_Trail", new[]
            {
                new Vector2(398f, 3.5f), new Vector2(400f, 4.4f), new Vector2(402f, 3.5f),
            });
            Hint("DARK TRAIL · press 1 (UNDER): a streak bridges the gap — just walk across.", new Vector2(400f, 5.4f), new Vector2(3.6f, 2f));

            // 2) LIGHT BRIDGE — light fills the gap, solid only in OVER, top flush
            Box("Lab_B_R", SG, new Vector2(412f, 3f), new Vector2(4f, 1f)); // x410-414
            LightBridge("Lab_Bridge", new Vector2(408f, 3.3f), new Vector2(4f, 0.4f)); // x406-410, top 3.5
            Hint("LIGHT BRIDGE · press 3 (OVER): light fills the gap — cross while lit.", new Vector2(408f, 5.4f), new Vector2(3.6f, 2f));

            // shared floor for the last three stations
            Box("Lab_Floor2", SG, new Vector2(427f, 3f), new Vector2(26f, 1f)); // x414-440

            // 3) BURN PAPER — hold OVER to burn through; the guard wakes
            BurnWall("Lab_BurnWall", new Vector2(419f, 4.5f), new Vector2(0.6f, 3f));
            Box("Lab_Burn_Ceil", SG, new Vector2(419f, 6.4f), new Vector2(2f, 0.4f));
            Enemy("Lab_Burn_Guard", new Vector2(422f, 4f), 1f, 1f);
            Hint("BURN PAPER · hold OVER (3) by the wall ~1.5s to burn a hole — OVER wakes the guard.", new Vector2(418f, 5.6f), new Vector2(4.2f, 2f));

            // 4) FIX / 定影 — flash OVER near the ghosts to PRINT them solid
            Box("Lab_F_Shelf", SG, new Vector2(430f, 7f), new Vector2(3f, 0.4f));
            Latent("Lab_F_S1", new Vector2(426f, 4.4f), new Vector2(1.4f, 0.4f));
            Latent("Lab_F_S2", new Vector2(427.6f, 5.5f), new Vector2(1.4f, 0.4f));
            Latent("Lab_F_S3", new Vector2(429.2f, 6.6f), new Vector2(1.4f, 0.4f));
            Hint("FIX / 定影 · flash OVER (3) near the faint steps to PRINT them solid, then climb.", new Vector2(425f, 5.6f), new Vector2(4.2f, 2f));

            // 5) RISE LIFT / 光浮力 — press 3 (OVER): a light slab rises
            Box("Lab_R_Shelf", SG, new Vector2(437f, 8.1f), new Vector2(3f, 0.4f));
            LightLift("Lab_RiseLift", 434f, 8f, 3.8f, new Vector2(2.4f, 0.6f));
            Hint("RISE LIFT / 光浮力 · press 3 (OVER): a light slab rises — ride it up.", new Vector2(433f, 5.4f), new Vector2(4.2f, 2f));

            Hint("END · press [ to leave, P to reset.", new Vector2(438f, 8.7f), new Vector2(3.5f, 2f));
        }

        // ---------- helpers (spec 8.14) ----------

        public static GameObject Box(string name, ExposureObjectType t, Vector2 c, Vector2 s)
        {
            GameObject go;
            if (t == ExposureObjectType.DarkPath)
            {
                // root stays unscaled (colliders/lights keep world units);
                // only the gradient band child is stretched
                go = new GameObject(name);
                go.layer = Layers.World;
                go.transform.SetParent(_root, false);
                go.transform.position = new Vector3(c.x, c.y, 0f);

                var band = new GameObject("Band");
                band.transform.SetParent(go.transform, false);
                band.transform.localScale = new Vector3(s.x, s.y, 1f);
                var bsr = band.AddComponent<SpriteRenderer>();
                bsr.sprite = PixelArt.DarkPathTile;
                bsr.sharedMaterial = VisualFactory.GlowMat;
                bsr.color = Color.white;
                bsr.sortingOrder = VisualFactory.OrderFor(t);

                var dbc = go.AddComponent<BoxCollider2D>();
                dbc.size = s;
                var deo = go.AddComponent<ExposureObject>();
                deo.type = t;
                deo.boxSize = s;
                var dgsr = Halo(go.transform, new Vector2(s.x + 0.5f, s.y + 0.5f),
                    new Color(0.36f, 0.46f, 0.78f, 0f), VisualFactory.OrderExposure - 1);
                var dlight = LightDirector.CreatePoint(go.transform, Vector2.zero,
                    new Color(0.45f, 0.56f, 0.90f), Mathf.Max(s.x, s.y) * 0.5f + 1.6f, 0f);
                deo.OnAlphaApplied = a =>
                {
                    var bc2 = bsr.color; bc2.a = a; bsr.color = bc2;
                    var c2 = dgsr.color; c2.a = a * 0.28f; dgsr.color = c2;
                    dlight.intensity = a * 0.55f;
                };
                deo.Reapply();
                return go;
            }

            go = NewTiledBox(name, c, s, TileFor(t, s), VisualFactory.OrderFor(t), Layers.World);
            if (t == ExposureObjectType.StaticGround)
            {
                // photo textures are bright — tint them down into the dark
                bool tall = s.y > s.x * 1.5f;
                go.GetComponent<SpriteRenderer>().color = tall
                    ? new Color(0.26f, 0.25f, 0.28f, 1f)
                    : new Color(0.30f, 0.30f, 0.34f, 1f);
            }
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = s;

            // catch-light along walkable tops (cinematic restyle)
            if (t == ExposureObjectType.StaticGround && s.x >= 1f)
            {
                var edge = new GameObject("EdgeLight");
                edge.transform.SetParent(go.transform, false);
                edge.transform.localPosition = new Vector3(0f, s.y / 2f - 0.03f, 0f);
                edge.transform.localScale = new Vector3(s.x, 0.06f, 1f);
                var esr = edge.AddComponent<SpriteRenderer>();
                esr.sprite = VisualFactory.WhiteSprite;
                esr.sharedMaterial = VisualFactory.SpriteMat;
                esr.color = new Color(0.30f, 0.30f, 0.34f, 0.6f);
                esr.sortingOrder = VisualFactory.OrderGround + 1;
            }
            if (t != ExposureObjectType.StaticGround)
            {
                var eo = go.AddComponent<ExposureObject>();
                eo.type = t;
                eo.boxSize = s;
            }
            return go;
        }

        /// Soft radial halo behind an object.
        static SpriteRenderer Halo(Transform parent, Vector2 size, Color color, int order)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size.x * 1.6f, size.y * 1.6f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.SoftGlow;
            sr.sharedMaterial = VisualFactory.GlowMat;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        static Sprite TileFor(ExposureObjectType t, Vector2 s)
        {
            switch (t)
            {
                case ExposureObjectType.DarkPath:      return PixelArt.DarkPathTile;
                case ExposureObjectType.BrightBarrier: return PixelArt.BarrierTile;
                default:
                    // tall boxes read as brick walls, flat ones as concrete
                    return s.y > s.x * 1.5f ? PixelArt.BrickTile : PixelArt.ConcreteTile;
            }
        }

        public static GameObject Static(string name, Vector2 c, Vector2 s)
            => Box(name, ExposureObjectType.StaticGround, c, s);

        /// A wall of shadow: solid until a delivered stroke lights it. Drawn on
        /// GlowMat so it stays readable even in the dark (a shadow you can see),
        /// and on the World layer so the player collides while it is sealed.
        public static GameObject Umbral(string name, Vector2 c, Vector2 s, float threshold)
        {
            var go = NewTiledBox(name, c, s, PixelArt.BarrierTile, VisualFactory.OrderExposure, Layers.World);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sharedMaterial = VisualFactory.GlowMat;
            sr.color = new Color(0.16f, 0.13f, 0.22f, 1f); // deep-violet shadow matter

            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = s;

            // a cold halo so the shade reads against the near-black background
            Halo(go.transform, new Vector2(s.x + 0.7f, s.y + 0.7f),
                new Color(0.24f, 0.20f, 0.36f, 0.22f), VisualFactory.OrderExposure - 1);

            var ub = go.AddComponent<UmbralBarrier>();
            ub.retractThreshold = threshold;
            ub.boxSize = s;
            return go;
        }

        /// A shadow lift: a kinematic shadow slab that sinks in UNDER, holds in
        /// NORMAL, and dissolves in OVER (dropping its rider). Built at topY.
        public static GameObject Lift(string name, float x, float topY, float bottomY, Vector2 s)
        {
            // GlowMat (unlit) so it reads in the dark; no halo, so it leaves
            // nothing visible once it fades out in NORMAL/OVER
            var go = NewTiledBox(name, new Vector2(x, topY), s, PixelArt.BarrierTile, VisualFactory.OrderExposure, Layers.World);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sharedMaterial = VisualFactory.GlowMat;
            sr.color = new Color(0.30f, 0.28f, 0.45f, 1f); // shadow slab, readable in UNDER

            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = s;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate; // smooth ride

            var lift = go.AddComponent<ShadowLift>();
            lift.topY = topY;
            lift.bottomY = bottomY;
            lift.boxSize = s;
            return go;
        }

        // ---------- new exposure mechanics ----------

        /// A pre-authored dark light-streak (DarkTrail): solid + visible only in
        /// UNDER, a smooth tapered glowing curve. Points are world-space.
        public static GameObject Trail(string name, Vector2[] worldPoints)
        {
            var go = new GameObject(name);
            go.layer = Layers.Strokes; // collides with the player only, like a stroke
            go.transform.SetParent(_root, false);
            go.SetActive(false);       // configure points before OnEnable builds it
            var dt = go.AddComponent<DarkTrail>();
            dt.points = worldPoints;
            go.SetActive(true);
            return go;
        }

        /// A bridge of light: solid only in OVER (an ExposureObject of type
        /// BrightStroke), the bright twin of a dark platform.
        public static GameObject LightBridge(string name, Vector2 c, Vector2 s)
        {
            var go = NewTiledBox(name, c, s, PixelArt.BarrierTile, VisualFactory.OrderStroke, Layers.World);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sharedMaterial = VisualFactory.GlowMat;
            sr.color = VisualFactory.BrightStroke; // warm cream glow
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = s;
            var eo = go.AddComponent<ExposureObject>();
            eo.type = ExposureObjectType.BrightStroke;
            eo.boxSize = s;
            return go;
        }

        /// A white sheet that OVER burns through (BurnPaper): hold OVER nearby
        /// ~1.5 s and it burns a permanent hole. Pair with an enemy for tension.
        public static GameObject BurnWall(string name, Vector2 c, Vector2 s)
        {
            var go = NewTiledBox(name, c, s, PixelArt.BarrierTile, VisualFactory.OrderExposure, Layers.World);
            go.GetComponent<SpriteRenderer>().color = new Color(0.92f, 0.92f, 0.90f, 1f); // white paper
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = s;
            var bp = go.AddComponent<BurnPaper>();
            bp.boxSize = s;
            return go;
        }

        /// A latent platform (FixPlatform): a faint ghost until you flash OVER
        /// near it, which prints it permanently solid.
        public static GameObject Latent(string name, Vector2 c, Vector2 s)
        {
            var go = NewTiledBox(name, c, s, PixelArt.ConcreteTile, VisualFactory.OrderGround, Layers.World);
            go.GetComponent<SpriteRenderer>().color = new Color(0.55f, 0.78f, 0.95f, 1f); // cool latent tint
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = s;
            var fp = go.AddComponent<FixPlatform>();
            fp.boxSize = s;
            return go;
        }

        /// A light lift (RiseLift): the mirror of the shadow lift — real only in
        /// OVER, rises from bottomY up to topY. Built at the bottom.
        public static GameObject LightLift(string name, float x, float topY, float bottomY, Vector2 s)
        {
            var go = NewTiledBox(name, new Vector2(x, bottomY), s, PixelArt.BarrierTile, VisualFactory.OrderStroke, Layers.World);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sharedMaterial = VisualFactory.GlowMat;
            sr.color = new Color(1f, 0.95f, 0.78f, 1f); // warm light slab

            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = s;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var rl = go.AddComponent<RiseLift>();
            rl.topY = topY;
            rl.bottomY = bottomY;
            rl.boxSize = s;
            return go;
        }

        public static GameObject Enemy(string name, Vector2 c, float patrolRange, float speed)
        {
            var go = new GameObject(name);
            go.layer = Layers.World;
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = LightSensitiveEnemy.AsleepSprite;
            sr.sharedMaterial = VisualFactory.SpriteMat;
            sr.sortingOrder = VisualFactory.OrderEnemy;

            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(0.8f, 0.8f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            // red glow while awake (toggled by the enemy)
            var light = LightDirector.CreatePoint(go.transform, Vector2.zero,
                new Color(0.75f, 0.15f, 0.15f), 2.4f, 0.5f);
            light.enabled = false;

            var en = go.AddComponent<LightSensitiveEnemy>();
            en.Init(patrolRange, speed);
            return go;
        }

        public static GameObject Sensor(string name, Vector2 c, SensorDoor door, int mode = 0, float lux = 0.6f)
        {
            bool lightMeter = mode == 1; // LocalLux: reads delivered light, not OVER
            var go = NewSpriteBox(name, c, Vector2.one, VisualFactory.SensorInactive, VisualFactory.OrderSensor, Layers.Triggers);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one;
            bc.isTrigger = true;

            // accent bar (visible once active): warm for photo sensors, cool
            // cyan for light meters so the two devices read as different tools
            var accentGO = new GameObject("Accent");
            accentGO.transform.SetParent(go.transform, false);
            accentGO.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            accentGO.transform.localScale = new Vector3(1f, 0.16f, 1f);
            var accent = accentGO.AddComponent<SpriteRenderer>();
            accent.sprite = VisualFactory.WhiteSprite;
            accent.sharedMaterial = VisualFactory.SpriteMat;
            accent.color = lightMeter ? VisualFactory.DarkStroke : VisualFactory.SafelightRed;
            accent.sortingOrder = VisualFactory.OrderSensor + 1;

            // a light meter wears a small cool "iris" so the player knows OVER
            // alone won't trip it — it is waiting for light to be brought to it
            if (lightMeter)
            {
                var iris = new GameObject("Iris");
                iris.transform.SetParent(go.transform, false);
                iris.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                var isr = iris.AddComponent<SpriteRenderer>();
                isr.sprite = PixelArt.Disc;
                isr.sharedMaterial = VisualFactory.GlowMat;
                isr.color = new Color(VisualFactory.DarkStroke.r, VisualFactory.DarkStroke.g, VisualFactory.DarkStroke.b, 0.5f);
                isr.sortingOrder = VisualFactory.OrderSensor + 1;
            }

            var sensor = go.AddComponent<PhotoSensor>();
            sensor.Door = door;
            sensor.mode = (PhotoSensor.SensorMode)mode;
            sensor.luxThreshold = lux;
            sensor.Init(go.GetComponent<SpriteRenderer>(), accent);
            sensor.ActivateLight = LightDirector.CreatePoint(go.transform, Vector2.zero,
                lightMeter ? new Color(0.62f, 0.85f, 0.90f) : new Color(1f, 0.93f, 0.78f), 2.5f, 0.5f);
            sensor.ActivateLight.enabled = false;
            return go;
        }

        public static SensorDoor Door(string id, Vector2 c, Vector2 s)
        {
            var go = NewTiledBox(id, c, s, PixelArt.DoorTile, VisualFactory.OrderDoor, Layers.World);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = s;

            // two circular "speaker" lenses on the panel (concept-art look)
            for (int i = 0; i < 2; i++)
            {
                var lens = new GameObject("Lens" + i);
                lens.transform.SetParent(go.transform, false);
                lens.transform.localPosition = new Vector3(0f, s.y * (i == 0 ? 0.18f : -0.10f), 0f);
                lens.transform.localScale = new Vector3(s.x * 0.62f, s.x * 0.62f, 1f);
                var lsr = lens.AddComponent<SpriteRenderer>();
                lsr.sprite = PixelArt.Disc;
                lsr.sharedMaterial = VisualFactory.SpriteMat;
                lsr.color = new Color(0.10f, 0.10f, 0.12f, 1f);
                lsr.sortingOrder = VisualFactory.OrderDoor + 1;
            }
            return go.AddComponent<SensorDoor>();
        }

        public static GameObject Pickup(Ability a, Vector2 c)
        {
            var go = new GameObject("Pickup_" + a);
            go.layer = Layers.Triggers;
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = a == Ability.Flash ? PixelArt.FlashPickup
                      : a == Ability.Shutter ? PixelArt.ShutterPickup
                      : PixelArt.NegativePickup;
            sr.sharedMaterial = VisualFactory.GlowMat;
            sr.sortingOrder = VisualFactory.OrderPickup;
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(0.5f, 0.5f); // per spec
            bc.isTrigger = true;
            var pk = go.AddComponent<AbilityPickup>();
            pk.ability = a;

            var halo = Halo(go.transform, new Vector2(1.1f, 1.1f),
                new Color(1f, 0.95f, 0.84f, 0.12f), VisualFactory.OrderPickup - 1);
            var pulse = go.AddComponent<GlowPulse>();
            pulse.Target = halo;
            pulse.Min = 0.07f;
            pulse.Max = 0.16f;
            pulse.Speed = 2.5f;
            pulse.Light = LightDirector.CreatePoint(go.transform, Vector2.zero,
                new Color(1f, 0.93f, 0.78f), 2.2f, 0.4f);
            pulse.LightMin = 0.30f;
            pulse.LightMax = 0.55f;
            return go;
        }

        public static GameObject CheckpointAt(string name, Vector2 c, string caption = "", int roomIndex = -1)
        {
            var go = NewTrigger(name, c, new Vector2(1f, 2f));

            // small hanging-photo marker: dim until developed (grayscale only —
            // safelight red is reserved)
            var marker = new GameObject("Marker");
            marker.transform.SetParent(go.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            var sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.CheckpointMarker;
            sr.sharedMaterial = VisualFactory.GlowMat;
            sr.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            sr.sortingOrder = VisualFactory.OrderExposure - 2;

            var cp = go.AddComponent<Checkpoint>();
            cp.Caption = caption;
            cp.RoomIndex = roomIndex;
            return go;
        }

        public static GameObject Hint(string text, Vector2 c, Vector2 s)
        {
            var go = NewTrigger("Hint", c, s);
            var ht = go.AddComponent<HintTrigger>();
            ht.Text = text;
            return go;
        }

        public static GameObject Exit(Vector2 c, Vector2 s)
        {
            // a glowing white doorway (concept-art style)
            var go = new GameObject("LevelExit");
            go.layer = Layers.Triggers;
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = s;
            bc.isTrigger = true;
            go.AddComponent<LevelExit>();

            // dark frame
            var frame = new GameObject("Frame");
            frame.transform.SetParent(go.transform, false);
            frame.transform.localScale = new Vector3(s.x + 0.25f, s.y + 0.15f, 1f);
            var fsr = frame.AddComponent<SpriteRenderer>();
            fsr.sprite = VisualFactory.WhiteSprite;
            fsr.sharedMaterial = VisualFactory.SpriteMat;
            fsr.color = new Color(0.09f, 0.09f, 0.10f, 1f);
            fsr.sortingOrder = VisualFactory.OrderExit - 1;

            // blinding inner light
            var inner = new GameObject("Inner");
            inner.transform.SetParent(go.transform, false);
            inner.transform.localScale = new Vector3(s.x * 0.78f, s.y * 0.94f, 1f);
            var isr = inner.AddComponent<SpriteRenderer>();
            isr.sprite = VisualFactory.WhiteSprite;
            isr.sharedMaterial = VisualFactory.GlowMat;
            isr.color = new Color(1f, 0.98f, 0.92f, 0.96f);
            isr.sortingOrder = VisualFactory.OrderExit;

            var halo = Halo(go.transform, new Vector2(s.x + 3.5f, s.y + 2.5f),
                new Color(1f, 0.97f, 0.88f, 0.18f), VisualFactory.OrderExit - 2);
            var pulse = go.AddComponent<GlowPulse>();
            pulse.Target = halo;
            pulse.Min = 0.12f;
            pulse.Max = 0.24f;
            pulse.Speed = 1.6f;
            pulse.Light = LightDirector.CreatePoint(go.transform, Vector2.zero,
                new Color(1f, 0.96f, 0.86f), 5f, 0.7f);
            pulse.LightMin = 0.55f;
            pulse.LightMax = 0.85f;
            return go;
        }

        // ---------- primitives ----------

        /// Tiled draw mode keeps texture density uniform regardless of box size
        /// (localScale stays 1, so colliders are sized directly).
        static GameObject NewTiledBox(string name, Vector2 c, Vector2 s, Sprite tile, int order, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = tile;
            sr.sharedMaterial = VisualFactory.SpriteMat;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = s;
            sr.color = Color.white;
            sr.sortingOrder = order;
            return go;
        }

        static GameObject NewSpriteBox(string name, Vector2 c, Vector2 s, Color color, int order, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);
            go.transform.localScale = new Vector3(s.x, s.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = VisualFactory.WhiteSprite;
            sr.sharedMaterial = VisualFactory.SpriteMat;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        static GameObject NewTrigger(string name, Vector2 c, Vector2 s)
        {
            var go = new GameObject(name);
            go.layer = Layers.Triggers;
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);
            go.transform.localScale = new Vector3(s.x, s.y, 1f);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one;
            bc.isTrigger = true;
            return go;
        }
    }
}
