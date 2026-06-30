using UnityEngine;

namespace Darkroom
{
    /// Drifting chemical fog (a W4 atmosphere pass). A few large quads parented to the
    /// camera, each running the Darkroom/Fog procedural-noise shader: domain-warped FBM in
    /// world space that EVOLVES over time, so dense patches form and dissipate (gather /
    /// disperse) and flow internally. The shader self-animates; this script only re-tints
    /// the fog per exposure (cool-blue Under / warm Over / neutral Normal) so the haze
    /// belongs to the room's light. Purely cosmetic; lives on the camera (added by
    /// Bootstrap); no-op if the Fog shader is missing.
    public class VaporMotes : MonoBehaviour
    {
        const int Layers = 5;
        // far (big slow soft clumps, behind the scene) -> near (fine fast wisps, in front
        // of the player). _Scale = noise frequency (low = big far clouds, high = fine wisps).
        static readonly float[] LScale = { 0.05f, 0.09f, 0.14f, 0.19f, 0.24f };
        static readonly float[] LSpeed = { 0.03f, 0.05f, 0.07f, 0.09f, 0.12f };
        static readonly float[] LAlpha = { 0.07f, 0.09f, 0.10f, 0.12f, 0.10f };
        // far 3 (6/18/32) stay behind the player (OrderPlayer 50); the two NEAR layers
        // (54, 58) sit IN FRONT of the player (top part = lens core 53) so wisps visibly
        // veil the character, yet stay BELOW the foreground band (OrderForeground 60).
        static readonly int[]   LSort  = { 6, 18, 32, 54, 58 };

        SpriteRenderer[] _sr;
        float[] _baseA;
        static readonly Color Neutral = new Color(0.80f, 0.84f, 0.90f);
        static readonly Color Cool = new Color(0.66f, 0.74f, 0.94f);
        static readonly Color Warm = new Color(0.97f, 0.89f, 0.78f);
        Color _tint = Neutral, _tintTarget = Neutral;

        void Start()
        {
            var sh = Shader.Find("Darkroom/Fog");
            if (sh == null) return; // graceful: no fog rather than a grey rectangle

            var root = new GameObject("_Vapor").transform;
            root.SetParent(transform, false);          // parent to the camera -> always in view
            root.localPosition = new Vector3(0f, 0f, 10f);

            _sr = new SpriteRenderer[Layers];
            _baseA = new float[Layers];
            for (int i = 0; i < Layers; i++)
            {
                var go = new GameObject("FogLayer" + i);
                go.transform.SetParent(root, false);
                go.transform.localScale = new Vector3(30f, 18f, 1f); // covers the ortho view + margin

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = VisualFactory.WhiteSprite;
                sr.sortingOrder = LSort[i];
                _baseA[i] = LAlpha[i];
                sr.color = new Color(_tint.r, _tint.g, _tint.b, _baseA[i]);

                var m = new Material(sh);
                m.SetFloat("_Scale", LScale[i]); // far big clouds -> near fine wisps
                m.SetFloat("_Speed", LSpeed[i]); // far slow -> near faster (parallax depth)
                m.SetFloat("_Seed", i * 41.7f);
                sr.sharedMaterial = m;
                _sr[i] = sr;
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
            _tintTarget = e == Exposure.Underexposed ? Cool
                        : e == Exposure.Overexposed ? Warm
                        : Neutral;
        }

        void Update()
        {
            if (_sr == null || PauseController.IsPaused) return;
            _tint = Color.Lerp(_tint, _tintTarget, Time.deltaTime * 2.5f);
            for (int i = 0; i < Layers; i++)
                if (_sr[i] != null) _sr[i].color = new Color(_tint.r, _tint.g, _tint.b, _baseA[i]);
        }
    }
}
