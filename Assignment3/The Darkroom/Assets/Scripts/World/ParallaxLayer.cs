using UnityEngine;

namespace Darkroom
{
    /// Moves a backdrop layer at a fraction of the camera's horizontal travel.
    /// Optionally also TRACKS the camera vertically (VerticalFollow > 0) so a layer stays
    /// in frame as the camera rises/falls with the player — used by the mid-ground clutter,
    /// which otherwise drifts out of view whenever the player climbs or drops.
    public class ParallaxLayer : MonoBehaviour
    {
        public float Factor = 0.3f;
        public float VerticalFollow = 0f;   // 0 = fixed world Y (default); >0 = track camera Y by this factor
        public float VerticalOffset = 0f;   // world-Y offset applied when following (where the band hangs)

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
            float y = VerticalFollow > 0f ? _cam.position.y * VerticalFollow + VerticalOffset : _base.y;
            transform.position = new Vector3(
                _base.x + (_cam.position.x - _cam0) * Factor, y, _base.z);
        }
    }
}
