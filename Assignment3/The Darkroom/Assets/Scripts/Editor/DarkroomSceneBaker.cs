using System.Text;
using UnityEditor;
using UnityEngine;

namespace Darkroom
{
    /// Builds the runtime-generated world (backdrop + level, INCLUDING the exposure-gated
    /// DarkPath/BrightBarrier and the latent "develops-solid" FixPlatform slabs) into the
    /// OPEN scene in EDIT MODE — so you can SEE and DRAG every element without pressing Play.
    ///
    /// It is a re-buildable PREVIEW, not a saved scene: the procedural sprites are generated
    /// in memory and the roots are HideFlags.DontSave, so a recompile / scene reopen / Play
    /// clears it — just run "Build Scene In Editor" again. This keeps the project's
    /// runtime-procedural, zero-asset design intact.
    ///
    /// To make a tuned position PERMANENT in the actual game, run "Log Element Positions":
    /// the figures print ready-to-paste `Spec(...)` lines for BackdropFigures.Figures[], and
    /// the current selection prints name + world pos + scale to fold back into the builders.
    [InitializeOnLoad]
    public static class DarkroomSceneBaker
    {
        // gated/latent platforms sit at alpha 0 (DarkPath in Normal) or 0.16 (latent slab)
        // in edit mode (no ExposureManager); lift them so they can be seen and positioned.
        const float PreviewAlpha = 0.85f;

        static DarkroomSceneBaker()
        {
            // entering Play rebuilds the world from code (Bootstrap); drop the edit-mode
            // preview first so the scene is never doubled (covers domain-reload-disabled too).
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.ExitingEditMode) Clear();
            };
        }

        [MenuItem("Darkroom/Build Scene In Editor")]
        public static void Build()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[DarkroomSceneBaker] Stop Play mode first.");
                return;
            }
            Clear(); // idempotent: never stack two previews

            BackdropBuilder.Build();
            var backdrop = GameObject.Find("_Backdrop");
            var level = LevelBuilder.Build(Bootstrap.BuildThroughRoomCount);

            Mark(backdrop);
            Mark(level);
            if (level != null) ForceVisible(level);

            Selection.activeGameObject = backdrop != null ? backdrop : level;
            Debug.Log("[DarkroomSceneBaker] Built edit-mode preview (backdrop + level, gated/latent " +
                      "platforms forced visible). Drag to tune; run Darkroom ▸ Log Element Positions to " +
                      "capture. It clears automatically on Play or recompile — rebuild any time.");
        }

        // build roots created by the baker / Bootstrap; cleared by name as well as by
        // marker, since a marker can be lost across a recompile and leave stale roots that
        // make Play skip the backdrop and double the level.
        static readonly string[] BuildRoots = { "_Backdrop", "_Level", "_HUD", "_Managers", "Player", "_Dust" };

        [MenuItem("Darkroom/Clear Generated Scene")]
        public static void Clear()
        {
            foreach (var m in Object.FindObjectsByType<DarkroomPreviewMarker>(FindObjectsInactive.Include))
                if (m != null) Object.DestroyImmediate(m.gameObject);
            foreach (var n in BuildRoots)
                for (var s = GameObject.Find(n); s != null; s = GameObject.Find(n))
                    Object.DestroyImmediate(s);
        }

        [MenuItem("Darkroom/Log Element Positions")]
        public static void LogPositions()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Darkroom layout capture");

            foreach (var m in Object.FindObjectsByType<DarkroomPreviewMarker>(FindObjectsInactive.Include))
            {
                var figs = m.transform.Find("Figures");
                if (figs == null) continue;
                sb.AppendLine("\n// paste into BackdropFigures.Figures[]:");
                foreach (Transform f in figs)
                {
                    var pl = f.GetComponent<ParallaxLayer>();
                    var visual = f.Find("Visual");
                    float scale = visual != null ? Mathf.Abs(visual.localScale.y) : 1f;
                    bool faceLeft = visual != null && visual.localScale.x < 0f;
                    string kind = f.name.Replace("Figure_", "");
                    float factor = pl != null ? pl.Factor : 0.3f;
                    sb.AppendLine(string.Format(
                        "    new Spec({0,6:0.##}f, {1,5:0.##}f, {2:0.##}f, {3:0.##}f, Kind.{4}, {5}),",
                        f.localPosition.x, f.localPosition.y, factor, scale, kind,
                        faceLeft ? "true" : "false"));
                }
            }

            if (Selection.gameObjects.Length > 0)
            {
                sb.AppendLine("\n// selected elements (name  worldPos  localScale):");
                foreach (var go in Selection.gameObjects)
                    sb.AppendLine(string.Format("{0,-22} pos=({1:0.##}, {2:0.##})  scale=({3:0.##}, {4:0.##})",
                        go.name, go.transform.position.x, go.transform.position.y,
                        go.transform.localScale.x, go.transform.localScale.y));
            }
            else
            {
                sb.AppendLine("\n// (select objects in the Hierarchy to capture their exact pos/scale)");
            }

            Debug.Log("[DarkroomSceneBaker] " + sb);
            try
            {
                string path = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, "..", "art-pipeline", "_layout_dump.txt"));
                System.IO.File.WriteAllText(path, sb.ToString());
                Debug.Log("[DarkroomSceneBaker] wrote " + path);
            }
            catch (System.Exception e) { Debug.LogWarning("[DarkroomSceneBaker] dump write failed: " + e.Message); }
        }

        static void Mark(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponent<DarkroomPreviewMarker>() == null)
                go.AddComponent<DarkroomPreviewMarker>();
            go.hideFlags |= HideFlags.DontSave; // never serialise the preview into the scene
        }

        /// Lift the gated/latent platforms (and their visual children) to a visible alpha so
        /// they can be seen and positioned in edit mode, where the exposure system is inert.
        static void ForceVisible(GameObject root)
        {
            foreach (var eo in root.GetComponentsInChildren<ExposureObject>(true)) Bump(eo.gameObject);
            foreach (var fp in root.GetComponentsInChildren<FixPlatform>(true)) Bump(fp.gameObject);
        }

        static void Bump(GameObject go)
        {
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.color.a < PreviewAlpha) { var c = sr.color; c.a = PreviewAlpha; sr.color = c; }
            foreach (var lr in go.GetComponentsInChildren<LineRenderer>(true))
                if (lr.startColor.a < PreviewAlpha)
                { var c = lr.startColor; c.a = PreviewAlpha; lr.startColor = c; lr.endColor = c; }
        }
    }
}
