using System.Collections.Generic;
using UnityEngine;

namespace Darkroom
{
    /// Pixel-art player visuals: 2-frame walk, jump pose, facing flip,
    /// squash on landing and a slight stretch in the air.
    /// Lives on a child "Visual" object so scale tweens never touch the collider.
    public class PlayerAnimator : MonoBehaviour
    {
        static Sprite _idle, _walkA, _walkB, _jump;

        PlayerController _pc;
        SpriteRenderer _sr;
        Transform _visual;
        float _walkClock, _squash;
        bool _wasGrounded = true;

        public static void Attach(PlayerController pc)
        {
            EnsureSprites();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(pc.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = _idle;
            sr.sharedMaterial = VisualFactory.SpriteMat;
            sr.sortingOrder = VisualFactory.OrderPlayer;

            // faint aura so the silhouette reads against pure dark
            var aura = new GameObject("Aura");
            aura.transform.SetParent(visual.transform, false);
            aura.transform.localScale = new Vector3(2.2f, 2.6f, 1f);
            var asr = aura.AddComponent<SpriteRenderer>();
            asr.sprite = PixelArt.SoftGlow;
            asr.sharedMaterial = VisualFactory.GlowMat;
            asr.color = new Color(0.85f, 0.88f, 0.95f, 0.10f);
            asr.sortingOrder = VisualFactory.OrderPlayer - 1;

            var anim = pc.gameObject.AddComponent<PlayerAnimator>();
            anim._pc = pc;
            anim._sr = sr;
            anim._visual = visual.transform;
        }

        float _lastFallSpeed;

        void Update()
        {
            if (PauseController.IsPaused) return;
            float vx = _pc.Body.linearVelocity.x;
            float vy = _pc.Body.linearVelocity.y;
            bool grounded = _pc.IsGrounded;
            if (vy < 0f) _lastFallSpeed = -vy;

            if (Mathf.Abs(vx) > 0.1f) _sr.flipX = vx < 0f;

            if (!grounded) _sr.sprite = _jump;
            else if (Mathf.Abs(vx) > 0.3f)
            {
                int before = (int)_walkClock;
                _walkClock += Time.deltaTime * 9f;
                int after = (int)_walkClock;
                _sr.sprite = (after % 2 == 0) ? _walkA : _walkB;
                if (after != before && AudioDirector.Instance != null)
                    AudioDirector.Instance.PlayFootstep();
            }
            else _sr.sprite = _idle;

            if (grounded && !_wasGrounded)
            {
                _squash = 0.20f;
                if (AudioDirector.Instance != null)
                    AudioDirector.Instance.PlayLand(_lastFallSpeed / 15f);
            }
            if (!grounded && _wasGrounded && vy > 1f && AudioDirector.Instance != null)
                AudioDirector.Instance.PlayJumpSound();
            _wasGrounded = grounded;
            _squash = Mathf.MoveTowards(_squash, 0f, Time.deltaTime * 1.1f);

            float stretch = grounded ? 0f : Mathf.Clamp(Mathf.Abs(vy) * 0.008f, 0f, 0.08f);
            _visual.localScale = new Vector3(
                (1f + _squash - stretch * 0.5f) * _developScale,
                (1f - _squash + stretch) * _developScale, 1f);
        }

        // ---------- death / develop-in ----------

        float _developScale = 1f;
        Coroutine _developCo;

        public void SetVisible(bool visible) { _sr.enabled = visible; }

        /// The photograph re-develops: alpha and scale ease back in.
        public void PlayDevelopIn()
        {
            _sr.enabled = true;
            if (_developCo != null) StopCoroutine(_developCo);
            _developCo = StartCoroutine(DevelopRoutine());
        }

        System.Collections.IEnumerator DevelopRoutine()
        {
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.35f);
                var c = _sr.color;
                c.a = k;
                _sr.color = c;
                _developScale = Mathf.Lerp(0.9f, 1f, k);
                yield return null;
            }
            var done = _sr.color;
            done.a = 1f;
            _sr.color = done;
            _developScale = 1f;
            _developCo = null;
        }

        static void EnsureSprites()
        {
            if (_idle != null) return;
            // soft silhouette girl (concept-art style), drawn by SilhouetteArt
            _idle = SilhouetteArt.PlayerIdle;
            _walkA = SilhouetteArt.PlayerWalkA;
            _walkB = SilhouetteArt.PlayerWalkB;
            _jump = SilhouetteArt.PlayerJump;
        }
    }
}
