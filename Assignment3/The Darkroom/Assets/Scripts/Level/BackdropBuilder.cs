using UnityEngine;

namespace Darkroom
{
    /// The non-colliding backdrop: big illustrated darkroom scenes on the deepest
    /// parallax layer, plus hanging cone lamps with volumetric beams + dust.
    /// The old procedural box decor (shelves / crates / enlargers / wall-patches) was
    /// retired: it was monotonous, and once the scenes were enlarged its dark
    /// rectangles (especially the big WallPatch) occluded them as "black blocks". The
    /// illustrated scenes already depict shelves / enlargers / bottles, so they carry
    /// the darkroom now.
    public static class BackdropBuilder
    {
        public static void Build()
        {
            if (GameObject.Find("_Backdrop") != null) return;
            var root = new GameObject("_Backdrop").transform;
            BuildScenes(root);
            BuildMidground(root);   // hanging darkroom clutter — mid-depth, in front of the
                                    // photos, behind the lamps (whose beams glow over it)
            BuildLamps(root);
            root.gameObject.AddComponent<BackdropTint>(); // background colour per exposure
        }

        /// Illustrated darkroom vignettes on the furthest parallax layer. Each is a
        /// dim, edge-feathered scene that fades into the surrounding black, so they
        /// read as "rooms behind the room". Drawn UNLIT and named "Layer_Scenes" so
        /// BackdropTint colours them per exposure with the rest of the background.
        static void BuildScenes(Transform root)
        {
            // ppu 98 => the ~1774px hero backdrops span ~18 world units (wider than the
            // ~19.5u view), so each scene fills the background rather than sitting as a
            // small distant pocket. Bigger + denser + more opaque = a present darkroom
            // wall behind the play space (the student wanted it more prominent).
            var scenes = PixelArt.BackdropScenes(98f);
            if (scenes.Count == 0) return; // no authored/AI backdrops yet — graceful

            var layerGO = new GameObject("Layer_Scenes");
            layerGO.transform.SetParent(root, false);
            layerGO.AddComponent<ParallaxLayer>().Factor = 0.15f; // calmest, deepest layer
            var rng = new System.Random(101);

            int si = rng.Next(scenes.Count);
            float x = -8f;
            while (x < 156f)
            {
                var sprite = scenes[si % scenes.Count]; si++;
                float scale = 1.0f + (float)rng.NextDouble() * 0.45f;   // bigger (was 0.85–1.4)
                var go = new GameObject("Scene");
                go.transform.SetParent(layerGO.transform, false);
                go.transform.localPosition = new Vector3(x, 4.8f + (float)rng.NextDouble() * 2.2f, 0f);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = VisualFactory.GlowMat; // unlit; BackdropTint recolours per exposure
                sr.color = new Color(0.97f, 0.98f, 1.0f, 0.92f);  // brighter + more opaque (was α0.82)
                sr.sortingOrder = -22;                             // behind every other backdrop layer
                x += 20f + (float)rng.NextDouble() * 16f;          // denser (was 28–52)
            }

            // NOTE: the isolated prologue pocket (x −48..−5) deliberately gets NO deep
            // illustrated scenes. Tiled wide hero backdrops read as pasted, seam-y panels
            // in the sparse prologue (and a generic photo-wall fought the procedural
            // drying roll). The prologue stays a clean silhouette darkroom instead —
            // procedural props (workbench / enlarger / drying roll / trays / safelight)
            // + its own warm lamps (added in BuildLamps) over the dark background.
        }

        /// Hanging cone lamps with light pools — world-fixed (no parallax),
        /// their Light2Ds genuinely light the play space.
        static void BuildLamps(Transform root)
        {
            var lamps = new GameObject("Lamps").transform;
            lamps.SetParent(root, false);
            var rng = new System.Random(77);
            float x = -3f;
            while (x < 174f)
            {
                // hang each lamp a fixed clearance above the LOCAL floor (read from the
                // level data). Lamps used to be pinned at y~10.6, but the camera (ortho
                // 5.5) clamps to the player's height, so in the low early rooms the bulb
                // sat off the top of the frame and only the beam tail showed. Now the
                // source rides ~4-5 units above the floor wherever the floor is.
                Lamp(lamps, x, FloorTopAt(x) + 5.8f + (float)rng.NextDouble() * 1.0f, rng);
                x += 10f + (float)rng.NextDouble() * 5f;
            }
            // the Room 9 corridor gets its own low lamps
            Lamp(lamps, 130f, 1.4f, rng);
            Lamp(lamps, 137f, 1.4f, rng);

            // the isolated prologue pocket (x −48..−5) sits left of the main loop's
            // start, so hang its own warm lamps to key-light the long corridor —
            // explicit (after the loop) so the rest of the level's lamps don't shift.
            Lamp(lamps, -44f, FloorTopAt(-44f) + 5.8f, rng);
            Lamp(lamps, -33f, FloorTopAt(-33f) + 5.8f, rng);
            Lamp(lamps, -22f, FloorTopAt(-22f) + 5.4f, rng);
            Lamp(lamps, -9f,  FloorTopAt(-9f)  + 5.8f, rng);
        }

