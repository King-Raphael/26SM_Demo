using UnityEngine;

namespace Darkroom
{
    /// Pixel-art player visuals: 2-frame walk, jump pose, facing flip, squash on landing,
    /// a slight stretch in the air, and procedural secondary motion — the body leans into a
    /// walk while the skirt hem trails back, and the skirt + hair swing on jump and land.
    ///
    /// The figure is a split "code rig" (no SpriteSkin): a body CORE plus standalone Skirt
    /// and Hair overlays hung on joint transforms. Hierarchy built in Attach():
    ///   Player → Lean (forward-lean rotation + facing scale.x=±1, replaces flipX)
    ///            ├── Visual (squash/stretch scale + develop; carries Body sprite + Aura)
    ///            ├── SkirtJoint → Skirt   (sibling of Visual so the squash never shears it)
    ///            └── HairJoint  → Hair
    /// All scale/rotation lives under the Player root so the collider is never touched.
    public class PlayerAnimator : MonoBehaviour
    {
        static Sprite _bodyIdle, _bodyWalkA, _bodyWalkB, _bodyJump;
        static readonly int DevelopId = Shader.PropertyToID("_Develop");

        // tuning (degrees; springs use k=(2πf)², d=2ζ(2πf), unit-agnostic)
        const float MoveSpeed = 7f;                 // mirrors PlayerController.MoveSpeed
        const float LeanMax = 7f;                   // body forward-lean at full speed
        const float SkirtFlowMax = 14f, HairFlowMax = 6f; // hem / hair trail-back at full speed
        const float SkirtK = 191f, SkirtD = 8.3f;   // f≈2.2Hz, ζ≈0.30
        const float HairK = 310f, HairD = 10.6f;    // f≈2.8Hz, ζ≈0.30
        const float JumpKickSkirt = 220f, JumpKickHair = 160f;
        const float LandKickSkirt = 260f, LandKickHair = 200f;

        PlayerController _pc;
        SpriteRenderer _sr;          // Body
        SpriteRenderer _skr, _hsr;   // Skirt, Hair overlays
        Transform _visual;           // squash/stretch + develop
        Transform _lean;             // forward-lean + facing
        Transform _skirtJoint, _hairJoint;

        // body-centre offset, used as the ingest target for the Shutter pickup beat
        static readonly Vector3 BodyLocal = new Vector3(0f, 0.12f, 0f);

        float _walkClock, _squash;
        bool _wasGrounded = true;
        float _facing = 1f;

        // secondary-motion state (facing-local frame; the Lean mirror handles L/R)
        float _leanZ, _leanVel;
        float _skirtAngle, _skirtVel;
        float _hairAngle, _hairVel;

        public static void Attach(PlayerController pc)
        {
            EnsureSprites();

            // Lean: body forward-lean (rotation) + facing flip (scale.x=±1). A ±1 reflection
            // plus a rotation stays rigid — no shear. Replaces the old per-renderer flipX.
            var lean = new GameObject("Lean");
            lean.transform.SetParent(pc.transform, false);

            // Visual carries squash/stretch (non-uniform) + the Body sprite. The swing joints
            // sit OUTSIDE this node (siblings under Lean) so the squash never shears them.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(lean.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = _bodyIdle;
            sr.sharedMaterial = VisualFactory.SpriteMat;
            sr.sortingOrder = VisualFactory.OrderPlayer;

            // faint aura so the silhouette reads against pure dark (tracks the body's squash)
            var aura = new GameObject("Aura");
            aura.transform.SetParent(visual.transform, false);
            aura.transform.localScale = new Vector3(2.4f, 2.8f, 1f);
            var asr = aura.AddComponent<SpriteRenderer>();
            asr.sprite = PixelArt.SoftGlow;
            asr.sharedMaterial = VisualFactory.GlowMat;
            asr.color = new Color(0.95f, 0.93f, 0.86f, 0.14f); // faint warm safelight halo
            asr.sortingOrder = VisualFactory.OrderPlayer - 1;

            // skirt: hung from the waist, swung by a joint (waist-pivot sprite)
            var skirtJoint = new GameObject("SkirtJoint");
            skirtJoint.transform.SetParent(lean.transform, false);
            skirtJoint.transform.localPosition = new Vector3(0f, 0.117f, 0f); // waist (tex y46)
            var skirtGO = new GameObject("Skirt");
            skirtGO.transform.SetParent(skirtJoint.transform, false);
            var skr = skirtGO.AddComponent<SpriteRenderer>();
            skr.sprite = SilhouetteArt.PlayerSkirt;
            skr.sharedMaterial = VisualFactory.SpriteMat;
            skr.sortingOrder = VisualFactory.OrderPlayer + 1; // over the body

            // hair bun: swung from the crown
            var hairJoint = new GameObject("HairJoint");
            hairJoint.transform.SetParent(lean.transform, false);
            hairJoint.transform.localPosition = new Vector3(-0.05f, 0.467f, 0f); // crown (tex (18,67))
            var hairGO = new GameObject("Hair");
            hairGO.transform.SetParent(hairJoint.transform, false);
            var hsr = hairGO.AddComponent<SpriteRenderer>();
            hsr.sprite = SilhouetteArt.PlayerHair;
            hsr.sharedMaterial = VisualFactory.SpriteMat;
            hsr.sortingOrder = VisualFactory.OrderPlayer + 1;

            var anim = pc.gameObject.AddComponent<PlayerAnimator>();
            anim._pc = pc;
            anim._sr = sr;
            anim._skr = skr;
            anim._hsr = hsr;
            anim._visual = visual.transform;
            anim._lean = lean.transform;
            anim._skirtJoint = skirtJoint.transform;
            anim._hairJoint = hairJoint.transform;
        }

