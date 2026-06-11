using UnityEngine;

namespace Darkroom
{
    /// Monospace UI font for the camera-viewfinder look.
    /// Priority: bundled font (Resources/Fonts/Mono) -> OS Menlo -> LegacyRuntime.
    public static class FontLoader
    {
        static Font _mono;

        public static Font Mono
        {
            get
            {
                if (_mono != null) return _mono;
                _mono = Resources.Load<Font>("Fonts/Mono");
                if (_mono == null)
                {
                    try { _mono = Font.CreateDynamicFontFromOSFont("Menlo", 24); }
                    catch { _mono = null; }
                }
                if (_mono == null)
                    _mono = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _mono;
            }
        }
    }
}
