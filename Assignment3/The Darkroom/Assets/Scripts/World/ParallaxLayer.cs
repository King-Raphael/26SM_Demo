using UnityEngine;

namespace Darkroom
{
    /// Moves a backdrop layer at a fraction of the camera's horizontal travel.
    public class ParallaxLayer : MonoBehaviour
    {
        public float Factor = 0.3f;

        Transform _cam;
        float _cam0;
        Vector3 _base;

        void Start()
        {
            var c = Camera.main;
            if (c != null) { _cam = c.transform; _cam0 = _cam.position.x; }
            _base = transform.position;
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            transform.position = new Vector3(
                _base.x + (_cam.position.x - _cam0) * Factor, _base.y, _base.z);
        }
    }
}
