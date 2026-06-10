using UnityEngine;

namespace Darkroom
{
    public enum Ability { Flash, Shutter }

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
            float pulse = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
            _sr.color = Color.Lerp(VisualFactory.PickupColor, VisualFactory.SafelightRed, pulse * 0.35f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_consumed) return;
            if (other.gameObject.layer != Layers.Player) return;
            _consumed = true;
            if (GameManager.Instance != null) GameManager.Instance.Unlock(ability);
            Destroy(gameObject);
        }
    }
}
