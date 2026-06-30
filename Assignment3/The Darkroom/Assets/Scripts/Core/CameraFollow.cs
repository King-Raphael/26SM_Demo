using UnityEngine;

namespace Darkroom
{
    /// Smooth-follows the player, camera center clamped to the spec bounds.
    /// Plus "life": a faint idle breathing drift (only while she's still) and a
    /// trauma-based impact shake (landings, death, burn) layered ON TOP of the follow.
    public class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance { get; private set; }

        public Transform Target;

        // default bounds frame the real level; the dev lab widens them, and the
        // prologue fences them to its isolated far-left pocket
        public float MinX = -2f, MaxX = 170f, MinY = -1f, MaxY = 9f;
        // tighter follow → less trail during a walk and a smaller recenter glide on stop
        const float SmoothTime = 0.08f;

        public void SetBounds(float minX, float maxX, float minY, float maxY)
        { MinX = minX; MaxX = maxX; MinY = minY; MaxY = maxY; }

        /// Scripted moments (the Room 9 drop) stretch the follow so the
        /// camera visibly fails to keep up. 1 = normal.
        public float LagScale = 1f;

        Vector3 _vel;

        // --- idle breathing (only when still) ---
        const float BreatheAmp = 0.011f;  // world units — a barely-there vertical breath
        const float BreatheFreq = 0.09f;  // Hz — slow
        // --- trauma shake (impact, not earthquake) ---
        const float TraumaDecay = 1.8f;   // /s — settles in ~0.55s (snappier = reads smoother)
        // gentle + LOW frequency so a hit reads as a smooth thud, not a buzzy rattle
        const float ShakeMaxOffset = 0.10f, ShakeMaxRoll = 0.30f, ShakeFreq = 15f;
        float _seedB, _seedS;             // breath / shake noise seeds
        float _trauma, _prevTargetX, _breath;

        // cutscenes (prologue exit, finale) + the win screen want a still, composed frame —
        // suppress breathing + impact shake while either is active (robust, no manual pairing).
        static bool Composed => GameManager.Instance != null
            && (GameManager.Instance.IsCinematic || GameManager.Instance.HasWon);

        void Awake()
        {
            Instance = this;
            _seedB = Random.value * 1000f;
            _seedS = Random.value * 1000f + 100f;
        }

        /// Impact shake, additive & clamped. Landings pass fallSpeed/15 * k; death/burn a
        /// fixed punch. Ignored during cinematics / the win screen.
        public void AddTrauma(float amount)
        {
            if (Composed) return;
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        void LateUpdate()
        {
            if (Target == null) return;
            var desired = new Vector3(Target.position.x, Target.position.y, transform.position.z);
            var p = Vector3.SmoothDamp(transform.position, desired, ref _vel, SmoothTime * LagScale);
            p.x = Mathf.Clamp(p.x, MinX, MaxX);
            p.y = Mathf.Clamp(p.y, MinY, MaxY);
            // The camera CLEAR colour (the void behind the backdrop) is driven PER EXPOSURE
            // by LightDirector (Under blue / Normal near-black / Over amber).

            // breathing + shake ride ON TOP of the clamped follow, written this frame only —
            // never fed back into _vel or the clamp, so the follow baseline stays pristine.
            Vector3 offset = Vector3.zero;
            float roll = 0f;
            if (!PauseController.IsPaused)
            {
                bool composed = Composed;
                float dt = Time.deltaTime;
                float speed = dt > 0f ? Mathf.Abs(Target.position.x - _prevTargetX) / dt : 0f;
                float stillness = 1f - Mathf.Clamp01(speed / 3f); // full breath when idle, gone by ~3 u/s
                // ease the breath in/out (~0.6s) so it never pops in on a hard stop; vertical-only
                // so it reads as a slow breath, not a fidgety wander
                _breath = Mathf.MoveTowards(_breath, composed ? 0f : stillness, dt * 1.6f);
                if (_breath > 0.001f)
                    offset.y += (Mathf.PerlinNoise(_seedB, Time.time * BreatheFreq) - 0.5f) * 2f * BreatheAmp * _breath;
                if (_trauma > 0.0001f)
                {
                    if (!composed) // decay always, but don't shake a composed/cutscene frame
                    {
                        float s = _trauma * _trauma; // squared = sharp spike, fast settle
                        float tt = Time.time * ShakeFreq;
                        offset.x += (Mathf.PerlinNoise(_seedS, tt) - 0.5f) * 2f * ShakeMaxOffset * s;
                        offset.y += (Mathf.PerlinNoise(_seedS + 11f, tt) - 0.5f) * 2f * ShakeMaxOffset * s;
                        roll = (Mathf.PerlinNoise(_seedS + 23f, tt) - 0.5f) * 2f * ShakeMaxRoll * s;
                    }
                    _trauma = Mathf.MoveTowards(_trauma, 0f, TraumaDecay * dt);
                }
            }
            _prevTargetX = Target.position.x;

            transform.position = p + offset;
            transform.localEulerAngles = new Vector3(0f, 0f, roll);
        }

        public void Snap()
        {
            if (Target == null) return;
            var p = new Vector3(
                Mathf.Clamp(Target.position.x, MinX, MaxX),
                Mathf.Clamp(Target.position.y, MinY, MaxY),
                transform.position.z);
            transform.position = p;
            transform.localEulerAngles = Vector3.zero;
            _vel = Vector3.zero;
            _trauma = 0f;
            _prevTargetX = Target.position.x;
        }
    }
}
