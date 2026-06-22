using UnityEngine;

namespace Darkroom
{
    /// Soft silhouette characters (concept-art style, not pixel art):
    /// a small girl in a dress with glowing eyes, and shadow-blob creatures.
    /// Drawn into bilinear textures from simple shapes.
    public static class SilhouetteArt
    {
        // ---------- tiny canvas ----------

        /// Shapes are rasterised into a supersampled (SSx) buffer in logical
        /// coordinates, then box-downsampled — so hard binary-alpha fills come out
        /// with anti-aliased edges (no staircase) instead of blocky silhouettes.
        class Buf
        {
            const int SS = 4;
            public readonly int W, H, PW, PH;
            public readonly Color32[] Px; // physical (supersampled) RGBA

            public Buf(int w, int h) { W = w; H = h; PW = w * SS; PH = h * SS; Px = new Color32[PW * PH]; }

            public void FillRect(float x0, float y0, float x1, float y1, Color32 c)
            {
                x0 *= SS; y0 *= SS; x1 *= SS; y1 *= SS;
                for (int y = Mathf.Max(0, (int)y0); y < Mathf.Min(PH, (int)y1); y++)
                    for (int x = Mathf.Max(0, (int)x0); x < Mathf.Min(PW, (int)x1); x++)
                        Px[y * PW + x] = c;
            }

            public void FillEllipse(float cx, float cy, float rx, float ry, Color32 c)
            {
                cx *= SS; cy *= SS; rx *= SS; ry *= SS;
                for (int y = Mathf.Max(0, (int)(cy - ry)); y <= Mathf.Min(PH - 1, (int)(cy + ry)); y++)
                {
                    float dy = (y - cy) / ry;
                    float span = rx * Mathf.Sqrt(Mathf.Max(0f, 1f - dy * dy));
                    for (int x = Mathf.Max(0, (int)(cx - span)); x <= Mathf.Min(PW - 1, (int)(cx + span)); x++)
                        Px[y * PW + x] = c;
                }
            }

            /// Vertical trapezoid: half-width lerps from hw0 (at y0) to hw1 (at y1).
            public void FillTaper(float cx, float y0, float y1, float hw0, float hw1, Color32 c)
            {
                cx *= SS; y0 *= SS; y1 *= SS; hw0 *= SS; hw1 *= SS;
                for (int y = Mathf.Max(0, (int)y0); y < Mathf.Min(PH, (int)y1); y++)
                {
                    float k = (y - y0) / Mathf.Max(1f, y1 - y0);
                    float hw = Mathf.Lerp(hw0, hw1, k);
                    for (int x = Mathf.Max(0, (int)(cx - hw)); x <= Mathf.Min(PW - 1, (int)(cx + hw)); x++)
                        Px[y * PW + x] = c;
                }
            }

            /// Soft radial fill for gradients (the enemy aura, the blob body):
            /// pixels with r&gt;=1 (or below yMin) are left untouched.
            public void Radial(float cx, float cy, float rx, float ry, float yMin, Color32 rgb, System.Func<float, float> alphaByR)
            {
                cx *= SS; cy *= SS; rx *= SS; ry *= SS; yMin *= SS;
                for (int y = Mathf.Max(0, (int)yMin); y < PH; y++)
                    for (int x = 0; x < PW; x++)
                    {
                        float dx = (x - cx) / rx, dy = (y - cy) / ry;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        if (r >= 1f) continue;
                        float a = Mathf.Clamp01(alphaByR(r));
                        if (a <= 0f) continue;
                        Px[y * PW + x] = new Color32(rgb.r, rgb.g, rgb.b, (byte)(a * 255f));
                    }
            }

            /// Soft rim along the left silhouette edge — the first `width` logical
            /// pixels of each row become a soft sub-pixel rim once downsampled.
            public void RimLeft(Color32 rim, int width = 1)
            {
                int w = Mathf.Max(1, width * SS);
                for (int y = 0; y < PH; y++)
                {
                    int found = -1;
                    for (int x = 0; x < PW; x++)
                        if (Px[y * PW + x].a > 0) { found = x; break; }
                    if (found < 0) continue;
                    for (int x = found; x < Mathf.Min(PW, found + w); x++)
                        Px[y * PW + x] = rim;
                }
            }

            public Sprite ToSprite(string name, float ppu)
            {
                // alpha-weighted box downsample SS×SS -> 1 (no dark edge fringes)
                var outPx = new Color32[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float r = 0f, g = 0f, b = 0f, a = 0f;
                        for (int sy = 0; sy < SS; sy++)
                            for (int sx = 0; sx < SS; sx++)
                            {
                                var p = Px[(y * SS + sy) * PW + (x * SS + sx)];
                                float pa = p.a / 255f;
                                r += p.r * pa; g += p.g * pa; b += p.b * pa; a += pa;
                            }
                        if (a < 0.0001f) { outPx[y * W + x] = new Color32(0, 0, 0, 0); continue; }
                        byte oa = (byte)(a / (SS * SS) * 255f);
                        outPx[y * W + x] = new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), oa);
                    }
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.SetPixels32(outPx);
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

