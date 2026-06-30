using UnityEngine;

namespace Darkroom
{
    /// Tags a root spawned by the edit-mode scene-preview baker (DarkroomSceneBaker) so it
    /// can be found and cleared. Pure marker, no behaviour. Preview roots are also
    /// HideFlags.DontSave, so they never serialise into the scene and are dropped on a
    /// domain reload — the preview is meant to be rebuilt, not saved.
    public class DarkroomPreviewMarker : MonoBehaviour { }
}
