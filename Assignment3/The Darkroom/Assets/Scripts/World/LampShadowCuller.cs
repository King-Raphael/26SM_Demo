using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkroom
{
    /// Caps the cost of 2D shadows: only the nearest N lamp lights keep shadowsEnabled, the
    /// rest switch off. 2D shadows render per-light serially, so bounding the number of
    /// shadow-casting LIGHTS is the real cost lever. Re-evaluated on a coarse cadence (not
    /// every frame) by distance to the camera. Lives on the "Lamps" root, so the lamp point
    /// lights are exactly its child Light2Ds — exposure/sensor/enemy lights live elsewhere.
    public class LampShadowCuller : MonoBehaviour
    {
        public int maxCasters = 4;
        public float interval = 0.25f;

        Light2D[] _lights;
        float _t;

        void Start()
        {
            _lights = GetComponentsInChildren<Light2D>(true);
        }

        void Update()
        {
            if (PauseController.IsPaused) return;
            _t -= Time.deltaTime;
            if (_t > 0f) return;
            _t = interval;
            if (_lights == null || _lights.Length == 0) return;

            var cam = Camera.main;
            if (cam == null) return;
            float cx = cam.transform.position.x, cy = cam.transform.position.y;

            // all off, then enable the nearest N (uses shadowsEnabled itself as the picked mark)
            for (int i = 0; i < _lights.Length; i++)
                if (_lights[i] != null) _lights[i].shadowsEnabled = false;

            int n = Mathf.Min(maxCasters, _lights.Length);
            for (int k = 0; k < n; k++)
            {
                int best = -1;
                float bestD = float.MaxValue;
                for (int i = 0; i < _lights.Length; i++)
                {
                    var l = _lights[i];
                    if (l == null || l.shadowsEnabled) continue; // null or already picked
                    var p = l.transform.position;
                    float d = (p.x - cx) * (p.x - cx) + (p.y - cy) * (p.y - cy);
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best >= 0) _lights[best].shadowsEnabled = true;
            }
        }
    }
}
