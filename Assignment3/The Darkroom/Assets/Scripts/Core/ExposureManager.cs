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
        bool _previewing;

        void Awake() { Instance = this; }

        /// Hold-to-preview: ghost the objects that WOULD become solid in `cand`
        /// (a non-committing peek — shows WHERE, never whether it would jam).
        public void PreviewExposure(Exposure cand)
        {
            _previewing = true;
            foreach (var o in _registered)
                if (o != null) o.SetPreviewGhost(!o.IsSolidIn(Current) && o.IsSolidIn(cand));
        }

        public void ClearPreview()
        {
            if (!_previewing) return;
            _previewing = false;
            foreach (var o in _registered)
                if (o != null) o.SetPreviewGhost(false);
        }

        public void Register(ExposureObject o) { if (!_registered.Contains(o)) _registered.Add(o); }
        public void Unregister(ExposureObject o) { _registered.Remove(o); }

        public bool TrySetExposure(Exposure next)
        {
            if (next == Current) return false;
            if (Locked) { Jam(false); return false; }
            var gm = GameManager.Instance;
            if (next == Exposure.Overexposed && (gm == null || !gm.HasFlash)) { Jam(false); return false; }
            if (next == Exposure.Underexposed && (gm == null || !gm.HasNegative)) { Jam(false); return false; }
            var jammer = FirstJamObject(next);
            if (jammer != null) { Jam(true, jammer); return false; }
            Apply(next);
            return true;
        }

        /// Used by respawn and scripted moments: never refused, never gated.
        /// silent = no shutter click, no white pop (the blackout's quiet hand).
        public void ForceSet(Exposure next, bool silent = false)
        {
            ClearPreview(); // respawn / scripted moments cancel any peek
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

        /// The first registered object that is non-solid now but solid in `next`
        /// and overlaps the player's bounds (expanded 0.02, per spec 8.1) — i.e.
        /// the matter that would develop inside her. null when the switch is clear.
        ExposureObject FirstJamObject(Exposure next)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return null;
            Bounds pb = gm.Player.Box.bounds;
            pb.Expand(0.02f);
            foreach (var o in _registered)
            {
                if (o == null) continue;
                if (!o.IsSolidIn(Current) && o.IsSolidIn(next) && o.OverlapsPlayer(pb))
                    return o;
            }
            return null;
        }

        bool WouldJam(Exposure next) => FirstJamObject(next) != null;

        void Apply(Exposure next)
        {
            ClearPreview();
            BeforeExposureChanged?.Invoke();
            Current = next;
            OnExposureChanged?.Invoke(next);
        }

        /// physical = the switch was refused because matter would develop
        /// inside the player (as opposed to a merely locked/unavailable state).
        /// When physical, the offending object flashes amber so the refusal
        /// points at its cause instead of only shaking the slider.
        void Jam(bool physical, ExposureObject offender = null)
        {
            if (HUDController.Instance != null) HUDController.Instance.JamFeedback(physical);
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayJam();
            if (physical && offender != null) offender.FlashJam();
        }
    }
}
