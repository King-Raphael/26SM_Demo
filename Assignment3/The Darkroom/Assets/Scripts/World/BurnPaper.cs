using UnityEngine;

namespace Darkroom
{
    /// A sheet of white that OVER doesn't merely pass — it BURNS. Hold the world
    /// overexposed nearby and it heats: an ember glow swells and flickers over
    /// ~burnSeconds, then it burns through — collider off for good, a charred
    /// scar stays, and a spark burst marks the moment. Leaving OVER lets it
    /// cool. The cost is whatever else OVER wakes nearby (the light-sensitive
    /// guard), so burning is a committed, world-altering act.
    public class BurnPaper : MonoBehaviour
    {
        public float burnSeconds = 1.5f;
        public float range = 2.6f;
        public Vector2 boxSize;

        Collider2D _col;
        SpriteRenderer _sr;
        SpriteRenderer _ember;
        Color _baseColor;
        float _t;
        bool _burned;

        void Awake()
        {
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;

            // a heat glow that swells as it burns
            var ember = new GameObject("Ember");
            ember.transform.SetParent(transform, false);
            ember.transform.localScale = new Vector3(Mathf.Max(boxSize.x, 0.6f) * 1.4f,
                                                     Mathf.Max(boxSize.y, 0.6f) * 1.1f, 1f);
            _ember = ember.AddComponent<SpriteRenderer>();
            _ember.sprite = PixelArt.SoftGlow;
            _ember.sharedMaterial = VisualFactory.GlowMat;
            _ember.color = new Color(1f, 0.52f, 0.16f, 0f);
            _ember.sortingOrder = VisualFactory.OrderExposure + 2;
        }

        void Update()
        {
            if (_burned) return;
            var em = ExposureManager.Instance;
            var gm = GameManager.Instance;
            if (em == null || gm == null || gm.Player == null) return;

            bool firing = em.Current == Exposure.Overexposed
                && Vector2.Distance(gm.Player.transform.position, transform.position) <= range;

            // heats while held in OVER nearby; cools faster than it heats
            _t = Mathf.Clamp(_t + (firing ? Time.deltaTime : -Time.deltaTime * 1.5f), 0f, burnSeconds);
            float k = _t / burnSeconds;

            // the paper browns and glows; the ember swells and flickers harder
            if (_sr != null)
                _sr.color = Color.Lerp(_baseColor, new Color(1f, 0.74f, 0.42f, 1f), k);
            if (_ember != null)
            {
                float flick = 1f + (k > 0.25f ? 0.18f * Mathf.Sin(Time.time * 42f) : 0f);
                var c = _ember.color;
                c.a = Mathf.Clamp01(k * 0.8f * flick);
                _ember.color = c;
            }

            if (_t >= burnSeconds) Burn();
        }

        void Burn()
        {
            _burned = true;
            if (_col != null) _col.enabled = false;                  // burnt through, for good
            if (_sr != null) _sr.color = new Color(0.10f, 0.09f, 0.10f, 0.30f); // the scar
            if (_ember != null) _ember.enabled = false;
            StrokeSparkle.Burst(transform.position, new Color(1f, 0.6f, 0.25f, 1f), 16);
        }
    }
}
