using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkroom
{
    /// One long-exposure stroke: LineRenderer + EdgeCollider2D + ExposureObject.
    /// Intangible while drawing (collider and ExposureObject disabled);
    /// Fix() enables both and from then on solidity follows the matrix.
    public class TrailStroke : MonoBehaviour
    {
        public const float Width = 0.14f;
        public const float EdgeRadius = 0.07f;

        public bool IsFixed { get; private set; }
        public EdgeCollider2D Edge { get; private set; }

        LineRenderer _lr;
        LineRenderer _glow;
        Light2D _light;
        ExposureObject _eo;
        readonly List<Vector2> _pts = new List<Vector2>();
        bool _despawning;

        public static TrailStroke Create(ExposureObjectType type)
        {
            var go = new GameObject(type == ExposureObjectType.DarkStroke ? "DarkStroke" : "BrightStroke");
            go.layer = Layers.Strokes;
            go.SetActive(false); // defer Awake/OnEnable until configured

            // tapered ends: the pen pressure of light
            var taper = new AnimationCurve(
                new Keyframe(0f, 0.7f), new Keyframe(0.15f, 1f),
                new Keyframe(0.85f, 1f), new Keyframe(1f, 0.7f));

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = Width;
            lr.widthCurve = taper;
            lr.sharedMaterial = VisualFactory.GlowMat;
            lr.sortingOrder = VisualFactory.OrderStroke;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            lr.positionCount = 0;
            var c = VisualFactory.ColorFor(type);
            lr.startColor = c;
            lr.endColor = c;

            // soft glow line behind the stroke
            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(go.transform, false);
            var glow = glowGO.AddComponent<LineRenderer>();
            glow.useWorldSpace = true;
            glow.widthMultiplier = Width * 2.8f;
            glow.widthCurve = taper;
            glow.sharedMaterial = VisualFactory.GlowMat;
            glow.sortingOrder = VisualFactory.OrderStroke - 1;
            glow.numCapVertices = 4;
            glow.numCornerVertices = 2;
            glow.positionCount = 0;
            var gc = c;
            gc.a = 0.30f; // visible while drawing; matrix-driven after fix
            glow.startColor = gc;
            glow.endColor = gc;

            var ec = go.AddComponent<EdgeCollider2D>();
            ec.edgeRadius = EdgeRadius;
            ec.enabled = false;

            var eo = go.AddComponent<ExposureObject>();
            eo.enabled = false; // not registered / matrix-driven until fixed
            eo.type = type;

            // the stroke is itself a light source
            var light = LightDirector.CreatePoint(go.transform, Vector2.zero,
                Color.Lerp(c, Color.white, 0.2f), 2.6f, 0.5f);

            var ts = go.AddComponent<TrailStroke>();
            ts._lr = lr;
            ts._glow = glow;
            ts._light = light;
            ts.Edge = ec;
            ts._eo = eo;
            eo.BoundsProvider = ts.ComputeBounds;
            eo.OverlapTester = ts.OverlapsBounds;
            eo.OnAlphaApplied = a =>
            {
                var g = glow.startColor;
                g.a = a * 0.30f;
                glow.startColor = g;
                glow.endColor = g;
                light.intensity = a * 0.6f;
            };

            go.SetActive(true);
            return ts;
        }

        public int PointCount => _pts.Count;

        public void AddPoint(Vector2 p)
        {
            _pts.Add(p);
            _lr.positionCount = _pts.Count;
            _lr.SetPosition(_pts.Count - 1, p);
            _glow.positionCount = _pts.Count;
            _glow.SetPosition(_pts.Count - 1, p);
            _light.transform.position = p; // light rides the pen while drawing
        }

        /// Returns false (caller should discard) if the stroke is too short to fix.
        public bool Fix()
        {
            if (_pts.Count < 2) return false;
            IsFixed = true;
            Edge.points = _pts.ToArray(); // transform is identity: local == world
            var b = ComputeBounds();
            _light.transform.position = b.center;
            _light.pointLightOuterRadius = Mathf.Max(2.6f, b.extents.magnitude + 1.2f);
            _eo.enabled = true;           // registers + applies the matrix now
            return true;
        }

        public Bounds ComputeBounds()
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

        /// Per-segment overlap (jam check): the whole-stroke AABB of an arc
        /// covers space the line never touches.
        public bool OverlapsBounds(Bounds playerBounds)
        {
            if (_pts.Count == 1)
            {
                var b1 = new Bounds(_pts[0], Vector3.zero);
                b1.Expand(EdgeRadius * 2f);
                return b1.Intersects(playerBounds);
            }
            for (int i = 0; i < _pts.Count - 1; i++)
            {
                var b = new Bounds();
                b.SetMinMax(Vector2.Min(_pts[i], _pts[i + 1]), Vector2.Max(_pts[i], _pts[i + 1]));
                b.Expand(EdgeRadius * 2f);
                if (b.Intersects(playerBounds)) return true;
            }
            return false;
        }

        /// Budget overflow: blink 0.5 s then despawn — deferred while stood on.
        public void BeginDespawn(PlayerController player)
        {
            if (_despawning) return;
            _despawning = true;
            StartCoroutine(DespawnRoutine(player));
        }

        IEnumerator DespawnRoutine(PlayerController player)
        {
            float t = 0f;
            while (t < 0.5f)
            {
                _lr.enabled = !_lr.enabled;
                _glow.enabled = _lr.enabled;
                yield return new WaitForSeconds(0.08f);
                t += 0.08f;
            }
            // keep blinking while the player is standing on it
            while (player != null && player.IsStandingOn(Edge))
            {
                _lr.enabled = !_lr.enabled;
                _glow.enabled = _lr.enabled;
                yield return new WaitForSeconds(0.08f);
            }
            Destroy(gameObject);
        }
    }
}
