using UnityEngine;

namespace Darkroom
{
    /// The exit door. The real exit (R10) begins the finale — she turns and takes
    /// the final photograph herself. The prologue's blank-paper door instead begins
    /// the enter-photo cinematic — she is developed INTO the unprinted frame.
    public class LevelExit : MonoBehaviour
    {
        public bool IsPrologueDoor;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.layer != Layers.Player) return;
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (IsPrologueDoor) gm.BeginPrologueExit(transform.position);
            else gm.BeginFinale();
        }
    }
}
