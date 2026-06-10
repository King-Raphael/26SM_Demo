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
            var gm = GameManager.Instance;
            if (next == Exposure.Overexposed && (gm == null || !gm.HasFlash)) { Jam(); return false; }
            if (WouldJam(next)) { Jam(); return false; }
            Apply(next);
            return true;
        }

        /// Used by respawn: never refused, never gated.
        public void ForceSet(Exposure next)
        {
            if (next == Current) return;
            Apply(next);
        }

        /// E/Q cycling: skips locked states and states that would jam.
        public void Cycle(int dir)
        {
            var gm = GameManager.Instance;
            for (int i = 1; i <= 2; i++)
            {
                var cand = (Exposure)((((int)Current + dir * i) % 3 + 3) % 3);
                if (cand == Exposure.Overexposed && (gm == null || !gm.HasFlash)) continue;
                if (WouldJam(cand)) continue;
                Apply(cand);
                return;
            }
            Jam();
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

        void Jam()
        {
            if (HUDController.Instance != null) HUDController.Instance.JamFeedback();
        }
    }
}
