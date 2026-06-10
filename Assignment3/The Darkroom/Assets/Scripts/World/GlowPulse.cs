using UnityEngine;

namespace Darkroom
{
    /// Slow sine pulse on a sprite's alpha (halos: exit door, pickups).
    public class GlowPulse : MonoBehaviour
    {
        public SpriteRenderer Target;
        public float Min = 0.10f;
        public float Max = 0.22f;
        public float Speed = 2f;

        void Update()
        {
            if (Target == null) return;
            var c = Target.color;
            c.a = Mathf.Lerp(Min, Max, (Mathf.Sin(Time.time * Speed) + 1f) * 0.5f);
            Target.color = c;
        }
    }
}
