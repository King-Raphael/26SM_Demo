using UnityEngine;

namespace Darkroom
{
    public class Checkpoint : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.layer != Layers.Player) return;
            if (GameManager.Instance != null)
                GameManager.Instance.SetCheckpoint(transform.position);
        }
    }
}
