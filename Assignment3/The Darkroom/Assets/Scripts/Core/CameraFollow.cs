using UnityEngine;

namespace Darkroom
{
    /// Smooth-follows the player, camera center clamped to the spec bounds.
    public class CameraFollow : MonoBehaviour
    {
        public Transform Target;

        const float MinX = -2f, MaxX = 170f, MinY = -1f, MaxY = 9f;
        const float SmoothTime = 0.12f;

        Vector3 _vel;

        void LateUpdate()
        {
            if (Target == null) return;
            var desired = new Vector3(Target.position.x, Target.position.y, transform.position.z);
            var p = Vector3.SmoothDamp(transform.position, desired, ref _vel, SmoothTime);
            p.x = Mathf.Clamp(p.x, MinX, MaxX);
            p.y = Mathf.Clamp(p.y, MinY, MaxY);
            transform.position = p;
        }

        public void Snap()
        {
            if (Target == null) return;
            var p = new Vector3(
                Mathf.Clamp(Target.position.x, MinX, MaxX),
                Mathf.Clamp(Target.position.y, MinY, MaxY),
                transform.position.z);
            transform.position = p;
            _vel = Vector3.zero;
        }
    }
}
