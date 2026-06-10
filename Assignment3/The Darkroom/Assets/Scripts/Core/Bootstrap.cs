using UnityEngine;

namespace Darkroom
{
    /// Zero-wiring entry point: builds managers, camera, HUD, level and player
    /// at play start in whatever scene is open. Guarded by the GameManager
    /// existence check (safe with domain reload disabled).
    public static class Bootstrap
    {
        /// Demo milestone: rooms 0-2. Full game: 10.
        public const int BuildThroughRoomCount = 2;

        public static readonly Vector2 SpawnPos = new Vector2(-3f, 1.5f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<GameManager>() != null) return;

            Layers.Validate();
            // Strokes collide with Player only; everything else stays default.
            for (int i = 0; i < 32; i++)
                Physics2D.IgnoreLayerCollision(Layers.Strokes, i, i != Layers.Player);

            var managers = new GameObject("_Managers");
            var gm = managers.AddComponent<GameManager>();
            managers.AddComponent<ExposureManager>();

            HUDController.Build();
            LevelBuilder.Build(BuildThroughRoomCount);

            var player = PlayerController.Create(SpawnPos);
            gm.Player = player;
            gm.InitCheckpoint(SpawnPos);

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

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.Target = target;
            follow.Snap();
        }
    }
}
