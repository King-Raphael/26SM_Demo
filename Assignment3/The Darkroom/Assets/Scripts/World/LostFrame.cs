using UnityEngine;

namespace Darkroom
{
    /// A LOST FRAME — an undeveloped negative hidden in a verb-gated pocket
    /// (reachable only by using a verb the room already taught: switch to Under
    /// behind a dark path, draw a step, print latent footing, burn a recess).
    /// Walk into it and it develops into the photographer's PRIVATE gallery — a
    /// separate roll from the sacred eleven, so the contact sheet is untouched.
    /// Found once per run; respawns on FullRestart (the level is rebuilt).
    public class LostFrame : MonoBehaviour
    {
        bool _found;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_found || other.gameObject.layer != Layers.Player) return;
            _found = true;
            if (PhotoAlbum.Instance != null) PhotoAlbum.Instance.CaptureLost();
            if (GameManager.Instance != null) GameManager.Instance.FoundLostFrame();
            StrokeSparkle.Burst(transform.position, new Color(0.72f, 0.86f, 1f, 1f), 12);
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayDevelop();
            if (HUDController.Instance != null) HUDController.Instance.ShowLostFrame();
            Destroy(gameObject);
        }
    }
}
