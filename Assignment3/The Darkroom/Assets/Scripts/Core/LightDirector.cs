using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkroom
{
    /// Real URP 2D lighting driven by the exposure state. Underexposed turns
    /// the world genuinely dark — only light sources (strokes, dark paths,
    /// the player's own glow, the safelight exit) remain readable.
    public class LightDirector : MonoBehaviour
    {
        public static LightDirector Instance { get; private set; }

        // cinematic grading: lamp pools carry the scene in Normal,
        // Under is genuinely cold-dark, Over blows out warm sepia
        // warmer key in Normal, colder/deeper Under, hotter Over — reads more
        // filmic now that bloom + ACES tonemapping carry the highlights
        static readonly Color NormalColor = new Color(0.98f, 0.95f, 0.90f);
        static readonly Color UnderColor = new Color(0.52f, 0.60f, 0.84f);
        static readonly Color OverColor = new Color(1.0f, 0.92f, 0.80f);
        const float NormalIntensity = 1.38f;
        const float UnderIntensity = 0.74f;
        // Over's "overexposed" identity now comes mainly from the warm-WHITE camera
        // background (see *Bg below), so the global light stays moderate — that keeps
        // the foreground reading as shapes against the bright back, not blown too.
        const float OverIntensity = 1.3f;
        const float LerpSpeed = 6f;

        // Per-exposure CAMERA BACKGROUND colour: the mode's identity lives in the
        // backdrop, not a full-screen tint over the foreground — so elements keep
        // their own colour and read as silhouettes against it. Under = deep blue,
        // Normal = near-black (a touch grey), Over = warm white.
        static readonly Color NormalBg = new Color(0.07f, 0.07f, 0.08f);
        static readonly Color UnderBg = new Color(0.04f, 0.06f, 0.14f);
        static readonly Color OverBg = new Color(0.88f, 0.83f, 0.73f);

        Light2D _global;
        Camera _cam;
        Color _targetColor = NormalColor;
        float _targetIntensity = NormalIntensity;
        Color _targetBg = NormalBg;

        // scripted override (Room 9 blackout): wins over the exposure grading
        Color _overrideColor;
        float _overrideIntensity = -1f;

        public void SetOverride(Color c, float intensity)
        {
            _overrideColor = c;
            _overrideIntensity = intensity;
        }

        public void ClearOverride() { _overrideIntensity = -1f; }

        void Awake()
        {
            Instance = this;
            _global = FindGlobal();
            _global.color = NormalColor;
            _global.intensity = NormalIntensity;
        }

        Light2D FindGlobal()
        {
            foreach (var l in Object.FindObjectsByType<Light2D>())
                if (l.lightType == Light2D.LightType.Global) return l;
            var go = new GameObject("GlobalLight2D");
            var l2 = go.AddComponent<Light2D>();
            l2.lightType = Light2D.LightType.Global;
            return l2;
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
            switch (e)
            {
                case Exposure.Underexposed: _targetColor = UnderColor; _targetIntensity = UnderIntensity; _targetBg = UnderBg; break;
                case Exposure.Overexposed: _targetColor = OverColor; _targetIntensity = OverIntensity; _targetBg = OverBg; break;
                default: _targetColor = NormalColor; _targetIntensity = NormalIntensity; _targetBg = NormalBg; break;
            }
        }

        void Update()
        {
            bool over = _overrideIntensity >= 0f;
            var tc = over ? _overrideColor : _targetColor;
            float ti = over ? _overrideIntensity : _targetIntensity;
            float k = Time.deltaTime * LerpSpeed;
            _global.color = Color.Lerp(_global.color, tc, k);
            _global.intensity = Mathf.Lerp(_global.intensity, ti, k);

            // drive the camera background per exposure (override stays on exposure bg)
            if (_cam == null) _cam = Camera.main;
            if (_cam != null) _cam.backgroundColor = Color.Lerp(_cam.backgroundColor, _targetBg, k);
        }

        /// Point light helper used by the builders.
        public static Light2D CreatePoint(Transform parent, Vector2 localPos, Color c, float radius, float intensity)
        {
            var go = new GameObject("Light2D");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.pointLightInnerRadius = 0f;
            l.pointLightOuterRadius = radius;
            l.falloffIntensity = 0.7f;
            return l;
        }
    }
}
