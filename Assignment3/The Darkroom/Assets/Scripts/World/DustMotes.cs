using UnityEngine;

namespace Darkroom
{
    /// Slow dust drifting in the darkroom air around the camera.
    /// Motes that leave the view re-enter from the opposite side.
    public class DustMotes : MonoBehaviour
    {
        const int Count = 22;
        const float HalfW = 11f, HalfH = 7f;

        Transform[] _motes;
        SpriteRenderer[] _srs;
        Vector2[] _vel;

        void Start()
        {
            _motes = new Transform[Count];
            _srs = new SpriteRenderer[Count];
            _vel = new Vector2[Count];
            var root = new GameObject("_Dust").transform;
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("Mote");
                go.transform.SetParent(root, false);
                float s = Random.Range(0.035f, 0.09f);
                go.transform.localScale = new Vector3(s, s, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = VisualFactory.WhiteSprite;
                sr.sharedMaterial = VisualFactory.GlowMat;
                sr.color = new Color(0.9f, 0.9f, 0.88f, Random.Range(0.05f, 0.12f));
                sr.sortingOrder = VisualFactory.OrderStroke + 2;
                _motes[i] = go.transform;
                _srs[i] = sr;
                _vel[i] = DriftVel();
                go.transform.position = RandomInView();
            }
        }

        Vector2 DriftVel() =>
            new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(-0.12f, 0.18f));

        Vector3 RandomInView() =>
            transform.position + new Vector3(Random.Range(-HalfW, HalfW), Random.Range(-HalfH, HalfH), 10f);

        void Update()
        {
            if (PauseController.IsPaused) return;
            Vector3 cam = transform.position;
            for (int i = 0; i < Count; i++)
            {
                var p = _motes[i].position + (Vector3)(_vel[i] * Time.deltaTime);
                // wrap around the view, re-rolling the drift
                if (p.x < cam.x - HalfW) { p.x = cam.x + HalfW; _vel[i] = DriftVel(); }
                else if (p.x > cam.x + HalfW) { p.x = cam.x - HalfW; _vel[i] = DriftVel(); }
                if (p.y < cam.y - HalfH) { p.y = cam.y + HalfH; _vel[i] = DriftVel(); }
                else if (p.y > cam.y + HalfH) { p.y = cam.y - HalfH; _vel[i] = DriftVel(); }
                p.z = 0f;
                _motes[i].position = p;
            }
        }
    }
}
