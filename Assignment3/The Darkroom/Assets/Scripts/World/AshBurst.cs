using UnityEngine;

namespace Darkroom
{
    /// One-shot ash-and-ember aftermath for a burn-through. Spawns a single pooled
    /// batch of dark ash flakes that flutter DOWN with sway and a few warm embers
    /// that drift up and COOL from amber to dull soot, then self-destroys when the
    /// longest-lived particle is gone (no permanent per-frame system). Mirrors the
    /// Drift/StrokeSparkle lifecycle: fixed array, per-frame add + fade, Destroy.
    public class AshBurst : MonoBehaviour
    {
        struct P
        {
            public Transform t;
            public SpriteRenderer sr;
            public Vector2 vel;
            public float life, age, phase, sway, spin;
            public bool ember;
        }

        P[] _p;

        /// Build a one-shot aftermath burst centred at `center`, scattered over `box`
        /// (the wall's world size). Falling ash + a few cooling embers.
        public static void Play(Vector2 center, Vector2 box)
        {
            var root = new GameObject("_AshBurst");
            root.transform.position = new Vector3(center.x, center.y, 0f);
            root.AddComponent<AshBurst>().Build(box);
        }

        /// A single falling ash flake (for the live trickle while the wall burns).
        /// Self-destroys via a tiny OneShotFlake. Uses SoftGlow on GlowMat like the
        /// other particles so it reads against the dark room.
        public static void Flake(Vector2 pos, Color color)
        {
            var go = new GameObject("AshFlake");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            float s = Random.Range(0.04f, 0.10f);
            go.transform.localScale = new Vector3(s, s, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.SoftGlow;
            sr.sharedMaterial = VisualFactory.GlowMat;
            sr.color = color;
            sr.sortingOrder = VisualFactory.OrderExposure + 2;
            var f = go.AddComponent<OneShotFlake>();
            f.Init(sr, new Vector2(Random.Range(-0.18f, 0.18f), Random.Range(-0.7f, -0.3f)),
                   Random.Range(0.8f, 1.5f), color);
        }

        void Build(Vector2 box)
        {
            const int ash = 14, emb = 6;
            _p = new P[ash + emb];
            float hx = Mathf.Max(0.05f, box.x * 0.45f);
            float hy = Mathf.Max(0.05f, box.y * 0.45f);
            for (int i = 0; i < _p.Length; i++)
            {
                bool e = i >= ash;
                var go = new GameObject(e ? "Ember" : "Ash");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(
                    Random.Range(-hx, hx), Random.Range(-hy, hy), 0f);
                float sz = e ? Random.Range(0.12f, 0.20f) : Random.Range(0.05f, 0.11f);
                go.transform.localScale = new Vector3(sz, sz, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PixelArt.SoftGlow;
                sr.sharedMaterial = VisualFactory.GlowMat;
                sr.sortingOrder = VisualFactory.OrderExposure + 2;
                sr.color = e
                    ? new Color(1f, 0.52f, 0.16f, 1f)                                   // warm ember
                    : new Color(0.20f, 0.19f, 0.18f, Random.Range(0.55f, 0.85f));       // cool ash

                _p[i] = new P
                {
                    t = go.transform,
                    sr = sr,
                    ember = e,
                    vel = e
                        ? new Vector2(Random.Range(-0.18f, 0.18f), Random.Range(0.10f, 0.40f))   // embers rise
                        : new Vector2(Random.Range(-0.22f, 0.22f), Random.Range(-0.65f, -0.25f)), // ash falls
                    life = e ? Random.Range(1.2f, 1.7f) : Random.Range(1.0f, 1.9f),
                    age = 0f,
                    phase = Random.value * 6.2832f,
                    sway = Random.Range(0.25f, 0.7f),
                    spin = Random.Range(-160f, 160f)
                };
            }
        }

        void Update()
        {
            if (PauseController.IsPaused || _p == null) return;
            float dt = Time.deltaTime;
            bool any = false;
            for (int i = 0; i < _p.Length; i++)
            {
                if (_p[i].sr == null) continue;
                any = true;
                _p[i].age += dt;
                float u = _p[i].age / _p[i].life;
                if (u >= 1f)
                {
                    Destroy(_p[i].sr.gameObject);
                    _p[i].sr = null;
                    continue;
                }
                float vx = _p[i].vel.x + Mathf.Sin(Time.time * 2.4f + _p[i].phase) * _p[i].sway;
                _p[i].t.localPosition += new Vector3(vx, _p[i].vel.y, 0f) * dt;
                if (_p[i].ember)
                {
                    // cool from amber to dull soot, fade with 1-u^2 so it glows then dies
                    var c = Color.Lerp(new Color(1f, 0.52f, 0.16f, 1f),
                                       new Color(0.25f, 0.10f, 0.04f, 1f), u);
                    c.a = 1f - u * u;
                    _p[i].sr.color = c;
                }
                else
                {
                    _p[i].t.Rotate(0f, 0f, _p[i].spin * dt);
                    var c = _p[i].sr.color;
                    c.a = (1f - u) * 0.85f;
                    _p[i].sr.color = c;
                }
            }
            if (!any) Destroy(gameObject);
        }
    }

    /// A single self-destructing flake/ember for the live burning trickle. Drifts,
    /// fades over its life, then removes itself. Trivial sibling of StrokeSparkle.
    public class OneShotFlake : MonoBehaviour
    {
        SpriteRenderer _sr;
        Vector2 _vel;
        float _life, _age, _phase;
        Color _base;

        public void Init(SpriteRenderer sr, Vector2 vel, float life, Color baseColor)
        {
            _sr = sr;
            _vel = vel;
            _life = Mathf.Max(0.05f, life);
            _base = baseColor;
            _phase = Random.value * 6.2832f;
        }

        void Update()
        {
            if (PauseController.IsPaused || _sr == null) return;
            _age += Time.deltaTime;
            if (_age >= _life) { Destroy(gameObject); return; }
            float vx = _vel.x + Mathf.Sin(Time.time * 2.4f + _phase) * 0.35f;
            transform.position += new Vector3(vx, _vel.y, 0f) * Time.deltaTime;
            var c = _base;
            c.a = _base.a * (1f - _age / _life);
            _sr.color = c;
        }
    }
}
