using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Darkroom
{
    /// Code-built UGUI canvas: exposure overlay, film-strip HUD (3 frames,
    /// lock glyph, key labels), stroke-budget dots, hint line, banner,
    /// respawn fade and the 1-frame switch flash.
    public class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }

        public Transform CanvasRoot { get; private set; }

        Image _overlay, _whiteFlash, _blackFade;
        RawImage _grain, _vignette;
        Texture2D[] _grainTex;
        Text _timerText;
        RectTransform _strip;
        Vector2 _stripBasePos;
        readonly Image[] _frameBorders = new Image[3];
        readonly Image[] _frameInners = new Image[3];
        readonly Color[] _frameColors = new Color[3];
        GameObject _lockGlyph;
        readonly Image[] _dots = new Image[3];
        Text _hintText, _bannerText, _checkpointText;

        object _hintKey;
        Coroutine _overlayCo, _shakeCo, _bannerCo, _hintHideCo, _checkpointCo, _flashCo;

        static readonly Color BorderDim = new Color(0.23f, 0.23f, 0.23f, 1f);
        static readonly Color DotBright = VisualFactory.BrightStroke;
        static readonly Color DotDim = new Color(0.23f, 0.23f, 0.23f, 1f);
        // Real 2D lighting now does most of the look; the overlay only tints.
        static readonly Color OverlayUnder = new Color(0.02f, 0.04f, 0.10f, 0.34f);
        static readonly Color OverlayNormal = new Color(0f, 0f, 0f, 0f);
        static readonly Color OverlayOver = new Color(1f, 0.97f, 0.88f, 0.28f);

        public static HUDController Build()
        {
            var go = new GameObject("_HUD");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = go.AddComponent<HUDController>();
            Instance = hud;
            hud.CanvasRoot = go.transform;
            hud.BuildUI();
            if (ExposureManager.Instance != null)
                ExposureManager.Instance.OnExposureChanged += hud.HandleExposureChanged;
            return hud;
        }

        void OnDestroy()
        {
            if (ExposureManager.Instance != null)
                ExposureManager.Instance.OnExposureChanged -= HandleExposureChanged;
        }

        // ---------- shared UI helpers (also used by WinScreen) ----------

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image NewImage(string name, Transform parent, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text NewText(string name, Transform parent, string content, int size, Color color, TextAnchor align)
        {
            var rt = NewRect(name, parent);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = content;
            txt.fontSize = size;
            txt.color = color;
            txt.alignment = align;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ---------- construction ----------

        void BuildUI()
        {
            // exposure overlay (bottom of the stack)
            _overlay = NewImage("ExposureOverlay", CanvasRoot, OverlayNormal);
            Stretch(_overlay.rectTransform);

            // faint film grain (stretch #3): flickering tiled noise
            _grainTex = new Texture2D[3];
            for (int g = 0; g < 3; g++) _grainTex[g] = MakeGrainTexture(g);
            var grainRT = NewRect("FilmGrain", CanvasRoot);
            Stretch(grainRT);
            _grain = grainRT.gameObject.AddComponent<RawImage>();
            _grain.texture = _grainTex[0];
            _grain.color = new Color(1f, 1f, 1f, 0.05f);
            _grain.raycastTarget = false;
            _grain.uvRect = new Rect(0f, 0f, 15f, 8.44f);
            StartCoroutine(GrainFlicker());

            // radial vignette, fades in while Underexposed
            var vigRT = NewRect("Vignette", CanvasRoot);
            Stretch(vigRT);
            _vignette = vigRT.gameObject.AddComponent<RawImage>();
            _vignette.texture = MakeVignetteTexture();
            _vignette.color = new Color(1f, 1f, 1f, 0f);
            _vignette.raycastTarget = false;

            // film strip
            _strip = NewRect("FilmStrip", CanvasRoot);
            Place(_strip, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(330f, 150f));
            _stripBasePos = _strip.anchoredPosition;

            // film backing + sprocket holes
            var backing = NewImage("FilmBacking", _strip, new Color(0.055f, 0.055f, 0.055f, 0.92f));
            Place(backing.rectTransform, new Vector2(0f, 1f), new Vector2(-12f, 12f), new Vector2(336f, 92f));
            for (int i = 0; i < 7; i++)
            {
                var hTop = NewImage("SprocketT" + i, _strip, new Color(0.14f, 0.14f, 0.14f, 1f));
                Place(hTop.rectTransform, new Vector2(0f, 1f), new Vector2(-2f + i * 52f, 6f), new Vector2(12f, 9f));
                var hBot = NewImage("SprocketB" + i, _strip, new Color(0.14f, 0.14f, 0.14f, 1f));
                Place(hBot.rectTransform, new Vector2(0f, 1f), new Vector2(-2f + i * 52f, -71f), new Vector2(12f, 9f));
            }

            _frameColors[0] = new Color(0.10f, 0.13f, 0.25f, 1f);  // Under preview
            _frameColors[1] = new Color(0.55f, 0.55f, 0.55f, 1f);  // Normal preview
            _frameColors[2] = new Color(1f, 0.95f, 0.85f, 1f);     // Over preview
            string[] keys = { "1", "2", "3" };
            for (int i = 0; i < 3; i++)
            {
                var border = NewImage("Frame" + i, _strip, BorderDim);
                Place(border.rectTransform, new Vector2(0f, 1f), new Vector2(i * 108f, 0f), new Vector2(96f, 68f));
                _frameBorders[i] = border;

                var inner = NewImage("Inner", border.transform, _frameColors[i]);
                Place(inner.rectTransform, new Vector2(0f, 1f), new Vector2(4f, -4f), new Vector2(88f, 60f));
                _frameInners[i] = inner;

                var label = NewText("Key" + i, _strip, keys[i], 20, new Color(0.73f, 0.73f, 0.73f, 1f), TextAnchor.MiddleCenter);
                Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(i * 108f + 28f, -70f), new Vector2(40f, 26f));
            }

            // lock glyph on the Over frame (built from rects; font has no padlock)
            var glyph = NewRect("Lock", _frameBorders[2].transform);
            Place(glyph, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            var body = NewImage("Body", glyph, new Color(0.07f, 0.07f, 0.07f, 0.95f));
            Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), new Vector2(22f, 16f));
            var shackle = NewImage("Shackle", glyph, new Color(0.07f, 0.07f, 0.07f, 0.95f));
            Place(shackle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(14f, 12f));
            _lockGlyph = glyph.gameObject;

            // stroke-budget dots
            for (int i = 0; i < 3; i++)
            {
                var dot = NewImage("Dot" + i, _strip, DotBright);
                Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(8f + i * 24f, -104f), new Vector2(14f, 14f));
                _dots[i] = dot;
            }

            // hint line, banner, checkpoint text
            _hintText = NewText("Hint", CanvasRoot, "", 26, new Color(0.88f, 0.88f, 0.88f, 1f), TextAnchor.MiddleCenter);
            Place(_hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1400f, 90f));

            _bannerText = NewText("Banner", CanvasRoot, "", 30, VisualFactory.BrightStroke, TextAnchor.MiddleCenter);
            Place(_bannerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1500f, 60f));

            _checkpointText = NewText("CheckpointFlash", CanvasRoot, "", 22, new Color(0.81f, 0.81f, 0.81f, 1f), TextAnchor.MiddleCenter);
            Place(_checkpointText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(800f, 40f));

            // replay timer (stretch #5): hidden until the first win
            _timerText = NewText("ReplayTimer", CanvasRoot, "", 24, new Color(0.73f, 0.73f, 0.73f, 1f), TextAnchor.MiddleRight);
            Place(_timerText.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(220f, 36f));

            // respawn fade + switch flash (topmost)
            _blackFade = NewImage("BlackFade", CanvasRoot, new Color(0f, 0f, 0f, 0f));
            Stretch(_blackFade.rectTransform);
            _whiteFlash = NewImage("WhiteFlash", CanvasRoot, new Color(1f, 1f, 1f, 0f));
            Stretch(_whiteFlash.rectTransform);

            ApplyLockVisual(false);
            HighlightFrame(Exposure.Normal);
            StartCoroutine(TitleCard());
        }

        IEnumerator TitleCard()
        {
            var title = NewText("Title", CanvasRoot, "THE DARKROOM", 72,
                new Color(0.95f, 0.93f, 0.88f, 1f), TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1400f, 110f));
            var sub = NewText("TitleSub", CanvasRoot, "develop the world — fix the light", 26,
                new Color(0.62f, 0.62f, 0.60f, 1f), TextAnchor.MiddleCenter);
            Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(1000f, 40f));

            yield return new WaitForSeconds(2.0f);
            float t = 0f;
            while (t < 1.6f)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / 1.6f));
                var c1 = title.color; c1.a = a; title.color = c1;
                var c2 = sub.color; c2.a = a; sub.color = c2;
                yield return null;
            }
            Destroy(title.gameObject);
            Destroy(sub.gameObject);
        }

        Texture2D MakeVignetteTexture()
        {
            int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - 127.5f) / 127.5f;
                    float dy = (y - 127.5f) / 127.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.5f) / 0.55f));
                    px[y * n + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        Texture2D MakeGrainTexture(int seed)
        {
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point;
            var rng = new System.Random(7000 + seed);
            var px = new Color32[128 * 128];
            for (int i = 0; i < px.Length; i++)
            {
                // mostly transparent, a few bright specks
                float a = Mathf.Pow((float)rng.NextDouble(), 7f);
                px[i] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        IEnumerator GrainFlicker()
        {
            var wait = new WaitForSeconds(0.09f);
            int i = 0;
            while (true)
            {
                i = (i + 1) % _grainTex.Length;
                _grain.texture = _grainTex[i];
                _grain.uvRect = new Rect(Random.value, Random.value, 15f, 8.44f);
                yield return wait;
            }
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || _timerText == null) return;
            if (gm.HasEverWon && !gm.HasWon) _timerText.text = FormatTime(gm.RunTime);
            else if (!gm.HasEverWon) _timerText.text = "";
        }

        public static string FormatTime(float t)
        {
            int m = (int)(t / 60f);
            return m + ":" + (t - m * 60f).ToString("00.0");
        }

        // ---------- exposure feedback ----------

        void HandleExposureChanged(Exposure e)
        {
            HighlightFrame(e);
            Color target = e == Exposure.Underexposed ? OverlayUnder
                         : e == Exposure.Overexposed ? OverlayOver : OverlayNormal;
            float vignette = e == Exposure.Underexposed ? 0.55f : 0f;
            if (_overlayCo != null) StopCoroutine(_overlayCo);
            _overlayCo = StartCoroutine(LerpOverlay(target, vignette, 0.15f));
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(OneFrameFlash());
        }

        void HighlightFrame(Exposure e)
        {
            for (int i = 0; i < 3; i++)
                _frameBorders[i].color = (int)e == i ? VisualFactory.SafelightRed : BorderDim;
        }

        IEnumerator LerpOverlay(Color target, float vignetteTarget, float dur)
        {
            Color start = _overlay.color;
            float vStart = _vignette.color.a;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                _overlay.color = Color.Lerp(start, target, k);
                _vignette.color = new Color(1f, 1f, 1f, Mathf.Lerp(vStart, vignetteTarget, k));
                yield return null;
            }
            _overlay.color = target;
            _vignette.color = new Color(1f, 1f, 1f, vignetteTarget);
        }

        IEnumerator OneFrameFlash()
        {
            _whiteFlash.color = new Color(1f, 1f, 1f, 0.8f);
            yield return null;
            _whiteFlash.color = new Color(1f, 1f, 1f, 0f);
        }

        /// Jam: film strip shakes 0.15 s (refused switch).
        public void JamFeedback()
        {
            if (_shakeCo != null) StopCoroutine(_shakeCo);
            _shakeCo = StartCoroutine(ShakeStrip());
        }

        IEnumerator ShakeStrip()
        {
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                _strip.anchoredPosition = _stripBasePos + Random.insideUnitCircle * 6f;
                yield return null;
            }
            _strip.anchoredPosition = _stripBasePos;
        }

        // ---------- abilities ----------

        public void OnAbilityUnlocked(Ability a)
        {
            if (a == Ability.Flash)
            {
                ApplyLockVisual(true);
                ShowBanner("FLASH ACQUIRED — press 3: OVEREXPOSED.");
                StartCoroutine(PunchScale(_frameBorders[2].rectTransform));
            }
            else
            {
                ShowBanner("SHUTTER ACQUIRED — hold SHIFT in UNDER or OVER to draw light. Release to fix it.");
                for (int i = 0; i < 3; i++) StartCoroutine(PunchScale(_dots[i].rectTransform));
            }
            StartCoroutine(FullFlashRoutine(0.2f));
        }

        void ApplyLockVisual(bool unlocked)
        {
            _lockGlyph.SetActive(!unlocked);
            _frameInners[2].color = unlocked ? _frameColors[2] : _frameColors[2] * new Color(0.3f, 0.3f, 0.3f, 1f);
        }

        IEnumerator PunchScale(RectTransform rt)
        {
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float s = 1f + 0.3f * Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        // ---------- strokes ----------

        public void SetStrokeDots(int remaining)
        {
            for (int i = 0; i < 3; i++)
                _dots[i].color = i < remaining ? DotBright : DotDim;
        }

        // ---------- hints / banner / checkpoint ----------

        public void ShowHint(string text, object key)
        {
            _hintKey = key;
            if (_hintHideCo != null) { StopCoroutine(_hintHideCo); _hintHideCo = null; }
            _hintText.text = text;
        }

        public void OnHintExit(object key)
        {
            if (_hintKey != key) return;
            if (_hintHideCo != null) StopCoroutine(_hintHideCo);
            _hintHideCo = StartCoroutine(HideHintAfter(6f, key));
        }

        IEnumerator HideHintAfter(float delay, object key)
        {
            yield return new WaitForSeconds(delay);
            if (_hintKey == key) { _hintText.text = ""; _hintKey = null; }
            _hintHideCo = null;
        }

        public void ShowBanner(string text)
        {
            if (_bannerCo != null) StopCoroutine(_bannerCo);
            _bannerCo = StartCoroutine(BannerRoutine(text));
        }

        IEnumerator BannerRoutine(string text)
        {
            _bannerText.text = text;
            yield return new WaitForSeconds(4f);
            _bannerText.text = "";
        }

        public void CheckpointFlash()
        {
            if (_checkpointCo != null) StopCoroutine(_checkpointCo);
            _checkpointCo = StartCoroutine(CheckpointRoutine());
        }

        IEnumerator CheckpointRoutine()
        {
            _checkpointText.text = "CHECKPOINT DEVELOPED";
            yield return new WaitForSeconds(1.6f);
            _checkpointText.text = "";
        }

        // ---------- fades ----------

        public Coroutine FadeBlack(bool toBlack, float dur)
        {
            return StartCoroutine(FadeBlackRoutine(toBlack, dur));
        }

        IEnumerator FadeBlackRoutine(bool toBlack, float dur)
        {
            float start = _blackFade.color.a;
            float end = toBlack ? 1f : 0f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                _blackFade.color = new Color(0f, 0f, 0f, Mathf.Lerp(start, end, Mathf.Clamp01(t / dur)));
                yield return null;
            }
            _blackFade.color = new Color(0f, 0f, 0f, end);
        }

        IEnumerator FullFlashRoutine(float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                _whiteFlash.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.9f, 0f, Mathf.Clamp01(t / dur)));
                yield return null;
            }
            _whiteFlash.color = new Color(1f, 1f, 1f, 0f);
        }

        // ---------- restart ----------

        public void ResetForRestart()
        {
            var win = CanvasRoot.Find("WinScreen");
            if (win != null) Destroy(win.gameObject);
            ApplyLockVisual(false);
            SetStrokeDots(3);
            HighlightFrame(Exposure.Normal);
            _overlay.color = OverlayNormal;
            _vignette.color = new Color(1f, 1f, 1f, 0f);
            _hintText.text = "";
            _bannerText.text = "";
            _checkpointText.text = "";
            _blackFade.color = new Color(0f, 0f, 0f, 0f);
            _whiteFlash.color = new Color(1f, 1f, 1f, 0f);
            _hintKey = null;
        }
    }
}
