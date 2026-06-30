using UnityEngine;

namespace Darkroom
{
    /// Animates one distant background silhouette figure: a slow body bob (breathing /
    /// work weight) and, for a worker, an arm swinging from the shoulder. When the figure
    /// is an authored SpriteSkin rig prefab instead of the code puppet, its Animator owns
    /// the motion and this does nothing (SetRigged).
    ///
    /// Transform-only: the per-exposure alpha fade for the whole "Figures" band is owned by
    /// BackdropTint (these figures read as black blobs on the bright Over wall, so Over
    /// drops them), so RigActor never touches colour/alpha and the two can't fight.
    /// The technique mirrors PlayerAnimator's squash and ScriptedBlackout's glint sway.
    public class RigActor : MonoBehaviour
    {
        public Transform body;       // bobbed up/down a hair
        public Transform armJoint;   // swung from the shoulder (worker only)
        public float speed = 1.0f;
        public float armBaseDeg = 0f;
        public float armAmpDeg = 16f;
        public float bobAmp = 0.025f;

        Vector3 _bodyBase;
        float _phase;
        bool _rigged;

        /// Mark this figure as a SpriteSkin rig prefab — its Animator drives the motion.
        public void SetRigged() { _rigged = true; }

        void Start()
        {
            if (body != null) _bodyBase = body.localPosition;
            // deterministic per-figure phase (by world x) so figures don't move in lockstep
            _phase = Mathf.Repeat(transform.position.x * 1.37f, 6.2832f);
        }

        void Update()
        {
            if (_rigged || PauseController.IsPaused) return;
            float t = Time.time * speed + _phase;
            if (armJoint != null)
                armJoint.localRotation = Quaternion.Euler(0f, 0f, armBaseDeg + Mathf.Sin(t) * armAmpDeg);
            if (body != null)
                body.localPosition = _bodyBase + Vector3.up * (Mathf.Sin(t * 0.7f) * bobAmp);
        }
    }
}
