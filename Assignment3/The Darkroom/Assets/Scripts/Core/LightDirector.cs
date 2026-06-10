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

        static readonly Color NormalColor = new Color(1f, 1f, 1f);
        static readonly Color UnderColor = new Color(0.42f, 0.50f, 0.72f);
        static readonly Color OverColor = new Color(1f, 0.94f, 0.82f);
        const float NormalIntensity = 1.0f;
        const float UnderIntensity = 0.30f;
        const float OverIntensity = 1.35f;
        const float LerpSpeed = 6f;

        Light2D _global;
        Color _targetColor = NormalColor;
        float _targetIntensity = NormalIntensity;

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
                case Exposure.Underexposed: _targetColor = UnderColor; _targetIntensity = UnderIntensity; break;
                case Exposure.Overexposed: _targetColor = OverColor; _targetIntensity = OverIntensity; break;
                default: _targetColor = NormalColor; _targetIntensity = NormalIntensity; break;
            }
        }

        void Update()
        {
            float k = Time.deltaTime * LerpSpeed;
            _global.color = Color.Lerp(_global.color, _targetColor, k);
            _global.intensity = Mathf.Lerp(_global.intensity, _targetIntensity, k);
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
