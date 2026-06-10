using System.Collections.Generic;
using UnityEngine;

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
            var go = NewSpriteBox(name, c, s, VisualFactory.ColorFor(t), VisualFactory.OrderFor(t), Layers.World);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one;
            if (t != ExposureObjectType.StaticGround)
            {
                var eo = go.AddComponent<ExposureObject>();
                eo.type = t;
            }
            return go;
        }

        public static GameObject Static(string name, Vector2 c, Vector2 s)
            => Box(name, ExposureObjectType.StaticGround, c, s);

        public static GameObject Enemy(string name, Vector2 c, float patrolRange, float speed)
        {
            var go = NewSpriteBox(name, c, new Vector2(0.8f, 0.8f), VisualFactory.EnemyAsleep, VisualFactory.OrderEnemy, Layers.World);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
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
            return go;
        }

        public static SensorDoor Door(string id, Vector2 c, Vector2 s)
        {
            var go = NewSpriteBox(id, c, s, VisualFactory.DoorClosed, VisualFactory.OrderDoor, Layers.World);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one;
            return go.AddComponent<SensorDoor>();
        }

        public static GameObject Pickup(Ability a, Vector2 c)
        {
            var go = NewSpriteBox(a == Ability.Flash ? "Pickup_Flash" : "Pickup_Shutter",
                c, new Vector2(0.5f, 0.5f), VisualFactory.PickupColor, VisualFactory.OrderPickup, Layers.Triggers);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one; // 0.5 x 0.5 world, per spec
            bc.isTrigger = true;
            var pk = go.AddComponent<AbilityPickup>();
            pk.ability = a;
            return go;
        }

        public static GameObject CheckpointAt(string name, Vector2 c)
        {
            var go = NewTrigger(name, c, new Vector2(1f, 2f));
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
            var go = NewSpriteBox("LevelExit", c, s, VisualFactory.ExitRed, VisualFactory.OrderExit, Layers.Triggers);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one;
            bc.isTrigger = true;
            go.AddComponent<LevelExit>();
            return go;
        }

        // ---------- primitives ----------

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
