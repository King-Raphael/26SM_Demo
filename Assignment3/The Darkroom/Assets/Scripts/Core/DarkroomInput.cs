using UnityEngine.InputSystem;

namespace Darkroom
{
    /// All keyboard input goes through here (new Input System only;
    /// activeInputHandler=1 so legacy UnityEngine.Input is unavailable).
    /// Read *Pressed properties only from Update(), never FixedUpdate().
    public static class DarkroomInput
    {
        static Keyboard K => Keyboard.current;

        public static float MoveAxis
        {
            get
            {
                var k = K;
                if (k == null) return 0f;
                float x = 0f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
                return x;
            }
        }

        public static bool JumpPressed => K != null && K.spaceKey.wasPressedThisFrame;
        public static bool JumpHeld => K != null && K.spaceKey.isPressed;   // variable jump (level, safe in FixedUpdate)
        public static bool Set1Pressed => K != null && K.digit1Key.wasPressedThisFrame;
        public static bool Set2Pressed => K != null && K.digit2Key.wasPressedThisFrame;
        public static bool Set3Pressed => K != null && K.digit3Key.wasPressedThisFrame;
        // held state for hold-to-preview (1/2/3): 0 = none, else the digit held
        public static int ExposureDigitHeld
        {
            get
            {
                var k = K;
                if (k == null) return 0;
                if (k.digit1Key.isPressed) return 1;
                if (k.digit2Key.isPressed) return 2;
                if (k.digit3Key.isPressed) return 3;
                return 0;
            }
        }
        public static int ExposureDigitReleased
        {
            get
            {
                var k = K;
                if (k == null) return 0;
                if (k.digit1Key.wasReleasedThisFrame) return 1;
                if (k.digit2Key.wasReleasedThisFrame) return 2;
                if (k.digit3Key.wasReleasedThisFrame) return 3;
                return 0;
            }
        }
        public static bool CycleForwardPressed => K != null && K.eKey.wasPressedThisFrame;
        public static bool CycleBackPressed => K != null && K.qKey.wasPressedThisFrame;
        public static bool RestartPressed => K != null && K.rKey.wasPressedThisFrame;
        public static bool PausePressed => K != null && K.escapeKey.wasPressedThisFrame;
        public static bool MutePressed => K != null && K.mKey.wasPressedThisFrame;

        // DEV room warp ( [ previous / ] next, P = mechanic lab ) — remove before final build
        public static bool WarpPrevPressed => K != null && K.leftBracketKey.wasPressedThisFrame;
        public static bool WarpNextPressed => K != null && K.rightBracketKey.wasPressedThisFrame;
        public static bool LabWarpPressed => K != null && K.pKey.wasPressedThisFrame;

        public static bool DrawHeld
        {
            get
            {
                var k = K;
                return k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed || k.lKey.isPressed);
            }
        }
    }
}
