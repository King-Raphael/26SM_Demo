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
        /// Optional extra effect fired once when the sensor trips (e.g. the colour wash).
        public System.Action onActivated;
        /// Optional readout (light meters only): an iris fill that grows + brightens
        /// as delivered light nears the threshold, so "bring light here" is legible
        /// and the additive sum is visible while drawing.
        public SpriteRenderer LuxFill;

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
            if (mode != SensorMode.LocalLux) return;
            var lf = LightField.Instance;
            float lux = lf != null ? lf.SampleAt(transform.position) : 0f;
            UpdateLuxFill(lux);
            // a rising charge tone as delivered light fills the meter (resolves
            // into the door's slam when it trips; silenced the instant it does)
            if (!_activated && AudioDirector.Instance != null)
                AudioDirector.Instance.SetSensorChargeTone(Mathf.Clamp01(lux / Mathf.Max(0.0001f, luxThreshold)));
            if (!_activated && lux >= luxThreshold) Activate();
        }

        void UpdateLuxFill(float lux)
        {
            if (LuxFill == null) return;
            float fill = _activated ? 1f : Mathf.Clamp01(lux / Mathf.Max(0.0001f, luxThreshold));
            float s = Mathf.Lerp(0.12f, 0.5f, fill);
            LuxFill.transform.localScale = new Vector3(s, s, 1f);
            var c = LuxFill.color;
            c.a = Mathf.Lerp(0.10f, 0.9f, fill);
            LuxFill.color = c;
        }

        void Activate()
        {
            _activated = true;
            if (mode == SensorMode.LocalLux && AudioDirector.Instance != null)
                AudioDirector.Instance.SetSensorChargeTone(0f); // door takes it from here
            if (_sr != null) _sr.color = VisualFactory.SensorActive;
            if (_accent != null) _accent.enabled = true;
            if (ActivateLight != null) ActivateLight.enabled = true;
            if (Door != null) Door.Open();
            onActivated?.Invoke();
        }
    }
}
