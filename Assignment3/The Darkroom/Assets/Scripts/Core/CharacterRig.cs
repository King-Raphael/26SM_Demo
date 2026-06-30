using UnityEngine;

namespace Darkroom
{
    /// Loads an authored Unity 2D-bone (SpriteSkin) rig prefab from
    /// Resources/Characters/&lt;name&gt; and returns a fresh instance, or null when none
    /// exists — callers fall back to the code-drawn puppet. Same graceful-fallback spirit
    /// as PixelArt's external-art loaders: the game always runs, and the rig "slots in"
    /// the moment the prefab lands in Resources.
    ///
    /// Authoring the rig is an in-editor step (it can't be done headlessly): import a
    /// layered silhouette sprite, place bones + auto-weights in the Skinning Editor, and
    /// build a prefab with SpriteSkin + an Animator (idle/work clip). Save it as e.g.
    /// Assets/Resources/Characters/Worker.prefab and figures upgrade with no code change.
    public static class CharacterRig
    {
        public static GameObject Load(string name)
        {
            var prefab = Resources.Load<GameObject>("Characters/" + name);
            return prefab != null ? Object.Instantiate(prefab) : null;
        }
    }
}
