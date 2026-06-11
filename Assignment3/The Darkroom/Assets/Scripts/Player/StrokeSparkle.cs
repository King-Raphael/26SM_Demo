using UnityEngine;

namespace Darkroom
{
    /// Tiny drifting glow particle spawned along a stroke while drawing
    /// (spec stretch #2). Self-destroys after fading out.
    public class StrokeSparkle : MonoBehaviour
    {
        SpriteRenderer _sr;
        Vector2 _vel;
        float _life, _age;
        Color _base;

        /// A puff of particles (death grain-burst, respawn develop-in).
        public static void Burst(Vector2 pos, Color color, int count)
        {
            for (int i = 0; i < count; i++)
                Spawn(pos + Random.insideUnitCircle * 0.35f, color);
        }

        public static void Spawn(Vector2 pos, Color color)
        {
            var go = new GameObject("Sparkle");
            go.transform.position = new Vector3(
                pos.x + Random.Range(-0.06f, 0.06f),
                pos.y + Random.Range(-0.04f, 0.08f), 0f);
            float s = Random.Range(0.10f, 0.22f); // soft-glow sprite fades at its edges
            go.transform.localScale = new Vector3(s, s, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.SoftGlow;
            sr.sharedMaterial = VisualFactory.GlowMat;
            sr.color = color;
            sr.sortingOrder = VisualFactory.OrderStroke + 1;

            var p = go.AddComponent<StrokeSparkle>();
            p._sr = sr;
            p._base = color;
            p._vel = new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.1f, 0.6f));
            p._life = Random.Range(0.25f, 0.45f);
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _life) { Destroy(gameObject); return; }
            transform.position += (Vector3)(_vel * Time.deltaTime);
            var c = _base;
            c.a = 1f - _age / _life;
            _sr.color = c;
        }
    }
}
