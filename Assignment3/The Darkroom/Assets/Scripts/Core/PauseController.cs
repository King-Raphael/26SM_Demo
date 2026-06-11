using UnityEngine;

namespace Darkroom
{
    /// Esc = pause (freeze time + audio, overlay with a controls recap),
    /// M = mute. A post-DoD completeness addition, documented in README.
    public class PauseController : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }

        bool _muted;

        void OnDestroy()
        {
            // never leave a frozen timescale behind (domain reload off etc.)
            if (IsPaused) { Time.timeScale = 1f; AudioListener.pause = false; IsPaused = false; }
        }

        void Update()
        {
            var gm = GameManager.Instance;

            if (DarkroomInput.MutePressed)
            {
                _muted = !_muted;
                AudioListener.volume = _muted ? 0f : 1f;
                if (HUDController.Instance != null) HUDController.Instance.SetMuted(_muted);
            }

            if (!DarkroomInput.PausePressed) return;
            if (gm != null && gm.HasWon) return; // win screen owns the input

            IsPaused = !IsPaused;
            Time.timeScale = IsPaused ? 0f : 1f;
            AudioListener.pause = IsPaused;
            if (HUDController.Instance != null) HUDController.Instance.ShowPause(IsPaused);
        }
    }
}
