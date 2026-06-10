using UnityEngine;

namespace Darkroom
{
    /// Activates when the player overlaps it while Overexposed.
    /// Opens its linked door permanently for the run.
    public class PhotoSensor : MonoBehaviour
    {
        public SensorDoor Door;

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
            if (_activated) return;
            if (other.gameObject.layer != Layers.Player) return;
            var em = ExposureManager.Instance;
            if (em != null && em.Current == Exposure.Overexposed) Activate();
        }

        void Activate()
        {
            _activated = true;
            if (_sr != null) _sr.color = VisualFactory.SensorActive;
            if (_accent != null) _accent.enabled = true;
            if (Door != null) Door.Open();
        }
    }
}
