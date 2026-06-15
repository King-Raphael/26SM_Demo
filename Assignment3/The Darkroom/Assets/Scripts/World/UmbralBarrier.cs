using UnityEngine;

namespace Darkroom
{
    /// A wall of shadow: solid while dark, it recoils (collider off, fades to a
    /// thin ghost) when delivered light reaches it above `retractThreshold`,
    /// and re-seals when the light leaves. The inverse of BrightBarrier —
    /// cleared by ROUTED light (a drawn stroke), not by a global exposure state.
    ///
    /// Its solidity changes from light, NOT from an exposure switch, so the
    /// ExposureManager jam-check never guards it. It therefore guards itself:
    ///  - it never re-seals while the player overlaps it (it waits, like a
    ///    stroke defers its despawn while stood on), so it can never crush;
    ///  - hysteresis keeps a fading stroke from making the collider chatter;
    ///  - on respawn the strokes are wiped, so lux falls to ~0 and the barrier
    ///    re-seals through this same guarded path — no special reset needed.
    public class UmbralBarrier : MonoBehaviour
    {
        public float retractThreshold = 0.6f;
        public Vector2 boxSize;

        const float SolidAlpha = 1f, GhostAlpha = 0.12f;
        const float Hysteresis = 0.1f;
        const float LerpSpeed = 8f;

        Collider2D _col;
        SpriteRenderer _sr;
        Color _baseColor;
        bool _retracted;     // logical state: true = light has opened the way
        float _alpha = SolidAlpha;

        void Awake()
        {
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        void OnEnable()
        {
            _retracted = false;
            _alpha = SolidAlpha;
            if (_col != null) _col.enabled = true;
            ApplyAlpha(_alpha);
        }

        void FixedUpdate()
        {
            var lf = LightField.Instance;
            // probe at the barrier's foot, where a ground-drawn stroke lands —
            // so a tall shade (one you cannot simply jump over) can still be
            // cleared by lighting its base.
            Vector2 probe = (Vector2)transform.position + new Vector2(0f, -boxSize.y * 0.5f + 0.35f);
            float lux = lf != null ? lf.SampleAt(probe) : 0f;

            if (!_retracted)
            {
                if (lux >= retractThreshold) SetRetracted(true);
            }
            else
            {
                // re-seal only well below threshold (hysteresis) and never onto
                // the player — defer the seal until they have stepped clear
                if (lux <= retractThreshold - Hysteresis && !WouldTrapPlayer())
                    SetRetracted(false);
            }

            float target = _retracted ? GhostAlpha : SolidAlpha;
            if (!Mathf.Approximately(_alpha, target))
            {
                _alpha = Mathf.MoveTowards(_alpha, target, Time.fixedDeltaTime * LerpSpeed);
                ApplyAlpha(_alpha);
            }
        }

        void SetRetracted(bool r)
        {
            _retracted = r;
            if (_col != null) _col.enabled = !r; // collider follows the logic at once
        }

        // Mirrors ExposureManager.WouldJam: the disabled collider has invalid
        // bounds, so test against boxSize (the authored footprint) directly.
        bool WouldTrapPlayer()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return false;
            Bounds pb = gm.Player.Box.bounds;
            pb.Expand(0.02f);
            var mine = new Bounds(transform.position, new Vector3(boxSize.x, boxSize.y, 0.1f));
            return mine.Intersects(pb);
        }

        void ApplyAlpha(float a)
        {
            if (_sr == null) return;
            var c = _baseColor;
            c.a = a;
            _sr.color = c;
        }
    }
}