        static Sprite _pIdle, _pWalkA, _pWalkB, _pJump, _pShoot, _pBlank;

        public static Sprite PlayerIdle { get { EnsurePlayer(); return _pIdle; } }
        public static Sprite PlayerWalkA { get { EnsurePlayer(); return _pWalkA; } }
        public static Sprite PlayerWalkB { get { EnsurePlayer(); return _pWalkB; } }
        public static Sprite PlayerJump { get { EnsurePlayer(); return _pJump; } }
        /// Finale pose: the camera raised to her eye.
        public static Sprite PlayerShoot { get { EnsurePlayer(); return _pShoot; } }
        /// The unprinted self: the idle silhouette with no eye — a blank face,
        /// the latent image that develops on the paper in the prologue cinematic.
        public static Sprite PlayerBlank { get { EnsurePlayer(); return _pBlank; } }

        static void EnsurePlayer()
        {
            if (_pIdle != null) return;
            _pIdle = Girl("GirlIdle", 17f, 24f, 0f, 13f);
            _pWalkA = Girl("GirlWalkA", 12f, 29f, 0f, 14f);
            _pWalkB = Girl("GirlWalkB", 19f, 22f, 0f, 13f);
            _pJump = Girl("GirlJump", 16f, 25f, 10f, 16f);
            _pShoot = GirlShoot();
            _pBlank = Girl("GirlBlank", 17f, 24f, 0f, 13f, false); // idle pose, faceless
        }

        /// Idle stance, one arm raised, the camera at her eye. The lens
        /// glint replaces the glowing eye — she sees through it now.
        static Sprite GirlShoot()
        {
            var b = new Buf(42, 78);

            // legs + shoes (idle stance)
            b.FillRect(15f, 0f, 19f, 20f, Body);
            b.FillRect(22f, 0f, 26f, 20f, Body);
            b.FillRect(14f, 0f, 20f, 3f, Body);
            b.FillRect(21f, 0f, 27f, 3f, Body);

            // A-line dress + torso (as idle)
            b.FillTaper(21f, 16f, 46f, 13f, 6f, Body);
            b.FillTaper(21f, 46f, 55f, 6f, 5f, Body);

            // left arm down, right arm raised toward the face
            b.FillRect(14f, 28f, 17f, 50f, Body);
            b.FillTaper(28.5f, 44f, 60f, 2f, 2f, Body);

            // head + bun
            b.FillEllipse(21f, 64f, 8.5f, 9f, Body);
            b.FillEllipse(13f, 69f, 4.5f, 4.5f, Body);

            // the camera, held to her eye
            b.FillRect(24f, 57f, 37f, 66f, Body);

            b.RimLeft(Rim, 3);

            // viewfinder/lens glint where the glowing eye used to be
            b.FillRect(29f, 60f, 32f, 63f, Eye);

            return b.ToSprite("GirlShoot", 60f);
        }

        /// legL/legR = leg center x; legLift = raised feet (jump tuck); hemHW = dress hem half-width.
        /// eye = draw the glowing eye (false leaves the face blank — the unprinted self).
        static Sprite Girl(string name, float legL, float legR, float legLift, float hemHW, bool eye = true)
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

            b.RimLeft(Rim, 3);

            // glowing eye (omitted for the blank, faceless self)
            if (eye) b.FillRect(24f, 62f, 27f, 65f, Eye);

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

            // awake: dim red aura behind the creature
            if (eyesOpen)
                b.Radial(23.5f, 21f, 24f, 23f, 0f, new Color32(0x70, 0x12, 0x12, 255),
                    r => Mathf.Pow(1f - r, 2f) * (110f / 255f));

            // crouched shade: squashed disc with a soft edge (flat-ish bottom)
            b.Radial(23.5f, 19f, 21f, 17f, 4f, body, r => Mathf.Clamp01((1f - r) * 8f));

            // little horn nubs
            b.FillTaper(14f, 33f, 43f, 3.5f, 0.8f, body);
            b.FillTaper(33f, 33f, 43f, 3.5f, 0.8f, body);

            if (eyesOpen)
            {
                b.FillEllipse(16f, 23f, 4f, 4.5f, eye);
                b.FillEllipse(31f, 23f, 4f, 4.5f, eye);
                b.FillRect(15f, 22f, 18f, 25f, Eye);
                b.FillRect(30f, 22f, 33f, 25f, Eye);
            }
            else
            {
                b.FillRect(12f, 22f, 20f, 24f, eye);
                b.FillRect(27f, 22f, 35f, 24f, eye);
            }

            return b.ToSprite(name, 60f);
        }
    }
}
