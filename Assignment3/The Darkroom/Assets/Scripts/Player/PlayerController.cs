using UnityEngine;

namespace Darkroom
{
    /// Movement per the spec physics contract (section 2), with mandatory
    /// coyote time and jump buffering (ground vanishes under the player here).
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        public const float MoveSpeed = 7f;
        public const float JumpForce = 12.5f;
        // Ground speed RAMP — starts/stops carry a little weight instead of
        // snapping. Air control stays near-instant (jump-to-ledge precision).
        public const float GroundAccel = 110f;  // u/s^2 — ~0.064s to full speed
        public const float GroundDecel = 130f;  // u/s^2 — ~0.054s to a stop; keep short or edge-landings get mushy
        public const float CoyoteTime = 0.10f;
        public const float JumpBufferTime = 0.10f;
        public const float HalfHeight = 0.65f;   // box is 0.7 x 1.3

        public bool InputEnabled = true;
        public bool IsGrounded { get; private set; }
        public Rigidbody2D Body { get; private set; }
        public BoxCollider2D Box { get; private set; }

        float _coyote, _jumpBuffer, _moveX;
        bool _jumpHeld, _jumpCut;
        readonly Collider2D[] _groundHits = new Collider2D[8];
        int _groundHitCount;
        ContactFilter2D _groundFilter;
        Vector2 _groundNormal = Vector2.up;

        public static PlayerController Create(Vector2 pos)
        {
            var go = new GameObject("Player");
            go.layer = Layers.Player;
            go.transform.position = pos;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3.2f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            // A sleeping body stops receiving OnTriggerStay2D — sensors and
            // wake-on-top enemy kills depend on it while standing still.
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(0.7f, 1.3f);
            var mat = new PhysicsMaterial2D("PlayerFrictionless") { friction = 0f, bounciness = 0f };
            bc.sharedMaterial = mat;

            var pc = go.AddComponent<PlayerController>();
            go.AddComponent<TrailSystem>();
            PlayerAnimator.Attach(pc);
            // the photographer's own faint glow: never fully blind in Under
            var glow = LightDirector.CreatePoint(go.transform, Vector2.zero,
                new Color(0.92f, 0.90f, 0.84f), 2.8f, 0.35f);
            // count this glow as puzzle-light, but with a small reach so the
            // player alone never trips a shadowed meter — you must still draw.
            if (LightField.Instance != null)
                LightField.Instance.Register(glow.transform, 1.5f, () => glow.intensity);
            return pc;
        }

        void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Box = GetComponent<BoxCollider2D>();
            _groundFilter = new ContactFilter2D();
            _groundFilter.SetLayerMask(Layers.GroundMask);
            _groundFilter.useTriggers = false;
        }

        void Update()
        {
            if (PauseController.IsPaused) return;
            if (!InputEnabled) { _moveX = 0f; _jumpBuffer = 0f; _jumpHeld = false; return; }
            _moveX = DarkroomInput.MoveAxis;
            _jumpHeld = DarkroomInput.JumpHeld;
            if (DarkroomInput.JumpPressed) _jumpBuffer = JumpBufferTime;
            else _jumpBuffer -= Time.deltaTime;
        }

        void FixedUpdate()
        {
            GroundCheck();
            if (IsGrounded) { _coyote = CoyoteTime; _jumpCut = false; }
            else _coyote -= Time.fixedDeltaTime;

            bool jumping = _jumpBuffer > 0f && (IsGrounded || _coyote > 0f);
            var v = Body.linearVelocity;

            if (IsGrounded && !jumping && _groundNormal.y > 0.35f)
            {
                // walk ALONG the surface: on flat ground the tangent is
                // horizontal and y stays 0 (identical to before), on a slope it
                // carries you up/down so inclines are walkable, and zero input
                // means zero velocity so the frictionless body never slides.
                // ...and RAMP toward the target speed along that tangent. Zero
                // input -> target 0 -> the frictionless body still parks (the
                // MoveTowards lands exactly on 0, so no slope creep).
                Vector2 tangent = new Vector2(_groundNormal.y, -_groundNormal.x);
                float target = _moveX * MoveSpeed;
                float cur = Vector2.Dot(v, tangent);
                float rate = Mathf.Abs(target) > 0.01f ? GroundAccel : GroundDecel;
                v = tangent * Mathf.MoveTowards(cur, target, rate * Time.fixedDeltaTime);
            }
            else
            {
                v.x = _moveX * MoveSpeed;   // air control stays near-instant
            }

            if (jumping)
            {
                v.y = JumpForce;
                _jumpBuffer = 0f;
                _coyote = 0f;
                IsGrounded = false;
                _jumpCut = false;
            }
            // Variable jump: release Space while still climbing -> cut it short
            // (tap = hop, hold = full). Max JumpForce is untouched, so every
            // authored gap still clears at a full hold; only early release shortens.
            if (!IsGrounded && !_jumpHeld && !_jumpCut && v.y > 3f)
            {
                v.y *= 0.5f;
                _jumpCut = true;
            }
            Body.linearVelocity = v;
        }

        void GroundCheck()
        {
            // Body.position, not transform.position: with interpolation on,
            // the Transform lags the body by up to one fixed step.
            Vector2 feet = Body.position + new Vector2(0f, -HalfHeight - 0.02f);
            _groundHitCount = Physics2D.OverlapBox(feet, new Vector2(0.6f, 0.08f), 0f, _groundFilter, _groundHits);
            IsGrounded = _groundHitCount > 0;

            // surface normal (for walking slopes); flat or unknown stays "up"
            _groundNormal = Vector2.up;
            if (IsGrounded)
            {
                var hit = Physics2D.Raycast(Body.position, Vector2.down, HalfHeight + 0.25f, Layers.GroundMask);
                if (hit.collider != null && hit.normal.y > 0.01f) _groundNormal = hit.normal;
            }
        }

        /// True while the grounded check currently touches this collider
        /// (used to defer stroke despawn while stood on).
        public bool IsStandingOn(Collider2D c)
        {
            for (int i = 0; i < _groundHitCount; i++)
                if (_groundHits[i] == c) return true;
            return false;
        }

        public Vector2 FeetPos => (Vector2)transform.position + new Vector2(0f, -HalfHeight);

        public void Teleport(Vector2 p)
        {
            Body.position = p;
            transform.position = p;
            Body.linearVelocity = Vector2.zero;
            _coyote = 0f;
            _jumpBuffer = 0f;
            _jumpCut = false;
        }
    }
}
