using UnityEngine;

namespace Darkroom
{
    /// Layer indices must match ProjectSettings/TagManager.asset (layers 6-9).
    public static class Layers
    {
        public const int World = 6;
        public const int Strokes = 7;
        public const int Player = 8;
        public const int Triggers = 9;

        /// Ground check mask: World | Strokes only (never Triggers).
        public static int GroundMask => (1 << World) | (1 << Strokes);

        public static bool Validate()
        {
            bool ok = LayerMask.NameToLayer("World") == World
                   && LayerMask.NameToLayer("Strokes") == Strokes
                   && LayerMask.NameToLayer("Player") == Player
                   && LayerMask.NameToLayer("Triggers") == Triggers;
            if (!ok)
                Debug.LogError("[Darkroom] Layer mismatch: ProjectSettings/TagManager.asset must define layers 6=World 7=Strokes 8=Player 9=Triggers.");
            return ok;
        }
    }
}
