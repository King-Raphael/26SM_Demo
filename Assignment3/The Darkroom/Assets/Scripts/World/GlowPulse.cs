using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkroom
{
    /// Slow sine pulse on a sprite halo's alpha and (optionally) a 2D light.
    public class GlowPulse : MonoBehaviour
    {
        public SpriteRenderer Target;
        public float Min = 0.10f;
        public float Max = 0.22f;
        public float Speed = 2f;
        public Light2D Light;
        public float LightMin = 0.3f;
        public float LightMax = 0.6f;

        void Update()
        {
            if (PauseController.IsPaused) return;
            float k = (Mathf.Sin(Time.time * Speed) + 1f) * 0.5f;
            if (Target != null)
            {
                var c = Target.color;
                c.a = Mathf.Lerp(Min, Max, k);
                Target.color = c;
            }
            if (Light != null)
                Light.intensity = Mathf.Lerp(LightMin, LightMax, k);
        }
    }
}
