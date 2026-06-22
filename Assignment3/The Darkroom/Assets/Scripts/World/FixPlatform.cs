using UnityEngine;

namespace Darkroom
{
    /// A latent platform — a faint ghost until you PRINT it. Flash the world
    /// overexposed once while standing near it (but not inside it) and it
    /// develops: collider on, fully solid, and it STAYS, even after you leave
    /// OVER. Lets you lay permanent footing on purpose — planning, not a state
    /// you have to keep holding.
    public class FixPlatform : MonoBehaviour
    {
        public float range = 3.2f;
        public Vector2 boxSize;
        /// Optional latent grain veil that resolves away as the print develops.
        public SpriteRenderer grainVeil;

        const float GhostAlpha = 0.16f, SolidAlpha = 1f;

        Collider2D _col;
        SpriteRenderer _sr;
        Color _baseColor;
        bool _fixed;

        void Awake()
        {
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
            if (_col != null) _col.enabled = false;
            if (_sr != null) { var c = _baseColor; c.a = GhostAlpha; _sr.color = c; }
        }

        void Update()
        {
            if (_fixed) return;
            var em = ExposureManager.Instance;
            var gm = GameManager.Instance;
            if (em == null || gm == null || gm.Player == null) return;
            if (em.Current != Exposure.Overexposed) return;

            Vector2 pp = gm.Player.transform.position;
            if (Vector2.Distance(pp, transform.position) > range) return;

            // never develop a platform the player is standing inside (would trap)
            var mine = new Bounds(transform.position, new Vector3(boxSize.x, boxSize.y, 0.1f));
            var pb = gm.Player.Box.bounds; pb.Expand(0.02f);
            if (mine.Intersects(pb)) return;

            Fix();
        }

        void Fix()
        {
            _fixed = true;
            if (_col != null) _col.enabled = true; // steppable the instant you print
            StartCoroutine(DevelopIn());           // ...the look develops in (cosmetic)
        }

        System.Collections.IEnumerator DevelopIn()
        {
            StrokeSparkle.Burst(transform.position, new Color(0.72f, 0.86f, 1f, 1f), 10);
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayFixPlatform();
            Color veilBase = grainVeil != null ? grainVeil.color : Color.clear;
            float t = 0f; const float dur = 0.5f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                if (_sr != null) { var c = _baseColor; c.a = Mathf.Lerp(GhostAlpha, SolidAlpha, k); _sr.color = c; }
                if (grainVeil != null) { var g = veilBase; g.a = Mathf.Lerp(veilBase.a, 0f, k); grainVeil.color = g; }
                yield return null;
            }
            if (_sr != null) { var c = _baseColor; c.a = SolidAlpha; _sr.color = c; }
            if (grainVeil != null) { var g = veilBase; g.a = 0f; grainVeil.color = g; }
        }
    }
}
