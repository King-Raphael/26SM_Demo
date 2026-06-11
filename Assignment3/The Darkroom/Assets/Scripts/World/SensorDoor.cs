using UnityEngine;

namespace Darkroom
{
    /// Starts closed (solid, World). Open = collider off, alpha 0.15.
    /// Stays open across respawns; resets only on full restart (rebuild).
    public class SensorDoor : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        Collider2D _col;
        SpriteRenderer _sr;

        void Awake()
        {
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _col.enabled = false;
            var c = _sr.color;
            c.a = 0.15f;
            _sr.color = c;
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayDoor();
        }
    }
}
