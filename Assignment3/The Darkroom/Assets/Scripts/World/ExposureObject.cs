using System;
using System.Collections;
using UnityEngine;

namespace Darkroom
{
    public enum ExposureObjectType { StaticGround, DarkPath, BrightBarrier, DarkStroke, BrightStroke }

    /// Applies the solidity/alpha matrix (spec section 6). Collider toggles
    /// instantly; alpha lerps 0.15 s, or 0.3 s when becoming non-solid (ghost).
    /// Registers with ExposureManager for the jam check while enabled.
    public class ExposureObject : MonoBehaviour
    {
        public ExposureObjectType type;
        /// World-space size for boxes (tiled sprites keep localScale at 1,
        /// so lossyScale no longer reflects the real footprint).
        public Vector2 boxSize;
        /// Override for strokes (disabled colliders have invalid bounds).
        public Func<Bounds> BoundsProvider;
        /// Optional precise overlap test (strokes test per segment — a whole-
        /// stroke AABB would jam far away from the actual line).
        public Func<Bounds, bool> OverlapTester;
        /// Mirrors every alpha change to attached visuals (glow halos).
        public Action<float> OnAlphaApplied;

        const float SolidAlpha = 1f, FadedAlpha = 0.18f, GoneAlpha = 0f;
        const float FadeTime = 0.15f, GhostFadeTime = 0.3f;

        Collider2D _col;
        SpriteRenderer _sr;
        LineRenderer _lr;
        Color _baseColor;
        bool _baseCached;
        bool _prevSolid;
        Coroutine _fade;

        public static bool IsSolid(ExposureObjectType t, Exposure e)
        {
            switch (t)
            {
                case ExposureObjectType.DarkPath:      return e == Exposure.Underexposed;
                case ExposureObjectType.BrightBarrier: return e != Exposure.Overexposed;
                case ExposureObjectType.DarkStroke:    return e == Exposure.Underexposed;
                case ExposureObjectType.BrightStroke:  return e == Exposure.Overexposed;
                default:                               return true; // StaticGround
            }
        }

        public static float TargetAlpha(ExposureObjectType t, Exposure e)
        {
            switch (t)
            {
                case ExposureObjectType.DarkPath:
                    return e == Exposure.Underexposed ? SolidAlpha : GoneAlpha;
                case ExposureObjectType.BrightBarrier:
                    return e == Exposure.Overexposed ? FadedAlpha : SolidAlpha;
                case ExposureObjectType.DarkStroke:
                    if (e == Exposure.Underexposed) return SolidAlpha;
                    return e == Exposure.Normal ? FadedAlpha : GoneAlpha;
                case ExposureObjectType.BrightStroke:
                    if (e == Exposure.Overexposed) return SolidAlpha;
                    return e == Exposure.Normal ? FadedAlpha : GoneAlpha;
                default:
                    return SolidAlpha;
            }
        }

        public bool IsSolidIn(Exposure e) => IsSolid(type, e);

        void Awake()
        {
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();
            _lr = GetComponent<LineRenderer>();
        }

        void OnEnable()
        {
            CacheBase();
            var mgr = ExposureManager.Instance;
            if (mgr == null)
            {
                ApplyImmediate(Exposure.Normal); // edit-mode build (validator)
                return;
            }
            mgr.Register(this);
            mgr.OnExposureChanged += HandleChanged;
            ApplyImmediate(mgr.Current);
        }

        void OnDisable()
        {
            var mgr = ExposureManager.Instance;
            if (mgr != null)
            {
                mgr.Unregister(this);
                mgr.OnExposureChanged -= HandleChanged;
            }
        }

        void CacheBase()
        {
            if (_baseCached) return;
            if (_sr != null) _baseColor = _sr.color;
            else if (_lr != null) _baseColor = _lr.startColor;
            else _baseColor = Color.white;
            _baseCached = true;
        }

        public Bounds GetWorldBounds()
        {
            if (BoundsProvider != null) return BoundsProvider();
            if (boxSize.sqrMagnitude > 0f)
                return new Bounds(transform.position, new Vector3(boxSize.x, boxSize.y, 0.1f));
            return new Bounds(transform.position, transform.lossyScale);
        }

        public bool OverlapsPlayer(Bounds playerBounds)
        {
            if (OverlapTester != null) return OverlapTester(playerBounds);
            return GetWorldBounds().Intersects(playerBounds);
        }

        void ApplyImmediate(Exposure e)
        {
            bool solid = IsSolid(type, e);
            if (_col != null) _col.enabled = solid;
            SetAlpha(TargetAlpha(type, e));
            _prevSolid = solid;
        }

        void HandleChanged(Exposure e)
        {
            bool solid = IsSolid(type, e);
            if (_col != null) _col.enabled = solid;
            float dur = (_prevSolid && !solid) ? GhostFadeTime : FadeTime;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeTo(TargetAlpha(type, e), dur));
            _prevSolid = solid;
        }

        IEnumerator FadeTo(float target, float dur)
        {
            float start = CurrentAlpha();
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(start, target, Mathf.Clamp01(t / dur)));
                yield return null;
            }
            SetAlpha(target);
            _fade = null;
        }

        float CurrentAlpha()
        {
            if (_sr != null) return _sr.color.a;
            if (_lr != null) return _lr.startColor.a;
            return 1f;
        }

        void SetAlpha(float a)
        {
            var c = _baseColor;
            c.a = a;
            if (_sr != null) _sr.color = c;
            if (_lr != null) { _lr.startColor = c; _lr.endColor = c; }
            OnAlphaApplied?.Invoke(a);
        }

        /// Re-apply the current state (call after wiring OnAlphaApplied).
        public void Reapply()
        {
            var mgr = ExposureManager.Instance;
            ApplyImmediate(mgr != null ? mgr.Current : Exposure.Normal);
        }
    }
}
