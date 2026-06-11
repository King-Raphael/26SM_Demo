using UnityEngine;

namespace Darkroom
{
    /// Silhouette darkroom props on two parallax layers: hanging photo lines,
    /// shelves with bottles, enlargers. Pure decoration — no colliders.
    public static class BackdropBuilder
    {
        static Transform _layer;
        static System.Random _rng;
        static Color _lo, _hi;

        public static void Build()
        {
            if (GameObject.Find("_Backdrop") != null) return;
            var root = new GameObject("_Backdrop").transform;
            // far layer: bigger, darker, slower
            BuildLayer(root, "Far", 0.25f, -16, new Color(0.050f, 0.050f, 0.054f),
                new Color(0.066f, 0.064f, 0.068f), 140f, 11);
            // near layer: smaller, slightly lighter, faster
            BuildLayer(root, "Near", 0.5f, -12, new Color(0.070f, 0.068f, 0.072f),
                new Color(0.092f, 0.088f, 0.092f), 98f, 23);
            BuildLamps(root);
        }

        /// Hanging cone lamps with light pools — world-fixed (no parallax),
        /// their Light2Ds genuinely light the play space.
        static void BuildLamps(Transform root)
        {
            var lamps = new GameObject("Lamps").transform;
            lamps.SetParent(root, false);
            var rng = new System.Random(77);
            float x = -3f;
            while (x < 174f)
            {
                Lamp(lamps, x, 10.6f + (float)rng.NextDouble() * 1.6f, rng);
                x += 10f + (float)rng.NextDouble() * 5f;
            }
            // the Room 9 corridor gets its own low lamps
            Lamp(lamps, 130f, 1.4f, rng);
            Lamp(lamps, 137f, 1.4f, rng);
        }

        static void Lamp(Transform parent, float x, float topY, System.Random rng)
        {
            var go = new GameObject("Lamp");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, topY, 0f);

            float cord = 0.8f + (float)rng.NextDouble() * 1.4f;
            var cordSr = Decoration(go.transform, new Vector3(0f, -cord / 2f, 0f),
                new Vector3(0.045f, cord, 1f), VisualFactory.WhiteSprite, new Color(0.08f, 0.08f, 0.09f, 1f), -6);

            var shade = Decoration(go.transform, new Vector3(0f, -cord - 0.55f, 0f),
                Vector3.one, PixelArt.ConeShade, Color.white, -5);

            var bulb = Decoration(go.transform, new Vector3(0f, -cord - 0.52f, 0f),
                new Vector3(0.16f, 0.16f, 1f), PixelArt.Disc, new Color(1f, 0.95f, 0.82f, 0.95f), -4);
            bulb.sharedMaterial = VisualFactory.GlowMat;

            var cone = Decoration(go.transform, new Vector3(0f, -cord - 0.5f, 0f),
                new Vector3(2.6f, 1.6f, 1f), PixelArt.LightCone, Color.white, -5);
            cone.sharedMaterial = VisualFactory.GlowMat;

            LightDirector.CreatePoint(go.transform, new Vector2(0f, -cord - 0.8f),
                new Color(1f, 0.92f, 0.76f), 4.2f, 0.5f);
        }

        static SpriteRenderer Decoration(Transform parent, Vector3 localPos, Vector3 scale, Sprite sprite, Color color, int order)
        {
            var go = new GameObject("Part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = VisualFactory.SpriteMat;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        static void BuildLayer(Transform root, string name, float factor, int order,
            Color lo, Color hi, float span, int seed)
        {
            var layerGO = new GameObject("Layer_" + name);
            layerGO.transform.SetParent(root, false);
            layerGO.AddComponent<ParallaxLayer>().Factor = factor;
            _layer = layerGO.transform;
            _rng = new System.Random(seed);
            _lo = lo;
            _hi = hi;

            float x = -11f;
            while (x < span)
            {
                switch (_rng.Next(4))
                {
                    case 0: HangingLine(x, Range(7f, 10.5f), order); break;
                    case 1: Shelf(x, Range(1.5f, 5.5f), order); break;
                    case 2: Enlarger(x, Range(1.5f, 4f), order); break;
                    default: HangingLine(x, Range(5f, 8f), order); break;
                }
                x += Range(7f, 13f);
            }
        }

        static float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
        static Color Tint() => Color.Lerp(_lo, _hi, (float)_rng.NextDouble());

        static void HangingLine(float x, float y, int order)
        {
            float w = Range(4f, 7f);
            var c = Tint();
            Decor(x + w / 2f, y, w, 0.07f, c, order);                       // the line
            int photos = 2 + _rng.Next(3);
            for (int i = 0; i < photos; i++)
            {
                float px = x + Range(0.4f, w - 0.4f);
                Decor(px, y - 0.10f, 0.10f, 0.14f, c, order);               // clip
                Decor(px, y - 0.58f, Range(0.5f, 0.7f), Range(0.65f, 0.85f), Tint(), order);
            }
        }

        static void Shelf(float x, float y, int order)
        {
            float w = Range(3f, 6f);
            var c = Tint();
            Decor(x + w / 2f, y, w, 0.16f, c, order);                       // board
            int bottles = 2 + _rng.Next(3);
            for (int i = 0; i < bottles; i++)
            {
                float bw = Range(0.22f, 0.4f);
                float bh = Range(0.4f, 0.85f);
                Decor(x + Range(0.4f, w - 0.4f), y + 0.08f + bh / 2f, bw, bh, Tint(), order);
            }
        }

        static void Enlarger(float x, float y, int order)
        {
            var c = Tint();
            Decor(x, y - 0.9f, 1.5f, 0.2f, c, order);                       // base
            Decor(x + 0.45f, y + 0.3f, 0.25f, 2.4f, c, order);              // column
            Decor(x - 0.1f, y + 1.25f, 1.2f, 0.6f, c, order);               // head
            Decor(x - 0.1f, y + 0.75f, 0.5f, 0.45f, Tint(), order);         // bellows
        }

        static void Decor(float cx, float cy, float w, float h, Color color, int order)
        {
            var go = new GameObject("Decor");
            go.transform.SetParent(_layer, false);
            go.transform.localPosition = new Vector3(cx, cy, 0f);
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = VisualFactory.WhiteSprite;
            sr.sharedMaterial = VisualFactory.SpriteMat;
            sr.color = color;
            sr.sortingOrder = order;
        }
    }
}