        /// Approx top-Y of the walkable floor at world x, read from the level data, so
        /// lamps hang a consistent height above the LOCAL floor instead of a fixed high Y.
        /// Skips thin walls (w<3) and high ceilings (top>7); falls back to 0.5.
        static float FloorTopAt(float x)
        {
            float best = float.NegativeInfinity;
            foreach (var room in LevelData.Rooms)
                foreach (var b in room.boxes)
                {
                    if (b.type != ExposureObjectType.StaticGround) continue;
                    if (b.w < 3f) continue;                      // skip thin walls
                    float top = b.cy + b.h * 0.5f;
                    if (top > 7f) continue;                      // skip high ceilings
                    if (x < b.cx - b.w * 0.5f || x > b.cx + b.w * 0.5f) continue;
                    if (top > best) best = top;
                }
            return best > float.NegativeInfinity ? best : 0.5f;
        }

        static void Lamp(Transform parent, float x, float topY, System.Random rng)
        {
            var go = new GameObject("Lamp");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, topY, 0f);

            float cord = 0.8f + (float)rng.NextDouble() * 1.4f;
            var cordSr = Decoration(go.transform, new Vector3(0f, -cord / 2f, 0f),
                new Vector3(0.045f, cord, 1f), VisualFactory.WhiteSprite, new Color(0.08f, 0.08f, 0.09f, 1f), -6);

            var shade = Decoration(go.transform, new Vector3(0f, -cord - 0.55f, 0f),
                Vector3.one, PixelArt.ConeShade, Color.white, -5);

            // HDR-bright bulb (>1) so the bloom pass blooms it into a soft halo
            var bulb = Decoration(go.transform, new Vector3(0f, -cord - 0.52f, 0f),
                new Vector3(0.16f, 0.16f, 1f), PixelArt.Disc, new Color(1.7f, 1.5f, 1.12f, 1f), -4);
            bulb.sharedMaterial = VisualFactory.GlowMat;

            // a clear glowing source at the bulb so the eye reads WHERE the light
            // comes from (HDR>1 so it blooms into a soft warm orb).
            var srcGlow = Decoration(go.transform, new Vector3(0f, -cord - 0.5f, 0f),
                new Vector3(1.05f, 1.05f, 1f), PixelArt.SoftGlow, new Color(1.7f, 1.5f, 1.12f, 0.95f), -5);
            srcGlow.sharedMaterial = VisualFactory.GlowMat;

            // ONE soft volumetric beam (hot at the source, feathered, fading) — replaces
            // the old hard double-cone that read as two disconnected shapes. It fades out
            // well above the floor, so it never "reappears brighter" past a platform/box.
            var beam = Decoration(go.transform, new Vector3(0f, -cord - 0.5f, 0f),
                new Vector3(0.95f, 0.82f, 1f), PixelArt.LightBeam, Color.white, -5);
            beam.sharedMaterial = VisualFactory.GlowMat;

            // dust adrift in the beam — the classic darkroom "light catching motes" cue.
            // Pooled, warm, slow; clearly atmosphere (never reads as interactable).
            var beamDust = new GameObject("BeamDust");
            beamDust.transform.SetParent(go.transform, false);
            beamDust.transform.localPosition = new Vector3(0f, -cord - 1.55f, 0f);
            var drift = beamDust.AddComponent<Drift>();
            drift.count = 5;
            drift.area = new Vector2(1.2f, 2.3f);
            drift.velocity = new Vector2(0.04f, -0.10f);
            drift.size = 0.13f;
            drift.color = new Color(1f, 0.93f, 0.78f, 0.5f);
            drift.sortingOrder = -4;

            LightDirector.CreatePoint(go.transform, new Vector2(0f, -cord - 0.8f),
                new Color(1f, 0.92f, 0.76f), 5.5f, 0.7f);
        }

        // ---------- mid-ground hanging clutter ----------

        const int MidOrder = -10;   // in front of scenes (-22), behind lamps (-6..-4) + gameplay
        // dark silhouette, but bright enough to survive ACES + contrast and read against the
        // backdrop (was 0.12 — too dark/sparse, the clutter was nearly invisible).
        static readonly Color ClutterDark = new Color(0.22f, 0.22f, 0.245f, 1f);

