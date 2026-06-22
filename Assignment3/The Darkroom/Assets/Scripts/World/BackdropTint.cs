using System.Collections.Generic;
using UnityEngine;

namespace Darkroom
{
    /// Tints the whole parallax backdrop toward the exposure's "background colour"
    /// so the SPACE BEHIND the play area reads as the mode (Under = deep blue,
    /// Normal = near-black, Over = warm white) — the camera clear colour alone is
    /// hidden behind the backdrop, so we tint the backdrop itself. Each prop keeps
    /// a little of its own value, so structure stays faintly visible within the tint.
    /// Lamps live under a separate "Lamps" root and are NOT tinted (they're lights).
    public class BackdropTint : MonoBehaviour
    {
        static readonly Color UnderTint = new Color(0.12f, 0.16f, 0.34f);
        static readonly Color OverTint = new Color(0.90f, 0.85f, 0.74f);
        const float UnderStrength = 0.62f;
        const float OverStrength = 0.80f;
        const float LerpSpeed = 6f;

        SpriteRenderer[] _srs;
        Color[] _base;
        Color _curTint = Color.black, _tTint = Color.black;
        float _curStrength, _tStrength;

        void Start()
        {
            var list = new List<SpriteRenderer>();
            foreach (Transform child in transform)
                if (child.name.StartsWith("Layer_"))
                    list.AddRange(child.GetComponentsInChildren<SpriteRenderer>(true));
            _srs = list.ToArray();
            _base = new Color[_srs.Length];
            for (int i = 0; i < _srs.Length; i++) _base[i] = _srs[i].color;

            if (ExposureManager.Instance != null)
            {
                ExposureManager.Instance.OnExposureChanged += OnExposure;
                OnExposure(ExposureManager.Instance.Current);
            }
        }

        void OnDestroy()
        {
            if (ExposureManager.Instance != null)
                ExposureManager.Instance.OnExposureChanged -= OnExposure;
        }

        void OnExposure(Exposure e)
        {
            switch (e)
            {
                case Exposure.Underexposed: _tTint = UnderTint; _tStrength = UnderStrength; break;
                case Exposure.Overexposed: _tTint = OverTint; _tStrength = OverStrength; break;
                default: _tTint = Color.black; _tStrength = 0f; break; // Normal: keep base (dark)
            }
        }

        void Update()
        {
            if (_srs == null || PauseController.IsPaused) return;
            float k = Time.deltaTime * LerpSpeed;
            _curStrength = Mathf.Lerp(_curStrength, _tStrength, k);
            _curTint = Color.Lerp(_curTint, _tTint, k);
            for (int i = 0; i < _srs.Length; i++)
            {
                if (_srs[i] == null) continue;
                var c = Color.Lerp(_base[i], _curTint, _curStrength);
                c.a = _base[i].a;
                _srs[i].color = c;
            }
        }
    }
}
