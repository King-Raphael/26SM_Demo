using UnityEngine;

namespace Darkroom
{
    /// The exit door: reaching it begins the finale — she turns and takes
    /// the final photograph herself.
    public class LevelExit : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.layer != Layers.Player) return;
            if (GameManager.Instance != null) GameManager.Instance.BeginFinale();
        }
    }
}
