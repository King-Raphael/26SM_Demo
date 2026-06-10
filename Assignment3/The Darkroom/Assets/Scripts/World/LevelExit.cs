using UnityEngine;

namespace Darkroom
{
    /// The red exit door: entering it takes the final photograph (win).
    public class LevelExit : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.layer != Layers.Player) return;
            if (GameManager.Instance != null) GameManager.Instance.Win();
        }
    }
}