        /// World position of her body centre (the Shutter pickup's ingest target).
        public Vector3 BodyWorldPos =>
            _visual != null ? _visual.TransformPoint(BodyLocal)
            : _pc != null ? _pc.transform.position : transform.position;

        float _lastFallSpeed;

        // finale pose lock: while set, the per-frame sprite/flip writes pause and the
        // overlays are hidden (the baked pose sprite already contains skirt + bun)
        Sprite _poseLock;
        bool _poseFaceLeft;

        public void SetPose(Sprite pose, bool faceLeft)
        {
            _poseLock = pose;
            _poseFaceLeft = faceLeft;
            if (_skr != null) _skr.enabled = false;
            if (_hsr != null) _hsr.enabled = false;
            ResetSecondaryMotion();
        }

        public void ClearPose()
        {
            _poseLock = null;
            if (_skr != null) _skr.enabled = true;
            if (_hsr != null) _hsr.enabled = true;
            ResetSecondaryMotion();
        }

        void ResetSecondaryMotion()
        {
            _leanZ = _leanVel = 0f;
            _skirtAngle = _skirtVel = 0f;
            _hairAngle = _hairVel = 0f;
            if (_skirtJoint != null) _skirtJoint.localRotation = Quaternion.identity;
            if (_hairJoint != null) _hairJoint.localRotation = Quaternion.identity;
        }

        static void IntegrateSpring(ref float angle, ref float vel, float target, float k, float d, float dt)
        {
            vel += (-k * (angle - target) - d * vel) * dt;
            angle += vel * dt;
        }

        void Update()
        {
            if (PauseController.IsPaused) return;
            if (_poseLock != null)
            {
                _sr.sprite = _poseLock;
                _lean.localScale = new Vector3(_poseFaceLeft ? -1f : 1f, 1f, 1f);
                _lean.localRotation = Quaternion.identity;
                _visual.localScale = new Vector3(_developScale, _developScale, 1f);
                return;
            }
            float vx = _pc.Body.linearVelocity.x;
            float vy = _pc.Body.linearVelocity.y;
            bool grounded = _pc.IsGrounded;
            if (vy < 0f) _lastFallSpeed = -vy;

            // facing via the Lean node (replaces sr.flipX); off-centre joints mirror with it
            if (Mathf.Abs(vx) > 0.1f) _facing = vx < 0f ? -1f : 1f;

            if (!grounded) _sr.sprite = _bodyJump;
            else if (Mathf.Abs(vx) > 0.3f)
            {
                int before = (int)_walkClock;
                _walkClock += Time.deltaTime * 9f;
                int after = (int)_walkClock;
                _sr.sprite = (after % 2 == 0) ? _bodyWalkA : _bodyWalkB;
                if (after != before && AudioDirector.Instance != null)
                    AudioDirector.Instance.PlayFootstep();
            }
            else _sr.sprite = _bodyIdle;

            // landing / jump edges — one detector feeds squash, audio AND swing impulses
            if (grounded && !_wasGrounded)
            {
                // squash scales with fall speed (same /15 reference the landing audio uses):
                // light hops barely dip, the R9 drop compresses hard.
                float f = Mathf.Clamp01(_lastFallSpeed / 15f);
                _squash = Mathf.Lerp(0.06f, 0.28f, f);
                if (AudioDirector.Instance != null)
                    AudioDirector.Instance.PlayLand(f);
                _skirtVel += LandKickSkirt * f; // skirt + hair billow on impact
                _hairVel += LandKickHair * f;
                // only real drops shake: a routine hop returns at ~12.5 u/s and must NOT jolt
                // the camera; ramp from ~13 up so big falls (the R9 drop) still land a thump
                CameraFollow.Instance?.AddTrauma(Mathf.Clamp01((_lastFallSpeed - 13f) / 10f) * 0.6f);
            }
            if (!grounded && _wasGrounded && vy > 1f)
            {
                if (AudioDirector.Instance != null)
                    AudioDirector.Instance.PlayJumpSound();
                _skirtVel += JumpKickSkirt;     // they lag as she leaves the ground
                _hairVel += JumpKickHair;
            }
            _wasGrounded = grounded;
            _squash = Mathf.MoveTowards(_squash, 0f, Time.deltaTime * 1.1f);

            // ---- secondary motion (facing-local; the Lean mirror handles L/R) ----
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f); // clamp so frame spikes can't blow up the spring
            float s = Mathf.Clamp01(Mathf.Abs(vx) / MoveSpeed);

