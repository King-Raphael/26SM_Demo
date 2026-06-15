using UnityEngine;

namespace Darkroom
{
    /// A slab of shadow that works as a slow vertical lift — and a DARK one:
    /// like a DarkPath it is real only in UNDER. In NORMAL or OVER it is
    /// invisible and intangible, so the shaft below is just a deadly drop.
    /// In UNDER it appears, waits to catch whatever falls onto it, then sinks
    /// slowly (only while ridden) from topY to bottomY, carrying its rider down.
    /// Let the light back in mid-descent and it vanishes — you fall.
    ///
    /// Exposure-driven (no LightField). Resets to the top on respawn.
    public class ShadowLift : MonoBehaviour
    {
        public float topY;
        public float bottomY;
        public float sinkSpeed = 1.6f;
        public Vector2 boxSize;

        const float SolidAlpha = 1f, GoneAlpha = 0f;
        const float FadeSpeed = 10f;

        Rigidbody2D _rb;
        Collider2D _col;
        SpriteRenderer _sr;
        Color _baseColor;
        float _alpha;
        bool _solid;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        void OnEnable()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.OnRespawn += ResetToTop;
            ResetToTop();
        }

        void OnDisable()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.OnRespawn -= ResetToTop;
        }

        void ResetToTop()
        {
            if (_rb != null) _rb.position = new Vector2(_rb.position.x, topY);
            else transform.position = new Vector3(transform.position.x, topY, 0f);
            _solid = false;
            _alpha = GoneAlpha;
            if (_col != null) _col.enabled = false;
            ApplyAlpha(_alpha);
        }

        void FixedUpdate()
        {
            if (_rb == null) return;
            var em = ExposureManager.Instance;
            var e = em != null ? em.Current : Exposure.Normal;

            // a dark shadow: solid + visible only in UNDER
            bool solid = e == Exposure.Underexposed;
            if (solid != _solid)
            {
                _solid = solid;
                if (_col != null) _col.enabled = solid;
            }

            // sink only while BEING RIDDEN, so it waits at the top to catch the
            // player and then carries them down (not before)
            if (solid && _rb.position.y > bottomY)
            {
                var gm = GameManager.Instance;
                bool ridden = gm != null && gm.Player != null && _col != null
                              && gm.Player.IsStandingOn(_col);
                if (ridden)
                {
                    float newY = Mathf.MoveTowards(_rb.position.y, bottomY, sinkSpeed * Time.fixedDeltaTime);
                    float dy = newY - _rb.position.y;
                    if (dy != 0f)
                    {
                        _rb.MovePosition(new Vector2(_rb.position.x, newY));
                        gm.Player.Body.position += new Vector2(0f, dy);
                    }
                }
            }

            float target = solid ? SolidAlpha : GoneAlpha;
            if (!Mathf.Approximately(_alpha, target))
            {
                _alpha = Mathf.MoveTowards(_alpha, target, FadeSpeed * Time.fixedDeltaTime);
                ApplyAlpha(_alpha);
            }
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