        /// Unlit silhouette part — reuses Decoration's plumbing but swaps to GlowMat so the
        /// clutter is a constant dark value (BackdropTint skips it; the lamp beams composite over it).
        static SpriteRenderer Part(Transform parent, Vector3 localPos, Vector3 scale, Sprite sprite, Color color, int order)
        {
            var sr = Decoration(parent, localPos, scale, sprite, color, order);
            sr.sharedMaterial = VisualFactory.GlowMat;
            return sr;
        }

        /// Hanging darkroom clutter on a mid-depth parallax layer: drying lines of prints +
        /// film negatives, overhead pipes, a red safelight. Constant dark silhouettes (the
        /// root is NOT "Layer_*", so BackdropTint leaves it alone — it stays dark even against
        /// the warm-bright Over wall). Adds depth in the upper frame; never blocks the path.
        static void BuildMidground(Transform root)
        {
            var layer = new GameObject("MidGround");            // no "Layer_" prefix -> not tinted
            layer.transform.SetParent(root, false);
            // Horizontal parallax for depth (0.40) + VERTICAL FOLLOW so the band tracks the
            // camera's Y and stays in the upper frame as the player climbs/drops (it used to be
            // pinned to the world floor and drifted out of view). VerticalOffset = where it hangs.
            var pl = layer.AddComponent<ParallaxLayer>();
            pl.Factor = 0.40f;
            pl.VerticalFollow = 0.85f;     // ~tracks the camera (mild vertical parallax)
            pl.VerticalOffset = 4.5f;      // hang from the upper part of the view
            var rng = new System.Random(313);

            // illustrated hanging clutter (art/mid_*.png) when present; else code silhouettes
            var cutouts = PixelArt.MidgroundClutter(220f);

            float x = -2f;
            while (x < 174f)
            {
                if (rng.NextDouble() < 0.94)
                {
                    float y = -(float)rng.NextDouble() * 1.6f;   // small height variation within the band
                    // prefer an illustrated cutout when we have them (mix in some code
                    // clusters for variety); otherwise fall back to code silhouettes entirely.
                    if (cutouts.Count > 0 && rng.NextDouble() < 0.75)
                        ClutterArt(layer.transform, x, y, cutouts, rng);
                    else
                        ClutterCode(layer.transform, x, y, rng);
                }
                x += 4f + (float)rng.NextDouble() * 3f;          // dense -> ~3 in view at once
            }
        }

        /// Place one illustrated cutout, hung from the band (top-centre pivot -> hangs down).
        /// y is a small local offset (the layer's VerticalFollow keeps the band in the upper frame).
        static void ClutterArt(Transform layer, float x, float y,
            System.Collections.Generic.List<Sprite> cutouts, System.Random rng)
        {
            var sprite = cutouts[rng.Next(cutouts.Count)];
            float targetH = 1.5f + (float)rng.NextDouble() * 1.4f;        // ~1.5-2.9u (smaller -> packs denser)
            float nativeH = sprite.bounds.size.y;
            float scale = nativeH > 0.01f ? targetH / nativeH : 1f;
            var go = new GameObject("ClutterArt");
            go.transform.SetParent(layer, false);
            go.transform.localPosition = new Vector3(x, y, 0f);          // top-pivot hangs down from here
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = VisualFactory.GlowMat;
            sr.color = new Color(0.82f, 0.82f, 0.90f, 1f);               // slightly cool; darkened on import
            sr.sortingOrder = MidOrder;
        }

        /// One code-drawn silhouette cluster (fallback when no cutouts, or variety filler).
        static void ClutterCode(Transform layer, float x, float y, System.Random rng)
        {
            var cluster = new GameObject("Clutter");
            cluster.transform.SetParent(layer, false);
            cluster.transform.localPosition = new Vector3(x, y, 0f); // band Y comes from the layer's VerticalFollow
            double r = rng.NextDouble();
            if (r < 0.50) DryingLine(cluster.transform, rng);
            else if (r < 0.80) PipeRun(cluster.transform, rng);
            else SafelightFixture(cluster.transform, rng);
        }

        /// A thin wire segment between two local points (rotated to connect them).
        static void WireSeg(Transform parent, Vector2 a, Vector2 b, float thick, Color c)
        {
            Vector2 mid = (a + b) * 0.5f;
            float len = (b - a).magnitude;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            var sr = Part(parent, new Vector3(mid.x, mid.y, 0f), new Vector3(len, thick, 1f),
                VisualFactory.WhiteSprite, c, MidOrder);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
        }