            float leanTarget = -LeanMax * s;                // top tilts toward travel
            _leanZ = Mathf.SmoothDamp(_leanZ, leanTarget, ref _leanVel, 0.08f);

            IntegrateSpring(ref _skirtAngle, ref _skirtVel, -SkirtFlowMax * s, SkirtK, SkirtD, dt);
            IntegrateSpring(ref _hairAngle, ref _hairVel, -HairFlowMax * s, HairK, HairD, dt);

            // ---- apply ----
            _lean.localScale = new Vector3(_facing, 1f, 1f);
            _lean.localRotation = Quaternion.Euler(0f, 0f, _leanZ);
            _skirtJoint.localRotation = Quaternion.Euler(0f, 0f, _skirtAngle);
            _hairJoint.localRotation = Quaternion.Euler(0f, 0f, _hairAngle);

            float stretch = grounded ? 0f : Mathf.Clamp(Mathf.Abs(vy) * 0.008f, 0f, 0.08f);
            _visual.localScale = new Vector3(
                (1f + _squash - stretch * 0.5f) * _developScale,
                (1f - _squash + stretch) * _developScale, 1f);
        }

        // ---------- death / develop-in ----------

        float _developScale = 1f;
        Coroutine _developCo;
        MaterialPropertyBlock _developMpb;

        public void SetVisible(bool visible)
        {
            _sr.enabled = visible;
            if (_skr != null) _skr.enabled = visible;
            if (_hsr != null) _hsr.enabled = visible;
        }

        /// The photograph re-develops: alpha and scale ease back in. Only the Body flashes
        /// through the develop shader; the overlays stay hidden until the reveal completes.
        public void PlayDevelopIn()
        {
            _sr.enabled = true;
            if (_skr != null) _skr.enabled = false;
            if (_hsr != null) _hsr.enabled = false;
            if (_developCo != null) StopCoroutine(_developCo);
            _developCo = StartCoroutine(DevelopRoutine());
        }

        System.Collections.IEnumerator DevelopRoutine()
        {
            var dev = VisualFactory.DevelopMat;             // null if the custom shader is absent
            bool useShader = dev != null;
            if (useShader)
            {
                _sr.sharedMaterial = dev;                   // unlit develop pass for the flash-in
                var w = _sr.color; w.a = 1f; _sr.color = w; // the shader drives the reveal, not alpha
                if (_developMpb == null) _developMpb = new MaterialPropertyBlock();
            }
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.35f);
                if (useShader)
                {
                    _sr.GetPropertyBlock(_developMpb);
                    _developMpb.SetFloat(DevelopId, k);     // the latent image rises out of the grain
                    _sr.SetPropertyBlock(_developMpb);
                }
                else
                {
                    var c = _sr.color; c.a = k; _sr.color = c; // fallback: the original alpha fade
                }
                _developScale = Mathf.Lerp(0.9f, 1f, k);
                yield return null;
            }
            if (useShader)
            {
                _sr.SetPropertyBlock(null);                 // clear the per-renderer override
                _sr.sharedMaterial = VisualFactory.SpriteMat; // restore the lit sprite material
            }
            var done = _sr.color;
            done.a = 1f;
            _sr.color = done;
            _developScale = 1f;
            if (_skr != null) _skr.enabled = true;          // overlays return now the body is fully developed
            if (_hsr != null) _hsr.enabled = true;
            ResetSecondaryMotion();
            _developCo = null;
        }

        static void EnsureSprites()
        {
            if (_bodyIdle != null) return;
            // soft silhouette girl (concept-art style), split into a body core; the skirt
            // and hair are separate swinging overlays applied in Attach().
            _bodyIdle = SilhouetteArt.PlayerBodyIdle;
            _bodyWalkA = SilhouetteArt.PlayerBodyWalkA;
            _bodyWalkB = SilhouetteArt.PlayerBodyWalkB;
            _bodyJump = SilhouetteArt.PlayerBodyJump;
        }
    }
}
