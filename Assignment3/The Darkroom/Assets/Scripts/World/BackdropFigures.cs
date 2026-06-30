using UnityEngine;

namespace Darkroom
{
    /// A few SPARSE, distant silhouette figures on a deep parallax band — her photo
    /// subjects, at work or watching, never near the play plane. Each is either an authored
    /// SpriteSkin rig prefab (Resources/Characters/*) or a code-drawn puppet (a body + a
    /// swinging arm) animated by RigActor. Faceless per the art bible; only the watcher
    /// carries a faint white eye-glint (never red). Non-colliding scenery — they never
    /// block or kill. Lives under "Figures" (NO "Layer_" prefix) so BackdropTint leaves
    /// the colour alone but still fades the band out in the bright Over world.
    public static class BackdropFigures
    {
        public enum Kind { Worker, Watcher }

        struct Spec
        {
            public float x, y, parallax, scale;
            public Kind kind;
            public bool faceLeft;
            public Spec(float x, float y, float parallax, float scale, Kind kind, bool faceLeft)
            { this.x = x; this.y = y; this.parallax = parallax; this.scale = scale; this.kind = kind; this.faceLeft = faceLeft; }
        }

        // Deliberately sparse & on-theme: NO figure in the prologue (frame 1 is "she walks
        // the dark alone" — the separation rite), and none in the R9 corridor (it already
        // stages her three shades). Deep parallax (0.34-0.42), behind the play space.
        static readonly Spec[] Figures =
        {
            new Spec( 20f, 0.30f, 0.40f, 1.30f, Kind.Worker,  true),
            new Spec( 68f, 0.20f, 0.34f, 1.40f, Kind.Watcher, true),
            new Spec(112f, 0.20f, 0.42f, 1.30f, Kind.Watcher, false),
        };

        public static void Build(Transform root)
        {
            var parent = new GameObject("Figures").transform; // no "Layer_" -> not tinted, only Over-faded
            parent.SetParent(root, false);
            foreach (var s in Figures) BuildOne(parent, s);
        }

        static void BuildOne(Transform parent, Spec s)
        {
            var go = new GameObject("Figure_" + s.kind);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(s.x, s.y, 0f);
            go.AddComponent<ParallaxLayer>().Factor = s.parallax; // deep; stands on the world floor (no vertical follow)

            // a Visual child carries the scale + facing flip, leaving the figure root purely
            // for position/parallax (ParallaxLayer writes position, never scale).
            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            visual.localScale = new Vector3(s.faceLeft ? -s.scale : s.scale, s.scale, 1f);

            var actor = go.AddComponent<RigActor>();

            // prefer an authored SpriteSkin rig; otherwise build the procedural puppet
            var rig = CharacterRig.Load(s.kind.ToString());
            if (rig != null)
            {
                rig.transform.SetParent(visual, false);
                foreach (var sr in rig.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.sharedMaterial = VisualFactory.SpriteMat;
                    sr.sortingOrder = VisualFactory.OrderFigure;
                }
                actor.SetRigged(); // the prefab's Animator owns the motion
                return;
            }

            // next: a flat silhouette cutout (art/char_<kind>.png) when dropped in
            var cut = PixelArt.FigureCutout(s.kind.ToString(), 100f);
            if (cut != null)
            {
                BuildCutout(visual, s, actor, cut);
                return;
            }

            BuildPuppet(visual, s, actor);
        }

        /// A dropped-in PNG silhouette: scaled to the figure's world height (bottom-pivot
        /// keeps the feet on the line), gently bobbed. The arm is part of the cutout, so
        /// there is no separate swinging joint.
        static void BuildCutout(Transform visual, Spec s, RigActor actor, Sprite cut)
        {
            var bodyGO = new GameObject("Body");
            bodyGO.transform.SetParent(visual, false);
            float nativeH = cut.bounds.size.y;
            float k = nativeH > 0.01f ? 1.33f / nativeH : 1f; // pre-Visual-scale height ~ the puppet's
            bodyGO.transform.localScale = new Vector3(k, k, 1f);
            var bsr = bodyGO.AddComponent<SpriteRenderer>();
            bsr.sprite = cut;
            bsr.sharedMaterial = VisualFactory.SpriteMat; // baked near-black; the lamps lift the rim
            bsr.sortingOrder = VisualFactory.OrderFigure;
            actor.body = bodyGO.transform;
            actor.speed = s.kind == Kind.Watcher ? 0.8f : 1.1f;
            actor.bobAmp = 0.02f;
        }

        static void BuildPuppet(Transform visual, Spec s, RigActor actor)
        {
            // body: a lit silhouette (the safelights add a faint rim), standing on its feet
            // line via the bottom-centre pivot baked into the figure sprites.
            var bodyGO = new GameObject("Body");
            bodyGO.transform.SetParent(visual, false);
            var bsr = bodyGO.AddComponent<SpriteRenderer>();
            bsr.sprite = s.kind == Kind.Worker ? SilhouetteArt.FigureWorker : SilhouetteArt.FigureWatcher;
            bsr.sharedMaterial = VisualFactory.SpriteMat;
            bsr.color = new Color(0.16f, 0.16f, 0.18f, 1f); // near-black; the lamps lift the rim
            bsr.sortingOrder = VisualFactory.OrderFigure;
            actor.body = bodyGO.transform;

            if (s.kind == Kind.Worker)
            {
                // a working arm that swings from the shoulder — the "rigged" motion
                var joint = new GameObject("ArmJoint").transform;
                joint.SetParent(visual, false);
                joint.localPosition = new Vector3(0.12f, 1.03f, 0f); // shoulder, working side
                var armGO = new GameObject("Arm");
                armGO.transform.SetParent(joint, false);
                var asr = armGO.AddComponent<SpriteRenderer>();
                asr.sprite = SilhouetteArt.FigureArm;
                asr.sharedMaterial = VisualFactory.SpriteMat;
                asr.color = bsr.color;
                asr.sortingOrder = VisualFactory.OrderFigure + 1; // in front of the body
                actor.armJoint = joint;
                actor.armBaseDeg = 28f; // reaching forward/down toward the work
                actor.armAmpDeg = 17f;  // the working stroke
                actor.speed = 1.4f;
            }
            else
            {
                // watcher: a faint white eye-glint (never red); faded with the band in Over
                for (int e = 0; e < 2; e++)
                {
                    var eye = new GameObject("Glint" + e);
                    eye.transform.SetParent(visual, false);
                    eye.transform.localPosition = new Vector3(e == 0 ? -0.10f : 0.10f, 0.74f, 0f);
                    eye.transform.localScale = new Vector3(0.07f, 0.045f, 1f);
                    var esr = eye.AddComponent<SpriteRenderer>();
                    esr.sprite = VisualFactory.WhiteSprite;
                    esr.sharedMaterial = VisualFactory.GlowMat;
                    esr.color = new Color(1f, 1f, 1f, 0.5f);
                    esr.sortingOrder = VisualFactory.OrderFigure + 1;
                }
                actor.speed = 0.8f;
                actor.bobAmp = 0.015f; // a slow breathing sway
            }
        }
    }
}
