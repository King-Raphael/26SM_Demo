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
            if (_col != null) _col.enabled = true;
            if (_sr != null) { var c = _baseColor; c.a = SolidAlpha; _sr.color = c; }
        }
    }
}
