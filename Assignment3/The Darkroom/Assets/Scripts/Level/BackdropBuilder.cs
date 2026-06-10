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
            BuildLayer(root, "Far", 0.25f, -16, new Color(0.080f, 0.080f, 0.082f),
                new Color(0.098f, 0.096f, 0.098f), 140f, 11);
            // near layer: smaller, slightly lighter, faster
            BuildLayer(root, "Near", 0.5f, -12, new Color(0.105f, 0.103f, 0.105f),
                new Color(0.135f, 0.130f, 0.132f), 98f, 23);
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
