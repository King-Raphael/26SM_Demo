using UnityEngine;

namespace Darkroom
{
    /// UI fonts. Mono keeps the camera-viewfinder readout identity; Display is an
    /// elegant face reserved for the full-screen narrative beats (title, cards,
    /// "DEVELOPED"), so the technical HUD and the story type read as two voices.
    public static class FontLoader
    {
        static Font _mono;
        static Font _display;

        /// Priority: bundled font (Resources/Fonts/Mono) -> OS Menlo -> LegacyRuntime.
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

        /// Cinematic display face. Priority: bundled (Resources/Fonts/Display) ->
        /// an elegant OS face (Optima/Didot/Palatino…, as used in film titling) ->
        /// Mono. Bundle a Display.ttf to make it deterministic across machines.
        public static Font Display
        {
            get
            {
                if (_display != null) return _display;
                _display = Resources.Load<Font>("Fonts/Display");
                if (_display == null)
                {
                    string[] faces = { "Optima", "Didot", "Palatino", "Palatino Linotype", "Georgia", "Baskerville" };
                    foreach (var f in faces)
                    {
                        try { _display = Font.CreateDynamicFontFromOSFont(f, 48); }
                        catch { _display = null; }
                        if (_display != null) break;
                    }
                }
                if (_display == null) _display = Mono;
                return _display;
            }
        }

        static Font _title;

        /// Title face for the room name — a cursive/script hand so the LEVEL NAME
        /// reads as a title, not UI. Priority: bundled (Resources/Fonts/Title) -> a
        /// script OS face (Snell Roundhand / Apple Chancery / Segoe Script…) -> Display.
        public static Font Title
        {
            get
            {
                if (_title != null) return _title;
                _title = Resources.Load<Font>("Fonts/Title");
                if (_title == null)
                {
                    string[] faces = { "Snell Roundhand", "Apple Chancery", "Zapfino", "Segoe Script", "Brush Script MT", "Bradley Hand ITC" };
                    foreach (var f in faces)
                    {
                        try { _title = Font.CreateDynamicFontFromOSFont(f, 48); }
                        catch { _title = null; }
                        if (_title != null) break;
                    }
                }
                if (_title == null) _title = Display;
                return _title;
            }
        }
    }
}
