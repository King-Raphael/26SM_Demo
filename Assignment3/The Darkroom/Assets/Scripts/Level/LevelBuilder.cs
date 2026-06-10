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
                    CheckpointAt(c.name, new Vector2(c.cx, c.cy));
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
            var go = NewTiledBox(name, c, s, TileFor(t), VisualFactory.OrderFor(t), Layers.World);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = s;
            if (t != ExposureObjectType.StaticGround)
            {
                var eo = go.AddComponent<ExposureObject>();
                eo.type = t;
                eo.boxSize = s;
                if (t == ExposureObjectType.DarkPath)
                {
                    go.GetComponent<SpriteRenderer>().sharedMaterial = VisualFactory.GlowMat;
                    var gsr = Halo(go.transform, new Vector2(s.x + 0.5f, s.y + 0.5f),
                        new Color(0.36f, 0.46f, 0.78f, 0f), VisualFactory.OrderExposure - 1);
                    var light = LightDirector.CreatePoint(go.transform, Vector2.zero,
                        new Color(0.45f, 0.56f, 0.90f), Mathf.Max(s.x, s.y) * 0.5f + 1.6f, 0f);
                    eo.OnAlphaApplied = a =>
                    {
                        var c2 = gsr.color; c2.a = a * 0.28f; gsr.color = c2;
                        light.intensity = a * 0.55f;
                    };
                    eo.Reapply();
                }
            }
            return go;
        }

        /// Soft additive-looking halo behind an object (just a faint quad).
        static SpriteRenderer Halo(Transform parent, Vector2 size, Color color, int order)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = VisualFactory.WhiteSprite;
            sr.sharedMaterial = VisualFactory.GlowMat;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        static Sprite TileFor(ExposureObjectType t)
        {
            switch (t)
            {
                case ExposureObjectType.DarkPath:      return PixelArt.DarkPathTile;
                case ExposureObjectType.BrightBarrier: return PixelArt.BarrierTile;
                default:                               return PixelArt.GroundTile;
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
            return go.AddComponent<SensorDoor>();
        }

        public static GameObject Pickup(Ability a, Vector2 c)
        {
            var go = new GameObject(a == Ability.Flash ? "Pickup_Flash" : "Pickup_Shutter");
            go.layer = Layers.Triggers;
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = a == Ability.Flash ? PixelArt.FlashPickup : PixelArt.ShutterPickup;
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

        public static GameObject CheckpointAt(string name, Vector2 c)
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

            go.AddComponent<Checkpoint>();
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
            var go = new GameObject("LevelExit");
            go.layer = Layers.Triggers;
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(c.x, c.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.ExitDoor;
            sr.sharedMaterial = VisualFactory.SpriteMat;
            sr.sortingOrder = VisualFactory.OrderExit;
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = s;
            bc.isTrigger = true;
            go.AddComponent<LevelExit>();

            // pulsing safelight halo around the exit
            var halo = Halo(go.transform, new Vector2(s.x + 1.4f, s.y + 1.0f),
                new Color(0.545f, 0.10f, 0.10f, 0.15f), VisualFactory.OrderExit - 1);
            var pulse = go.AddComponent<GlowPulse>();
            pulse.Target = halo;
            pulse.Min = 0.10f;
            pulse.Max = 0.22f;
            pulse.Speed = 1.6f;
            pulse.Light = LightDirector.CreatePoint(go.transform, Vector2.zero,
                new Color(0.80f, 0.18f, 0.18f), 4.5f, 0.5f);
            pulse.LightMin = 0.35f;
            pulse.LightMax = 0.65f;
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
