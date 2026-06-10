using UnityEngine;

namespace Darkroom
{
    public class Checkpoint : MonoBehaviour
    {
        SpriteRenderer _marker;

        void Awake() { _marker = GetComponentInChildren<SpriteRenderer>(); }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.layer != Layers.Player) return;
            if (GameManager.Instance != null)
                GameManager.Instance.SetCheckpoint(transform.position);
            if (_marker != null)
                _marker.color = Color.white; // developed: the photo brightens
        }
    }
}
