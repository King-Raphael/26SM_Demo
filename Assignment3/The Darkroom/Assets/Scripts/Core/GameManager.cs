using System;
using System.Collections;
using UnityEngine;

namespace Darkroom
{
    /// What burned the print (drives the post-respawn margin note).
    public enum DeathCause { Fall, Enemy, Restart }

    /// Abilities, checkpoint, respawn, win state, and global key handling (1/2/3, E/Q, R).
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public bool HasFlash { get; private set; }
        public bool HasShutter { get; private set; }
        /// The "negative" unlocks Underexposed (first ability gate in the tutorial chain).
        public bool HasNegative { get; private set; }
        public bool IsRespawning { get; private set; }
        public bool HasWon { get; private set; }
        /// True during the finale: input ignored, RunTime frozen.
        public bool IsCinematic { get; private set; }
        /// Set on the first win; survives FullRestart. Unlocks the replay timer HUD.
        public bool HasEverWon { get; private set; }
        /// Every burned print this run — the win caption calls the run "take N+1".
        public int Deaths { get; private set; }
        public float RunTime { get; private set; }
        public Vector2 CheckpointPos { get; private set; }
        public PlayerController Player { get; set; }

        /// Fired at the moment of respawn / full restart (TrailSystem clears strokes here).
        public event Action OnRespawn;

        public const float KillY = -10f;

        /// DEV: room warp via [ and ] for testing (grants all abilities and
        /// jumps to a room's first checkpoint). Set false before a final build.
        public const bool DevWarpEnabled = true;

        void Awake() { Instance = this; }

        void Update()
        {
            if (Player == null) return;
            if (PauseController.IsPaused) return;
            if (!HasWon && !IsCinematic) RunTime += Time.deltaTime;

            if (HasWon)
            {
                if (DarkroomInput.RestartPressed) FullRestart();
                return;
            }
            if (IsRespawning || IsCinematic) return;

            var em = ExposureManager.Instance;
            if (DarkroomInput.Set1Pressed) em.TrySetExposure(Exposure.Underexposed);
            else if (DarkroomInput.Set2Pressed) em.TrySetExposure(Exposure.Normal);
            else if (DarkroomInput.Set3Pressed) em.TrySetExposure(Exposure.Overexposed);
            else if (DarkroomInput.CycleForwardPressed) em.Cycle(1);
            else if (DarkroomInput.CycleBackPressed) em.Cycle(-1);

            if (DevWarpEnabled)
            {
                int cur = LevelData.RoomIndexAt(Player.transform.position.x);
                if (DarkroomInput.WarpNextPressed) { WarpToRoom(cur + 1); return; }
                if (DarkroomInput.WarpPrevPressed) { WarpToRoom(cur - 1); return; }
                if (DarkroomInput.LabWarpPressed) { WarpToLab(); return; }
            }

            if (DarkroomInput.RestartPressed) { Kill(DeathCause.Restart); return; }
            if (Player.transform.position.y < KillY) Kill(DeathCause.Fall);
        }

        public void InitCheckpoint(Vector2 p) { CheckpointPos = p; }

        public void SetCheckpoint(Vector2 p, string caption = "")
        {
            if ((p - CheckpointPos).sqrMagnitude < 0.0001f) return;
            CheckpointPos = p;
            if (HUDController.Instance != null) HUDController.Instance.CheckpointFlash(caption);
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayCheckpoint();
        }

        // ---------- DEV: room warp (testing only) ----------

        /// Jump straight to a room: grants all abilities, lands on its first
        /// checkpoint, clears live strokes and resets exposure. [ prev / ] next.
        public void WarpToRoom(int room)
        {
            if (Player == null || HasWon || IsCinematic || IsRespawning) return;
            int maxRoom = Mathf.Min(LevelData.Rooms.Length - 1, Bootstrap.BuildThroughRoomCount);
            room = Mathf.Clamp(room, 0, maxRoom);

            HasNegative = true; HasFlash = true; HasShutter = true;
            OnRespawn?.Invoke(); // clear live strokes / transient light state

            Vector2 pos = RoomWarpPos(room);
            InitCheckpoint(pos);
            Player.Teleport(pos);
            if (ExposureManager.Instance != null) ExposureManager.Instance.ForceSet(Exposure.Normal);
            SnapCamera(-2f, 170f, -1f, 9f); // restore the real-level bounds

            var hud = HUDController.Instance;
            if (hud != null)
            {
                hud.RefreshAbilityHud();
                hud.ShowBanner("DEV WARP → FRAME " + (room + 1) + " : " + LevelData.Rooms[room].title);
            }
        }

