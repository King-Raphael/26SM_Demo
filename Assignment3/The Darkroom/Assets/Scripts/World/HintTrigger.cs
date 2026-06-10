using UnityEngine;

namespace Darkroom
{
    /// Shows its text in the HUD hint line while the player is inside;
    /// the HUD auto-hides it after leaving.
    public class HintTrigger : MonoBehaviour
    {
        public string Text;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.layer != Layers.Player) return;
            if (HUDController.Instance != null) HUDController.Instance.ShowHint(Text, this);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.gameObject.layer != Layers.Player) return;
            if (HUDController.Instance != null) HUDController.Instance.OnHintExit(this);
        }
    }
}
