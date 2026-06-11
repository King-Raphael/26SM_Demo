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
            // a small photographer: hair, face, cold scarf, coat, camera
            var pal = new Dictionary<char, Color32>
            {
                { 'H', new Color32(0x34, 0x38, 0x3E, 0xFF) },  // hair
                { 'F', new Color32(0xF2, 0xF2, 0xF2, 0xFF) },  // face
                { 'D', new Color32(0x2A, 0x2E, 0x36, 0xFF) },  // eyes
                { 'S', new Color32(0xAE, 0xBB, 0xD0, 0xFF) },  // scarf
                { 'B', new Color32(0xE6, 0xE8, 0xEC, 0xFF) },  // coat
                { 'C', new Color32(0x2E, 0x31, 0x38, 0xFF) },  // camera
                { 'L', new Color32(0x9F, 0xD8, 0xE6, 0xFF) },  // lens
                { 'G', new Color32(0xC2, 0xC6, 0xCE, 0xFF) },  // legs
                { 'K', new Color32(0x2B, 0x2E, 0x34, 0xFF) },  // shoes
            };
            string[] torso =
            {
                "..............",
                "....HHHHHH....",
                "...HHHHHHHH...",
                "...HFFFFFFH...",
                "...FFFFFFFF...",
                "...FFDFFDFF...",
                "...FFFFFFFF...",
                "....FFFFFF....",
                "...SSSSSSSS...",
                "..BBSSSSSSBB..",
                ".BBBBBBBBBBBB.",
                ".BBBBBBBBBBBB.",
                ".BBCCCCCCCBBB.",
                ".BBCCLLCCCBBB.",
                ".BBCCLLCCCBBB.",
                ".BBCCCCCCCBBB.",
                ".BBBBBBBBBBBB.",
                "..BBBBBBBBBB..",
                "..BBBBBBBBBB..",
            };
            _idle = Build("PlayerIdle", torso, pal,
                "...BBB..BBB...",
                "...GGG..GGG...",
                "...GGG..GGG...",
                "...GGG..GGG...",
                "...GGG..GGG...",
                "...GG....GG...",
                "...KK....KK...");
            _walkA = Build("PlayerWalkA", torso, pal,
                "...BBB..BBB...",
                "..GGG....GGG..",
                "..GGG....GGG..",
                ".GGG......GGG.",
                ".GGG......GGG.",
                ".GG........GG.",
                ".KK........KK.");
            _walkB = Build("PlayerWalkB", torso, pal,
                "...BBB..BBB...",
                "....GGGGGG....",
                "....GGGGGG....",
                "....GG..GG....",
                "....GG..GG....",
                "....GG..GG....",
                "....KK..KK....");
            _jump = Build("PlayerJump", torso, pal,
                "...BBB..BBB...",
                "..GGG....GGG..",
                "...GGG..GGG...",
                "....GGGGGG....",
                ".....GGGG.....",
                "..............",
                "..............");
        }

        static Sprite Build(string name, string[] torso, Dictionary<char, Color32> pal, params string[] legs)
        {
            var rows = new string[torso.Length + legs.Length];
            torso.CopyTo(rows, 0);
            legs.CopyTo(rows, torso.Length);
            return PixelArt.FromMap(name, rows, pal, 20f); // 14x26 px @ 20 ppu = 0.7 x 1.3
        }
    }
}
