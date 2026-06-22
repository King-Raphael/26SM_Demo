using UnityEngine;

namespace Darkroom
{
    /// Cycles a SpriteRenderer through a set of frames (turbulence / boil / flicker).
    /// Reliable on the URP 2D sprite shaders where UV scrolling is not, and cheap.
    /// Optional random frame order for organic, non-looping motion.
    [RequireComponent(typeof(SpriteRenderer))]
    public class FrameCycle : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 8f;
        public bool randomOrder;

        SpriteRenderer _sr;
        float _t;
        int _i;
        System.Random _rng;
        static int _seed = 8192;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _rng = new System.Random(unchecked(++_seed * 2654435761u).GetHashCode());
        }

        void Update()
        {
            if (PauseController.IsPaused || frames == null || frames.Length == 0) return;
            _t += Time.deltaTime;
            if (_t >= 1f / Mathf.Max(0.01f, fps))
            {
                _t = 0f;
                _i = randomOrder ? _rng.Next(frames.Length) : (_i + 1) % frames.Length;
                if (frames[_i] != null) _sr.sprite = frames[_i];
            }
        }
    }
}
