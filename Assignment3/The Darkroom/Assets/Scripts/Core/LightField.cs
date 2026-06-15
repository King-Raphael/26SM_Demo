using System;
using System.Collections.Generic;
using UnityEngine;

namespace Darkroom
{
    /// A gameplay-only model of "how much light reaches a world point".
    /// Deliberately NOT a read of URP Light2D: the hundreds of decorative
    /// backdrop lamps and the player's vanity glow would pollute puzzle math,
    /// and FindObjectsByType per frame is unaffordable. Only emitters that
    /// should COUNT for puzzles register here — drawn strokes (the light you
    /// deliver), the player's own faint safelight, and any designated lamp.
    ///
    /// Mirrors ExposureManager's Instance + List + Register/Unregister shape.
    /// Consumers (LightSensor, UmbralBarrier) probe SampleAt in FixedUpdate.
    public class LightField : MonoBehaviour
    {
        public static LightField Instance { get; private set; }

        struct Emitter
        {
            public Transform key;             // identity for dedupe / removal
            public float radius;              // beyond this, contributes nothing
            public Func<float> intensity;     // live getter — stroke light fades
            public Func<Vector2, float> dist; // optional precise distance (strokes test per-segment)
        }

        readonly List<Emitter> _emitters = new List<Emitter>();

        void Awake() { Instance = this; }

        /// Register an emitter. `dist` is optional: when null the emitter is a
        /// point at key.position; a stroke passes its per-segment nearest-
        /// distance so a long stroke lights along its whole line rather than
        /// from one inflated centre-radius.
        public void Register(Transform key, float radius, Func<float> intensity,
                             Func<Vector2, float> dist = null)
        {
            if (key == null || intensity == null) return;
            for (int i = 0; i < _emitters.Count; i++)
                if (_emitters[i].key == key) return; // no duplicates
            _emitters.Add(new Emitter { key = key, radius = radius, intensity = intensity, dist = dist });
        }

        public void Unregister(Transform key)
        {
            for (int i = _emitters.Count - 1; i >= 0; i--)
                if (_emitters[i].key == null || _emitters[i].key == key) _emitters.RemoveAt(i);
        }

        /// Summed light at p. Each emitter adds intensity * linear falloff
        /// (1 at the source, 0 at its radius). Linear, not URP's curve, so the
        /// values stay predictable for hand-authoring thresholds.
        public float SampleAt(Vector2 p)
        {
            float sum = 0f;
            for (int i = _emitters.Count - 1; i >= 0; i--)
            {
                var e = _emitters[i];
                if (e.key == null) { _emitters.RemoveAt(i); continue; } // self-heal stale
                if (e.radius <= 0f) continue;
                float d = e.dist != null ? e.dist(p) : Vector2.Distance(p, (Vector2)e.key.position);
                if (d >= e.radius) continue;
                sum += e.intensity() * (1f - d / e.radius);
            }
            return sum;
        }

        public bool IsLit(Vector2 p, float threshold) => SampleAt(p) >= threshold;
    }
}
