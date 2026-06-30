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
        /// Optional latent grain veil that resolves into a faint warm emulsion
        /// residual as the print develops — the permanent "this is a print" mark.
        public SpriteRenderer grainVeil;

        const float GhostAlpha = 0.16f, SolidAlpha = 1f;
        // a slow chemical-bath develop, not an instant pop — this is the first
        // time the player SEES they changed the photo, so it gets room to breathe
        const float DevelopDur = 1.1f;
        // the cool undeveloped negative warms toward developed paper, and the
        // grain veil settles to a faint warm tooth that NEVER fully clears
        static readonly Color PrintedTint = new Color(0.82f, 0.80f, 0.74f, 1f);
        static readonly Color EmulsionResidual = new Color(0.96f, 0.86f, 0.66f, 0.10f);
        static readonly int DevelopId = Shader.PropertyToID("_Develop");

        Collider2D _col;
        SpriteRenderer _sr;
        Color _baseColor;
        MaterialPropertyBlock _mpb;
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
            if (AudioDirector.Instance != null)
            {
                AudioDirector.Instance.PlayFixPlatform(transform.position.x);
                AudioDirector.Instance.PlayDevelopLong(); // the lush chemical bath under the click
            }
            StrokeSparkle.Burst(transform.position, new Color(0.72f, 0.86f, 1f, 1f), 10);

            Color veilFrom = grainVeil != null ? grainVeil.color : Color.clear;

            // re-point the develop at the authored SpriteDevelop shader: the latent
            // image rises out of the grain behind a warm halation front (grain →
            // surfacing → solid) instead of a flat alpha crossfade. Per-renderer via
            // an MPB so two slabs printing at once never share the shared _Develop.
            var dev = VisualFactory.DevelopMat; // null if the custom shader is absent
            bool useShader = dev != null && _sr != null;
            Material restoreMat = _sr != null ? _sr.sharedMaterial : null;
            if (useShader)
            {
                _sr.sharedMaterial = dev;
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
            }

            float t = 0f;
            bool midSpark = false;
            while (t < DevelopDur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / DevelopDur);

                // cool undeveloped negative → warm developed paper as it surfaces
                if (_sr != null)
                {
                    var c = Color.Lerp(_baseColor, PrintedTint, k);
                    c.a = useShader ? SolidAlpha : Mathf.Lerp(GhostAlpha, SolidAlpha, k);
                    _sr.color = c;
                }
                if (useShader)
                {
                    _sr.GetPropertyBlock(_mpb);
                    _mpb.SetFloat(DevelopId, k); // drives the grain-threshold reveal
                    _sr.SetPropertyBlock(_mpb);
                }

                // a warm spark as the develop front crosses the slab's middle
                if (!midSpark && k > 0.5f)
                {
                    midSpark = true;
                    StrokeSparkle.Burst(transform.position, new Color(1f, 0.92f, 0.74f, 1f), 8);
                }

                if (grainVeil != null) grainVeil.color = Color.Lerp(veilFrom, EmulsionResidual, k);
                yield return null;
            }

            // settle to a PERMANENT printed slab: lit material restored, warm paper
            // tint kept, and a faint warm emulsion tooth left behind for good — so
            // printed footing reads as a developed print forever, not native ground
            if (useShader)
            {
                _sr.SetPropertyBlock(null);
                _sr.sharedMaterial = restoreMat;
            }
            if (_sr != null) _sr.color = PrintedTint;
            if (grainVeil != null) grainVeil.color = EmulsionResidual;
        }
    }
}
