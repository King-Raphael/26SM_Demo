using System.Collections.Generic;
using UnityEngine;

namespace Darkroom
{
    /// Tints the whole parallax backdrop toward the exposure's "background colour" so
    /// the SPACE BEHIND the play area reads as the mode (Under = deep blue, Normal =
    /// near-black, Over = soft warm-white). Also FADES the constant-dark silhouette bands
    /// (MidGround hangings, Foreground framing, background Figures) out in Over — they read
    /// as odd black blobs against the bright Over wall, so Over drops them for a clean,
    /// harmonious frame. The camera clear colour (the empty void) is the partner layer,
    /// driven per exposure by LightDirector. Lamps live under "Lamps" and are NOT touched.
    public class BackdropTint : MonoBehaviour
    {
        // Under = a deep cool blue; Over = a soft WARM-WHITE (amber read too orange).
        static readonly Color UnderTint = new Color(0.12f, 0.16f, 0.34f);
        static readonly Color OverTint = new Color(0.90f, 0.85f, 0.74f);
        const float UnderStrength = 0.62f;
        const float OverStrength = 0.82f;
        const float LerpSpeed = 6f;

        SpriteRenderer[] _srs;
        Color[] _base;
        Color _curTint = Color.black, _tTint = Color.black;
        float _curStrength, _tStrength;

        // the constant-dark silhouette bands (MidGround hangings, Foreground framing,
        // background Figures) read as odd black blobs against the bright Over wall, so we
        // FADE THEM OUT in Over (and back in Under/Normal) for a clean frame. These roots
        // deliberately LACK the "Layer_" prefix, so they are not tinted — only faded.
        static readonly string[] FadeRoots = { "MidGround", "Foreground", "Figures" };
        SpriteRenderer[] _mid;
        float[] _midA;
        float _midHide, _midHideT;

        void Start()
        {
            var list = new List<SpriteRenderer>();
            foreach (Transform child in transform)
                if (child.name.StartsWith("Layer_"))
                    list.AddRange(child.GetComponentsInChildren<SpriteRenderer>(true));
            _srs = list.ToArray();
            _base = new Color[_srs.Length];
            for (int i = 0; i < _srs.Length; i++) _base[i] = _srs[i].color;

            var fade = new List<SpriteRenderer>();
            foreach (var name in FadeRoots)
            {
                var t = transform.Find(name);
                if (t != null) fade.AddRange(t.GetComponentsInChildren<SpriteRenderer>(true));
            }
            if (fade.Count > 0)
            {
                _mid = fade.ToArray();
                _midA = new float[_mid.Length];
                for (int i = 0; i < _mid.Length; i++) _midA[i] = _mid[i].color.a;
            }

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
            _midHideT = e == Exposure.Overexposed ? 1f : 0f; // hide the dark hangings in Over
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

            // fade the constant-dark bands out in Over (lerped, so it eases with the tint)
            if (_mid != null)
            {
                _midHide = Mathf.Lerp(_midHide, _midHideT, k);
                for (int i = 0; i < _mid.Length; i++)
                {
                    if (_mid[i] == null) continue;
                    var c = _mid[i].color;
                    c.a = _midA[i] * (1f - _midHide);
                    _mid[i].color = c;
                }
            }
        }
    }
}
