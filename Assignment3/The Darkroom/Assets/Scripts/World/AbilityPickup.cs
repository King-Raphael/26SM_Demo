using UnityEngine;

namespace Darkroom
{
    public enum Ability { Flash, Shutter, Negative }

    /// Floats with a slow 0.2-unit bob and a faint red pulse.
    /// On pickup: unlock the ability, full-screen flash, banner, despawn.
    public class AbilityPickup : MonoBehaviour
    {
        public Ability ability;

        float _baseY;
        SpriteRenderer _sr;
        bool _consumed;

        void Awake()
        {
            _baseY = transform.position.y;
            _sr = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            var p = transform.position;
            p.y = _baseY + 0.2f * Mathf.Sin(Time.time * 2f);
            transform.position = p;
            // tint over the baked pixel sprite: white -> faint safelight red
            float pulse = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
            _sr.color = Color.Lerp(Color.white, VisualFactory.SafelightRed, pulse * 0.3f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_consumed) return;
            if (other.gameObject.layer != Layers.Player) return;
            _consumed = true;
            var gm = GameManager.Instance;
            if (gm != null)
            {
                // the Shutter gets the camera-raise + light-bloom ceremony (it grants a
                // VERB); the other abilities unlock straight (HUD + flash only).
                if (ability == Ability.Shutter) gm.AcquireShutter(transform.position);
                else gm.Unlock(ability);
            }
            Destroy(gameObject);
        }
    }
}
