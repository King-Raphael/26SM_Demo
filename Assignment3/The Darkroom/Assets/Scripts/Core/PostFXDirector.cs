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
    ///   Over   — FLASH: blown exposure, broad bloom, bleached, no vignette, a flash-punch on entry
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
        ChromaticAberration _ca;
        LensDistortion _lens;

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
        // Over = a warm GOLDEN PRINT, not a white blow-out. Earlier passes pushed
        // exposure + bloom so hard that mid-grey surfaces CLIPPED to flat white boxes and
        // the lamps smeared — ugly, and the edges read worse, not better. The lesson: now
        // that the BACKGROUND is amber (LightDirector void + BackdropTint), Over already
        // reads as another world, so the foreground must NOT blow out — it should hold its
        // tones in a rich, warm, sepia/golden grade (a developed print under the enlarger).
        // Lifted a touch over Normal + warm filter + a warm vignette frame; bloom is gentle
        // and high-threshold so only true lights (lamps, strokes) glow, nothing clips.
        static readonly Grade Over = new Grade
        {
            bloom = 1.0f, bloomThreshold = 0.62f, vignette = 0.18f, saturation = -2f, contrast = 5f, postExposure = 0.42f,
            vignetteColor = new Color(0.09f, 0.07f, 0.05f), filter = new Color(1.04f, 0.97f, 0.88f),
            stShadows = new Color(0.48f, 0.50f, 0.54f), stHighlights = new Color(0.55f, 0.53f, 0.49f), stBalance = 0f,
        };

        Grade _target = Normal;
        Grade _cur = Normal;
        bool _override;
        Grade _overrideGrade;
        const float LerpSpeed = 4.5f;
        bool _washed; float _wash;        // the fixer wash: COLOUR floods back at the end
        const float WashSat = 32f;
        float _flash;                     // shutter-flash punch fired on committing to Over
        const float FlashDecay = 0.2f, FlashExposure = 0.6f, FlashBloom = 0.8f;
        // exposure JOLT: a brief chromatic-aberration + lens-distortion pulse on EVERY
        // non-silent switch (Over strongest, Under pinches). Separate from _flash (Over-only).
        float _jolt; Exposure _joltMode = Exposure.Normal;
        const float JoltDecay = 0.28f;
        const float CAOver = 0.55f, CANormal = 0.30f, CAUnder = 0.22f;
        // gentle lens warp — the strong pinch read as a camera "shake" on every switch
        const float LensUnder = -0.08f, LensOver = 0.04f, LensNormal = 0f;

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

            // at rest these are invisible (intensity 0); only the exposure jolt drives them
            _ca = profile.Add<ChromaticAberration>(true);
            _ca.intensity.value = 0f;
            _lens = profile.Add<LensDistortion>(true);
            _lens.intensity.value = 0f;
            _lens.scale.value = 1f;
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
            // the shutter FIRES: a bright bloom punch when you commit to Over — but not
            // on a silent scripted/respawn set (the blackout's quiet hand has no flash)
            if (e == Exposure.Overexposed && ExposureManager.Instance != null
                && !ExposureManager.Instance.LastChangeSilent)
                _flash = 1f;
            // the optics jolt: fires on every COMMITTED (non-silent) switch — Under/Normal too,
            // where _flash stays quiet — plus a tiny camera tick so the frame "blinks"
            if (ExposureManager.Instance != null && !ExposureManager.Instance.LastChangeSilent)
            {
                _jolt = 1f;
                _joltMode = e;
                CameraFollow.Instance?.AddTrauma(0.05f); // a barely-there blink, not a shake
            }
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

        /// The fixer wash: once she develops the final print, true COLOUR floods
        /// back for the first time in the game. A persistent layer applied ON TOP
        /// of the exposure grade AND the finale flare, so the world stays in colour
        /// through the ending (and the captured frame 11 develops in colour).
        public void BeginColorWash() { _washed = true; }

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

            _wash = Mathf.MoveTowards(_wash, _washed ? 1f : 0f, Time.deltaTime * 1.2f);
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime / FlashDecay);
            _jolt = Mathf.MoveTowards(_jolt, 0f, Time.deltaTime / JoltDecay);

            var shown = _cur;
            // the colour wash sits ON TOP of everything (exposure grade + finale
            // flare): saturation goes clearly POSITIVE and the warm/cool filters
            // neutralise, so it reads as "now it's in colour", not just-another-grade.
            if (_wash > 0.0001f)
            {
                shown.saturation = Mathf.Lerp(_cur.saturation, WashSat, _wash);
                shown.filter = Color.Lerp(_cur.filter, Color.white, _wash);
                shown.stShadows = Color.Lerp(_cur.stShadows, StNeutral, _wash);
                shown.stHighlights = Color.Lerp(_cur.stHighlights, StNeutral, _wash);
            }
            // the shutter-flash punch rides on top: a bright bloom spike that decays
            // into the blown Over grade — the camera firing the instant you commit
            if (_flash > 0.0001f)
            {
                shown.postExposure += FlashExposure * _flash;
                shown.bloom += FlashBloom * _flash;
                shown.bloomThreshold = Mathf.Lerp(shown.bloomThreshold, 0.4f, _flash);
            }
            Apply(shown);

            // the optics jolt: CA fringe + lens distortion, squared for a snap-and-settle.
            // Not part of the grade lerp — they live at 0 at rest, so drive them straight.
            if (_ca != null)
            {
                float j = _jolt * _jolt;
                float caPeak = _joltMode == Exposure.Overexposed ? CAOver
                             : _joltMode == Exposure.Underexposed ? CAUnder : CANormal;
                float lensPeak = _joltMode == Exposure.Overexposed ? LensOver
                               : _joltMode == Exposure.Underexposed ? LensUnder : LensNormal;
                _ca.intensity.value = caPeak * j;
                _lens.intensity.value = lensPeak * j;
            }
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
            _washed = false;
            _wash = 0f;
            _flash = 0f;
            _jolt = 0f;
            if (_ca != null) _ca.intensity.value = 0f;
            if (_lens != null) _lens.intensity.value = 0f;
            _target = Normal;
            _cur = Normal;
            Apply(_cur);
        }
    }
}
