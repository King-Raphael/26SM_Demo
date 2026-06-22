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
                foreach (var fp in room.fixPlats)
                    Latent(fp.name, new Vector2(fp.cx, fp.cy), new Vector2(fp.w, fp.h));
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
                {
                    var ex = Exit(new Vector2(x.cx, x.cy), new Vector2(x.w, x.h));
                    // room 0's exit is the prologue's blank-paper door (enter-photo
                    // cinematic), not the finale door — and it reads as a calm blank
                    // sheet, not a blinding doorway, until the cinematic's flash
                    if (r == 0)
                    {
                        var le = ex.GetComponent<LevelExit>();
                        if (le != null) le.IsPrologueDoor = true;
                        var inner = ex.transform.Find("Inner");
                        if (inner != null)
                        {
                            var isr = inner.GetComponent<SpriteRenderer>();
                            if (isr != null) isr.color = new Color(0.95f, 0.93f, 0.87f, 1f); // blank photo paper
                        }
                    }
                }
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

            // the prologue's restrained darkroom dressing (room 0 is always built)
            BuildPrologueProps();

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

        /// Restrained darkroom set-dressing for the prologue entrance: a dim red
        /// safelight on the back wall (prominent in UNDER, washed out by the work
        /// light in NORMAL — the per-exposure lighting carries the "safelight rises"
        /// read for free) and a couple of developing trays on the bench. All pure
        /// decoration — no colliders, behind gameplay; the illustrated backdrop
        /// carries the rest.
        static void BuildPrologueProps()
        {
            // single container (one direct child of _root; the validator accounts
            // for it) so the loose decoration doesn't drift the object-count check
            var container = new GameObject("PrologueProps");
            container.transform.SetParent(_root, false);
            var parent = container.transform;
            var dir = container.AddComponent<PrologueDirector>();

            // --- the red safelights: drawn IN CODE like the hanging work-lamps (cord +
            // shade + bulb + glow + a short beam) but RED, plus the real red light each
            // casts. Several down the corridor so one is always in view; the director
            // swings BOTH the glow and the red Light2D hard with safelight/work-light. ---
            // interleaved BETWEEN the warm work-lamps (which hang at -44/-33/-22/-9 in
            // BackdropBuilder) so a red safelight never stacks on top of a white lamp
            BuildSafelight(parent, dir, -38.5f);
            BuildSafelight(parent, dir, -27.5f);
            BuildSafelight(parent, dir, -15.5f);

            // --- LAYER 1: the roll on the drying line — eleven clipped frames, ten
            // dim/developed, the eleventh a blank sheet (the premise, shown not told) ---
            BuildDryingRoll(parent);

            // --- LAYER 2: a FEW faint negative-scratch lines, scattered down the
            // corridor walls (varied length/angle so they read as film damage, not a
            // pattern). The director fades them IN under the safelight, OUT in work light ---
            float[,] sc = { { -42f, 3.2f, 2.4f, 7f }, { -31f, 4.0f, 1.8f, -9f },
                            { -18f, 3.4f, 2.8f, 5f }, { -9f, 3.6f, 2.0f, -6f } };
            for (int i = 0; i < sc.GetLength(0); i++)
                dir.Scratches.Add(BuildScratch(parent, sc[i, 0], sc[i, 1], sc[i, 2], sc[i, 3]));

            // developing trays on the bench (mid-corridor, under the drying roll)
            PrologueTray(parent, -30.6f, 0.72f);
            PrologueTray(parent, -29.6f, 0.72f);

            // --- LAYER 1 (cont.): the workbench the trays sit on, and the enlarger
            // standing beside it — the iconic darkroom set, pure dark silhouettes ---
            PrologueBench(parent);
            PrologueEnlarger(parent);

            // --- LAYER 2 (cont.): the "photo lines over reality" overlay. Platform
            // tops develop into photo-edges and seams surface on the blank-paper door;
            // the director fades them IN under the safelight (UNDER), OUT under the
            // work light (NORMAL), alongside the negative scratches. ---
            Color edge = new Color(0.78f, 0.80f, 0.84f, 1f); // soft off-white, not neon
            dir.Overlays.Add(BuildOverlayLine(parent, "R0_FloorEdge", -35.25f, 0.5f, 24f, 0.04f, 0f, edge, VisualFactory.OrderExposure - 1));
            dir.Overlays.Add(BuildOverlayLine(parent, "R0_FarEdge",    -7f,   2.8f, 4f,  0.04f, 0f, edge, VisualFactory.OrderExposure - 1));
            Color seam = new Color(0.05f, 0.05f, 0.07f, 1f);
            dir.Overlays.Add(BuildOverlayLine(parent, "R0_DoorSeam",   -6f,   4.4f, 0.05f, 2.8f,  0f, seam, VisualFactory.OrderExit + 1));
            dir.Overlays.Add(BuildOverlayLine(parent, "R0_DoorCrackA", -6.3f, 4.9f, 0.04f, 1.2f,  9f, seam, VisualFactory.OrderExit + 1));
            dir.Overlays.Add(BuildOverlayLine(parent, "R0_DoorCrackB", -5.7f, 3.9f, 0.04f, 1.4f, -7f, seam, VisualFactory.OrderExit + 1));
        }

        /// The red safelight, drawn procedurally in the same idiom as the hanging
        /// work-lamps (BackdropBuilder.Lamp): a cord, a dark shade, an HDR bulb, a
        /// soft glow halo, and a short downward beam — but RED. The glow is handed to
        /// the PrologueDirector, which lifts it under the safelight (UNDER) and washes
        /// it out under the work light (NORMAL). A real red Light2D rides along.
        static void BuildSafelight(Transform parent, PrologueDirector dir, float x)
        {
            var go = new GameObject("R0_Safelight");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, 3.9f, 0f); // the hang point (ceiling)

            // cord down to the fixture, and a dark housing/shade
            SafelightPart(go.transform, new Vector3(0f, -0.35f, 0f), new Vector3(0.04f, 0.7f, 1f),
                VisualFactory.WhiteSprite, new Color(0.08f, 0.08f, 0.09f, 1f), VisualFactory.SpriteMat, VisualFactory.OrderExposure - 6);
            SafelightPart(go.transform, new Vector3(0f, -0.78f, 0f), new Vector3(0.6f, 0.6f, 1f),
                PixelArt.ConeShade, new Color(0.12f, 0.05f, 0.05f, 1f), VisualFactory.SpriteMat, VisualFactory.OrderExposure - 5);

            // HDR red bulb (>1 so the bloom haloes it into a soft red orb)
            var bulb = SafelightPart(go.transform, new Vector3(0f, -0.8f, 0f), new Vector3(0.13f, 0.13f, 1f),
                PixelArt.Disc, new Color(1.3f, 0.14f, 0.11f, 1f), VisualFactory.GlowMat, VisualFactory.OrderExposure - 3);

            // the soft red glow halo — the director drives THIS one
            var glow = SafelightPart(go.transform, new Vector3(0f, -0.78f, 0f), new Vector3(1.15f, 1.15f, 1f),
                PixelArt.SoftGlow, new Color(1.4f, 0.13f, 0.10f, 1f), VisualFactory.GlowMat, VisualFactory.OrderExposure - 4);

            // a short red beam spilling down from the bulb (the lamps' volumetric cue)
            SafelightPart(go.transform, new Vector3(0f, -1.2f, 0f), new Vector3(0.75f, 0.7f, 1f),
                PixelArt.LightBeam, new Color(1.0f, 0.2f, 0.15f, 0.75f), VisualFactory.GlowMat, VisualFactory.OrderExposure - 5);

            // the director swings both the halo and the real red light with the mode
            dir.Safelights.Add(glow); // rises under the safelight, washes out under the work light
            var light = LightDirector.CreatePoint(go.transform, new Vector2(0f, -0.8f),
                new Color(0.9f, 0.16f, 0.12f), 5.5f, 0.12f);
            dir.SafeLights2D.Add(light);
        }

        /// One part of the procedural safelight (sprite + colour + material + order).
        static SpriteRenderer SafelightPart(Transform parent, Vector3 localPos, Vector3 scale, Sprite sprite, Color col, Material mat, int order)
        {
            var g = new GameObject("Part");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = scale;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = mat;
            sr.color = col;
            sr.sortingOrder = order;
            return sr;
        }

        /// A simple unlit dark-silhouette rectangle for prologue set-dressing.
        static void PropRect(Transform parent, string name, float cx, float cy, float w, float h, Color col, int order)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = new Vector3(cx, cy, 0f);
            g.transform.localScale = new Vector3(w, h, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = VisualFactory.WhiteSprite;
            sr.sharedMaterial = VisualFactory.SpriteMat;
            sr.color = col;
            sr.sortingOrder = order;
        }

        /// A thin line that starts hidden (alpha 0) — the PrologueDirector fades its
        /// alpha in under the safelight. Returns the renderer so it can be registered.
        static SpriteRenderer BuildOverlayLine(Transform parent, string name, float cx, float cy, float w, float h, float angle, Color col, int order)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = new Vector3(cx, cy, 0f);
            if (Mathf.Abs(angle) > 0.001f) g.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            g.transform.localScale = new Vector3(w, h, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = VisualFactory.WhiteSprite;
            sr.sharedMaterial = VisualFactory.SpriteMat;
            col.a = 0f; sr.color = col; // hidden until the director lifts it under the safelight
            sr.sortingOrder = order;
            return sr;
        }

        /// The workbench under the developing trays: a dark counter top + apron.
        static void PrologueBench(Transform parent)
        {
            PropRect(parent, "R0_BenchTop",   -30f, 0.62f, 2.6f, 0.14f, new Color(0.12f, 0.12f, 0.14f, 1f), VisualFactory.OrderExposure - 4);
            PropRect(parent, "R0_BenchApron", -30f, 0.20f, 2.4f, 0.80f, new Color(0.08f, 0.08f, 0.10f, 1f), VisualFactory.OrderExposure - 5);
        }

        /// The enlarger: an upright column on the bench, a cantilevered lamphouse
        /// head, and the lens pointed down at the baseboard — a darkroom landmark.
        static void PrologueEnlarger(Transform parent)
        {
            const float ex = -32f;
            var col = new Color(0.09f, 0.09f, 0.11f, 1f);
            int order = VisualFactory.OrderExposure - 3;
            PropRect(parent, "R0_EnlargerBase",   ex,        0.74f, 0.90f, 0.10f, col, order);
            PropRect(parent, "R0_EnlargerColumn", ex - 0.32f, 1.70f, 0.12f, 2.00f, col, order);
            PropRect(parent, "R0_EnlargerArm",    ex - 0.18f, 2.55f, 0.40f, 0.14f, col, order);
            PropRect(parent, "R0_EnlargerHead",   ex,        2.55f, 0.78f, 0.46f, col, order);
            PropRect(parent, "R0_EnlargerLens",   ex,        2.18f, 0.26f, 0.34f, col, order);
        }

        /// The drying line: a wire with 11 clipped frames — ten dim (developed), the
        /// eleventh a brighter blank sheet. The "Frame 11 is blank" beat, made visible.
        static void BuildDryingRoll(Transform parent)
        {
            const float lineY = 3.7f, x0 = -34f, step = 0.9f;
            const int n = 11;

            var wire = new GameObject("R0_DryingWire");
            wire.transform.SetParent(parent, false);
            wire.transform.position = new Vector3(x0 + (n - 1) * step * 0.5f, lineY, 0f);
            wire.transform.localScale = new Vector3((n - 1) * step + 0.4f, 0.03f, 1f);
            var wsr = wire.AddComponent<SpriteRenderer>();
            wsr.sprite = VisualFactory.WhiteSprite;
            wsr.sharedMaterial = VisualFactory.SpriteMat;
            wsr.color = new Color(0.18f, 0.18f, 0.20f, 1f);
            wsr.sortingOrder = VisualFactory.OrderExposure - 3;

            for (int i = 0; i < n; i++)
            {
                bool blank = i == n - 1; // the 11th, unprinted
                var photo = new GameObject(blank ? "R0_Frame11_Blank" : "R0_Frame" + (i + 1));
                photo.transform.SetParent(parent, false);
                photo.transform.position = new Vector3(x0 + i * step, lineY - 0.34f, 0f);
                photo.transform.localScale = new Vector3(0.5f, 0.6f, 1f);
                var psr = photo.AddComponent<SpriteRenderer>();
                psr.sprite = VisualFactory.WhiteSprite;
                psr.sharedMaterial = VisualFactory.SpriteMat;
                psr.color = blank ? new Color(0.86f, 0.85f, 0.80f, 1f)
                                  : new Color(0.15f + 0.02f * (i % 3), 0.15f, 0.17f, 1f);
                psr.sortingOrder = VisualFactory.OrderExposure - 2;
            }
        }

        static SpriteRenderer BuildScratch(Transform parent, float x, float y, float len, float angle)
        {
            var g = new GameObject("R0_Scratch");
            g.transform.SetParent(parent, false);
            g.transform.position = new Vector3(x, y, 0f);
            g.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            g.transform.localScale = new Vector3(0.014f, len, 1f); // thin
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = VisualFactory.WhiteSprite;
            sr.sharedMaterial = VisualFactory.SpriteMat; // soft alpha, not a glowing neon stick
            sr.color = new Color(0.62f, 0.66f, 0.72f, 0f); // desaturated grey-blue film damage, faded in by the director
            sr.sortingOrder = VisualFactory.OrderExposure - 1;
            return sr;
        }

        static void PrologueTray(Transform parent, float x, float y)
        {
            var t = new GameObject("R0_Tray");
            t.transform.SetParent(parent, false);
            t.transform.position = new Vector3(x, y, 0f);
            t.transform.localScale = new Vector3(0.85f, 0.18f, 1f);
            var sr = t.AddComponent<SpriteRenderer>();
            sr.sprite = VisualFactory.WhiteSprite;
            sr.sharedMaterial = VisualFactory.SpriteMat;
            sr.color = new Color(0.10f, 0.10f, 0.12f, 1f);
            sr.sortingOrder = VisualFactory.OrderExposure - 3;
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

            // 6) DUAL-USE LIGHT — the bright stroke you climb is ALSO the key. A
            // cyan light-meter on the wall face is too high to stand beside; draw
            // a rising bright stroke (OVER) up the wall — it CLIMBS you up AND its
            // light trips the meter, opening the gate. One stroke = ladder + key.
            Box("Lab_L_Floor", SG, new Vector2(445f, 3f), new Vector2(10f, 1f));     // x440-450, top 3.5
            Box("Lab_L_Wall",  SG, new Vector2(448f, 5f), new Vector2(1f, 3f));      // x447.5-448.5, top 6.5
            var labDoorL = Door("Lab_Door_L", new Vector2(449.1f, 7f), new Vector2(0.4f, 2f)); // gate, y6-8
            Sensor("Lab_L_Meter", new Vector2(447.3f, 5.2f), labDoorL, 1, 0.4f);     // LocalLux on the wall face
            Box("Lab_L_Exit",  SG, new Vector2(450.5f, 6f), new Vector2(2f, 1f));    // landing past the gate
            Hint("DUAL-USE LIGHT · in OVER draw a rising stroke up the wall — it CLIMBS you AND lights the cyan meter to open the gate.", new Vector2(443.5f, 5.7f), new Vector2(4.8f, 2f));

            Hint("END · press [ to leave, P to reset.", new Vector2(450.5f, 8.2f), new Vector2(3.5f, 2f));
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

                // film-negative strip filling the box, behind the bright core line
                var neg = new GameObject("NegStrip");
                neg.transform.SetParent(go.transform, false);
                var nsr = neg.AddComponent<SpriteRenderer>();
                nsr.sprite = ProcGfx.FilmStripTile(false);
                nsr.sharedMaterial = VisualFactory.GlowMat;
                nsr.color = Color.white;
                nsr.sortingOrder = VisualFactory.OrderExposure - 1;
                var nb = nsr.sprite.bounds.size;
                neg.transform.localScale = new Vector3(s.x / nb.x, s.y / nb.y, 1f);

                var dbc = go.AddComponent<BoxCollider2D>();
                dbc.size = s;
                var deo = go.AddComponent<ExposureObject>();
                deo.type = t;
                deo.boxSize = s;
                var dgsr = Halo(go.transform, new Vector2(s.x + 0.5f, s.y + 0.5f),
                    new Color(0.36f, 0.46f, 0.78f, 0f), VisualFactory.OrderExposure - 2);
                var dlight = LightDirector.CreatePoint(go.transform, Vector2.zero,
                    new Color(0.45f, 0.56f, 0.90f), Mathf.Max(s.x, s.y) * 0.5f + 1.6f, 0f);

                // cold light motes drifting along the strip — the developing "life"
                var drift = go.AddComponent<Drift>();
                drift.area = new Vector2(s.x * 0.92f, s.y * 0.6f);
                drift.velocity = new Vector2(0.6f, 0f);
                drift.size = Mathf.Clamp(s.y * 0.5f, 0.08f, 0.22f);
                drift.count = Mathf.Clamp(Mathf.RoundToInt(s.x), 2, 6);
                drift.color = new Color(VisualFactory.DarkStroke.r, VisualFactory.DarkStroke.g, VisualFactory.DarkStroke.b, 0.5f);
                drift.sortingOrder = VisualFactory.OrderExposure + 1;

                deo.OnAlphaApplied = a =>
                {
                    var bc2 = bsr.color; bc2.a = a; bsr.color = bc2;
                    var ns = nsr.color; ns.a = a; nsr.color = ns;
                    var c2 = dgsr.color; c2.a = a * 0.28f; dgsr.color = c2;
                    dlight.intensity = a * 0.55f;
                    drift.SetMaster(a);
                };
                deo.Reapply();
                return go;
            }

            go = NewTiledBox(name, c, s, TileFor(t, s), VisualFactory.OrderFor(t), Layers.World);
            if (t == ExposureObjectType.StaticGround)
            {
                // darker face: the platforms are lit, so a dark face stays calm even when
                // a lamp / OVER warmth hits it (it used to "pop" jarringly bright); the
                // bright top lip below carries the read + the design.
                bool tall = s.y > s.x * 1.5f;
                go.GetComponent<SpriteRenderer>().color = tall
                    ? new Color(0.185f, 0.180f, 0.205f, 1f)
                    : new Color(0.205f, 0.205f, 0.235f, 1f);
            }
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = s;

            // walkable platforms get a "lit lip": a soft rim that bleeds light down from
            // the top edge (3D-ledge cue) + a bright core catch-light on top of it. The
            // dark face + this lip = a designed ledge instead of a flat bright slab.
            if (t == ExposureObjectType.StaticGround && s.x >= 1f)
            {
                var rim = new GameObject("EdgeRim");
                rim.transform.SetParent(go.transform, false);
                rim.transform.localPosition = new Vector3(0f, s.y / 2f - 0.02f, 0f);
                rim.transform.localScale = new Vector3(s.x, Mathf.Min(0.55f, s.y * 0.6f), 1f);
                var rsr = rim.AddComponent<SpriteRenderer>();
                rsr.sprite = PixelArt.EdgeFade;             // pivot top -> grows downward
                rsr.sharedMaterial = VisualFactory.GlowMat; // soft, blooms
                rsr.color = new Color(0.50f, 0.51f, 0.58f, 0.30f);
                rsr.sortingOrder = VisualFactory.OrderGround + 1;

                var edge = new GameObject("EdgeLight");
                edge.transform.SetParent(go.transform, false);
                edge.transform.localPosition = new Vector3(0f, s.y / 2f - 0.025f, 0f);
                edge.transform.localScale = new Vector3(s.x, 0.05f, 1f);
                var esr = edge.AddComponent<SpriteRenderer>();
                esr.sprite = VisualFactory.WhiteSprite;
                esr.sharedMaterial = VisualFactory.GlowMat; // bright core catch-light
                esr.color = new Color(0.66f, 0.67f, 0.74f, 0.85f);
                esr.sortingOrder = VisualFactory.OrderGround + 2; // on top of the rim

                // the lip is UNLIT (blooms in Normal/Over) so it won't dim with the world
                // in UNDER on its own — gate it down there or it would reveal the geometry.
                go.AddComponent<PlatformLip>().Bind(rsr, esr);
            }
            if (t != ExposureObjectType.StaticGround)
            {
                var eo = go.AddComponent<ExposureObject>();
                eo.type = t;
                eo.boxSize = s;

                // BrightBarrier reads as a framed frosted print, not a white slab
                if (t == ExposureObjectType.BrightBarrier)
                {
                    var frame = AddPaneFrame(go.transform, s,
                        new Color(0.10f, 0.10f, 0.12f, 1f), VisualFactory.OrderExposure + 1);
                    eo.OnAlphaApplied = a =>
                    {
                        for (int i = 0; i < frame.Length; i++)
                        {
                            var fc = frame[i].color; fc.a = a; frame[i].color = fc;
                        }
                    };
                    eo.Reapply(); // root pane auto-fades; this matches the frame to it
                }
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
                    // tall boxes read as plaster walls, flat ones as concrete
                    return s.y > s.x * 1.5f ? PixelArt.WallTile : PixelArt.ConcreteTile;
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
            var ubHalo = Halo(go.transform, new Vector2(s.x + 0.7f, s.y + 0.7f),
                new Color(0.24f, 0.20f, 0.36f, 0.22f), VisualFactory.OrderExposure - 1);

            // roiling undeveloped-emulsion overlay (the "alive" boil)
            var roilGO = new GameObject("Roil");
            roilGO.transform.SetParent(go.transform, false);
            var roil = roilGO.AddComponent<SpriteRenderer>();
            roil.sprite = ProcGfx.EmulsionFrames[0];
            roil.sharedMaterial = VisualFactory.GlowMat;
            roil.drawMode = SpriteDrawMode.Tiled;
            roil.size = s;
            roil.color = new Color(1f, 1f, 1f, 0.5f);
            roil.sortingOrder = VisualFactory.OrderExposure + 1;
            var fc = roilGO.AddComponent<FrameCycle>();
            fc.frames = ProcGfx.EmulsionFrames;
            fc.fps = 6f;
            fc.randomOrder = true;

            var ubLight = LightDirector.CreatePoint(go.transform, new Vector2(0f, -s.y * 0.4f),
                new Color(0.52f, 0.32f, 0.82f), Mathf.Max(s.x, s.y) * 0.5f + 1.2f, 0.4f);

            var ub = go.AddComponent<UmbralBarrier>();
            ub.retractThreshold = threshold;
            ub.boxSize = s;
            ub.onAlpha = a =>
            {
                var rc = roil.color; rc.a = a * 0.5f; roil.color = rc;
                var hc = ubHalo.color; hc.a = a * 0.22f; ubHalo.color = hc;
                ubLight.intensity = a * 0.45f;
            };
            ub.onAlpha(1f); // starts sealed/solid
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
            lift.onAlpha = BuildLiftDecor(go, x, topY, bottomY, s,
                new Color(0.70f, 0.55f, 1.10f, 1f),   // HDR violet catch-light
                new Color(0.32f, 0.22f, 0.52f, 0.42f), // churning underside
                VisualFactory.OrderExposure,
                rails: false); // no static rails — the hidden descent must be
                               // discovered (go dark), not telegraphed as a shaft
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

            // a projected light bridge: bright caustic top edge + dust in the beam
            var lbEdgeGO = new GameObject("Edge");
            lbEdgeGO.transform.SetParent(go.transform, false);
            lbEdgeGO.transform.localPosition = new Vector3(0f, s.y * 0.5f - 0.03f, 0f);
            lbEdgeGO.transform.localScale = new Vector3(s.x, 0.08f, 1f);
            var lbEdge = lbEdgeGO.AddComponent<SpriteRenderer>();
            lbEdge.sprite = VisualFactory.WhiteSprite;
            lbEdge.sharedMaterial = VisualFactory.GlowMat;
            lbEdge.color = new Color(1.35f, 1.22f, 0.92f, 1f); // HDR caustic
            lbEdge.sortingOrder = VisualFactory.OrderStroke + 1;

            var lbDrift = go.AddComponent<Drift>();
            lbDrift.area = new Vector2(s.x * 0.9f, s.y * 0.7f);
            lbDrift.velocity = new Vector2(0.4f, 0.05f);
            lbDrift.size = Mathf.Clamp(s.y * 0.4f, 0.07f, 0.18f);
            lbDrift.count = Mathf.Clamp(Mathf.RoundToInt(s.x), 2, 6);
            lbDrift.color = new Color(VisualFactory.BrightStroke.r, VisualFactory.BrightStroke.g, VisualFactory.BrightStroke.b, 0.4f);
            lbDrift.sortingOrder = VisualFactory.OrderStroke + 1;

            // a level-authored bridge (not a player stroke): like DarkTrail, it
            // must be a HIDDEN puzzle — fully invisible in NORMAL (no 0.18 ghost),
            // visible + solid only in OVER. Remap the matrix's faded NORMAL alpha
            // to zero so the "use OVER here" trick isn't given away by just looking.
            eo.OnAlphaApplied = a =>
            {
                float vis = Mathf.InverseLerp(0.18f, 1f, a); // 0 in NORMAL/UNDER, 1 in OVER
                var bodyC = sr.color; bodyC.a = vis; sr.color = bodyC;
                var ec = lbEdge.color; ec.a = vis; lbEdge.color = ec;
                lbDrift.SetMaster(vis);
            };
            eo.Reapply();
            return go;
        }

        /// A white sheet that OVER burns through (BurnPaper): hold OVER nearby
        /// ~1.5 s and it burns a permanent hole. Pair with an enemy for tension.
        public static GameObject BurnWall(string name, Vector2 c, Vector2 s)
        {
            var go = NewTiledBox(name, c, s, ProcGfx.PhotoPaperTile, VisualFactory.OrderExposure, Layers.World);
            go.GetComponent<SpriteRenderer>().color = new Color(0.92f, 0.92f, 0.90f, 1f); // white paper
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = s;
            var bp = go.AddComponent<BurnPaper>();
            bp.boxSize = s;

            // a charred hole that grows from the centre as the paper burns through
            var scarGO = new GameObject("Char");
            scarGO.transform.SetParent(go.transform, false);
            var scar = scarGO.AddComponent<SpriteRenderer>();
            scar.sprite = ProcGfx.CharScar;
            scar.sharedMaterial = VisualFactory.SpriteMat;
            scar.color = new Color(1f, 1f, 1f, 0f);
            scar.sortingOrder = VisualFactory.OrderExposure + 1;
            float scarFit = Mathf.Min(s.x, s.y) * 1.15f / Mathf.Max(0.01f, scar.sprite.bounds.size.x);
            bp.OnCharProgress = k =>
            {
                float sc = Mathf.Lerp(0.15f, 1.15f, k) * scarFit;
                scarGO.transform.localScale = new Vector3(sc, sc, 1f);
                var cc = scar.color; cc.a = Mathf.Clamp01(k * 1.1f); scar.color = cc;
            };
            bp.OnBurned = () =>
            {
                scarGO.transform.localScale = Vector3.one * (1.15f * scarFit);
                scar.color = new Color(1f, 1f, 1f, 0.85f);
            };
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

            // latent grain veil over the not-yet-developed platform (resolves on Fix)
            var veilGO = new GameObject("Grain");
            veilGO.transform.SetParent(go.transform, false);
            var veil = veilGO.AddComponent<SpriteRenderer>();
            veil.sprite = ProcGfx.GrainTile;
            veil.sharedMaterial = VisualFactory.GlowMat;
            veil.drawMode = SpriteDrawMode.Tiled;
            veil.size = s;
            veil.color = new Color(0.70f, 0.82f, 1f, 0.55f);
            veil.sortingOrder = VisualFactory.OrderGround + 1;

            var fp = go.AddComponent<FixPlatform>();
            fp.boxSize = s;
            fp.grainVeil = veil;
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
            rl.onAlpha = BuildLiftDecor(go, x, topY, bottomY, s,
                new Color(1.20f, 1.00f, 0.70f, 1f),   // HDR warm hot edge
                new Color(0.90f, 0.70f, 0.35f, 0.45f), // warm caustic underside
                VisualFactory.OrderStroke);
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
            // alone won't trip it — it is waiting for light to be brought to it.
            // Inside the iris ring sits a "fill" disc that the sensor grows +
            // brightens with delivered lux (the readout: bring light here).
            SpriteRenderer luxFill = null;
            if (lightMeter)
            {
                var iris = new GameObject("Iris");
                iris.transform.SetParent(go.transform, false);
                iris.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                var isr = iris.AddComponent<SpriteRenderer>();
                isr.sprite = PixelArt.Disc;
                isr.sharedMaterial = VisualFactory.GlowMat;
                isr.color = new Color(VisualFactory.DarkStroke.r, VisualFactory.DarkStroke.g, VisualFactory.DarkStroke.b, 0.28f);
                isr.sortingOrder = VisualFactory.OrderSensor + 1;

                var fillGO = new GameObject("LuxFill");
                fillGO.transform.SetParent(go.transform, false);
                fillGO.transform.localScale = new Vector3(0.12f, 0.12f, 1f);
                luxFill = fillGO.AddComponent<SpriteRenderer>();
                luxFill.sprite = PixelArt.Disc;
                luxFill.sharedMaterial = VisualFactory.GlowMat;
                luxFill.color = new Color(0.72f, 0.94f, 1f, 0.10f); // HDR cyan → blooms as it fills
                luxFill.sortingOrder = VisualFactory.OrderSensor + 2;
            }

            var sensor = go.AddComponent<PhotoSensor>();
            sensor.Door = door;
            sensor.mode = (PhotoSensor.SensorMode)mode;
            sensor.luxThreshold = lux;
            sensor.LuxFill = luxFill;
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

            // dark recessed lens lower on the panel (concept-art detail)
            var lens = new GameObject("Lens");
            lens.transform.SetParent(go.transform, false);
            lens.transform.localPosition = new Vector3(0f, s.y * -0.18f, 0f);
            lens.transform.localScale = new Vector3(s.x * 0.5f, s.x * 0.5f, 1f);
            var lsr = lens.AddComponent<SpriteRenderer>();
            lsr.sprite = PixelArt.Disc;
            lsr.sharedMaterial = VisualFactory.SpriteMat;
            lsr.color = new Color(0.09f, 0.09f, 0.11f, 1f);
            lsr.sortingOrder = VisualFactory.OrderDoor + 1;

            var door = go.AddComponent<SensorDoor>();

            // a darkroom light-lock: a safelight status lamp, red & breathing while
            // sealed, turning warm the instant it opens
            var lampGO = new GameObject("Lamp");
            lampGO.transform.SetParent(go.transform, false);
            lampGO.transform.localPosition = new Vector3(0f, s.y * 0.30f, 0f);
            lampGO.transform.localScale = new Vector3(s.x * 0.45f, s.x * 0.45f, 1f);
            var lamp = lampGO.AddComponent<SpriteRenderer>();
            lamp.sprite = PixelArt.Disc;
            lamp.sharedMaterial = VisualFactory.GlowMat;
            lamp.color = new Color(1.5f, 0.22f, 0.18f, 1f); // HDR safelight red → blooms
            lamp.sortingOrder = VisualFactory.OrderDoor + 2;
            var lampLight = LightDirector.CreatePoint(go.transform, new Vector2(0f, s.y * 0.30f),
                new Color(0.9f, 0.2f, 0.18f), 1.8f, 0.5f);
            var lampPulse = lampGO.AddComponent<GlowPulse>();
            lampPulse.Target = lamp; lampPulse.Min = 0.7f; lampPulse.Max = 1f; lampPulse.Speed = 2.5f;
            lampPulse.Light = lampLight; lampPulse.LightMin = 0.35f; lampPulse.LightMax = 0.6f;
            door.OnOpen = () =>
            {
                lampPulse.enabled = false;        // let the door's fade win
                lampLight.enabled = false;
                lamp.color = new Color(1.3f, 1.15f, 0.85f, 1f); // turns welcoming-cream
                StrokeSparkle.Burst(lampGO.transform.position, new Color(1f, 0.93f, 0.78f, 1f), 10);
            };
            return door;
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
            isr.color = new Color(1.6f, 1.5f, 1.32f, 1f); // HDR doorway — blooms blinding
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

            // warm dust motes drifting up through the doorway light
            var exitDrift = go.AddComponent<Drift>();
            exitDrift.area = new Vector2(s.x * 1.1f, s.y * 1.2f);
            exitDrift.velocity = new Vector2(0.05f, 0.5f);
            exitDrift.size = 0.16f;
            exitDrift.count = 6;
            exitDrift.color = new Color(1f, 0.95f, 0.84f, 0.5f);
            exitDrift.sortingOrder = VisualFactory.OrderExit + 1;
            return go;
        }

        /// Four thin dark bars forming an inset pane/print frame around a box.
        static SpriteRenderer[] AddPaneFrame(Transform parent, Vector2 s, Color col, int order)
        {
            const float t = 0.08f;
            var bars = new SpriteRenderer[4];
            var pos = new[]
            {
                new Vector3(0f, s.y * 0.5f - t * 0.5f, 0f), new Vector3(0f, -s.y * 0.5f + t * 0.5f, 0f),
                new Vector3(-s.x * 0.5f + t * 0.5f, 0f, 0f), new Vector3(s.x * 0.5f - t * 0.5f, 0f, 0f),
            };
            var scl = new[]
            {
                new Vector3(s.x, t, 1f), new Vector3(s.x, t, 1f),
                new Vector3(t, s.y, 1f), new Vector3(t, s.y, 1f),
            };
            for (int i = 0; i < 4; i++)
            {
                var g = new GameObject("Frame");
                g.transform.SetParent(parent, false);
                g.transform.localPosition = pos[i];
                g.transform.localScale = scl[i];
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = VisualFactory.WhiteSprite;
                sr.sharedMaterial = VisualFactory.SpriteMat;
                sr.color = col;
                sr.sortingOrder = order;
                bars[i] = sr;
            }
            return bars;
        }

        /// Shared lift dressing: a bright HDR top catch-light, a breathing underside
        /// glow, and always-visible shaft rails (parented to the level root, so they
        /// stay even when the slab is gone). Returns the onAlpha action to fade the
        /// slab-bound detail (rails persist; the breathing glow is gated on/off).
        static System.Action<float> BuildLiftDecor(GameObject go, float x, float topY, float bottomY,
            Vector2 s, Color edgeHDR, Color glow, int order, bool rails = true)
        {
            var edgeGO = new GameObject("Edge");
            edgeGO.transform.SetParent(go.transform, false);
            edgeGO.transform.localPosition = new Vector3(0f, s.y * 0.5f - 0.04f, 0f);
            edgeGO.transform.localScale = new Vector3(s.x, 0.10f, 1f);
            var edge = edgeGO.AddComponent<SpriteRenderer>();
            edge.sprite = VisualFactory.WhiteSprite;
            edge.sharedMaterial = VisualFactory.GlowMat;
            edge.color = edgeHDR;
            edge.sortingOrder = order + 1;

            var underGO = new GameObject("Under");
            underGO.transform.SetParent(go.transform, false);
            underGO.transform.localPosition = new Vector3(0f, -s.y * 0.5f, 0f);
            underGO.transform.localScale = new Vector3(s.x * 1.25f, s.y * 1.3f, 1f);
            var under = underGO.AddComponent<SpriteRenderer>();
            under.sprite = PixelArt.SoftGlow;
            under.sharedMaterial = VisualFactory.GlowMat;
            under.color = glow;
            under.sortingOrder = order - 1;
            var pulse = underGO.AddComponent<GlowPulse>();
            pulse.Target = under;
            pulse.Min = glow.a * 0.5f;
            pulse.Max = glow.a;
            pulse.Speed = 3f;

            // shaft rails — always visible, parented to the level root (skipped
            // for the shadow lift, whose whole point is to stay hidden until UNDER)
            if (rails)
            {
                float railTop = topY + s.y * 0.5f, railBot = bottomY - s.y * 0.5f;
                float railH = Mathf.Max(0.2f, railTop - railBot), railMid = (railTop + railBot) * 0.5f;
                for (int i = 0; i < 2; i++)
                {
                    float rx = x + (i == 0 ? -1f : 1f) * (s.x * 0.5f - 0.03f);
                    var railGO = new GameObject("Rail");
                    railGO.transform.SetParent(_root, false);
                    railGO.transform.position = new Vector3(rx, railMid, 0f);
                    railGO.transform.localScale = new Vector3(0.06f, railH, 1f);
                    var rail = railGO.AddComponent<SpriteRenderer>();
                    rail.sprite = VisualFactory.WhiteSprite;
                    rail.sharedMaterial = VisualFactory.SpriteMat;
                    rail.color = new Color(0.17f, 0.16f, 0.20f, 1f);
                    rail.sortingOrder = order - 2;
                }
            }

            return a =>
            {
                var t = edge.color; t.a = a; edge.color = t;
                under.enabled = a > 0.05f; // GlowPulse owns its alpha; gate on/off
            };
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
