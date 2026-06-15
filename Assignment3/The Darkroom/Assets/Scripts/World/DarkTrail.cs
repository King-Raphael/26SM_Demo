using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkroom
{
    /// A pre-authored, fixed light-streak — what a long exposure prints of a
    /// motion. Solid + visible ONLY in UNDER (a DarkStroke the level draws for
    /// you), it is a smooth tapered glowing curve, not a block. Set `points`
    /// (world-space) before the GameObject is activated; it builds itself once.
    public class DarkTrail : MonoBehaviour
    {
        public Vector2[] points;

        const float Width = 0.14f, EdgeRadius = 0.07f;

        LineRenderer _lr, _glow;
        Light2D _light;
        readonly List<Vector2> _pts = new List<Vector2>();

        void OnEnable()
        {
            if (_lr != null) return;                 // already built
            if (points == null || points.Length < 2) return;
            _pts.Clear();
            _pts.AddRange(points);

            var c = VisualFactory.DarkStroke;
            var taper = new AnimationCurve(
                new Keyframe(0f, 0.7f), new Keyframe(0.15f, 1f),
                new Keyframe(0.85f, 1f), new Keyframe(1f, 0.7f));

            _lr = gameObject.AddComponent<LineRenderer>();
            _lr.useWorldSpace = true;
            _lr.widthMultiplier = Width;
            _lr.widthCurve = taper;
            _lr.sharedMaterial = VisualFactory.GlowMat;
            _lr.sortingOrder = VisualFactory.OrderStroke;
            _lr.numCapVertices = 4;
            _lr.numCornerVertices = 3;                // smooth corners
            _lr.positionCount = _pts.Count;
            for (int i = 0; i < _pts.Count; i++) _lr.SetPosition(i, _pts[i]);
            _lr.startColor = c; _lr.endColor = c;

            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(transform, false);
            _glow = glowGO.AddComponent<LineRenderer>();
            _glow.useWorldSpace = true;
            _glow.widthMultiplier = Width * 3.6f;
            _glow.widthCurve = taper;
            _glow.sharedMaterial = VisualFactory.GlowMat;
            _glow.sortingOrder = VisualFactory.OrderStroke - 1;
            _glow.numCapVertices = 4;
            _glow.numCornerVertices = 3;
            _glow.positionCount = _pts.Count;
            for (int i = 0; i < _pts.Count; i++) _glow.SetPosition(i, _pts[i]);
            var gc = c; gc.a = 0.30f;
            _glow.startColor = gc; _glow.endColor = gc;

            var ec = gameObject.AddComponent<EdgeCollider2D>();
            ec.edgeRadius = EdgeRadius;
            ec.points = _pts.ToArray();

            var b = ComputeBounds();
            _light = LightDirector.CreatePoint(transform, Vector2.zero,
                Color.Lerp(c, Color.white, 0.2f), Mathf.Max(2.6f, b.extents.magnitude + 1.2f), 0.5f);
            _light.transform.position = b.center;

            // DarkStroke solidity: matrix makes it solid only in UNDER. A
            // hidden path the dark reveals — so override the matrix's faded
            // NORMAL alpha (0.18) to ZERO: the streak is invisible in NORMAL and
            // OVER, and only appears (and is solid) in UNDER.
            var eo = gameObject.AddComponent<ExposureObject>();
            eo.type = ExposureObjectType.DarkStroke;
            eo.BoundsProvider = ComputeBounds;
            eo.OverlapTester = OverlapsBounds;
            eo.OnAlphaApplied = a =>
            {
                float vis = Mathf.InverseLerp(0.18f, 1f, a); // 0 in NORMAL/OVER, 1 in UNDER
                if (_lr != null) { var lc = _lr.startColor; lc.a = vis; _lr.startColor = lc; _lr.endColor = lc; }
                if (_glow != null) { var g = _glow.startColor; g.a = vis * 0.30f; _glow.startColor = g; _glow.endColor = g; }
                if (_light != null) _light.intensity = vis * 0.6f;
            };
            eo.Reapply();
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
