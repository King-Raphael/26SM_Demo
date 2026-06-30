using UnityEngine;

namespace Darkroom
{
    /// Shared procedural-art toolkit. `Canvas` is the supersampled, anti-aliased
    /// shape rasteriser promoted out of SilhouetteArt (shapes are drawn in logical
    /// coordinates into an SSx buffer, then alpha-weighted box-downsampled so hard
    /// fills come out with smooth edges). Element texture generators live here too,
    /// cached as static sprites (one texture shared by every instance).
    public static class ProcGfx
    {
        public static Color32 C(int rgb) =>
            new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 255);

        public static Color32 C(int rgb, byte a) =>
            new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), a);

        /// Supersampled AA drawing canvas.
        public sealed class Canvas
        {
            readonly int SS;
            public readonly int W, H, PW, PH;
            public readonly Color32[] Px; // physical (supersampled) RGBA

            public Canvas(int w, int h, int ss = 4) { W = w; H = h; SS = ss; PW = w * ss; PH = h * ss; Px = new Color32[PW * PH]; }

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

            public void FillCircle(float cx, float cy, float r, Color32 c) => FillEllipse(cx, cy, r, r, c);

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

            /// Soft radial fill for gradients (auras, char scars, latent blobs):
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

            /// Thick line (capsule) between two logical points, half-width halfW.
            public void StrokeLine(float x0, float y0, float x1, float y1, float halfW, Color32 c)
            {
                x0 *= SS; y0 *= SS; x1 *= SS; y1 *= SS; halfW *= SS;
                int minX = Mathf.Max(0, (int)(Mathf.Min(x0, x1) - halfW - 1));
                int maxX = Mathf.Min(PW - 1, (int)(Mathf.Max(x0, x1) + halfW + 1));
                int minY = Mathf.Max(0, (int)(Mathf.Min(y0, y1) - halfW - 1));
                int maxY = Mathf.Min(PH - 1, (int)(Mathf.Max(y0, y1) + halfW + 1));
                float dx = x1 - x0, dy = y1 - y0;
                float len2 = dx * dx + dy * dy;
                float hw2 = halfW * halfW;
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        float t = len2 < 1e-4f ? 0f : Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / len2);
                        float px = x0 + t * dx, py = y0 + t * dy;
                        float d2 = (x - px) * (x - px) + (y - py) * (y - py);
                        if (d2 <= hw2) Px[y * PW + x] = c;
                    }
            }

            /// Scanline polygon fill (logical-space points).
            public void FillPolygon(Vector2[] pts, Color32 c)
            {
                int n = pts.Length;
                if (n < 3) return;
                float minYf = pts[0].y, maxYf = pts[0].y;
                for (int i = 1; i < n; i++) { minYf = Mathf.Min(minYf, pts[i].y); maxYf = Mathf.Max(maxYf, pts[i].y); }
                int y0 = Mathf.Max(0, (int)(minYf * SS)), y1 = Mathf.Min(PH - 1, (int)(maxYf * SS));
                var xs = new System.Collections.Generic.List<float>(n);
                for (int y = y0; y <= y1; y++)
                {
                    float fy = (y + 0.5f) / SS;
                    xs.Clear();
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        float yi = pts[i].y, yj = pts[j].y;
                        if ((yi <= fy && yj > fy) || (yj <= fy && yi > fy))
                            xs.Add(pts[i].x + (fy - yi) / (yj - yi) * (pts[j].x - pts[i].x));
                    }
                    xs.Sort();
                    for (int k = 0; k + 1 < xs.Count; k += 2)
                    {
                        int xa = Mathf.Max(0, (int)(xs[k] * SS)), xb = Mathf.Min(PW - 1, (int)(xs[k + 1] * SS));
                        for (int x = xa; x <= xb; x++) Px[y * PW + x] = c;
                    }
                }
            }

            /// Soft rim along the LEFT silhouette edge (first `width` logical px/row).
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

            /// Rim on ALL silhouette edges: opaque pixels adjacent (within width) to
            /// a transparent pixel become the rim colour.
            public void RimAll(Color32 rim, int width = 1)
            {
                int w = Mathf.Max(1, width * SS);
                var src = (Color32[])Px.Clone();
                for (int y = 0; y < PH; y++)
                    for (int x = 0; x < PW; x++)
                    {
                        if (src[y * PW + x].a == 0) continue;
                        bool edge = false;
                        for (int oy = -w; oy <= w && !edge; oy++)
                            for (int ox = -w; ox <= w; ox++)
                            {
                                int nx = x + ox, ny = y + oy;
                                if (nx < 0 || ny < 0 || nx >= PW || ny >= PH || src[ny * PW + nx].a == 0) { edge = true; break; }
                            }
                        if (edge) Px[y * PW + x] = rim;
                    }
            }

            /// Add deterministic grain into a rectangular region (latent emulsion).
            public void Grain(float x0, float y0, float x1, float y1, Color32 c, int seed, float amount)
            {
                x0 *= SS; y0 *= SS; x1 *= SS; y1 *= SS;
                for (int y = Mathf.Max(0, (int)y0); y < Mathf.Min(PH, (int)y1); y++)
                    for (int x = Mathf.Max(0, (int)x0); x < Mathf.Min(PW, (int)x1); x++)
                    {
                        float n = PixelArt.Hash(x, y, seed);
                        if (n > 1f - amount)
                            Px[y * PW + x] = new Color32(c.r, c.g, c.b, (byte)(c.a * (0.4f + n * 0.6f)));
                    }
            }

            public Sprite ToSprite(string name, float ppu) => ToSprite(name, ppu, new Vector2(0.5f, 0.5f));

            public Sprite ToSprite(string name, float ppu, Vector2 pivot)
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
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.SetPixels32(outPx);
                tex.Apply();
                var s = Sprite.Create(tex, new Rect(0, 0, W, H), pivot, ppu, 0, SpriteMeshType.FullRect);
                s.name = name;
                return s;
            }
        }

        // ---------- cached element textures (one shared sprite per type) ----------

        static Sprite _filmCold, _filmWarm;

        /// A strip of film: dark sprocket rails (with punched holes) top and bottom,
        /// a translucent latent window between. Drawn squarish, stretched to the
        /// element box. Cold = DarkPath (Under), warm = LightBridge (Over).
        public static Sprite FilmStripTile(bool warm)
        {
            if (warm) { if (_filmWarm == null) _filmWarm = BuildFilmStrip(true); return _filmWarm; }
            if (_filmCold == null) _filmCold = BuildFilmStrip(false);
            return _filmCold;
        }

        static Sprite BuildFilmStrip(bool warm)
        {
            const int W = 96, H = 64;
            var cv = new Canvas(W, H);
            // bold rails + a tinted latent window so the strip reads even when small
            Color32 rail = warm ? C(0x231F18) : C(0x10131C);
            Color32 win = warm ? C(0xEAD9A8, 150) : C(0x8FB0DC, 150);
            Color32 winGrain = warm ? C(0xFFF3D6, 170) : C(0xCFE2F6, 170);
            cv.FillRect(0, 17, W, 47, win);                  // latent window (centre)
            cv.Grain(2, 19, W - 2, 45, winGrain, 61, 0.16f); // emulsion grain
            cv.FillRect(0, 0, W, 17, rail);                  // bottom rail (bold)
            cv.FillRect(0, 47, W, H, rail);                  // top rail (bold)
            for (int x = 4; x < W; x += 18)                  // big sprocket holes
            {
                cv.FillRect(x, 4, x + 11, 13, new Color32(0, 0, 0, 0));
                cv.FillRect(x, 51, x + 11, 60, new Color32(0, 0, 0, 0));
            }
            return cv.ToSprite(warm ? "FilmStripWarm" : "FilmStripCold", H); // ~1 unit tall base
        }

        static Sprite _photoPaper;

        /// Fibrous off-white photo paper (tiled). BurnPaper tints it warm as it heats.
        public static Sprite PhotoPaperTile
        {
            get
            {
                if (_photoPaper == null)
                {
                    const int N = 64;
                    var cv = new Canvas(N, N);
                    cv.FillRect(0, 0, N, N, C(0xEDE9DE));
                    cv.Grain(0, 0, N, N, C(0xD6CFBE, 110), 71, 0.30f);  // paper fibre
                    cv.Grain(0, 0, N, N, C(0xFBF7EC, 90), 113, 0.10f);  // brighter flecks
                    _photoPaper = cv.ToSprite("PhotoPaper", N);          // 1×1 unit, tiled by sr.size
                }
                return _photoPaper;
            }
        }

        static Sprite[] _charScars;
        static Sprite[] _charRims;
        static Sprite[] _sootStains;
        const int CharVariants = 6;

        /// A charred burn-through, variant `v`: NOT a clean disc but a multi-lobe
        /// scorch with FBM-ragged edges and a warm ember rim. Several off-centre soot
        /// lobes union (darkest-wins) into an irregular silhouette; every seed gives a
        /// different ragged shape, so BurnWall can scatter distinct ones per spot.
        /// Built lazily and cached (one texture per variant, shared by all instances).
        public static Sprite CharScar(int v)
        {
            if (_charScars == null) _charScars = new Sprite[CharVariants];
            int i = ((v % CharVariants) + CharVariants) % CharVariants;
            if (_charScars[i] == null) _charScars[i] = BuildCharScar(1000 + i * 7);
            return _charScars[i];
        }

        /// A thin ragged charred RIM for variant `v` — the same multi-lobe silhouette
        /// outlined and hollowed, so the post-burn wall reads as a singed edge curling
        /// around emptiness rather than a solid dark fill.
        public static Sprite CharRim(int v)
        {
            if (_charRims == null) _charRims = new Sprite[CharVariants];
            int i = ((v % CharVariants) + CharVariants) % CharVariants;
            if (_charRims[i] == null) _charRims[i] = BuildCharRim(1000 + i * 7);
            return _charRims[i];
        }

        /// A SOFT charred soot smudge for variant `v` — the ragged multi-lobe silhouette
        /// rendered as a feathered stain (dense at the cores, fading to nothing at the
        /// edge), no crisp outline. The post-burn remnant: scorch residue, not a drawn ring.
        public static Sprite SootStain(int v)
        {
            if (_sootStains == null) _sootStains = new Sprite[CharVariants];
            int i = ((v % CharVariants) + CharVariants) % CharVariants;
            if (_sootStains[i] == null) _sootStains[i] = BuildSootStain(1000 + i * 7);
            return _sootStains[i];
        }

        // Per-pixel "inside the ragged multi-lobe scorch" test, shared by scar + rim.
        // `depth` (out) is the normalised distance-from-edge (0 at rim, ->1 at a lobe
        // core), used to darken the soot inward. Returns false outside every lobe.
        static bool CharField(float x, float y, float[] lcx, float[] lcy, float[] lr, int[] ls, int nL, out float depth)
        {
            bool hit = false; depth = 0f;
            for (int l = 0; l < nL; l++)
            {
                float dx = x - lcx[l], dy = y - lcy[l];
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > lr[l] * 1.7f) continue;
                float t = Mathf.Atan2(dy, dx);
                // value noise sampled around a unit circle => seamless ragged coastline
                float n = PixelArt.ValueNoise(Mathf.Cos(t) * 2.5f + l, Mathf.Sin(t) * 2.5f + l, ls[l]) * 0.6f
                        + PixelArt.ValueNoise(Mathf.Cos(t) * 5f, Mathf.Sin(t) * 5f, ls[l] + 9) * 0.4f;
                float rEdge = lr[l] * (0.62f + 0.55f * n);
                if (d < rEdge) { hit = true; depth = Mathf.Max(depth, 1f - d / rEdge); }
            }
            return hit;
        }

        // Lay out 3-4 off-centre overlapping lobes for a 64-canvas scar (deterministic
        // per seed). Returned arrays feed CharField.
        static void CharLobes(int seed, out float[] lcx, out float[] lcy, out float[] lr, out int[] ls, out int nL)
        {
            var rng = new System.Random(seed);
            nL = 3 + rng.Next(0, 2);                       // 3-4 lobes
            lcx = new float[nL]; lcy = new float[nL]; lr = new float[nL]; ls = new int[nL];
            for (int l = 0; l < nL; l++)
            {
                lcx[l] = 24f + (float)rng.NextDouble() * 16f;   // off-centre, overlapping mid
                lcy[l] = 24f + (float)rng.NextDouble() * 16f;
                lr[l] = 12f + (float)rng.NextDouble() * 8f;
                ls[l] = seed + l * 31;
            }
        }

        static Sprite BuildCharScar(int seed)
        {
            const int N = 64;
            var cv = new Canvas(N, N);
            float ss = cv.PW / (float)N;                   // physical-per-logical (SS)
            CharLobes(seed, out var lcx, out var lcy, out var lr, out var ls, out int nL);

            Color32 soot = C(0x16100C), inner = C(0x0E0907);
            for (int py = 0; py < cv.PH; py++)
                for (int px = 0; px < cv.PW; px++)
                {
                    float x = px / ss, y = py / ss;
                    if (CharField(x, y, lcx, lcy, lr, ls, nL, out float depth))
                        cv.Px[py * cv.PW + px] = Color32.Lerp(soot, inner, depth * 0.7f);
                }

            // charcoal speckle — ONLY inside the scorch (a full-rect Grain would tint the
            // transparent background too, leaving a faint speckled SQUARE behind each scar).
            // Keep the existing alpha so the soot stays solid (no see-through holes).
            Color32 spec = C(0x251A12);
            for (int py = 0; py < cv.PH; py++)
                for (int px = 0; px < cv.PW; px++)
                {
                    int idx = py * cv.PW + px;
                    if (cv.Px[idx].a == 0) continue;                       // leave background fully clear
                    if (PixelArt.Hash(px, py, seed + 3) > 1f - 0.28f)
                        cv.Px[idx] = new Color32(spec.r, spec.g, spec.b, cv.Px[idx].a);
                }

            // ember rim: a soot pixel touching transparency becomes warm amber (the
            // established 0x7A3A14 / 0x9A4A18 ember tones — no red).
            var src = (Color32[])cv.Px.Clone();
            int PW = cv.PW, PH = cv.PH;
            Color32 ember = C(0x7A3A14), hot = C(0x9A4A18);
            for (int py = 1; py < PH - 1; py++)
                for (int px = 1; px < PW - 1; px++)
                {
                    if (src[py * PW + px].a == 0) continue;
                    bool edge = src[py * PW + px - 1].a == 0 || src[py * PW + px + 1].a == 0
                             || src[(py - 1) * PW + px].a == 0 || src[(py + 1) * PW + px].a == 0;
                    if (edge) cv.Px[py * PW + px] = (PixelArt.Hash(px, py, seed) > 0.5f) ? hot : ember;
                }

            return cv.ToSprite("CharScar" + seed, N);
        }

        static Sprite BuildCharRim(int seed)
        {
            const int N = 64;
            var cv = new Canvas(N, N);
            float ss = cv.PW / (float)N;
            CharLobes(seed, out var lcx, out var lcy, out var lr, out var ls, out int nL);

            // fill the ragged silhouette solid, then keep only the edge band.
            Color32 fill = C(0x241712);
            for (int py = 0; py < cv.PH; py++)
                for (int px = 0; px < cv.PW; px++)
                {
                    float x = px / ss, y = py / ss;
                    if (CharField(x, y, lcx, lcy, lr, ls, nL, out _))
                        cv.Px[py * cv.PW + px] = fill;
                }

            // hollow it: an opaque pixel keeps a charred-rim colour only if it sits
            // within `w` of transparency; everything deeper is erased -> ragged outline.
            int PW = cv.PW, PH = cv.PH, w = Mathf.Max(1, (int)ss);
            var src = (Color32[])cv.Px.Clone();
            Color32 rim = C(0x2E1C12), ember = C(0x7A3A14);
            for (int py = 0; py < PH; py++)
                for (int px = 0; px < PW; px++)
                {
                    if (src[py * PW + px].a == 0) { cv.Px[py * PW + px] = new Color32(0, 0, 0, 0); continue; }
                    bool edge = false;
                    for (int oy = -w; oy <= w && !edge; oy++)
                        for (int ox = -w; ox <= w; ox++)
                        {
                            int nx = px + ox, ny = py + oy;
                            if (nx < 0 || ny < 0 || nx >= PW || ny >= PH || src[ny * PW + nx].a == 0) { edge = true; break; }
                        }
                    cv.Px[py * PW + px] = edge
                        ? ((PixelArt.Hash(px, py, seed + 5) > 0.78f) ? ember : rim)   // mostly soot, a few embers
                        : new Color32(0, 0, 0, 0);
                }

            return cv.ToSprite("CharRim" + seed, N);
        }

        static Sprite BuildSootStain(int seed)
        {
            const int N = 64;
            var cv = new Canvas(N, N);
            float ss = cv.PW / (float)N;
            CharLobes(seed, out var lcx, out var lcy, out var lr, out var ls, out int nL);

            Color32 soot = C(0x140E0A);
            for (int py = 0; py < cv.PH; py++)
                for (int px = 0; px < cv.PW; px++)
                {
                    float x = px / ss, y = py / ss;
                    if (CharField(x, y, lcx, lcy, lr, ls, nL, out float depth))
                    {
                        // soft: dense at the lobe cores, feathering to nothing at the ragged
                        // edge — a stain, not an outline. (depth: 0 at edge, ->1 at core.)
                        byte a = (byte)(Mathf.Clamp01(depth * 1.25f) * 205f);
                        cv.Px[py * cv.PW + px] = new Color32(soot.r, soot.g, soot.b, a);
                    }
                }
            return cv.ToSprite("SootStain" + seed, N);
        }

        static Sprite[] _emulsion;

        /// Roiling undeveloped emulsion — violet turbulence. Cycle the frames with
        /// FrameCycle for a "boiling shadow" look. Tiled (1×1 base).
        public static Sprite[] EmulsionFrames
        {
            get
            {
                if (_emulsion == null)
                {
                    _emulsion = new Sprite[3];
                    for (int i = 0; i < 3; i++)
                    {
                        const int N = 48;
                        var cv = new Canvas(N, N);
                        cv.FillRect(0, 0, N, N, C(0x271B3A, 205));
                        cv.Grain(0, 0, N, N, C(0x5C3F8A, 185), 200 + i * 37, 0.36f);  // bright roil
                        cv.Grain(0, 0, N, N, C(0x120A22, 165), 400 + i * 53, 0.30f);  // dark roil
                        _emulsion[i] = cv.ToSprite("Emulsion" + i, N);
                    }
                }
                return _emulsion;
            }
        }

        static Texture2D _softBeam;

        /// Feather texture for LineRenderers: white, fully opaque at the centre and
        /// fading to transparent across the line width (V), so a drawn stroke reads
        /// as a soft light beam instead of a hard translucent bar. Mapped via
        /// VisualFactory.BeamMat with LineTextureMode.Stretch.
        public static Texture2D SoftBeamTex
        {
            get
            {
                if (_softBeam == null)
                {
                    const int W = 4, H = 64;
                    var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                    var px = new Color32[W * H];
                    for (int y = 0; y < H; y++)
                    {
                        float v = (y + 0.5f) / H * 2f - 1f;             // -1..1 across width
                        float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v)), 1.6f);
                        for (int x = 0; x < W; x++) px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    _softBeam = tex;
                }
                return _softBeam;
            }
        }

        static Sprite _grainTile;

        /// Dense cool latent grain — a veil over a not-yet-developed platform.
        public static Sprite GrainTile
        {
            get
            {
                if (_grainTile == null)
                {
                    const int N = 48;
                    var cv = new Canvas(N, N);
                    cv.Grain(0, 0, N, N, C(0xAEC4E6, 205), 91, 0.55f);
                    _grainTile = cv.ToSprite("GrainTile", N);
                }
                return _grainTile;
            }
        }
    }
}
