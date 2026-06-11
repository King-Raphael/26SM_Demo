using UnityEngine;

namespace Darkroom
{
    /// Starts closed (solid, World). Open = collider off, alpha 0.15.
    /// Stays open across respawns; resets only on full restart (rebuild).
    public class SensorDoor : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        Collider2D _col;

        void Awake()
        {
            _col = GetComponent<Collider2D>();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _col.enabled = false;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                var c = sr.color;
                c.a = 0.15f;
                sr.color = c;
            }
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayDoor();
        }
    }
}
