using System;
using System.Collections;
using UnityEngine;

namespace Darkroom
{
    /// Abilities, checkpoint, respawn, win state, and global key handling (1/2/3, E/Q, R).
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public bool HasFlash { get; private set; }
        public bool HasShutter { get; private set; }
        public bool IsRespawning { get; private set; }
        public bool HasWon { get; private set; }
        public Vector2 CheckpointPos { get; private set; }
        public PlayerController Player { get; set; }

        /// Fired at the moment of respawn / full restart (TrailSystem clears strokes here).
        public event Action OnRespawn;

        public const float KillY = -10f;

        void Awake() { Instance = this; }

        void Update()
        {
            if (Player == null) return;

            if (HasWon)
            {
                if (DarkroomInput.RestartPressed) FullRestart();
                return;
            }
            if (IsRespawning) return;

            var em = ExposureManager.Instance;
            if (DarkroomInput.Set1Pressed) em.TrySetExposure(Exposure.Underexposed);
            else if (DarkroomInput.Set2Pressed) em.TrySetExposure(Exposure.Normal);
            else if (DarkroomInput.Set3Pressed) em.TrySetExposure(Exposure.Overexposed);
            else if (DarkroomInput.CycleForwardPressed) em.Cycle(1);
            else if (DarkroomInput.CycleBackPressed) em.Cycle(-1);

            if (DarkroomInput.RestartPressed) { Kill(); return; }
            if (Player.transform.position.y < KillY) Kill();
        }

        public void InitCheckpoint(Vector2 p) { CheckpointPos = p; }

        public void SetCheckpoint(Vector2 p)
        {
            if ((p - CheckpointPos).sqrMagnitude < 0.0001f) return;
            CheckpointPos = p;
            if (HUDController.Instance != null) HUDController.Instance.CheckpointFlash();
        }

        public void Unlock(Ability a)
        {
            if (a == Ability.Flash) HasFlash = true; else HasShutter = true;
            if (HUDController.Instance != null) HUDController.Instance.OnAbilityUnlocked(a);
        }

        /// Respawn at the checkpoint: <=0.3 s fade, exposure reset to Normal, strokes cleared.
        public void Kill()
        {
            if (IsRespawning || HasWon) return;
            StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            IsRespawning = true;
            Player.InputEnabled = false;
            var hud = HUDController.Instance;
            if (hud != null) yield return hud.FadeBlack(true, 0.12f);
            OnRespawn?.Invoke();
            Player.Teleport(CheckpointPos);
            ExposureManager.Instance.ForceSet(Exposure.Normal);
            if (hud != null) yield return hud.FadeBlack(false, 0.12f);
            Player.InputEnabled = true;
            IsRespawning = false;
        }

        public void Win()
        {
            if (HasWon || IsRespawning) return;
            HasWon = true;
            Player.InputEnabled = false;
            Player.Body.linearVelocity = Vector2.zero;
            WinScreen.Show();
        }

        /// Full restart (from the win screen): rebuild the level in place.
        /// Doors close again, pickups respawn, abilities relock.
        public void FullRestart()
        {
            StopAllCoroutines();
            IsRespawning = false;
            HasWon = false;
            HasFlash = false;
            HasShutter = false;
            OnRespawn?.Invoke();
            var old = GameObject.Find("_Level");
            if (old != null) Destroy(old);
            LevelBuilder.Build(Bootstrap.BuildThroughRoomCount);
            CheckpointPos = Bootstrap.SpawnPos;
            Player.Teleport(Bootstrap.SpawnPos);
            Player.InputEnabled = true;
            ExposureManager.Instance.ForceSet(Exposure.Normal);
            if (HUDController.Instance != null) HUDController.Instance.ResetForRestart();
        }
    }
}
