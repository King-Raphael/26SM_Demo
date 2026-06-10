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
        public const float CoyoteTime = 0.10f;
        public const float JumpBufferTime = 0.10f;
        public const float HalfHeight = 0.65f;   // box is 0.7 x 1.3

        public bool InputEnabled = true;
        public bool IsGrounded { get; private set; }
        public Rigidbody2D Body { get; private set; }
        public BoxCollider2D Box { get; private set; }

        float _coyote, _jumpBuffer, _moveX;
        readonly Collider2D[] _groundHits = new Collider2D[8];
        int _groundHitCount;
        ContactFilter2D _groundFilter;

        public static PlayerController Create(Vector2 pos)
        {
            var go = new GameObject("Player");
            go.layer = Layers.Player;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.7f, 1.3f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = VisualFactory.WhiteSprite;
            sr.sharedMaterial = VisualFactory.SpriteMat;
            sr.color = VisualFactory.PlayerColor;
            sr.sortingOrder = VisualFactory.OrderPlayer;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3.2f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            // A sleeping body stops receiving OnTriggerStay2D — sensors and
            // wake-on-top enemy kills depend on it while standing still.
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one;
            var mat = new PhysicsMaterial2D("PlayerFrictionless") { friction = 0f, bounciness = 0f };
            bc.sharedMaterial = mat;

            var pc = go.AddComponent<PlayerController>();
            go.AddComponent<TrailSystem>();
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
            if (!InputEnabled) { _moveX = 0f; _jumpBuffer = 0f; return; }
            _moveX = DarkroomInput.MoveAxis;
            if (DarkroomInput.JumpPressed) _jumpBuffer = JumpBufferTime;
            else _jumpBuffer -= Time.deltaTime;
        }

        void FixedUpdate()
        {
            GroundCheck();
            if (IsGrounded) _coyote = CoyoteTime;
            else _coyote -= Time.fixedDeltaTime;

            var v = Body.linearVelocity;
            v.x = _moveX * MoveSpeed;
            if (_jumpBuffer > 0f && (IsGrounded || _coyote > 0f))
            {
                v.y = JumpForce;
                _jumpBuffer = 0f;
                _coyote = 0f;
                IsGrounded = false;
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
        }
    }
}
