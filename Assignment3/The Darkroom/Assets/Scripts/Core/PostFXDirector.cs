using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Darkroom
{
    /// Cinematic post-processing, driven by the exposure state (mirrors LightDirector).
    /// The project ships a DefaultVolumeProfile with every effect present but zeroed, and
    /// nothing enabled post on the runtime camera — so none of it rendered. This builds a
    /// global Volume in code, turns the effects on, and lerps them per exposure:
    ///   Under  — deep cool vignette, low bloom, desaturated, pulled-down exposure
    ///   Normal — balanced filmic key (gentle contrast, faint warm filter)
    ///   Over   — warm bloom-out, lifted exposure, almost no vignette
    /// A scripted override (the finale warm flare) wins over the exposure grade, the same
    /// way LightDirector.SetOverride does for the global 2D light.
    public class PostFXDirector : MonoBehaviour
    {
        public static PostFXDirector Instance { get; private set; }

        Volume _volume;
        Bloom _bloom;
        Vignette _vignette;
        ColorAdjustments _color;
        SplitToning _split;

        static readonly Color StNeutral = new Color(0.5f, 0.5f, 0.5f); // grey split-tone = no effect

        struct Grade
        {
            public float bloom, bloomThreshold, vignette, saturation, contrast, postExposure, stBalance;
            public Color vignetteColor, filter, stShadows, stHighlights;
        }

        static readonly Grade Normal = new Grade
        {
            bloom = 0.9f, bloomThreshold = 0.78f, vignette = 0.22f, saturation = -8f, contrast = 4f, postExposure = 0.36f,
            vignetteColor = new Color(0.02f, 0.02f, 0.03f), filter = new Color(1.00f, 0.99f, 0.96f),
            stShadows = StNeutral, stHighlights = StNeutral, stBalance = 0f,
        };
        static readonly Grade Under = new Grade
        {
            bloom = 0.55f, bloomThreshold = 0.70f, vignette = 0.26f, saturation = -18f, contrast = 6f, postExposure = 0.32f,
            vignetteColor = new Color(0.01f, 0.02f, 0.05f), filter = new Color(0.86f, 0.92f, 1.06f),
            stShadows = StNeutral, stHighlights = StNeutral, stBalance = 0f,
        };
        // Over = graded OVEREXPOSURE, not a light flood: a darker base (low global
        // light) + high contrast keeps shadows deep, the warmth lives in a split-tone
        // (warm highlights / cool shadows) instead of a flat cream wash, and bloom
        // halates only the genuinely bright surfaces. Full tonal range = layers.
        static readonly Grade Over = new Grade
        {
            bloom = 0.5f, bloomThreshold = 0.95f, vignette = 0.20f, saturation = -4f, contrast = 8f, postExposure = 0.10f,
            vignetteColor = new Color(0.04f, 0.03f, 0.02f), filter = new Color(1.02f, 1.00f, 0.98f),
            stShadows = new Color(0.47f, 0.49f, 0.55f), stHighlights = new Color(0.63f, 0.58f, 0.47f), stBalance = 0f,
        };

        Grade _target = Normal;
        Grade _cur = Normal;
        bool _override;
        Grade _overrideGrade;
        const float LerpSpeed = 4.5f;

        void Awake()
        {
            Instance = this;
            BuildVolume();
            Apply(_cur);
        }

        void BuildVolume()
        {
            var go = new GameObject("_PostFX");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f; // beats the URP default volume

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.sharedProfile = profile;

            var tm = profile.Add<Tonemapping>(true);
            tm.mode.value = TonemappingMode.ACES;

            _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.value = 0.72f;
            _bloom.intensity.value = Normal.bloom;
            _bloom.scatter.value = 0.72f;
            _bloom.tint.value = new Color(1f, 0.97f, 0.90f);
            _bloom.highQualityFiltering.value = true;

            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.value = Normal.vignette;
            _vignette.smoothness.value = 0.45f;
            _vignette.color.value = Normal.vignetteColor;
            _vignette.rounded.value = false;

            _color = profile.Add<ColorAdjustments>(true);
            _color.contrast.value = Normal.contrast;
            _color.saturation.value = Normal.saturation;
            _color.colorFilter.value = Normal.filter;
            _color.postExposure.value = 0f;

            // split-toning: warm highlights / cool shadows in OVER for tonal layers
            // (neutral grey elsewhere = no effect)
            _split = profile.Add<SplitToning>(true);
            _split.shadows.value = Normal.stShadows;
            _split.highlights.value = Normal.stHighlights;
            _split.balance.value = Normal.stBalance;

            var grain = profile.Add<FilmGrain>(true);
            grain.type.value = FilmGrainLookup.Medium1;
            grain.intensity.value = 0.20f;
            grain.response.value = 0.8f;
        }

        void Start()
        {
            if (ExposureManager.Instance != null)
                ExposureManager.Instance.OnExposureChanged += HandleChanged;
        }

        void OnDestroy()
        {
            if (ExposureManager.Instance != null)
                ExposureManager.Instance.OnExposureChanged -= HandleChanged;
        }

        void HandleChanged(Exposure e)
        {
            _target = e == Exposure.Underexposed ? Under
                    : e == Exposure.Overexposed ? Over
                    : Normal;
        }

        /// Finale flare: warm filter, blown bloom, lifted exposure. Wins over exposure.
        public void SetOverride(Color warmFilter, float bloom, float postExposure)
        {
            _overrideGrade = Normal;
            _overrideGrade.filter = warmFilter;
            _overrideGrade.bloom = bloom;
            _overrideGrade.bloomThreshold = 0.6f; // the finale flare blooms generously
            _overrideGrade.postExposure = postExposure;
            _overrideGrade.vignette = 0.10f;
            _overrideGrade.saturation = 0f;
            _overrideGrade.stShadows = StNeutral;
            _overrideGrade.stHighlights = StNeutral;
            _overrideGrade.stBalance = 0f;
            _override = true;
        }

        public void ClearOverride() { _override = false; }

        void Update()
        {
            var dst = _override ? _overrideGrade : _target;
            float k = Time.deltaTime * LerpSpeed;
            _cur.bloom = Mathf.Lerp(_cur.bloom, dst.bloom, k);
            _cur.bloomThreshold = Mathf.Lerp(_cur.bloomThreshold, dst.bloomThreshold, k);
            _cur.vignette = Mathf.Lerp(_cur.vignette, dst.vignette, k);
            _cur.vignetteColor = Color.Lerp(_cur.vignetteColor, dst.vignetteColor, k);
            _cur.saturation = Mathf.Lerp(_cur.saturation, dst.saturation, k);
            _cur.contrast = Mathf.Lerp(_cur.contrast, dst.contrast, k);
            _cur.postExposure = Mathf.Lerp(_cur.postExposure, dst.postExposure, k);
            _cur.filter = Color.Lerp(_cur.filter, dst.filter, k);
            _cur.stShadows = Color.Lerp(_cur.stShadows, dst.stShadows, k);
            _cur.stHighlights = Color.Lerp(_cur.stHighlights, dst.stHighlights, k);
            _cur.stBalance = Mathf.Lerp(_cur.stBalance, dst.stBalance, k);
            Apply(_cur);
        }

        void Apply(Grade g)
        {
            if (_bloom == null) return;
            _bloom.intensity.value = g.bloom;
            _bloom.threshold.value = g.bloomThreshold;
            _vignette.intensity.value = g.vignette;
            _vignette.color.value = g.vignetteColor;
            _color.saturation.value = g.saturation;
            _color.contrast.value = g.contrast;
            _color.postExposure.value = g.postExposure;
            _color.colorFilter.value = g.filter;
            _split.shadows.value = g.stShadows;
            _split.highlights.value = g.stHighlights;
            _split.balance.value = g.stBalance;
        }

        /// Full restart: drop any finale flare and snap back to the Normal grade.
        public void ResetForRestart()
        {
            _override = false;
            _target = Normal;
            _cur = Normal;
            Apply(_cur);
        }
    }
}
