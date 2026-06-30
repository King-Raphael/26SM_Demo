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

        /// Char/heat progress 0..1 (drives the growing scar hole). Burned: fired once.
        public System.Action<float> OnCharProgress;
        public System.Action OnBurned;

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
            OnCharProgress?.Invoke(k);
            // a sizzle that swells with the heat (shared bed: loudest paper wins)
            if (AudioDirector.Instance != null) AudioDirector.Instance.RequestBurn(k, transform.position.x);

            // the paper CHARS and is consumed as it burns: brown→char + fading alpha, so
            // by punch-through the intact sheet has gone translucent (not a glowing
            // bright-orange rectangle). The scars/embers carry the heat colour.
            if (_sr != null)
            {
                var pc = Color.Lerp(_baseColor, new Color(0.35f, 0.20f, 0.10f, 1f), k);
                pc.a = Mathf.Lerp(1f, 0.12f, k * k);
                _sr.color = pc;
            }
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
            // No more dark-rectangle ghost: the paper itself nearly vanishes (a faint
            // 0.05 charred film so the gap isn't a clean cut). The ragged char rim
            // (driven from OnBurned in LevelBuilder) is what reads as the burnt edge.
            if (_sr != null) { var c = _sr.color; c.a = 0.05f; _sr.color = c; }
            if (_ember != null) _ember.enabled = false;
            // warm embers UP at the moment of punch-through
            StrokeSparkle.Burst(transform.position, new Color(1f, 0.55f, 0.2f, 1f), 18);
            // ash flakes DOWN + a few cooling embers (one-shot, self-destructing)
            AshBurst.Play(transform.position, boxSize);
            CameraFollow.Instance?.AddTrauma(0.3f); // the wall punches through
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayBurnThrough(transform.position.x);
            OnBurned?.Invoke();
        }
    }
}
