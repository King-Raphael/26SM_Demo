using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkroom
{
    /// GlobalOverexposed: activates when the player overlaps it while
    /// Overexposed (the original photo sensor). LocalLux: activates when enough
    /// *delivered* light reaches it — the global state is not a LightField
    /// emitter, so only a drawn stroke can trip it. Either way it opens its
    /// linked door permanently for the run.
    public class PhotoSensor : MonoBehaviour
    {
        public enum SensorMode { GlobalOverexposed, LocalLux }

        public SensorDoor Door;
        public Light2D ActivateLight;
        public SensorMode mode = SensorMode.GlobalOverexposed;
        public float luxThreshold = 0.6f;

        bool _activated;
        SpriteRenderer _sr;
        SpriteRenderer _accent;

        public void Init(SpriteRenderer body, SpriteRenderer accent)
        {
            _sr = body;
            _accent = accent;
            if (_accent != null) _accent.enabled = false;
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (_activated || mode != SensorMode.GlobalOverexposed) return;
            if (other.gameObject.layer != Layers.Player) return;
            var em = ExposureManager.Instance;
            if (em != null && em.Current == Exposure.Overexposed) Activate();
        }

        // LocalLux meters read the field directly (no player-overlap gate):
        // you deliver light to them with a stroke, you don't stand on them.
        void FixedUpdate()
        {
            if (_activated || mode != SensorMode.LocalLux) return;
            var lf = LightField.Instance;
            if (lf != null && lf.SampleAt(transform.position) >= luxThreshold) Activate();
        }

        void Activate()
        {
            _activated = true;
            if (_sr != null) _sr.color = VisualFactory.SensorActive;
            if (_accent != null) _accent.enabled = true;
            if (ActivateLight != null) ActivateLight.enabled = true;
            if (Door != null) Door.Open();
        }
    }
}
