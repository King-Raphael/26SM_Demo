using System.Collections;
using UnityEngine;

namespace Darkroom
{
    /// Asleep (exposure != Over): solid gray statue on layer World — stand on it.
    /// Awake (Over): safelight-red deadly trigger on layer Triggers, patrolling
    /// between homeX +- patrolRange. On sleep it freezes wherever it stands.
    public class LightSensitiveEnemy : MonoBehaviour
    {
        float _homeX;
        float _range;
        float _speed;
        int _dir = 1;
        bool _isAwake;

        Rigidbody2D _rb;
        Collider2D _col;
        SpriteRenderer _sr;
        Coroutine _crackle;

        public void Init(float patrolRange, float patrolSpeed)
        {
            _range = patrolRange;
            _speed = patrolSpeed;
            _homeX = transform.position.x;
        }

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();
        }

        void OnEnable()
        {
            var mgr = ExposureManager.Instance;
            if (mgr == null) { ApplyState(Exposure.Normal); return; } // edit-mode build
            mgr.OnExposureChanged += ApplyState;
            ApplyState(mgr.Current);
        }

        void OnDisable()
        {
            var mgr = ExposureManager.Instance;
            if (mgr != null) mgr.OnExposureChanged -= ApplyState;
        }

        void ApplyState(Exposure e)
        {
            bool wasAwake = _isAwake;
            _isAwake = e == Exposure.Overexposed;
            _col.isTrigger = _isAwake;
            gameObject.layer = _isAwake ? Layers.Triggers : Layers.World;
            _sr.color = _isAwake ? VisualFactory.EnemyAwake : VisualFactory.EnemyAsleep;

            if (_crackle != null) { StopCoroutine(_crackle); _crackle = null; }
            // statue "crackle" on freeze (spec stretch #4)
            if (wasAwake && !_isAwake && gameObject.activeInHierarchy)
                _crackle = StartCoroutine(Crackle());
        }

        IEnumerator Crackle()
        {
            var hi = new Color(0.42f, 0.42f, 0.42f, 1f);
            for (int i = 0; i < 3; i++)
            {
                _sr.color = hi;
                yield return new WaitForSeconds(0.05f);
                _sr.color = VisualFactory.EnemyAsleep;
                yield return new WaitForSeconds(0.05f);
            }
            _crackle = null;
        }

        void FixedUpdate()
        {
            if (!_isAwake || _range <= 0f || _speed <= 0f) return;
            float x = _rb.position.x + _dir * _speed * Time.fixedDeltaTime;
            if (x >= _homeX + _range) { x = _homeX + _range; _dir = -1; }
            else if (x <= _homeX - _range) { x = _homeX - _range; _dir = 1; }
            _rb.MovePosition(new Vector2(x, _rb.position.y));
        }

        void OnTriggerEnter2D(Collider2D other) { TryKill(other); }
        void OnTriggerStay2D(Collider2D other) { TryKill(other); }

        void TryKill(Collider2D other)
        {
            if (!_isAwake) return;
            if (other.gameObject.layer != Layers.Player) return;
            if (GameManager.Instance != null) GameManager.Instance.Kill();
        }
    }
}
