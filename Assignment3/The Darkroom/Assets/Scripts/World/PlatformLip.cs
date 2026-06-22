using UnityEngine;

namespace Darkroom
{
    /// Fades a platform's "lit lip" (rim falloff + core catch-light) almost to nothing
    /// in Underexposed, so it stops tracing the geometry — the spec rule is that normal
    /// geometry is hard to read in UNDER ("only the light you make remains readable").
    /// The lip is unlit (GlowMat) so it blooms softly in Normal/Over, but that also means
    /// it does NOT dim with the global light on its own — hence this explicit gate.
    public class PlatformLip : MonoBehaviour
    {
        const float UnderMul = 0.10f;   // lip almost gone in UNDER
        const float LerpSpeed = 6f;

        SpriteRenderer[] _srs;
        float[] _baseA;
        float _cur = 1f, _target = 1f;

        public void Bind(params SpriteRenderer[] srs)
        {
            _srs = srs;
            _baseA = new float[srs.Length];
            for (int i = 0; i < srs.Length; i++) _baseA[i] = srs[i] != null ? srs[i].color.a : 0f;
            if (ExposureManager.Instance != null)
            {
                ExposureManager.Instance.OnExposureChanged += OnExposure;
                OnExposure(ExposureManager.Instance.Current);
                _cur = _target;
                Apply();
            }
        }

        void OnDestroy()
        {
            if (ExposureManager.Instance != null)
                ExposureManager.Instance.OnExposureChanged -= OnExposure;
        }

        void OnExposure(Exposure e) => _target = e == Exposure.Underexposed ? UnderMul : 1f;

        void Update()
        {
            if (_srs == null || PauseController.IsPaused) return;
            if (Mathf.Abs(_cur - _target) < 0.001f) return; // settled — no per-frame work
            _cur = Mathf.Lerp(_cur, _target, Time.deltaTime * LerpSpeed);
            Apply();
        }

        void Apply()
        {
            for (int i = 0; i < _srs.Length; i++)
            {
                if (_srs[i] == null) continue;
                var c = _srs[i].color;
                c.a = _baseA[i] * _cur;
                _srs[i].color = c;
            }
        }
    }
}
