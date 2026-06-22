using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkroom
{
    /// Drives the prologue's "the darkroom develops into the photo" transformation
    /// (the doc's three layers). Lowering the room to the safelight (UNDER) fades in
    /// the negative-scratch lines across the walls and lifts the red safelight glow;
    /// raising the work light (NORMAL) fades them back. Also surfaces the
    /// "1 — safelight" / "2 — work light" key hints once each. Prologue-only; built
    /// by LevelBuilder.BuildPrologueProps.
    public class PrologueDirector : MonoBehaviour
    {
        public List<SpriteRenderer> Scratches = new List<SpriteRenderer>();
        // LAYER 2: the "photo lines over reality" set — platform photo-edges and the
        // door seams on the blank paper. Surface under the safelight, recede in work light.
        public List<SpriteRenderer> Overlays = new List<SpriteRenderer>();
        // the red safelights down the corridor: glow halos + their real red Light2Ds.
        // Both swing hard with the mode so pressing 1 visibly turns the safelights ON.
        public List<SpriteRenderer> Safelights = new List<SpriteRenderer>();
        public List<Light2D> SafeLights2D = new List<Light2D>();
        public float ScratchMax = 0.28f;
        public float OverlayMax = 0.5f;
        public float SafelightDim = 0.12f, SafelightLit = 1f;        // glow halo alpha (near-off → full)
        public float SafeIntensityDim = 0.12f, SafeIntensityLit = 0.6f; // red Light2D intensity

        float _t;        // 0 = work light, 1 = safelight
        float _target;
        bool _keySafelightShown, _keyWorkShown;

        void OnEnable()
        {
            var em = ExposureManager.Instance;
            if (em != null) { em.OnExposureChanged += OnExposure; OnExposure(em.Current); }
        }

        void OnDisable()
        {
            var em = ExposureManager.Instance;
            if (em != null) em.OnExposureChanged -= OnExposure;
        }

        void OnExposure(Exposure e)
        {
            bool under = e == Exposure.Underexposed;
            _target = under ? 1f : 0f;

            var hud = HUDController.Instance;
            if (hud == null) return;
            if (under && !_keySafelightShown) { _keySafelightShown = true; hud.ShowKeyHint("1 — safelight"); }
            else if (!under && _keySafelightShown && !_keyWorkShown) { _keyWorkShown = true; hud.ShowKeyHint("2 — work light"); }
        }

        void Update()
        {
            if (PauseController.IsPaused) return;
            _t = Mathf.MoveTowards(_t, _target, Time.deltaTime * 2.5f);

            float sa = _t * ScratchMax;
            for (int i = 0; i < Scratches.Count; i++)
            {
                var s = Scratches[i];
                if (s == null) continue;
                var c = s.color; c.a = sa; s.color = c;
            }
            float oa = _t * OverlayMax;
            for (int i = 0; i < Overlays.Count; i++)
            {
                var s = Overlays[i];
                if (s == null) continue;
                var c = s.color; c.a = oa; s.color = c;
            }
            float glowA = Mathf.Lerp(SafelightDim, SafelightLit, _t);
            for (int i = 0; i < Safelights.Count; i++)
            {
                var s = Safelights[i];
                if (s == null) continue;
                var c = s.color; c.a = glowA; s.color = c;
            }
            float inten = Mathf.Lerp(SafeIntensityDim, SafeIntensityLit, _t);
            for (int i = 0; i < SafeLights2D.Count; i++)
                if (SafeLights2D[i] != null) SafeLights2D[i].intensity = inten;
        }
    }
}
