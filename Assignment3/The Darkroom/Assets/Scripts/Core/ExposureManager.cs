using System;
using System.Collections.Generic;
using UnityEngine;

namespace Darkroom
{
    public enum Exposure { Underexposed, Normal, Overexposed }

    /// Owns the current exposure state, ability gating and the shutter-jam rule.
    public class ExposureManager : MonoBehaviour
    {
        public static ExposureManager Instance { get; private set; }

        public Exposure Current { get; private set; } = Exposure.Normal;

        /// While locked (the Room 9 blackout), every switch request is refused —
        /// the slider shakes: for once, the world decides.
        public bool Locked { get; private set; }
        /// True only while a silent ForceSet is being applied (no click, no flash).
        public bool LastChangeSilent { get; private set; }

        public void SetLocked(bool locked) { Locked = locked; }

        /// Fired after Current changes.
        public event Action<Exposure> OnExposureChanged;
        /// Fired just before Current changes (TrailSystem fixes the live stroke here).
        public event Action BeforeExposureChanged;

        readonly List<ExposureObject> _registered = new List<ExposureObject>();

        void Awake() { Instance = this; }

        public void Register(ExposureObject o) { if (!_registered.Contains(o)) _registered.Add(o); }
        public void Unregister(ExposureObject o) { _registered.Remove(o); }

        public bool TrySetExposure(Exposure next)
        {
            if (next == Current) return false;
            if (Locked) { Jam(false); return false; }
            var gm = GameManager.Instance;
            if (next == Exposure.Overexposed && (gm == null || !gm.HasFlash)) { Jam(false); return false; }
            if (next == Exposure.Underexposed && (gm == null || !gm.HasNegative)) { Jam(false); return false; }
            if (WouldJam(next)) { Jam(true); return false; }
            Apply(next);
            return true;
        }

        /// Used by respawn and scripted moments: never refused, never gated.
        /// silent = no shutter click, no white pop (the blackout's quiet hand).
        public void ForceSet(Exposure next, bool silent = false)
        {
            if (next == Current) return;
            LastChangeSilent = silent;
            Apply(next);
            LastChangeSilent = false;
        }

        /// E/Q cycling: skips locked states and states that would jam.
        public void Cycle(int dir)
        {
            if (Locked) { Jam(false); return; }
            var gm = GameManager.Instance;
            for (int i = 1; i <= 2; i++)
            {
                var cand = (Exposure)((((int)Current + dir * i) % 3 + 3) % 3);
                if (cand == Exposure.Overexposed && (gm == null || !gm.HasFlash)) continue;
                if (cand == Exposure.Underexposed && (gm == null || !gm.HasNegative)) continue;
                if (WouldJam(cand)) continue;
                Apply(cand);
                return;
            }
            Jam(false);
        }

        /// True if any registered object that is non-solid now but solid in `next`
        /// overlaps the player's bounds (expanded by 0.02, per spec 8.1).
        bool WouldJam(Exposure next)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return false;
            Bounds pb = gm.Player.Box.bounds;
            pb.Expand(0.02f);
            foreach (var o in _registered)
            {
                if (o == null) continue;
                if (!o.IsSolidIn(Current) && o.IsSolidIn(next) && o.OverlapsPlayer(pb))
                    return true;
            }
            return false;
        }

        void Apply(Exposure next)
        {
            BeforeExposureChanged?.Invoke();
            Current = next;
            OnExposureChanged?.Invoke(next);
        }

        /// physical = the switch was refused because matter would develop
        /// inside the player (as opposed to a merely locked/unavailable state).
        void Jam(bool physical)
        {
            if (HUDController.Instance != null) HUDController.Instance.JamFeedback(physical);
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayJam();
        }
    }
}
