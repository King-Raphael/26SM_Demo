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
        LineRenderer _halo;
        Light2D _light;
        ExposureObject _eo;
        readonly List<Vector2> _pts = new List<Vector2>();
        bool _despawning;

        public static TrailStroke Create(ExposureObjectType type)
        {
            var go = new GameObject(type == ExposureObjectType.DarkStroke ? "DarkStroke" : "BrightStroke");
            go.layer = Layers.Strokes;
            go.SetActive(false); // defer Awake/OnEnable until configured

            // tapered ends: light eases to a FINE POINT, so a stroke that ends in
            // mid-air (a sharp pen-up / hairpin) closes to a tip instead of a fat
            // glowing cap — the cap was reading as an "explosion" at the corner.
            var taper = new AnimationCurve(
                new Keyframe(0f, 0.25f), new Keyframe(0.12f, 1f),
                new Keyframe(0.88f, 1f), new Keyframe(1f, 0.25f));

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = Width;
            lr.widthCurve = taper;
            lr.sharedMaterial = VisualFactory.GlowMat;
            lr.sortingOrder = VisualFactory.OrderStroke;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 3;
            lr.positionCount = 0;
            var c = VisualFactory.ColorFor(type);
            var core = Color.Lerp(c, Color.white, 0.40f); // brighter near-white core
            lr.startColor = core;
            lr.endColor = core;

            // soft glow line behind the stroke
            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(go.transform, false);
            var glow = glowGO.AddComponent<LineRenderer>();
            glow.useWorldSpace = true;
            glow.widthMultiplier = Width * 5f;
            glow.widthCurve = taper;
            glow.sharedMaterial = VisualFactory.BeamMat;        // feathered soft beam
            glow.textureMode = LineTextureMode.Stretch;
            glow.sortingOrder = VisualFactory.OrderStroke - 1;
            glow.numCapVertices = 6;
            glow.numCornerVertices = 4;
            glow.positionCount = 0;
            var gc = c;
            gc.a = 0.50f; // visible while drawing; matrix-driven after fix
            glow.startColor = gc;
            glow.endColor = gc;

            // broad soft halo behind the beam — luminous falloff. Tighter than
            // DarkTrail's x11: a player stroke can hairpin in mid-air, where a very
            // wide beam folds over itself into a bright blob.
            var haloGO = new GameObject("Halo");
            haloGO.transform.SetParent(go.transform, false);
            var halo = haloGO.AddComponent<LineRenderer>();
            halo.useWorldSpace = true;
            halo.widthMultiplier = Width * 7f;
            halo.widthCurve = taper;
            halo.sharedMaterial = VisualFactory.BeamMat;
            halo.textureMode = LineTextureMode.Stretch;
            halo.sortingOrder = VisualFactory.OrderStroke - 2;
            halo.numCapVertices = 6;
            halo.numCornerVertices = 4;
            halo.positionCount = 0;
            var hc = c; hc.a = 0.22f;
            halo.startColor = hc; halo.endColor = hc;

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
            ts._halo = halo;
            ts._light = light;
            ts.Edge = ec;
            ts._eo = eo;
            eo.BoundsProvider = ts.ComputeBounds;
            eo.OverlapTester = ts.OverlapsBounds;
            eo.OnAlphaApplied = a =>
            {
                var g = glow.startColor; g.a = a * 0.50f; glow.startColor = g; glow.endColor = g;
                var h = halo.startColor; h.a = a * 0.22f; halo.startColor = h; halo.endColor = h;
                light.intensity = a * 0.6f;
            };

            go.SetActive(true);
            return ts;
        }

        public int PointCount => _pts.Count;

        public void AddPoint(Vector2 p)
        {
            _pts.Add(p);
            // render along a Chaikin-smoothed curve so sharp drawn corners don't
            // bunch the wide glow into a hotspot. The COLLIDER stays on the raw
            // _pts (Fix), so the platform you stand on is exactly what you drew.
            var sm = _pts.Count >= 3 ? DarkTrail.Chaikin(_pts.ToArray(), 1) : _pts.ToArray();
            SetLine(_lr, sm); SetLine(_glow, sm); SetLine(_halo, sm);
            _light.transform.position = p; // light rides the pen while drawing
        }

        static void SetLine(LineRenderer lr, Vector2[] pts)
        {
            lr.positionCount = pts.Length;
            for (int i = 0; i < pts.Length; i++) lr.SetPosition(i, pts[i]);
        }

        void SetRenderEnabled(bool on)
        {
            if (_lr != null) _lr.enabled = on;
            if (_glow != null) _glow.enabled = on;
            if (_halo != null) _halo.enabled = on;
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
            // only a BRIGHT stroke counts as delivered light (sensors/umbra read
            // it through the LightField, distance measured along the whole line).
            // A dark stroke is shadow, not light — it must never clear a shade
            // wall, so it never registers as an emitter.
            if (LightField.Instance != null && _eo.type == ExposureObjectType.BrightStroke)
                LightField.Instance.Register(_light.transform, _light.pointLightOuterRadius,
                    () => _light.intensity, DistanceToPoint);

            // juice: the stroke flares into being — endpoints spark, the glow
            // swells then settles, and a bright "set" blip sounds (distinct from
            // the shutter click), so creating terrain is its own rewarded moment.
            var sparkC = Color.Lerp(VisualFactory.ColorFor(_eo.type), Color.white, 0.45f);
            StrokeSparkle.Burst(_pts[0], sparkC, 5);
            StrokeSparkle.Burst(_pts[_pts.Count - 1], sparkC, 5);
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayFixStroke();
            StartCoroutine(FixFlash());
            return true;
        }

        // glow swell → settle, ~0.28 s; scales the glow line's width only (the
        // matrix owns alpha, so this never fights an exposure change).
        IEnumerator FixFlash()
        {
            const float baseW = Width * 5f;
            float t = 0f; const float dur = 0.28f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / dur);
                if (_glow != null) _glow.widthMultiplier = baseW * (1f + 0.9f * k);
                yield return null;
            }
            if (_glow != null) _glow.widthMultiplier = baseW;
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

        /// Nearest distance from a world point to this stroke's polyline. The
        /// LightField uses this so a long stroke lights along its whole length
        /// (a centre-radius would over-reach space the line never touches).
        public float DistanceToPoint(Vector2 p)
        {
            if (_pts.Count == 0) return float.PositiveInfinity;
            if (_pts.Count == 1) return Vector2.Distance(p, _pts[0]);
            float best = float.PositiveInfinity;
            for (int i = 0; i < _pts.Count - 1; i++)
            {
                Vector2 a = _pts[i], ab = _pts[i + 1] - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                float d = Vector2.Distance(p, a + t * ab);
                if (d < best) best = d;
            }
            return best;
        }

        // Strokes are destroyed three ways — budget-overflow despawn, respawn
        // ClearAll, and too-short discard — and all route through Destroy, so a
        // single OnDestroy deregisters the light from the field every time.
        void OnDestroy()
        {
            if (LightField.Instance != null && _light != null)
                LightField.Instance.Unregister(_light.transform);
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
                SetRenderEnabled(!_lr.enabled);
                yield return new WaitForSeconds(0.08f);
                t += 0.08f;
            }
            // keep blinking while the player is standing on it
            while (player != null && player.IsStandingOn(Edge))
            {
                SetRenderEnabled(!_lr.enabled);
                yield return new WaitForSeconds(0.08f);
            }
            Destroy(gameObject);
        }
    }
}
