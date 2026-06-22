using UnityEngine;

namespace Darkroom
{
    /// The mirror of ShadowLift — a slab of LIGHT, real only in OVER. In NORMAL
    /// or UNDER it is invisible and intangible (the shaft is a deadly drop). In
    /// OVER it appears at the bottom, waits to catch the player, then RISES
    /// slowly (only while ridden) from bottomY up to topY, carrying the rider
    /// up. Let the light go mid-rise and it vanishes — you fall.
    ///
    /// Exposure-driven (no LightField). Resets to the bottom on respawn.
    public class RiseLift : MonoBehaviour
    {
        public float topY;
        public float bottomY;
        public float riseSpeed = 1.6f;
        public Vector2 boxSize;
        /// Fades added child detail (beam supports/top edge) with the slab.
        public System.Action<float> onAlpha;

        const float SolidAlpha = 1f, GoneAlpha = 0f;
        const float FadeSpeed = 10f;

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
            if (gm != null) gm.OnRespawn += ResetToBottom;
            ResetToBottom();
        }

        void OnDisable()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.OnRespawn -= ResetToBottom;
        }

        void ResetToBottom()
        {
            if (_rb != null) _rb.position = new Vector2(_rb.position.x, bottomY);
            else transform.position = new Vector3(transform.position.x, bottomY, 0f);
            _solid = false;
            _alpha = GoneAlpha;
            if (_col != null) _col.enabled = false;
            ApplyAlpha(_alpha);
            if (_moving) { _moving = false; if (AudioDirector.Instance != null) AudioDirector.Instance.LiftOff(); }
        }

        void FixedUpdate()
        {
            if (_rb == null) return;
            var em = ExposureManager.Instance;
            var e = em != null ? em.Current : Exposure.Normal;

            // a slab of light: solid + visible only in OVER
            bool solid = e == Exposure.Overexposed;
            if (solid != _solid)
            {
                _solid = solid;
                if (_col != null) _col.enabled = solid;
            }

            // rise only while ridden, so it waits at the bottom to catch first
            bool moving = false;
            if (solid && _rb.position.y < topY)
            {
                var gm = GameManager.Instance;
                bool ridden = gm != null && gm.Player != null && _col != null
                              && gm.Player.IsStandingOn(_col);
                if (ridden)
                {
                    float newY = Mathf.MoveTowards(_rb.position.y, topY, riseSpeed * Time.fixedDeltaTime);
                    float dy = newY - _rb.position.y;
                    if (dy != 0f)
                    {
                        _rb.MovePosition(new Vector2(_rb.position.x, newY));
                        gm.Player.Body.position += new Vector2(0f, dy);
                        moving = true;
                    }
                }
            }
            // a slab of light, rising: a bright high motor while it actually moves
            if (moving != _moving)
            {
                _moving = moving;
                var ad = AudioDirector.Instance;
                if (ad != null) { if (moving) ad.LiftOn(1.25f); else ad.LiftOff(); }
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
