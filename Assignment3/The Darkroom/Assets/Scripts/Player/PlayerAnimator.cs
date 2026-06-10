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

        void Update()
        {
            float vx = _pc.Body.linearVelocity.x;
            float vy = _pc.Body.linearVelocity.y;
            bool grounded = _pc.IsGrounded;

            if (Mathf.Abs(vx) > 0.1f) _sr.flipX = vx < 0f;

            if (!grounded) _sr.sprite = _jump;
            else if (Mathf.Abs(vx) > 0.3f)
            {
                _walkClock += Time.deltaTime * 9f;
                _sr.sprite = ((int)_walkClock % 2 == 0) ? _walkA : _walkB;
            }
            else _sr.sprite = _idle;

            if (grounded && !_wasGrounded) _squash = 0.20f;
            _wasGrounded = grounded;
            _squash = Mathf.MoveTowards(_squash, 0f, Time.deltaTime * 1.1f);

            float stretch = grounded ? 0f : Mathf.Clamp(Mathf.Abs(vy) * 0.008f, 0f, 0.08f);
            _visual.localScale = new Vector3(1f + _squash - stretch * 0.5f, 1f - _squash + stretch, 1f);
        }

        static void EnsureSprites()
        {
            if (_idle != null) return;
            var pal = new Dictionary<char, Color32>
            {
                { 'B', new Color32(0xF2, 0xF2, 0xF2, 0xFF) },  // body
                { 'C', new Color32(0x2E, 0x2E, 0x2E, 0xFF) },  // camera strap
                { 'D', new Color32(0xC9, 0xC9, 0xC9, 0xFF) },  // legs
            };
            string[] torso =
            {
                "..BBB..",
                ".BBBBB.",
                ".BBBBB.",
                "..BBB..",
                ".BBBBB.",
                "BBBBBBB",
                "BBCCCBB",
                ".BBBBB.",
                ".BBBBB.",
            };
            _idle = Build("PlayerIdle", torso, pal, "..D.D..", "..D.D..", "..D.D..", "..D.D..");
            _walkA = Build("PlayerWalkA", torso, pal, "..D.D..", ".D...D.", ".D...D.", "D.....D");
            _walkB = Build("PlayerWalkB", torso, pal, "..DDD..", "..D.D..", "..D.D..", "..D.D..");
            _jump = Build("PlayerJump", torso, pal, ".D...D.", "..D.D..", "..DDD..", ".......");
        }

        static Sprite Build(string name, string[] torso, Dictionary<char, Color32> pal, params string[] legs)
        {
            var rows = new string[torso.Length + legs.Length];
            torso.CopyTo(rows, 0);
            legs.CopyTo(rows, torso.Length);
            return PixelArt.FromMap(name, rows, pal, 10f); // 7x13 px @ 10 ppu = 0.7 x 1.3
        }
    }
}
