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
                    Sensor(s.name, new Vector2(s.cx, s.cy), door);
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

            return rootGO;
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

        public static GameObject Sensor(string name, Vector2 c, SensorDoor door)
        {
            var go = NewSpriteBox(name, c, Vector2.one, VisualFactory.SensorInactive, VisualFactory.OrderSensor, Layers.Triggers);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one;
            bc.isTrigger = true;

            // red accent bar (visible once active)
            var accentGO = new GameObject("Accent");
            accentGO.transform.SetParent(go.transform, false);
            accentGO.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            accentGO.transform.localScale = new Vector3(1f, 0.16f, 1f);
            var accent = accentGO.AddComponent<SpriteRenderer>();
            accent.sprite = VisualFactory.WhiteSprite;
            accent.sharedMaterial = VisualFactory.SpriteMat;
            accent.color = VisualFactory.SafelightRed;
            accent.sortingOrder = VisualFactory.OrderSensor + 1;

            var sensor = go.AddComponent<PhotoSensor>();
            sensor.Door = door;
            sensor.Init(go.GetComponent<SpriteRenderer>(), accent);
            sensor.ActivateLight = LightDirector.CreatePoint(go.transform, Vector2.zero,
                new Color(1f, 0.93f, 0.78f), 2.5f, 0.5f);
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
