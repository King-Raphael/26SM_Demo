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
        /// Fades added child detail (catch-light/underside glow) with the slab.
        public System.Action<float> onAlpha;

        const float SolidAlpha = 1f, GoneAlpha = 0f;
        const float FadeSpeed = 10f;
        // the slab only appears once the player is within this x-range, so the
        // descent can't be spotted from the previous room's bridge while in UNDER
        const float RevealDistX = 4f;
        // a held breath as the shadow takes her weight before it begins to sink —
        // the "caught" beat lands instead of the floor just dropping out
        const float CatchHang = 0.45f;

        float _rideClock; // accumulates while ridden; gates the catch-hang

        Rigidbody2D _rb;
        Collider2D _col;
        SpriteRenderer _sr;
        Color _baseColor;
        float _alpha;
        bool _solid;
        bool _moving;   // drives the lift-motion audio bed (edge-triggered)

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
            _rideClock = 0f;
            if (_col != null) _col.enabled = false;
            ApplyAlpha(_alpha);
            if (_moving) { _moving = false; if (AudioDirector.Instance != null) AudioDirector.Instance.LiftOff(); }
        }

        void FixedUpdate()
        {
            if (_rb == null) return;
            var em = ExposureManager.Instance;
            var e = em != null ? em.Current : Exposure.Normal;
            var gm = GameManager.Instance;

            // a dark shadow: solid + visible only in UNDER — and only once the
            // player is near, so it can't be spotted from the previous room's
            // bridge while crossing it in UNDER (keeps the descent a discovery)
            bool near = gm == null || gm.Player == null
                        || Mathf.Abs(gm.Player.transform.position.x - _rb.position.x) < RevealDistX;
            bool solid = e == Exposure.Underexposed && near;
            if (solid != _solid)
            {
                _solid = solid;
                if (_col != null) _col.enabled = solid;
            }

            // sink only while BEING RIDDEN, so it waits at the top to catch the
            // player and then carries them down (not before) — and only after a
            // brief catch-hang, so the shadow visibly TAKES her weight first
            bool moving = false;
            if (solid && _rb.position.y > bottomY)
            {
                bool ridden = gm != null && gm.Player != null && _col != null
                              && gm.Player.IsStandingOn(_col);
                if (ridden)
                {
                    _rideClock += Time.fixedDeltaTime;
                    if (_rideClock >= CatchHang)
                    {
                        float newY = Mathf.MoveTowards(_rb.position.y, bottomY, sinkSpeed * Time.fixedDeltaTime);
                        float dy = newY - _rb.position.y;
                        if (dy != 0f)
                        {
                            _rb.MovePosition(new Vector2(_rb.position.x, newY));
                            gm.Player.Body.position += new Vector2(0f, dy);
                            moving = true;
                        }
                    }
                }
                else _rideClock = 0f;
            }
            // a slab of shadow, sinking: a dark low motor while it actually moves
            if (moving != _moving)
            {
                _moving = moving;
                var ad = AudioDirector.Instance;
                if (ad != null) { if (moving) ad.LiftOn(0.7f, transform.position.x); else ad.LiftOff(); }
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
            if (_sr != null) { var c = _baseColor; c.a = a; _sr.color = c; }
            onAlpha?.Invoke(a);
        }
    }
}
