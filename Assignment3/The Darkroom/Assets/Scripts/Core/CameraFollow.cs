using UnityEngine;

namespace Darkroom
{
    /// Smooth-follows the player, camera center clamped to the spec bounds.
    public class CameraFollow : MonoBehaviour
    {
        public Transform Target;

        // default bounds frame the real level; the dev lab widens them, and the
        // prologue fences them to its isolated far-left pocket
        public float MinX = -2f, MaxX = 170f, MinY = -1f, MaxY = 9f;
        // the warm "develop" tint tracks journey progress, NOT the live camera
        // bounds — so fencing the prologue's bounds can't warm it (or Frames 1-10)
        public float TintMinX = -2f, TintMaxX = 170f;
        const float SmoothTime = 0.12f;

        public void SetBounds(float minX, float maxX, float minY, float maxY)
        { MinX = minX; MaxX = maxX; MinY = minY; MaxY = maxY; }

        /// Scripted moments (the Room 9 drop) stretch the follow so the
        /// camera visibly fails to keep up. 1 = normal.
        public float LagScale = 1f;

        Vector3 _vel;
        Camera _cam;

        void Awake() { _cam = GetComponent<Camera>(); }

        void LateUpdate()
        {
            if (Target == null) return;
            var desired = new Vector3(Target.position.x, Target.position.y, transform.position.z);
            var p = Vector3.SmoothDamp(transform.position, desired, ref _vel, SmoothTime * LagScale);
            p.x = Mathf.Clamp(p.x, MinX, MaxX);
            p.y = Mathf.Clamp(p.y, MinY, MaxY);
            transform.position = p;

            // the print "develops" toward a faint warm tint as x increases
            if (_cam != null)
                _cam.backgroundColor = Color.Lerp(
                    VisualFactory.Background, VisualFactory.BackgroundWarm,
                    Mathf.Clamp01((p.x - TintMinX) / (TintMaxX - TintMinX)));
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
