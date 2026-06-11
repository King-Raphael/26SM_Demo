using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
        Transform _visual;
        Light2D _light;
        Coroutine _crackle;

        // ---------- pixel sprites (baked colors, no tinting) ----------

        static Sprite _sAsleep, _sAwake, _sCrackle;

        public static Sprite AsleepSprite { get { EnsureSprites(); return _sAsleep; } }

        static void EnsureSprites()
        {
            if (_sAsleep != null) return;
            // soft shadow-blob silhouettes (concept-art style)
            _sAsleep = SilhouetteArt.EnemyAsleep;
            _sAwake = SilhouetteArt.EnemyAwake;
            _sCrackle = SilhouetteArt.EnemyCrackle;
        }

        public void Init(float patrolRange, float patrolSpeed)
        {
            _range = patrolRange;
            _speed = patrolSpeed;
            _homeX = transform.position.x;
        }

        void Awake()
        {
            EnsureSprites();
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _visual = _sr.transform;
            _light = GetComponentInChildren<Light2D>(true);
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
            _sr.sprite = _isAwake ? _sAwake : _sAsleep;
            if (_light != null) _light.enabled = _isAwake;

            if (_crackle != null) { StopCoroutine(_crackle); _crackle = null; }
            // statue "crackle" on freeze (spec stretch #4)
            if (wasAwake && !_isAwake && gameObject.activeInHierarchy)
                _crackle = StartCoroutine(Crackle());
        }

        IEnumerator Crackle()
        {
            for (int i = 0; i < 3; i++)
            {
                _sr.sprite = _sCrackle;
                yield return new WaitForSeconds(0.05f);
                _sr.sprite = _sAsleep;
                yield return new WaitForSeconds(0.05f);
            }
            _crackle = null;
        }

        void Update()
        {
            if (_isAwake)
            {
                // breathing while hunting; face the patrol direction
                float b = 1f + 0.05f * Mathf.Sin(Time.time * 6f);
                _visual.localScale = new Vector3(1f, b, 1f);
                if (_range > 0f) _sr.flipX = _dir < 0;
            }
            else
            {
                _visual.localScale = Vector3.one;
            }
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
