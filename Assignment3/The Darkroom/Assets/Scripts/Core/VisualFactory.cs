using UnityEngine;

namespace Darkroom
{
    /// Shared sprite, material, palette (spec section 9) and sorting orders.
    /// All renderers must use SpriteMat explicitly (URP: default materials render pink).
    public static class VisualFactory
    {
        static Sprite _white;
        static Material _mat;

        /// 4x4 white texture at 4 PPU => sprite is exactly 1x1 world unit.
        public static Sprite WhiteSprite
        {
            get
            {
                if (_white == null)
                {
                    var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var px = new Color32[16];
                    for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                    tex.SetPixels32(px);
                    tex.filterMode = FilterMode.Point;
                    tex.Apply();
                    _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                    _white.name = "DarkroomWhite";
                }
                return _white;
            }
        }

        public static Material SpriteMat
        {
            get
            {
                if (_mat == null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (sh == null) sh = Shader.Find("Sprites/Default");
                    _mat = new Material(sh) { name = "DarkroomSprite" };
                }
                return _mat;
            }
        }

        static Color Hex(int v) =>
            new Color(((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f, 1f);

        // Palette (spec section 9)
        public static readonly Color Background     = Hex(0x121212);
        public static readonly Color StaticGround   = Hex(0x6E6E6E);
        public static readonly Color DarkPath       = Hex(0x3A4A8C);
        public static readonly Color BrightBarrier  = Hex(0xEDEDED);
        public static readonly Color DarkStroke     = Hex(0x9FD8E6);
        public static readonly Color BrightStroke   = Hex(0xFFF3D6);
        public static readonly Color PlayerColor    = Hex(0xF2F2F2);
        public static readonly Color EnemyAsleep    = Hex(0x444444);
        public static readonly Color EnemyAwake     = Hex(0x8B1A1A);
        public static readonly Color SensorInactive = Hex(0x555555);
        public static readonly Color SensorActive   = Hex(0xFFF3D6);
        public static readonly Color ExitRed        = Hex(0x8B1A1A);
        public static readonly Color PickupColor    = Hex(0xFFF3D6);
        public static readonly Color SafelightRed   = Hex(0x8B1A1A);
        public static readonly Color DoorClosed     = Hex(0x5E5E5E);

        // Sorting orders (back to front)
        public const int OrderExit     = 12;
        public const int OrderDoor     = 15;
        public const int OrderExposure = 10;  // DarkPath / BrightBarrier
        public const int OrderGround   = 20;
        public const int OrderSensor   = 22;
        public const int OrderStroke   = 30;
        public const int OrderEnemy    = 40;
        public const int OrderPickup   = 45;
        public const int OrderPlayer   = 50;

        public static Color ColorFor(ExposureObjectType t)
        {
            switch (t)
            {
                case ExposureObjectType.DarkPath:      return DarkPath;
                case ExposureObjectType.BrightBarrier: return BrightBarrier;
                case ExposureObjectType.DarkStroke:    return DarkStroke;
                case ExposureObjectType.BrightStroke:  return BrightStroke;
                default:                               return StaticGround;
            }
        }

        public static int OrderFor(ExposureObjectType t)
        {
            switch (t)
            {
                case ExposureObjectType.StaticGround: return OrderGround;
                case ExposureObjectType.DarkStroke:
                case ExposureObjectType.BrightStroke: return OrderStroke;
                default:                              return OrderExposure;
            }
        }
    }
}
