using UnityEngine;

namespace Darkroom
{
    /// Soft silhouette characters (concept-art style, not pixel art):
    /// a small girl in a dress with glowing eyes, and shadow-blob creatures.
    /// Drawn into bilinear textures from simple shapes.
    public static class SilhouetteArt
    {
        // ---------- tiny canvas ----------

        class Buf
        {
            public readonly int W, H;
            public readonly Color32[] Px;

            public Buf(int w, int h) { W = w; H = h; Px = new Color32[w * h]; }

            public void FillRect(float x0, float y0, float x1, float y1, Color32 c)
            {
                for (int y = Mathf.Max(0, (int)y0); y < Mathf.Min(H, (int)y1); y++)
                    for (int x = Mathf.Max(0, (int)x0); x < Mathf.Min(W, (int)x1); x++)
                        Px[y * W + x] = c;
            }

            public void FillEllipse(float cx, float cy, float rx, float ry, Color32 c)
            {
                for (int y = Mathf.Max(0, (int)(cy - ry)); y <= Mathf.Min(H - 1, (int)(cy + ry)); y++)
                {
                    float dy = (y - cy) / ry;
                    float span = rx * Mathf.Sqrt(Mathf.Max(0f, 1f - dy * dy));
                    for (int x = Mathf.Max(0, (int)(cx - span)); x <= Mathf.Min(W - 1, (int)(cx + span)); x++)
                        Px[y * W + x] = c;
                }
            }

            /// Vertical trapezoid: half-width lerps from hw0 (at y0) to hw1 (at y1).
            public void FillTaper(float cx, float y0, float y1, float hw0, float hw1, Color32 c)
            {
                for (int y = Mathf.Max(0, (int)y0); y < Mathf.Min(H, (int)y1); y++)
                {
                    float k = (y - y0) / Mathf.Max(1f, y1 - y0);
                    float hw = Mathf.Lerp(hw0, hw1, k);
                    for (int x = Mathf.Max(0, (int)(cx - hw)); x <= Mathf.Min(W - 1, (int)(cx + hw)); x++)
                        Px[y * W + x] = c;
                }
            }

            /// Faint rim light along the left silhouette edge.
            public void RimLeft(Color32 rim)
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        if (Px[y * W + x].a > 0)
                        {
                            Px[y * W + x] = rim;
                            break;
                        }
            }

            public Sprite ToSprite(string name, float ppu)
            {
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.SetPixels32(Px);
                tex.Apply();
                var s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
                s.name = name;
                return s;
            }
        }

        static Color32 C(int rgb) =>
            new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 255);

        static readonly Color32 Body = new Color32(0x0A, 0x0A, 0x0C, 0xFF);
        static readonly Color32 Rim = new Color32(0x2A, 0x2A, 0x30, 0xFF);
        static readonly Color32 Eye = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

        // ---------- the girl (42x78 @ 60ppu = 0.7 x 1.3) ----------

        static Sprite _pIdle, _pWalkA, _pWalkB, _pJump;

        public static Sprite PlayerIdle { get { EnsurePlayer(); return _pIdle; } }
        public static Sprite PlayerWalkA { get { EnsurePlayer(); return _pWalkA; } }
        public static Sprite PlayerWalkB { get { EnsurePlayer(); return _pWalkB; } }
        public static Sprite PlayerJump { get { EnsurePlayer(); return _pJump; } }

        static void EnsurePlayer()
        {
            if (_pIdle != null) return;
            _pIdle = Girl("GirlIdle", 17f, 24f, 0f, 13f);
            _pWalkA = Girl("GirlWalkA", 12f, 29f, 0f, 14f);
            _pWalkB = Girl("GirlWalkB", 19f, 22f, 0f, 13f);
            _pJump = Girl("GirlJump", 16f, 25f, 10f, 16f);
        }

        /// legL/legR = leg center x; legLift = raised feet (jump tuck); hemHW = dress hem half-width.
        static Sprite Girl(string name, float legL, float legR, float legLift, float hemHW)
        {
            var b = new Buf(42, 78);

            // legs + shoes
            float footY = legLift;
            b.FillRect(legL - 2f, footY, legL + 2f, footY + 20f, Body);
            b.FillRect(legR - 2f, footY, legR + 2f, footY + 20f, Body);
            b.FillRect(legL - 3f, footY, legL + 3f, footY + 3f, Body);
            b.FillRect(legR - 3f, footY, legR + 3f, footY + 3f, Body);

            // A-line dress: hem -> waist
            b.FillTaper(21f, 16f + legLift * 0.4f, 46f, hemHW, 6f, Body);
            // torso + shoulders
            b.FillTaper(21f, 46f, 55f, 6f, 5f, Body);
            // arms close to the body
            b.FillRect(14f, 28f, 17f, 50f, Body);
            b.FillRect(25f, 28f, 28f, 50f, Body);

            // head + bun (faces right; SpriteRenderer.flipX mirrors)
            b.FillEllipse(21f, 64f, 8.5f, 9f, Body);
            b.FillEllipse(13f, 69f, 4.5f, 4.5f, Body);

            b.RimLeft(Rim);

            // glowing eye
            b.FillRect(24f, 62f, 27f, 65f, Eye);

            return b.ToSprite(name, 60f);
        }

        // ---------- shadow-blob enemy (48x48 @ 60ppu = 0.8) ----------

        static Sprite _eAsleep, _eAwake, _eCrackle;

        public static Sprite EnemyAsleep { get { EnsureEnemy(); return _eAsleep; } }
        public static Sprite EnemyAwake { get { EnsureEnemy(); return _eAwake; } }
        public static Sprite EnemyCrackle { get { EnsureEnemy(); return _eCrackle; } }

        static void EnsureEnemy()
        {
            if (_eAsleep != null) return;
            _eAsleep = Blob("ShadeAsleep", C(0x2E2E32), C(0x1A1A1E), false);
            _eAwake = Blob("ShadeAwake", C(0x6E1414), C(0xFFC4C4), true);
            _eCrackle = Blob("ShadeCrackle", C(0x55555A), C(0x3A3A3E), false);
        }

        static Sprite Blob(string name, Color32 body, Color32 eye, bool eyesOpen)
        {
            var b = new Buf(48, 48);
            // soft-edged squashed disc
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 48; x++)
                {
                    float dx = (x - 23.5f) / 22f;
                    float dy = (y - 22f) / 20f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r < 1f)
                    {
                        byte a = (byte)(Mathf.Clamp01((1f - r) * 8f) * 255f);
                        b.Px[y * 48 + x] = new Color32(body.r, body.g, body.b, a);
                    }
                }

            if (eyesOpen)
            {
                b.FillEllipse(15f, 27f, 3.5f, 4f, eye);
                b.FillEllipse(32f, 27f, 3.5f, 4f, eye);
                b.FillRect(14f, 26f, 17f, 29f, Eye);
                b.FillRect(31f, 26f, 34f, 29f, Eye);
            }
            else
            {
                b.FillRect(11f, 26f, 19f, 28f, eye);
                b.FillRect(28f, 26f, 36f, 28f, eye);
            }

            return b.ToSprite(name, 60f);
        }
    }
}
