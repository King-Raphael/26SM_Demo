using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkroom
{
    /// A pre-authored, fixed light-streak — what a long exposure prints of a
    /// motion. Solid + visible ONLY in UNDER (a DarkStroke the level draws for
    /// you). Refined render: a Chaikin-smoothed silky curve drawn in three
    /// luminous layers (bright near-white core → soft beam → broad halo), a soft
    /// bloom anchoring each end where it meets the ground, and a gentle breathing
    /// — a developed print of light, not a flat block. Set `points` (world-space)
    /// before the GameObject is activated; it builds itself once. COLLISION stays
    /// on the authored control points so the flush ground seams are preserved.
    public class DarkTrail : MonoBehaviour
    {
        public Vector2[] points;

        const float Width = 0.14f, EdgeRadius = 0.07f;

        LineRenderer _core, _glow, _halo;
        SpriteRenderer _bloomA, _bloomB;
        Light2D _light;
        readonly List<Vector2> _pts = new List<Vector2>(); // control points (collider)
        float _vis;                                         // 0 in NORMAL/OVER, 1 in UNDER

        static readonly Color Cool = VisualFactory.DarkStroke;          // 0x9FD8E6
        static readonly Color CoreCol = Color.Lerp(VisualFactory.DarkStroke, Color.white, 0.40f);

        void OnEnable()
        {
            if (_core != null) return;                        // already built
            if (points == null || points.Length < 2) return;
            _pts.Clear();
            _pts.AddRange(points);

            // render along a corner-cut (Chaikin) curve — silky, and it stays
            // INSIDE the control hull so the glow never dips below the ground
            var smooth = Chaikin(points, 2);

            var taper = new AnimationCurve(
                new Keyframe(0f, 0.55f), new Keyframe(0.12f, 1f),
                new Keyframe(0.88f, 1f), new Keyframe(1f, 0.55f));

            _halo = MakeLine("Halo", smooth, Width * 11f, taper, VisualFactory.BeamMat,
                VisualFactory.OrderStroke - 2, 6, 4);
            _glow = MakeLine("Glow", smooth, Width * 5f, taper, VisualFactory.BeamMat,
                VisualFactory.OrderStroke - 1, 6, 4);
            _core = MakeLine("Core", smooth, Width, taper, VisualFactory.GlowMat,
                VisualFactory.OrderStroke, 4, 3);
            _core.startColor = CoreCol; _core.endColor = CoreCol;

            _bloomA = MakeBloom(points[0]);
            _bloomB = MakeBloom(points[points.Length - 1]);

            // collider on the AUTHORED control points (flush with the bridged ground)
            var ec = gameObject.AddComponent<EdgeCollider2D>();
            ec.edgeRadius = EdgeRadius;
            ec.points = _pts.ToArray();

            var b = ComputeBounds();
            _light = LightDirector.CreatePoint(transform, Vector2.zero,
                Color.Lerp(Cool, Color.white, 0.2f), Mathf.Max(2.6f, b.extents.magnitude + 1.2f), 0.5f);
            _light.transform.position = b.center;

            // DarkStroke solidity: matrix makes it solid only in UNDER. Override the
            // faded NORMAL alpha (0.18) to ZERO so the streak is invisible in
            // NORMAL/OVER and only appears (and is solid) in UNDER.
            var eo = gameObject.AddComponent<ExposureObject>();
            eo.type = ExposureObjectType.DarkStroke;
            eo.BoundsProvider = ComputeBounds;
            eo.OverlapTester = OverlapsBounds;
            eo.OnAlphaApplied = a =>
            {
                _vis = Mathf.InverseLerp(0.18f, 1f, a);   // 0 in NORMAL/OVER, 1 in UNDER
                Apply(_vis, 1f);
            };
            eo.Reapply();
        }

        LineRenderer MakeLine(string name, Vector2[] pts, float width, AnimationCurve taper,
            Material mat, int order, int caps, int corners)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = width;
            lr.widthCurve = taper;
            lr.sharedMaterial = mat;
            lr.textureMode = LineTextureMode.Stretch;
            lr.sortingOrder = order;
            lr.numCapVertices = caps;
            lr.numCornerVertices = corners;
            lr.positionCount = pts.Length;
            for (int i = 0; i < pts.Length; i++) lr.SetPosition(i, pts[i]);
            lr.startColor = Cool; lr.endColor = Cool;
            return lr;
        }

        SpriteRenderer MakeBloom(Vector2 at)
        {
            var go = new GameObject("Bloom");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            go.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.SoftGlow;
            sr.sharedMaterial = VisualFactory.GlowMat;
            sr.sortingOrder = VisualFactory.OrderStroke - 1;
            sr.color = new Color(CoreCol.r, CoreCol.g, CoreCol.b, 0f);
            return sr;
        }

        // breath only swells the soft layers; the core line stays a steady print
        void Apply(float vis, float breath)
        {
            if (_core != null)  { var c = CoreCol; c.a = vis;                  _core.startColor = c; _core.endColor = c; }
            if (_glow != null)  { var c = Cool;    c.a = vis * 0.50f * breath; _glow.startColor = c; _glow.endColor = c; }
            if (_halo != null)  { var c = Cool;    c.a = vis * 0.22f * breath; _halo.startColor = c; _halo.endColor = c; }
            if (_bloomA != null) { var c = _bloomA.color; c.a = vis * 0.45f * breath; _bloomA.color = c; }
            if (_bloomB != null) { var c = _bloomB.color; c.a = vis * 0.45f * breath; _bloomB.color = c; }
            if (_light != null) _light.intensity = vis * 0.6f;
        }

        void Update()
        {
            if (_core == null || _vis <= 0.001f) return;     // invisible in NORMAL/OVER
            float breath = 1f + 0.12f * Mathf.Sin(Time.time * 1.4f);
            Apply(_vis, breath);
        }

        /// Chaikin corner-cutting: rounds the polyline without overshooting the
        /// control hull (so the glow can never bulge below the ground it bridges).
        /// Endpoints are kept exact.
        internal static Vector2[] Chaikin(Vector2[] p, int iters)
        {
            var cur = new List<Vector2>(p);
            for (int it = 0; it < iters && cur.Count >= 3; it++)
            {
                var next = new List<Vector2> { cur[0] };
                for (int i = 0; i < cur.Count - 1; i++)
                {
                    Vector2 a = cur[i], b = cur[i + 1];
                    next.Add(Vector2.Lerp(a, b, 0.25f));
                    next.Add(Vector2.Lerp(a, b, 0.75f));
                }
                next.Add(cur[cur.Count - 1]);
                cur = next;
            }
            return cur.ToArray();
        }

        Bounds ComputeBounds()
        {
            if (_pts.Count == 0) return new Bounds(transform.position, Vector3.zero);
            Vector2 min = _pts[0], max = _pts[0];
            for (int i = 1; i < _pts.Count; i++)
            {
                min = Vector2.Min(min, _pts[i]);
                max = Vector2.Max(max, _pts[i]);
            }
            var b = new Bounds();
            b.SetMinMax(min, max);
            b.Expand(EdgeRadius * 2f);
            return b;
        }

        /// Per-segment overlap so an arc never jams the switch far from the line.
        bool OverlapsBounds(Bounds playerBounds)
        {
            for (int i = 0; i < _pts.Count - 1; i++)
            {
                var b = new Bounds();
                b.SetMinMax(Vector2.Min(_pts[i], _pts[i + 1]), Vector2.Max(_pts[i], _pts[i + 1]));
                b.Expand(EdgeRadius * 2f);
                if (b.Intersects(playerBounds)) return true;
            }
            return false;
        }
    }
}
