using UnityEngine;
using UnityEngine.Rendering;

namespace Darkroom
{
    /// Real-glass refraction for the HUD. Captures the rendered scene — AFTER the
    /// game camera (so it has the world + post-processing) but BEFORE the overlay HUD
    /// draws (so the glass never samples itself) — into a RenderTexture each frame,
    /// and feeds it to the glass material so the exposure bar refracts + frosts the
    /// background behind it. Degrades gracefully: if the shader is missing the
    /// material is null and the bar falls back to its baked glass sprite.
    public class GlassRefraction : MonoBehaviour
    {
        public static GlassRefraction Instance { get; private set; }
        public Material GlassMat { get; private set; }

        RenderTexture _grab;
        static readonly int GrabId = Shader.PropertyToID("_GrabTex");
        static readonly int TintId = Shader.PropertyToID("_Tint");

        // the glass body picks up the exposure mood: cool in Under, warm in Over.
        static readonly Color TintUnder  = new Color(0.78f, 0.86f, 1.00f);
        static readonly Color TintNormal = new Color(0.94f, 0.96f, 0.99f);
        static readonly Color TintOver   = new Color(1.00f, 0.92f, 0.80f);
        Color _tintCur = TintNormal, _tintTarget = TintNormal;

        void Awake()
        {
            Instance = this;
            var sh = Shader.Find("Darkroom/GlassRefract");
            if (sh != null) GlassMat = new Material(sh) { name = "GlassRefract" };
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        void Start()
        {
            var em = ExposureManager.Instance;
            if (em != null) { em.OnExposureChanged += OnExposure; OnExposure(em.Current); }
        }

        void OnExposure(Exposure e)
        {
            _tintTarget = e == Exposure.Underexposed ? TintUnder
                        : e == Exposure.Overexposed ? TintOver : TintNormal;
        }

        void Update()
        {
            if (GlassMat == null) return;
            _tintCur = Color.Lerp(_tintCur, _tintTarget, Time.deltaTime * 5f);
            GlassMat.SetColor(TintId, _tintCur);
        }

        void OnDestroy()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            if (ExposureManager.Instance != null) ExposureManager.Instance.OnExposureChanged -= OnExposure;
            if (_grab != null) { _grab.Release(); _grab = null; }
        }

        void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (GlassMat == null || cam == null || cam != Camera.main) return;

            int w = Mathf.Max(8, Screen.width), h = Mathf.Max(8, Screen.height);
            if (_grab == null || _grab.width != w || _grab.height != h)
            {
                if (_grab != null) _grab.Release();
                _grab = new RenderTexture(w, h, 0) { name = "GlassGrab" };
                _grab.Create();
                GlassMat.SetTexture(GrabId, _grab);
            }

            // grab the current screen (scene + post, no overlay HUD yet)
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_grab);
        }
    }
}
