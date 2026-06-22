using UnityEngine;

namespace Darkroom
{
    /// A few pooled glow motes drifting within a local rectangular band — e.g. light
    /// motes travelling along a DarkPath negative strip, dust in a light beam, or
    /// embers around the exit. Cheap and self-contained: a fixed pool, per-frame
    /// position add with wrap, sine-twinkle alpha. Local space — parent to the
    /// element root. The owning element gates visibility via SetMaster (so motes
    /// vanish when the element is gone in the wrong exposure).
    public class Drift : MonoBehaviour
    {
        public int count = 4;
        public Vector2 area = new Vector2(2f, 0.4f);     // local extents (full w, h)
        public Vector2 velocity = new Vector2(0.5f, 0f); // drift direction/speed (units/s)
        public float size = 0.18f;
        public Color color = Color.white;
        public int sortingOrder = 0;

        float _master = 1f;
        SpriteRenderer[] _sr;
        Vector2[] _pos;
        float[] _phase, _twk;
        System.Random _rng;
        static int _seed = 4096;

        void Start()
        {
            _rng = new System.Random(unchecked(++_seed * 2654435761u).GetHashCode());
            _sr = new SpriteRenderer[count];
            _pos = new Vector2[count];
            _phase = new float[count];
            _twk = new float[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Mote");
                go.transform.SetParent(transform, false);
                _pos[i] = RandomPos();
                go.transform.localPosition = _pos[i];
                go.transform.localScale = new Vector3(size, size, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PixelArt.SoftGlow;
                sr.sharedMaterial = VisualFactory.GlowMat;
                sr.color = color;
                sr.sortingOrder = sortingOrder;
                _sr[i] = sr;
                _phase[i] = (float)_rng.NextDouble() * 6.2832f;
                _twk[i] = 1.5f + (float)_rng.NextDouble() * 2f;
            }
        }

        Vector2 RandomPos() => new Vector2(
            ((float)_rng.NextDouble() - 0.5f) * area.x,
            ((float)_rng.NextDouble() - 0.5f) * area.y);

        void Update()
        {
            if (PauseController.IsPaused || _sr == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < count; i++)
            {
                if (_sr[i] == null) continue;
                _pos[i] += velocity * dt;
                if (Mathf.Abs(velocity.x) > 0.001f && Mathf.Abs(_pos[i].x) > area.x * 0.5f)
                    _pos[i].x = -Mathf.Sign(velocity.x) * area.x * 0.5f;
                if (Mathf.Abs(velocity.y) > 0.001f && Mathf.Abs(_pos[i].y) > area.y * 0.5f)
                    _pos[i].y = -Mathf.Sign(velocity.y) * area.y * 0.5f;
                _sr[i].transform.localPosition = _pos[i];
                float tw = (Mathf.Sin(Time.time * _twk[i] + _phase[i]) + 1f) * 0.5f;
                var c = color; c.a = color.a * tw * _master;
                _sr[i].color = c;
            }
        }

        /// Gate the whole swarm's alpha (called from the element's fade hook).
        public void SetMaster(float m) { _master = Mathf.Clamp01(m); }
    }
}
