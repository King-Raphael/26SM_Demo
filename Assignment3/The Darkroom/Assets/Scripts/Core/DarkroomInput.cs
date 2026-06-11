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
        public static bool Set1Pressed => K != null && K.digit1Key.wasPressedThisFrame;
        public static bool Set2Pressed => K != null && K.digit2Key.wasPressedThisFrame;
        public static bool Set3Pressed => K != null && K.digit3Key.wasPressedThisFrame;
        public static bool CycleForwardPressed => K != null && K.eKey.wasPressedThisFrame;
        public static bool CycleBackPressed => K != null && K.qKey.wasPressedThisFrame;
        public static bool RestartPressed => K != null && K.rKey.wasPressedThisFrame;
        public static bool PausePressed => K != null && K.escapeKey.wasPressedThisFrame;
        public static bool MutePressed => K != null && K.mKey.wasPressedThisFrame;

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