        static Vector2 RoomWarpPos(int room)
        {
            var cps = LevelData.Rooms[room].checkpoints;
            if (cps.Length > 0) return new Vector2(cps[0].cx, cps[0].cy);
            return new Vector2(LevelData.RoomStarts[room] + 1f, 2f);
        }

        /// Jump to the dev mechanic sandbox (built only when DevWarpEnabled).
        public void WarpToLab()
        {
            if (Player == null || HasWon || IsCinematic || IsRespawning) return;
            HasNegative = true; HasFlash = true; HasShutter = true;
            OnRespawn?.Invoke();
            Vector2 pos = new Vector2(394f, 4.5f);
            InitCheckpoint(pos);
            Player.Teleport(pos);
            if (ExposureManager.Instance != null) ExposureManager.Instance.ForceSet(Exposure.Normal);
            SnapCamera(-2f, 452f, -1f, 12f); // the lab lives far past the real-level bounds
            var hud = HUDController.Instance;
            if (hud != null)
            {
                hud.RefreshAbilityHud();
                hud.ShowBanner("DEV LAB — mechanic sandbox ([ to leave)");
            }
        }

        // DEV: widen the camera bounds to a region and snap onto the player, so
        // a warp far outside the real-level bounds doesn't leave them off-screen.
        static void SnapCamera(float minX, float maxX, float minY, float maxY)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) return;
            follow.SetBounds(minX, maxX, minY, maxY);
            follow.Snap();
        }

        public void Unlock(Ability a)
        {
            if (a == Ability.Flash) HasFlash = true;
            else if (a == Ability.Shutter) HasShutter = true;
            else HasNegative = true;
            if (HUDController.Instance != null) HUDController.Instance.OnAbilityUnlocked(a);
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayPickup();
        }

        /// Respawn at the checkpoint: <=0.3 s fade, exposure reset to Normal, strokes cleared.
        public void Kill(DeathCause cause)
        {
            if (IsRespawning || HasWon) return;
            Deaths++;
            StartCoroutine(RespawnRoutine(cause));
        }

        bool _firstDeathNoted;

        IEnumerator RespawnRoutine(DeathCause cause)
        {
            IsRespawning = true;
            Player.InputEnabled = false;

            // the image burns: grain burst, sprite gone, falling tone
            var anim = Player.GetComponent<PlayerAnimator>();
            StrokeSparkle.Burst(Player.transform.position, new Color(0.85f, 0.85f, 0.85f, 1f), 14);
            if (anim != null) anim.SetVisible(false);
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayDeath();

            var hud = HUDController.Instance;
            if (hud != null) yield return hud.FadeBlack(true, 0.12f);
            OnRespawn?.Invoke();
            Player.Teleport(CheckpointPos);
            ExposureManager.Instance.ForceSet(Exposure.Normal);
            if (hud != null) yield return hud.FadeBlack(false, 0.12f);

            // ...and re-develops at the checkpoint
            if (anim != null) anim.PlayDevelopIn();
            StrokeSparkle.Burst(Player.FeetPos, new Color(0.75f, 0.78f, 0.85f, 1f), 6);
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayDevelop();

            // margin note for real deaths (R stays a silent tool)
            if (hud != null && cause != DeathCause.Restart)
            {
                if (!_firstDeathNoted)
                {
                    _firstDeathNoted = true;
                    hud.ShowDeathNote("the print burned. the negative survives.", false);
                }
                else
                {
                    hud.ShowDeathNote(cause == DeathCause.Enemy
                        ? "BURNED — too much light."
                        : "OUT OF FRAME.", true);
                }
            }

            Player.InputEnabled = true;
            IsRespawning = false;
        }

        /// The exit no longer hard-cuts: she turns, the journey arrives as
        /// accelerating shutter clicks, warm light sweeps the frame, three
        /// beeps — and she takes frame 11 herself.
        public void BeginFinale()
        {
            if (HasWon || IsRespawning || IsCinematic) return;
            StartCoroutine(FinaleRoutine());
        }

        IEnumerator FinaleRoutine()
        {
            IsCinematic = true;
            Player.InputEnabled = false;
            Player.Body.linearVelocity = new Vector2(0f, Player.Body.linearVelocity.y);
            var ad = AudioDirector.Instance;
            var hud = HUDController.Instance;
            var anim = Player.GetComponent<PlayerAnimator>();

            // held breath: the pedal stops, the guard freezes gray
            if (ad != null) ad.CutPedal();
            if (ExposureManager.Instance != null)
                ExposureManager.Instance.ForceSet(Exposure.Normal);
            yield return new WaitForSeconds(0.55f);

            // she turns to face the world she walked
            if (anim != null) anim.SetPose(SilhouetteArt.PlayerIdle, true);
            yield return new WaitForSeconds(0.5f);

            // the whole journey approaches, click by click
            if (ad != null) yield return StartCoroutine(ad.FinaleBursts());

            // warm light sweeps the frame; the last lamps flare
            if (LightDirector.Instance != null)
                LightDirector.Instance.SetOverride(new Color(1.0f, 0.93f, 0.80f), 1.15f);
            FlareExitLamps();
            if (hud != null) { hud.SetShutterOpen(true); hud.SetRecFast(true); }
            if (ad != null) ad.PlayFinaleChord();

            // three beeps — and she raises the camera
            for (int i = 0; i < 3; i++)
            {
                if (ad != null) ad.PlayBeep();
                yield return new WaitForSeconds(0.42f);
            }
            if (anim != null) anim.SetPose(SilhouetteArt.PlayerShoot, true);
            yield return new WaitForSeconds(0.6f);

            // frame 11, taken by her own hand (retakes the checkpoint's shot)
            if (PhotoAlbum.Instance != null) PhotoAlbum.Instance.CaptureRoom(10, true);
            yield return null;
            yield return null; // capture lands at end of frame

            if (hud != null) { hud.SetShutterOpen(false); hud.SetRecFast(false); }
            if (LightDirector.Instance != null) LightDirector.Instance.ClearOverride();
            IsCinematic = false;
            Win();
        }

        /// The hanging lamps inside the final camera frame blaze up. The
        /// backdrop survives FullRestart, so originals are kept for restore.
        readonly System.Collections.Generic.List<UnityEngine.Rendering.Universal.Light2D> _flaredLamps
            = new System.Collections.Generic.List<UnityEngine.Rendering.Universal.Light2D>();
        readonly System.Collections.Generic.List<float> _flaredOriginals
            = new System.Collections.Generic.List<float>();

        void FlareExitLamps()
        {
            var lampsRoot = GameObject.Find("_Backdrop/Lamps");
            if (lampsRoot == null) return;
            foreach (Transform lamp in lampsRoot.transform)
            {
                float x = lamp.position.x;
                if (x < 160f || lamp.position.y < 5f) continue;
                foreach (var l in lamp.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>())
                {
                    if (_flaredLamps.Contains(l)) continue;
                    _flaredLamps.Add(l);
                    _flaredOriginals.Add(l.intensity);
                    l.intensity = 1.4f;
                }
            }
        }

        void UnflareExitLamps()
        {
            for (int i = 0; i < _flaredLamps.Count; i++)
                if (_flaredLamps[i] != null) _flaredLamps[i].intensity = _flaredOriginals[i];
            _flaredLamps.Clear();
            _flaredOriginals.Clear();
        }

        public void Win()
        {
            if (HasWon || IsRespawning) return;
            HasWon = true;
            HasEverWon = true;
            Player.InputEnabled = false;
            Player.Body.linearVelocity = Vector2.zero;
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayWin();
            WinScreen.Show();
        }

        /// Full restart (from the win screen): rebuild the level in place.
        /// Doors close again, pickups respawn, abilities relock.
        public void FullRestart()
        {
            StopAllCoroutines();
            IsRespawning = false;
            IsCinematic = false;
            HasWon = false;
            HasFlash = false;
            HasShutter = false;
            HasNegative = false;
            Deaths = 0;
            _firstDeathNoted = false;
            RunTime = 0f;
            OnRespawn?.Invoke();
            if (PhotoAlbum.Instance != null) PhotoAlbum.Instance.Clear();
            var old = GameObject.Find("_Level");
            if (old != null) Destroy(old);
            LevelBuilder.Build(Bootstrap.BuildThroughRoomCount);
            CheckpointPos = Bootstrap.SpawnPos;
            Player.Teleport(Bootstrap.SpawnPos);
            Player.InputEnabled = true;
            var animator = Player.GetComponent<PlayerAnimator>();
            if (animator != null) animator.ClearPose();
            ExposureManager.Instance.ForceSet(Exposure.Normal);
            UnflareExitLamps();
            if (AudioDirector.Instance != null) AudioDirector.Instance.ResetFinaleAudio();
            if (HUDController.Instance != null) HUDController.Instance.ResetForRestart();
        }
    }
}
