using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkroom
{
    /// Zero-wiring entry point: builds managers, camera, HUD, level and player
    /// at play start in whatever scene is open. Guarded by the GameManager
    /// existence check (safe with domain reload disabled).
    public static class Bootstrap
    {
        /// Full game: 10 (set to 2 for the rooms 0-2 demo build).
        public const int BuildThroughRoomCount = 10;

        // The prologue is an isolated pocket far to the left of Frame 1 (which
        // starts at x 5.5). She walks a long corridor here; the paper-door then
        // TELEPORTS her across the wide empty gap into Frame 1 — so the opening
        // never reveals the path ahead, and the cut reads as entering elsewhere.
        public static readonly Vector2 SpawnPos = new Vector2(-46f, 1.5f);

        // Camera bounds while in the prologue pocket: low enough to follow her in,
        // capped on the right so Frame 1 (x 5.5) is never on screen at the door.
        public const float PrologueCamMinX = -40f, PrologueCamMaxX = -4.5f;
        public const float LevelCamMinX = -2f, LevelCamMaxX = 170f;
        public const float CamMinY = -1f, CamMaxY = 9f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<GameManager>() != null) return;

            // Safety: an edit-mode scene preview (DarkroomSceneBaker) can leak into Play if
            // it wasn't cleared and Domain Reload is off. Its objects are forced-visible and
            // NOT exposure-synced, so a leftover set reads as "bridges show outside Under" and
            // "burned walls still block". Destroy any preview roots BEFORE building so the
            // runtime scene is never doubled. (No-op in real builds — no markers exist.)
            // A leftover build root in the open scene — a stale object, or an editor
            // preview (DarkroomSceneBaker) whose marker was lost across a recompile —
            // makes Boot SKIP BackdropBuilder (backdrop "gone") and DOUBLE the level
            // (ghost bridges that show outside Under, walls that still block after
            // burning). Boot always builds these fresh, so destroy any pre-existing
            // copies first — by marker AND by name (no-op in real builds).
            foreach (var m in Object.FindObjectsByType<DarkroomPreviewMarker>(FindObjectsInactive.Include))
                if (m != null) Object.DestroyImmediate(m.gameObject);
            foreach (var n in new[] { "_Backdrop", "_Level", "_HUD" })
                for (var s = GameObject.Find(n); s != null; s = GameObject.Find(n))
                    Object.DestroyImmediate(s);

            Layers.Validate();
            // Strokes collide with Player only; everything else stays default.
            for (int i = 0; i < 32; i++)
                Physics2D.IgnoreLayerCollision(Layers.Strokes, i, i != Layers.Player);

#if UNITY_WEBGL && !UNITY_EDITOR
            // Paint the scene camera near-black IMMEDIATELY: the preload below delays
            // BuildWorld (which sets the real clear colour), and the template camera's
            // default sky-blue clear would otherwise fill the canvas for the whole
            // fetch — the "blue screen" before the darkroom fades in.
            var bootCam = Camera.main;
            if (bootCam != null)
            {
                bootCam.clearFlags = CameraClearFlags.SolidColor;
                bootCam.backgroundColor = VisualFactory.Background;
            }

            // WebGL has no filesystem: preload StreamingAssets (manifest.txt + art)
            // ASYNCHRONOUSLY before building, so the synchronous art getters find
            // their bytes ready. A tiny host runs the coroutine; the screen stays on
            // Unity's loading bar until it finishes.
            var host = new GameObject("_BootPreloader").AddComponent<BootPreloader>();
            host.StartCoroutine(PreloadThenBuild(host.gameObject));
#else
            BuildWorld();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        static System.Collections.IEnumerator PreloadThenBuild(GameObject host)
        {
            yield return PixelArt.PreloadWebAssets();
            BuildWorld();
            Object.Destroy(host);
        }
#endif

        /// Managers → HUD → backdrop → level → player. Runs immediately on desktop;
        /// on WebGL it runs once the StreamingAssets preload has completed.
        static void BuildWorld()
        {
            var managers = new GameObject("_Managers");
            var gm = managers.AddComponent<GameManager>();
            managers.AddComponent<ExposureManager>();
            managers.AddComponent<AudioDirector>();
            managers.AddComponent<LightDirector>();
            managers.AddComponent<PostFXDirector>();
            managers.AddComponent<LightField>();
            managers.AddComponent<PauseController>();
            managers.AddComponent<PhotoAlbum>();
            managers.AddComponent<DarkroomPostDriver>(); // feeds the Darkroom/Post fullscreen shader globals

            HUDController.Build();
            BackdropBuilder.Build();
            LevelBuilder.Build(BuildThroughRoomCount);

            var player = PlayerController.Create(SpawnPos);
            gm.Player = player;
            gm.InitCheckpoint(SpawnPos);
            gm.GrantNegativeSilently(); // the safelight is on from the first frame (the prologue)

            SetupCamera(player.transform);
        }

        static void SetupCamera(Transform target)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                go.transform.position = new Vector3(SpawnPos.x, SpawnPos.y, -10f);
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 5.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = VisualFactory.Background;
            cam.allowHDR = true; // bloom needs an HDR buffer to bleed into

            // Turn the dormant URP post stack on for this camera and add post AA —
            // PostFXDirector then drives the global Volume per exposure state.
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;

            if (Object.FindAnyObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.Target = target;
            // fence the camera to the prologue pocket; the tint still tracks the
            // full journey (TintMin/Max default to the real-level span)
            follow.SetBounds(PrologueCamMinX, PrologueCamMaxX, CamMinY, CamMaxY);
            follow.Snap();

            if (cam.GetComponent<DustMotes>() == null)
                cam.gameObject.AddComponent<DustMotes>();
            if (cam.GetComponent<VaporMotes>() == null)
                cam.gameObject.AddComponent<VaporMotes>(); // faint rising chemical haze (W4 atmosphere)
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// Minimal coroutine host for the WebGL StreamingAssets preload at boot.
    public class BootPreloader : MonoBehaviour { }
#endif
}
