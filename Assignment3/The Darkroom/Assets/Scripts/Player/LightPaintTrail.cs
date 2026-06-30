using System.Collections.Generic;
using UnityEngine;

namespace Darkroom
{
    /// Light painting. Once the Shutter is hers she trails a faint luminous streak
    /// as she moves — a long-exposure ghost the film keeps for a breath, then
    /// forgets (no motion, no trail). While she is actually DRAWING (Under/Over +
    /// Shift), the same streak brightens and takes the stroke's colour, so the
    /// trace she leaves in passing reads as the thing that hardens into a real,
    /// standable stroke (TrailStroke renders solid just below, OrderStroke above).
    ///
    /// Built once on the player; two world-space LineRenderers (line + soft beam).
    public class LightPaintTrail : MonoBehaviour
    {
        const int   MaxPoints   = 28;
        const float Life        = 0.7f;    // a sample lingers this long, then fades out
        const float Spacing     = 0.05f;   // min distance between samples
        const float TeleportGap = 1.5f;    // a jump bigger than this = respawn/warp → clear
        const float FootDrop    = 0.12f;   // sampled just below the feet, near the draw line
        const float Width       = 0.085f;

        const float GhostAlpha  = 0.20f;   // faint while just walking
        const float DrawAlpha   = 0.70f;   // bright while drawing (igniting into the stroke)
        static readonly Color GhostTint = new Color(1.00f, 0.93f, 0.78f); // soft warm light

        PlayerController _pc;
        LineRenderer _lr, _glow;
        readonly List<Vector2> _pos = new List<Vector2>();
        readonly List<float> _born = new List<float>();
        Vector2 _last;
        float _igniteUntil = -1f;
        Color _curTint = GhostTint;   // eased so a mode switch glides, not snaps
        float _curHead;               // eased head brightness

        readonly Gradient _grad = new Gradient();
        readonly GradientColorKey[] _ck = new GradientColorKey[2];
        readonly GradientAlphaKey[] _ak = new GradientAlphaKey[3];

        public void Init(PlayerController pc)
        {
            _pc = pc;

            var lineGO = new GameObject("LightPaintLine");
            lineGO.transform.SetParent(transform, false);
            _lr = lineGO.AddComponent<LineRenderer>();
            _lr.useWorldSpace = true;
            _lr.widthMultiplier = Width;
            _lr.sharedMaterial = VisualFactory.GlowMat;
            _lr.textureMode = LineTextureMode.Stretch;
            _lr.sortingOrder = VisualFactory.OrderStroke - 2; // under real strokes + their glow
            _lr.numCapVertices = 4;
            _lr.numCornerVertices = 3;
            _lr.positionCount = 0;

            var glowGO = new GameObject("LightPaintGlow");
            glowGO.transform.SetParent(transform, false);
            _glow = glowGO.AddComponent<LineRenderer>();
            _glow.useWorldSpace = true;
            _glow.widthMultiplier = Width * 4.5f;
            _glow.sharedMaterial = VisualFactory.BeamMat; // feathered soft beam
            _glow.textureMode = LineTextureMode.Stretch;
            _glow.sortingOrder = VisualFactory.OrderStroke - 3;
            _glow.numCapVertices = 6;
            _glow.numCornerVertices = 4;
            _glow.positionCount = 0;
        }

        /// A brief bright sweep when the Shutter is first gained — her first steps
        /// paint vividly, then settle to the faint ghost.
        public void Ignite() => _igniteUntil = Time.time + 1.3f;

        void Update()
        {
            if (PauseController.IsPaused) return;
            var gm = GameManager.Instance;
            if (gm == null || _pc == null || _lr == null) return;

            bool active = gm.HasShutter && !gm.HasWon;
            float now = Time.time;

            Vector2 foot = _pc.FeetPos + new Vector2(0f, -FootDrop);
            if (active)
            {
                if (_pos.Count > 0 && Vector2.Distance(foot, _last) > TeleportGap)
                    { _pos.Clear(); _born.Clear(); } // a cut/respawn must not streak across
                if (_pos.Count == 0 || Vector2.Distance(foot, _last) >= Spacing)
                    { _pos.Add(foot); _born.Add(now); _last = foot; }
            }

            while (_born.Count > 0 && now - _born[0] > Life) { _born.RemoveAt(0); _pos.RemoveAt(0); }
            while (_pos.Count > MaxPoints) { _pos.RemoveAt(0); _born.RemoveAt(0); }

            if (!active || _pos.Count < 2)
            {
                if (_lr.positionCount != 0) { _lr.positionCount = 0; _glow.positionCount = 0; }
                return;
            }

            // colour follows the MODE: Under = cool, Over = warm, Normal = faint
            // neutral. Drawing brightens it — the ghost igniting into the stroke.
            var em = ExposureManager.Instance;
            Exposure mode = em != null ? em.Current : Exposure.Normal;
            bool drawing = mode != Exposure.Normal
                        && DarkroomInput.DrawHeld
                        && Mathf.Abs(_pc.Body.linearVelocity.x) > 0.25f;

            Color tint; float modeAlpha;
            switch (mode)
            {
                case Exposure.Underexposed:
                    tint = VisualFactory.ColorFor(ExposureObjectType.DarkStroke);   modeAlpha = GhostAlpha; break;
                case Exposure.Overexposed:
                    tint = VisualFactory.ColorFor(ExposureObjectType.BrightStroke); modeAlpha = GhostAlpha; break;
                default:
                    tint = GhostTint; modeAlpha = GhostAlpha * 0.5f; break; // Normal: fainter, neutral
            }
            float head = drawing ? DrawAlpha : modeAlpha;
            if (now < _igniteUntil) head = Mathf.Max(head, DrawAlpha * 0.85f);

            // ease colour + brightness so switching mode glides instead of snapping
            _curTint = Color.Lerp(_curTint, tint, Time.deltaTime * 9f);
            _curHead = Mathf.Lerp(_curHead, head, Time.deltaTime * 10f);
            tint = _curTint; head = _curHead;

            int n = _pos.Count;
            _lr.positionCount = n;
            _glow.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = _pos[i];
                _lr.SetPosition(i, p);
                _glow.SetPosition(i, p);
            }

            // fade head (newest, at her feet) → tail (oldest, gone)
            _ck[0] = new GradientColorKey(tint, 0f);
            _ck[1] = new GradientColorKey(tint, 1f);
            _ak[0] = new GradientAlphaKey(0f, 0f);
            _ak[1] = new GradientAlphaKey(head * 0.6f, 0.7f);
            _ak[2] = new GradientAlphaKey(head, 1f);
            _grad.SetKeys(_ck, _ak);
            _lr.colorGradient = _grad;

            _ak[1] = new GradientAlphaKey(head * 0.30f, 0.7f); // the beam reads softer
            _ak[2] = new GradientAlphaKey(head * 0.45f, 1f);
            _grad.SetKeys(_ck, _ak);
            _glow.colorGradient = _grad;
        }
    }
}
