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

        /// Hold a switch key (1/2/3) past ~0.18 s to PREVIEW what it would
        /// solidify (a non-committing ghost); a quick tap commits as before.
        /// Set false to restore instant switch-on-press if the timing feels off.
        /// (static readonly, not const, so the instant-path branch still compiles.)
        public static readonly bool HoldPreviewEnabled = true;
        const float PreviewHoldDelay = 0.18f;
        int _holdDigit;     // the switch digit currently being tracked (0 = none)
        float _holdTime;    // how long it has been held

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
            if (em != null)
            {
                if (HoldPreviewEnabled)
                {
                    HandleExposureHold(em);
                }
                else
                {
                    if (DarkroomInput.Set1Pressed) em.TrySetExposure(Exposure.Underexposed);
                    else if (DarkroomInput.Set2Pressed) em.TrySetExposure(Exposure.Normal);
                    else if (DarkroomInput.Set3Pressed) em.TrySetExposure(Exposure.Overexposed);
                }
                if (DarkroomInput.CycleForwardPressed) em.Cycle(1);
                else if (DarkroomInput.CycleBackPressed) em.Cycle(-1);
            }

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

        // Hold-to-preview: track the held switch digit, peek after a short hold,
        // and commit on RELEASE (a quick tap commits ~instantly). Release is read
        // as an event, so a release that lands during a skipped respawn/cinematic
        // frame can never leak a stale switch.
        void HandleExposureHold(ExposureManager em)
        {
            int held = DarkroomInput.ExposureDigitHeld;
            int released = DarkroomInput.ExposureDigitReleased;

            var ad = AudioDirector.Instance;
            if (held == 0) { _holdDigit = 0; _holdTime = 0f; if (ad != null) ad.StopPreviewTone(); }
            else
            {
                if (held != _holdDigit) { _holdDigit = held; _holdTime = 0f; }
                else _holdTime += Time.deltaTime;
                // no peek while the world holds the dial (the R9 blackout)
                if (_holdTime >= PreviewHoldDelay && !em.Locked)
                {
                    var cand = DigitToExposure(held);
                    em.PreviewExposure(cand);
                    // a soft tone confirms the peek — only when it would change state
                    if (ad != null) { if (cand != em.Current) ad.StartPreviewTone(cand); else ad.StopPreviewTone(); }
                }
                else if (ad != null) ad.StopPreviewTone();
            }

            if (released != 0)
            {
                em.ClearPreview();
                if (ad != null) ad.StopPreviewTone();
                em.TrySetExposure(DigitToExposure(released));
                _holdDigit = 0;
                _holdTime = 0f;
            }
        }

        static Exposure DigitToExposure(int d) =>
            d == 1 ? Exposure.Underexposed : d == 3 ? Exposure.Overexposed : Exposure.Normal;

        public void InitCheckpoint(Vector2 p) { CheckpointPos = p; }

        public void SetCheckpoint(Vector2 p, string caption = "")
        {
            if ((p - CheckpointPos).sqrMagnitude < 0.0001f) return;
            CheckpointPos = p;
            if (HUDController.Instance != null) HUDController.Instance.CheckpointFlash(caption);
            // crossing a checkpoint IS the camera firing this frame onto the roll —
            // the film-advance ratchet (not a generic chime), so the end-screen
            // contact sheet is audibly made of these moments. SoftFlash is reused.
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayFilmAdvance();
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

        /// The darkroom's safelight is always lit — Under is granted at boot (and
        /// after a full restart) with no pickup ceremony, so the prologue's first
        /// "lower the room to safelight" just works. Flash/Shutter stay earned.
        public void GrantNegativeSilently()
        {
            HasNegative = true;
            if (HUDController.Instance != null) HUDController.Instance.RefreshAbilityHud();
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
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayDeath(cause);

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

        /// The prologue exit: she steps into the blank 11th frame. The inverse of
        /// the finale — instead of taking the photo, she is developed INTO one.
        public void BeginPrologueExit(Vector3 paperPos)
        {
            if (HasWon || IsRespawning || IsCinematic) return;
            StartCoroutine(PrologueExitRoutine(paperPos));
        }

        IEnumerator PrologueExitRoutine(Vector3 paperPos)
        {
            IsCinematic = true;
            Player.InputEnabled = false;
            Player.Body.linearVelocity = new Vector2(0f, Player.Body.linearVelocity.y);
            var hud = HUDController.Instance;
            var ad = AudioDirector.Instance;
            var anim = Player.GetComponent<PlayerAnimator>();

            if (hud != null) hud.SetLetterbox(true, 1.0f);
            if (ExposureManager.Instance != null) ExposureManager.Instance.ForceSet(Exposure.Normal);
            if (anim != null) anim.SetPose(SilhouetteArt.PlayerIdle, false); // turn to face the paper
            yield return new WaitForSeconds(0.6f);

            // the door opens — not onto a room, but onto a giant sheet of photo paper.
            // the real darkroom recedes behind a scrim as the paper fills the frame.
            var cam = Camera.main;
            Vector3 center = cam != null ? cam.transform.position : paperPos;
            center.z = 0f;
            var reveal = new GameObject("PrologueReveal");
            var scrim  = MakeRevealSprite(reveal.transform, "Scrim", center, new Vector3(48f, 30f, 1f),
                new Color(0.04f, 0.04f, 0.05f, 0f), 200);
            var border = MakeRevealSprite(reveal.transform, "PaperBorder", center + new Vector3(0f, 0.35f, 0f), new Vector3(10.6f, 7.6f, 1f),
                new Color(0.02f, 0.02f, 0.03f, 0f), 201);
            var paper  = MakeRevealSprite(reveal.transform, "GiantPaper", center + new Vector3(0f, 0.35f, 0f), new Vector3(10f, 7f, 1f),
                new Color(0.86f, 0.85f, 0.80f, 0f), 202);
            float t = 0f;
            while (t < 0.9f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.9f);
                SetA(scrim, 0.74f * k); SetA(border, 0.95f * k); SetA(paper, k);
                yield return null;
            }

            // her latent, BLANK-FACED silhouette develops on the paper — the unprinted self
            var sil = MakeRevealSprite(reveal.transform, "PaperSilhouette",
                center + new Vector3(0f, -0.7f, 0f), new Vector3(1.8f, 1.8f, 1f),
                new Color(0.10f, 0.10f, 0.12f, 0f), 203);
            sil.GetComponent<SpriteRenderer>().sprite = SilhouetteArt.PlayerBlank;
            t = 0f;
            while (t < 1.1f)
            {
                t += Time.deltaTime;
                SetA(sil, Mathf.Clamp01(t / 1.1f) * 0.9f);
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);

            // shutter + white flash + a warm sweep as the photo takes her in
            if (ad != null) ad.PlayClick();
            if (hud != null) hud.FullFlash();
            if (LightDirector.Instance != null)
                LightDirector.Instance.SetOverride(new Color(1.0f, 0.96f, 0.86f), 1.2f);
            if (PostFXDirector.Instance != null)
                PostFXDirector.Instance.SetOverride(new Color(1.08f, 1.0f, 0.90f), 1.6f, 0.3f);
            yield return new WaitForSeconds(0.22f);

            // the game names itself as she steps inside it
            if (hud != null) hud.DropTitle();
            yield return new WaitForSeconds(0.7f);

            // cut to FRAME 1: hand control onto the first real frame, then let the
            // photo space "develop in" — the paper + scrim dissolve onto the new room
            if (LightDirector.Instance != null) LightDirector.Instance.ClearOverride();
            if (PostFXDirector.Instance != null) PostFXDirector.Instance.ClearOverride();
            var cp1 = LevelData.Rooms[1].checkpoints[0];
            Player.Teleport(new Vector2(cp1.cx, cp1.cy));
            InitCheckpoint(new Vector2(cp1.cx, cp1.cy));
            if (anim != null) anim.ClearPose();
            // leave the prologue pocket: restore the real-level bounds and SNAP across
            // the wide gap while the scrim still hides the jump, then dissolve onto Frame 1
            SnapCamera(Bootstrap.LevelCamMinX, Bootstrap.LevelCamMaxX, Bootstrap.CamMinY, Bootstrap.CamMaxY);
            // re-center the paper set-piece on the new view so it dissolves INTO Frame 1
            if (cam != null) reveal.transform.position += new Vector3(cam.transform.position.x - center.x, cam.transform.position.y - center.y, 0f);
            t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / 0.6f);
                SetA(scrim, 0.74f * k); SetA(border, 0.95f * k); SetA(paper, k); SetA(sil, 0.9f * k);
                yield return null;
            }
            Destroy(reveal);
            if (hud != null) hud.SetLetterbox(false, 1.0f);
            IsCinematic = false;
            Player.InputEnabled = true;
        }

        /// A full-screen-ish flat sprite for the prologue's enter-photo set-piece
        /// (scrim / giant paper / silhouette). Lives above all gameplay (order 200+).
        static GameObject MakeRevealSprite(Transform parent, string name, Vector3 pos, Vector3 scale, Color col, int order)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.localScale = scale;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = VisualFactory.WhiteSprite;
            sr.sharedMaterial = VisualFactory.SpriteMat;
            sr.color = col;
            sr.sortingOrder = order;
            return g;
        }

        static void SetA(GameObject g, float a)
        {
            if (g == null) return;
            var sr = g.GetComponent<SpriteRenderer>();
            if (sr == null) return;
            var c = sr.color; c.a = a; sr.color = c;
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
            if (hud != null) hud.SetLetterbox(true, 1.0f); // the frame closes to widescreen

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
            if (PostFXDirector.Instance != null)
                PostFXDirector.Instance.SetOverride(new Color(1.08f, 1.0f, 0.90f), 2.0f, 0.4f);
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

            // frame 11, taken by her own hand — it PRINTS into slot 0, the blank
            // "unprinted frame" she stepped through in the prologue, now complete.
            if (PhotoAlbum.Instance != null) PhotoAlbum.Instance.CaptureRoom(0, true);
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
            // a replay re-enters the prologue pocket: re-fence the camera to it
            SnapCamera(Bootstrap.PrologueCamMinX, Bootstrap.PrologueCamMaxX, Bootstrap.CamMinY, Bootstrap.CamMaxY);
            Player.InputEnabled = true;
            var animator = Player.GetComponent<PlayerAnimator>();
            if (animator != null) animator.ClearPose();
            ExposureManager.Instance.ForceSet(Exposure.Normal);
            UnflareExitLamps();
            if (AudioDirector.Instance != null) AudioDirector.Instance.ResetFinaleAudio();
            if (PostFXDirector.Instance != null) PostFXDirector.Instance.ResetForRestart();
            if (HUDController.Instance != null) HUDController.Instance.ResetForRestart();
            GrantNegativeSilently(); // the safelight is always lit; re-grant for the replayed prologue
        }
    }
}
