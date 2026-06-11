using System.Collections.Generic;
using UnityEngine;

namespace Darkroom
{
    /// Draws strokes at the player's feet while Shift is held (Under/Over only,
    /// requires horizontal motion), fixes them on release / exposure change /
    /// max length / death. Budget: 3 fixed strokes.
    public class TrailSystem : MonoBehaviour
    {
        public const float PointSpacing = 0.3f;
        public const float MaxLength = 12f;
        public const int MaxFixed = 3;
        public const float MinDrawSpeed = 0.25f;
        // Below the feet by more than the edge radius (0.07), so a stroke
        // drawn along the ground never protrudes above the floor surface
        // (a protruding stroke would jam the switch back to its exposure).
        // 0.25 (not just 0.08) so a stroke drawn near the jump apex sits
        // comfortably below the next jump's reach — without this margin the
        // player can barely land on top of their own apex-height stroke.
        public const float FeetOffset = 0.25f;

        PlayerController _player;
        TrailStroke _active;
        readonly List<TrailStroke> _fixed = new List<TrailStroke>();
        readonly List<TrailStroke> _despawning = new List<TrailStroke>();
        Vector2 _lastPoint;
        float _pathLen;
        Color _activeColor;

        void Awake() { _player = GetComponent<PlayerController>(); }

        void Start()
        {
            if (ExposureManager.Instance != null)
                ExposureManager.Instance.BeforeExposureChanged += FixActive;
            if (GameManager.Instance != null)
                GameManager.Instance.OnRespawn += ClearAll;
        }

        void OnDestroy()
        {
            if (ExposureManager.Instance != null)
                ExposureManager.Instance.BeforeExposureChanged -= FixActive;
            if (GameManager.Instance != null)
                GameManager.Instance.OnRespawn -= ClearAll;
        }

        void Update()
        {
            if (PauseController.IsPaused) return;
            var gm = GameManager.Instance;
            var em = ExposureManager.Instance;
            if (gm == null || em == null) return;

            bool drawState = em.Current != Exposure.Normal;
            bool canDraw = gm.HasShutter && !gm.IsRespawning && !gm.HasWon
                        && _player.InputEnabled
                        && DarkroomInput.DrawHeld
                        && drawState
                        && Mathf.Abs(_player.Body.linearVelocity.x) > MinDrawSpeed;

            if (canDraw)
            {
                if (_active == null) StartStroke(em.Current);
                Append();
            }
            else if (_active != null && !DarkroomInput.DrawHeld)
            {
                FixActive();
            }

            bool shutterOpen = _active != null && canDraw;
            if (AudioDirector.Instance != null)
                AudioDirector.Instance.SetDrawing(shutterOpen);
            if (shutterOpen != _shutterOpenUI && HUDController.Instance != null)
            {
                _shutterOpenUI = shutterOpen;
                HUDController.Instance.SetShutterOpen(shutterOpen);
            }
        }

        bool _shutterOpenUI;

        Vector2 DrawPos => _player.FeetPos + new Vector2(0f, -FeetOffset);

        void StartStroke(Exposure es)
        {
            var type = es == Exposure.Underexposed
                ? ExposureObjectType.DarkStroke
                : ExposureObjectType.BrightStroke;
            _active = TrailStroke.Create(type);
            _activeColor = VisualFactory.ColorFor(type);
            _lastPoint = DrawPos;
            _pathLen = 0f;
            _active.AddPoint(_lastPoint);
            StrokeSparkle.Spawn(_lastPoint, _activeColor);
        }

        void Append()
        {
            var p = DrawPos;
            float d = Vector2.Distance(p, _lastPoint);
            if (d < PointSpacing) return;
            _pathLen += d;
            _lastPoint = p;
            _active.AddPoint(p);
            StrokeSparkle.Spawn(p, _activeColor);
            if (_pathLen >= MaxLength) FixActive();
        }

        /// Fix the live stroke (also called just before any exposure change).
        public void FixActive()
        {
            if (_active == null) return;
            var s = _active;
            _active = null;
            if (!s.Fix())
            {
                Destroy(s.gameObject); // too short to exist
                return;
            }
            _fixed.Add(s);
            if (_fixed.Count > MaxFixed)
            {
                var oldest = _fixed[0];
                _fixed.RemoveAt(0);
                if (oldest != null)
                {
                    _despawning.Add(oldest); // still ours to clear on respawn
                    oldest.BeginDespawn(_player);
                }
            }
            UpdateDots();
        }

        public void ClearAll()
        {
            if (_active != null) { Destroy(_active.gameObject); _active = null; }
            foreach (var s in _fixed)
                if (s != null) Destroy(s.gameObject);
            _fixed.Clear();
            foreach (var s in _despawning)
                if (s != null) Destroy(s.gameObject);
            _despawning.Clear();
            UpdateDots();
        }

        void UpdateDots()
        {
            if (HUDController.Instance != null)
                HUDController.Instance.SetStrokeDots(MaxFixed - _fixed.Count);
        }
    }
}
