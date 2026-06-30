using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkroom
{
    /// Attaches a runtime ShadowCaster2D to a solid box so the lamps throw a real shadow
    /// from it. In the Editor URP derives the shape from the attached Collider2D; we ALSO
    /// write the exact box path via reflection (a graceful no-op if the private field ever
    /// moves), so the shape is correct in builds and even when the renderer sits on a child
    /// (the player). CastShadow (not self-shadow) keeps a platform from darkening its own
    /// lit top lip. Everything stays headless — no editor-authored shape.
    public static class ShadowFactory
    {
        static FieldInfo _pathField, _hashField;
        static bool _looked;

        public static ShadowCaster2D AddBoxCaster(GameObject go, Vector2 size)
        {
            var sc = go.AddComponent<ShadowCaster2D>();
            sc.castingOption = ShadowCaster2D.ShadowCastingOptions.CastShadow;

            if (!_looked)
            {
                _looked = true;
                var tp = typeof(ShadowCaster2D);
                _pathField = tp.GetField("m_ShapePath", BindingFlags.NonPublic | BindingFlags.Instance);
                _hashField = tp.GetField("m_ShapePathHash", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            if (_pathField != null)
            {
                float hw = size.x * 0.5f, hh = size.y * 0.5f;
                _pathField.SetValue(sc, new Vector3[]
                {
                    new Vector3(-hw, -hh, 0f),
                    new Vector3(-hw,  hh, 0f),
                    new Vector3( hw,  hh, 0f),
                    new Vector3( hw, -hh, 0f),
                });
                // a fresh hash so ShadowCaster2D.Update() rebuilds the mesh from this path
                if (_hashField != null)
                {
                    int h = ((int)(size.x * 1000f) * 73856093) ^ ((int)(size.y * 1000f) * 19349663);
                    _hashField.SetValue(sc, h == 0 ? 1 : h);
                }
            }
            return sc;
        }
    }
}
