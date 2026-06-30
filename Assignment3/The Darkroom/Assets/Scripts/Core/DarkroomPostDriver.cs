using UnityEngine;

namespace Darkroom
{
    /// Feeds the Darkroom/Post fullscreen shader its global params per exposure: a Sabattier
    /// solar PULSE on every switch (decays ~0.35s), plus grain/halation amounts that lerp per
    /// state — halation reddens hard in Over (the only place it goes red, per the locked rule),
    /// stays warm-cream otherwise. Added to the managers by Bootstrap; harmless (just sets
    /// unused globals) if the DarkroomPostFeature isn't attached to the URP renderer yet.
    public class DarkroomPostDriver : MonoBehaviour
    {
        static readonly int Solar = Shader.PropertyToID("_DR_Solar");
        static readonly int Grain = Shader.PropertyToID("_DR_Grain");
        static readonly int Halation = Shader.PropertyToID("_DR_Halation");
        static readonly int HalRed = Shader.PropertyToID("_DR_HalRed");
        static readonly int GrainA = Shader.PropertyToID("_DR_GrainA");
        static readonly int GrainB = Shader.PropertyToID("_DR_GrainB");
        static readonly int GrainMix = Shader.PropertyToID("_DR_GrainMix");

        [Tooltip("Grain morph rate — how many fresh grain fields per second it crossfades through.")]
        public float grainFps = 16f;

        float _solar;
        float _grain = 0.06f, _halation = 0.5f, _halRed = 0.1f;
        float _grainT = 0.06f, _halT = 0.5f, _halRedT = 0.1f;
        float _gA, _gB, _gMix; // two grain seeds + a crossfade phase: smooth, never a discrete jump

        void OnEnable()
        {
            _gA = Random.value; _gB = Random.value;
            if (ExposureManager.Instance != null)
            {
                ExposureManager.Instance.OnExposureChanged += OnExposure;
                OnExposure(ExposureManager.Instance.Current);
            }
        }

        void OnDisable()
        {
            if (ExposureManager.Instance != null)
                ExposureManager.Instance.OnExposureChanged -= OnExposure;
        }

        void OnExposure(Exposure e)
        {
            // NO solarization flash — the exposure change is carried by PostFXDirector's
            // colour grade (a smooth transition). This shader only holds the constant
            // film grain + halation, so _solar stays 0.
            switch (e)
            {
                case Exposure.Underexposed: _grainT = 0.075f; _halT = 0.35f; _halRedT = 0.0f; break;
                case Exposure.Overexposed:  _grainT = 0.045f; _halT = 0.90f; _halRedT = 0.85f; break;
                default:                    _grainT = 0.06f;  _halT = 0.50f; _halRedT = 0.10f; break;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _solar = Mathf.MoveTowards(_solar, 0f, dt / 0.28f);
            _grain = Mathf.Lerp(_grain, _grainT, dt * 4f);
            _halation = Mathf.Lerp(_halation, _halT, dt * 4f);
            _halRed = Mathf.Lerp(_halRed, _halRedT, dt * 4f);

            Shader.SetGlobalFloat(Solar, _solar);
            Shader.SetGlobalFloat(Grain, _grain);
            Shader.SetGlobalFloat(Halation, _halation);
            Shader.SetGlobalFloat(HalRed, _halRed);
            _gMix += dt * grainFps;
            while (_gMix >= 1f) { _gMix -= 1f; _gA = _gB; _gB = Random.value; } // roll to a fresh field, keep crossfading
            Shader.SetGlobalFloat(GrainA, _gA);
            Shader.SetGlobalFloat(GrainB, _gB);
            Shader.SetGlobalFloat(GrainMix, _gMix);
        }
    }
}