        /// A slack drying line: a catenary wire (straight-segment parabola) with hanging
        /// prints and film negatives — the iconic darkroom image, echoing bd_drying_line.png.
        static void DryingLine(Transform parent, System.Random rng)
        {
            float span = 4.5f + (float)rng.NextDouble() * 2.5f;
            float sag = 0.18f + (float)rng.NextDouble() * 0.16f;
            const int segs = 4;
            Vector2 prev = new Vector2(-span / 2f, 0f);
            for (int i = 1; i <= segs; i++)
            {
                float t = i / (float)segs;
                float tx = -span / 2f + t * span;
                float ty = -sag * (1f - Mathf.Pow(2f * t - 1f, 2f)); // parabolic dip
                Vector2 cur = new Vector2(tx, ty);
                WireSeg(parent, prev, cur, 0.03f, ClutterDark);
                prev = cur;
            }
            int n = 3 + rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                float hx = -span / 2f + t * span;
                float hy = -sag * (1f - Mathf.Pow(2f * t - 1f, 2f));
                Part(parent, new Vector3(hx, hy - 0.06f, 0f), new Vector3(0.05f, 0.12f, 1f),
                    VisualFactory.WhiteSprite, ClutterDark, MidOrder);          // clothespin clip
                if (rng.NextDouble() < 0.55)
                {
                    float pw = 0.42f + (float)rng.NextDouble() * 0.18f;
                    float ph = 0.55f + (float)rng.NextDouble() * 0.28f;
                    Part(parent, new Vector3(hx, hy - 0.12f - ph / 2f, 0f), new Vector3(pw, ph, 1f),
                        VisualFactory.WhiteSprite, ClutterDark, MidOrder);      // hanging print
                }
                else
                {
                    // hanging film strip — FilmStripTile is horizontal (1.5x1.0), rotate 90° to
                    // dangle vertically; tint dark so the latent window stays a faint negative.
                    var neg = Part(parent, new Vector3(hx, hy - 0.62f, 0f), new Vector3(0.66f, 0.66f, 1f),
                        ProcGfx.FilmStripTile(false), new Color(0.18f, 0.18f, 0.20f, 1f), MidOrder);
                    neg.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
        }

        /// An overhead pipe run: a horizontal pipe + brackets to the ceiling + an occasional drop.
        static void PipeRun(Transform parent, System.Random rng)
        {
            float len = 5f + (float)rng.NextDouble() * 3f;
            Part(parent, Vector3.zero, new Vector3(len, 0.16f, 1f),
                VisualFactory.WhiteSprite, ClutterDark, MidOrder);             // the pipe
            int br = 2 + rng.Next(2);
            for (int i = 0; i < br; i++)
            {
                float bx = -len / 2f + (i + 0.5f) / br * len;
                Part(parent, new Vector3(bx, 0.18f, 0f), new Vector3(0.08f, 0.30f, 1f),
                    VisualFactory.WhiteSprite, ClutterDark, MidOrder);         // bracket up to ceiling
            }
            if (rng.NextDouble() < 0.5)
            {
                float dx = -len / 2f + (float)rng.NextDouble() * len;
                Part(parent, new Vector3(dx, -0.6f, 0f), new Vector3(0.10f, 1.2f, 1f),
                    VisualFactory.WhiteSprite, ClutterDark, MidOrder);         // elbow drop / conduit
            }
        }

        /// A hanging safelight: cord + housing + a faint RED glow (value < 1 so it does NOT
        /// bloom — a dim wall accent, not a real light) — the darkroom-red note beside the warm lamps.
        static void SafelightFixture(Transform parent, System.Random rng)
        {
            Part(parent, new Vector3(0f, -0.18f, 0f), new Vector3(0.045f, 0.36f, 1f),
                VisualFactory.WhiteSprite, ClutterDark, MidOrder);             // cord
            Part(parent, new Vector3(0f, -0.46f, 0f), new Vector3(0.55f, 0.30f, 1f),
                VisualFactory.WhiteSprite, ClutterDark, MidOrder);             // housing
            Part(parent, new Vector3(0f, -0.58f, 0f), new Vector3(0.95f, 0.62f, 1f),
                PixelArt.SoftGlow, new Color(0.62f, 0.12f, 0.10f, 0.5f), MidOrder); // dim red glow
            if (rng.NextDouble() < 0.5)
                Part(parent, new Vector3(0.7f, -0.5f, 0f), new Vector3(0.4f, 0.4f, 1f),
                    PixelArt.Disc, ClutterDark, MidOrder);                     // hanging reel
        }

        static SpriteRenderer Decoration(Transform parent, Vector3 localPos, Vector3 scale, Sprite sprite, Color color, int order)
        {
            var go = new GameObject("Part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = VisualFactory.SpriteMat;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
