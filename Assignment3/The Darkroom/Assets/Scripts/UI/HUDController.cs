using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Darkroom
{
    /// Camera-viewfinder HUD (concept-art restyle): exposure slider with state
    /// badge, room title + objectives, progressive control hints, tutorial-only
    /// exposure card, world-anchored hint bubbles, and a viewfinder frame +
    /// blinking REC that appear only while the shutter is open (drawing).
    public class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }

        public Transform CanvasRoot { get; private set; }

        // full-screen layers (film grain + vignette now live in the URP post stack,
        // driven by PostFXDirector — the HUD only keeps the instant transition layers)
        Image _overlay, _whiteFlash, _blackFade;

        // exposure slider
        RectTransform _sliderGroup, _knob;
        Vector2 _sliderBasePos;
        readonly Text[] _stateLabels = new Text[3];
        GameObject _lockUnder, _lockOver;
        static readonly float[] KnobSlots = { -120f, 0f, 120f };

        // trails budget
        GameObject _trailsGroup;
        readonly Image[] _trailTicks = new Image[3];

        // top-left room info (objectives fade out after room entry)
        Text _roomTitle;
        readonly Text[] _objLines = new Text[2];
        CanvasGroup _objGroup;
        Coroutine _objCo;
        int _shownRoom = -1;

        // top-right controls (tutorial only)
        Text _controlsText;
        CanvasGroup _controlsGroup;
        bool _controlsGone;
        bool _prologueControls = true; // prologue HUD shows ONLY move/jump until Frame 1

        // bottom exposure card (tutorial only)
        CanvasGroup _cardGroup;
        Text _cardTitle, _cardBody;
        Coroutine _cardCo;

        // viewfinder + REC (shutter open only)
        CanvasGroup _shutterGroup;
        Image _recDot;

        // world-anchored hint bubble
        RectTransform _bubble;
        Text _bubbleText;
        Transform _bubbleAnchor;
        object _hintKey;
        Coroutine _hintHideCo, _hintDeferCo;
        readonly HashSet<object> _hintsHeard = new HashSet<object>(); // pop once per prompt

        // misc
        Text _timerText, _mutedText, _checkpointText, _checkpointCaption, _bannerText, _deathText, _jamText;
        GameObject _bannerBox, _pausePanel;
        bool _jamNoteShown;
        Coroutine _overlayCo, _shakeCo, _bannerCo, _checkpointCo, _flashCo, _knobCo, _shutterCo, _controlsCo, _deathCo, _jamCo;

        // cinematic flow: letterbox bars, boot fade-up, per-frame entry card
        RectTransform _barTop, _barBottom;
        Text _frameCard;
        CanvasGroup _bubbleGroup;
        bool _firstRoomShown;
        Coroutine _letterboxCo, _frameCardCo, _bubbleFadeCo;

        // Exposure tinting now lives in the URP post grade (PostFXDirector), which
        // tints WITHOUT flattening the frame. These full-screen HUD washes sat on
        // TOP of post (UI overlay) and lifted everything to a flat haze — kept only
        // as a faint nudge.
        static readonly Color OverlayUnder = new Color(0.02f, 0.04f, 0.10f, 0.10f);
        static readonly Color OverlayNormal = new Color(0f, 0f, 0f, 0f);
        static readonly Color OverlayOver = new Color(1f, 0.95f, 0.84f, 0.0f);
        // The switch flash echoes its DESTINATION (same palette the overlay tint
        // and the audio pitch encode): cool for Under, warm for Over, white for Normal.
        static readonly Color FlashUnder = new Color(0.85f, 0.88f, 1f);
        static readonly Color FlashNormal = new Color(1f, 1f, 1f);
        static readonly Color FlashOver = new Color(1f, 0.98f, 0.92f);
        static readonly Color TextBright = new Color(0.93f, 0.93f, 0.91f, 1f);
        static readonly Color TextDim = new Color(0.45f, 0.45f, 0.45f, 1f);
        static readonly Color RecRed = new Color(0.85f, 0.15f, 0.13f, 1f);
        static readonly Color PanelBg = new Color(0.04f, 0.04f, 0.045f, 0.86f);
        static readonly Color PanelBorder = new Color(0.85f, 0.85f, 0.83f, 0.55f);

        static readonly string[] BadgeNames = { "UNDEREXPOSED", "NORMAL", "OVEREXPOSED" };
        static readonly string[] CardBodies =
        {
            "Hidden paths emerge from the dark.\nDark trails are solid only here.",
            "Stable and readable.\nNo trails can be drawn.",
            "Light burns through white barriers\nand wakes what sleeps.",
        };

        // Darkroom-action vocabulary for the first minute (before the Flash pickup):
        // the player is operating the room's light, not switching abstract "modes".
        static readonly string[] ActionLabels = { "SAFELIGHT", "WORK LIGHT", "ENLARGER FLASH" };
        static readonly string[] ExposureLabels = { "UNDER", "BALANCED", "OVER" };
        static readonly string[] ActionBadges = { "SAFELIGHT", "WORK LIGHT", "ENLARGER FLASH" };
        static readonly string[] ActionBodies =
        {
            "The print develops in the dark.\nHidden paths surface under safelight.",
            "Raise the work light to read the room.\nThe real floor is always here.",
            "A flash of the enlarger —\nlight burns, and wakes what sleeps.",
        };
        bool _exposureVocab; // false = darkroom-action labels (prologue / before Flash)

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

        /// display: use the cinematic face (FontLoader.Display) for narrative beats;
        /// Mono (the camera-readout voice) otherwise. shadow: add a soft drop shadow
        /// so titles read against the now-bloomed scene.
        public static Text NewText(string name, Transform parent, string content, int size, Color color, TextAnchor align, bool display = false, bool shadow = false)
        {
            var rt = NewRect(name, parent);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.font = display ? FontLoader.Display : FontLoader.Mono;
            txt.text = content;
            txt.fontSize = size;
            txt.color = color;
            txt.alignment = align;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            if (shadow)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.55f);
                sh.effectDistance = new Vector2(1.5f, -1.5f);
            }
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

        /// 1.5px frame along all four edges of a rect.
        public static void AddBorder(RectTransform rt, Color c)
        {
            const float t = 1.5f;
            var top = NewImage("BorderT", rt, c);
            top.rectTransform.anchorMin = new Vector2(0f, 1f);
            top.rectTransform.anchorMax = new Vector2(1f, 1f);
            top.rectTransform.pivot = new Vector2(0.5f, 1f);
            top.rectTransform.sizeDelta = new Vector2(0f, t);
            top.rectTransform.anchoredPosition = Vector2.zero;
            var bot = NewImage("BorderB", rt, c);
            bot.rectTransform.anchorMin = new Vector2(0f, 0f);
            bot.rectTransform.anchorMax = new Vector2(1f, 0f);
            bot.rectTransform.pivot = new Vector2(0.5f, 0f);
            bot.rectTransform.sizeDelta = new Vector2(0f, t);
            bot.rectTransform.anchoredPosition = Vector2.zero;
            var left = NewImage("BorderL", rt, c);
            left.rectTransform.anchorMin = new Vector2(0f, 0f);
            left.rectTransform.anchorMax = new Vector2(0f, 1f);
            left.rectTransform.pivot = new Vector2(0f, 0.5f);
            left.rectTransform.sizeDelta = new Vector2(t, 0f);
            left.rectTransform.anchoredPosition = Vector2.zero;
            var right = NewImage("BorderR", rt, c);
            right.rectTransform.anchorMin = new Vector2(1f, 0f);
            right.rectTransform.anchorMax = new Vector2(1f, 1f);
            right.rectTransform.pivot = new Vector2(1f, 0.5f);
            right.rectTransform.sizeDelta = new Vector2(t, 0f);
            right.rectTransform.anchoredPosition = Vector2.zero;
        }

        // ---------- construction ----------

        void BuildUI()
        {
            _overlay = NewImage("ExposureOverlay", CanvasRoot, OverlayNormal);
            Stretch(_overlay.rectTransform);

            BuildShutterFrame();
            BuildExposureSlider();
            BuildTrailsGroup();
            BuildRoomInfo();
            BuildControlsBlock();
            BuildBubble();
            BuildCard();

            // banner (boxed)
            var bannerRT = NewRect("BannerBox", CanvasRoot);
            Place(bannerRT, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 52f));
            var bannerBg = NewImage("Bg", bannerRT, PanelBg);
            Stretch(bannerBg.rectTransform);
            AddBorder(bannerRT, PanelBorder);
            _bannerText = NewText("Banner", bannerRT, "", 25, new Color(1f, 0.95f, 0.84f, 1f), TextAnchor.MiddleCenter, display: true);
            Stretch(_bannerText.rectTransform);
            _bannerBox = bannerRT.gameObject;
            _bannerBox.SetActive(false);

            _checkpointText = NewText("CheckpointFlash", CanvasRoot, "", 23, new Color(0.86f, 0.86f, 0.84f, 1f), TextAnchor.MiddleCenter, display: true);
            Place(_checkpointText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(800f, 40f));

            _checkpointCaption = NewText("CheckpointCaption", CanvasRoot, "", 19, new Color(0.62f, 0.62f, 0.60f, 1f), TextAnchor.MiddleCenter, display: true);
            Place(_checkpointCaption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(900f, 28f));

            _deathText = NewText("DeathNote", CanvasRoot, "", 22, RecRed, TextAnchor.MiddleCenter, display: true);
            Place(_deathText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 244f), new Vector2(900f, 36f));

            _jamText = NewText("JamNote", CanvasRoot, "", 18, new Color(0.62f, 0.62f, 0.60f, 1f), TextAnchor.MiddleCenter);
            Place(_jamText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(600f, 26f));

            _timerText = NewText("ReplayTimer", CanvasRoot, "", 24, new Color(0.73f, 0.73f, 0.73f, 1f), TextAnchor.MiddleRight);
            Place(_timerText.rectTransform, new Vector2(1f, 1f), new Vector2(-36f, -176f), new Vector2(220f, 36f));

            _mutedText = NewText("Muted", CanvasRoot, "", 20, new Color(0.55f, 0.55f, 0.55f, 1f), TextAnchor.MiddleRight);
            Place(_mutedText.rectTransform, new Vector2(1f, 1f), new Vector2(-36f, -210f), new Vector2(160f, 28f));

            BuildPausePanel();

            // cinematic letterbox bars (retracted by default; used at boot + finale)
            _barTop = NewImage("LetterboxTop", CanvasRoot, Color.black).rectTransform;
            _barTop.anchorMin = new Vector2(0f, 1f); _barTop.anchorMax = new Vector2(1f, 1f);
            _barTop.pivot = new Vector2(0.5f, 1f);
            _barTop.sizeDelta = new Vector2(0f, 0f); _barTop.anchoredPosition = Vector2.zero;
            _barBottom = NewImage("LetterboxBottom", CanvasRoot, Color.black).rectTransform;
            _barBottom.anchorMin = new Vector2(0f, 0f); _barBottom.anchorMax = new Vector2(1f, 0f);
            _barBottom.pivot = new Vector2(0.5f, 0f);
            _barBottom.sizeDelta = new Vector2(0f, 0f); _barBottom.anchoredPosition = Vector2.zero;

            // centered "FRAME N" beat shown when crossing into a new frame
            _frameCard = NewText("FrameCard", CanvasRoot, "", 30, new Color(0.92f, 0.90f, 0.85f, 0f), TextAnchor.MiddleCenter, display: true, shadow: true);
            Place(_frameCard.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 188f), new Vector2(1300f, 46f));

            _blackFade = NewImage("BlackFade", CanvasRoot, new Color(0f, 0f, 0f, 0f));
            Stretch(_blackFade.rectTransform);
            _whiteFlash = NewImage("WhiteFlash", CanvasRoot, new Color(1f, 1f, 1f, 0f));
            Stretch(_whiteFlash.rectTransform);

            ApplyLocks();
            HighlightState(Exposure.Normal);
            StartCoroutine(BootIn());
            StartCoroutine(TitleCard());
            StartCoroutine(RecBlink());
        }

        void BuildShutterFrame()
        {
            // viewfinder corners + REC: visible only while drawing (shutter open)
            var rt = NewRect("ShutterFrame", CanvasRoot);
            Stretch(rt);
            _shutterGroup = rt.gameObject.AddComponent<CanvasGroup>();
            _shutterGroup.alpha = 0f;

            var c = new Color(0.92f, 0.92f, 0.90f, 0.8f);
            void Corner(Vector2 anchor, int sx, int sy)
            {
                var h = NewImage("CornerH", rt, c);
                Place(h.rectTransform, anchor, new Vector2(34f * sx, 34f * sy), new Vector2(58f, 4f));
                var v = NewImage("CornerV", rt, c);
                Place(v.rectTransform, anchor, new Vector2(34f * sx, 34f * sy), new Vector2(4f, 58f));
            }
            Corner(new Vector2(0f, 1f), 1, -1);
            Corner(new Vector2(1f, 1f), -1, -1);
            Corner(new Vector2(0f, 0f), 1, 1);
            Corner(new Vector2(1f, 0f), -1, 1);

            _recDot = NewImage("RecDot", rt, RecRed);
            _recDot.sprite = PixelArt.Disc;
            Place(_recDot.rectTransform, new Vector2(0f, 0f), new Vector2(44f, 44f), new Vector2(18f, 18f));
            var recText = NewText("RecText", rt, "REC", 26, RecRed, TextAnchor.MiddleLeft);
            Place(recText.rectTransform, new Vector2(0f, 0f), new Vector2(70f, 34f), new Vector2(120f, 36f));
        }

        void BuildExposureSlider()
        {
            _sliderGroup = NewRect("ExposureSlider", CanvasRoot);
            Place(_sliderGroup, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(460f, 120f));
            _sliderBasePos = _sliderGroup.anchoredPosition;

            var caption = NewText("Caption", _sliderGroup, "EXPOSURE", 22, TextBright, TextAnchor.MiddleCenter);
            Place(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(300f, 26f));

            var track = NewImage("Track", _sliderGroup, new Color(0.23f, 0.23f, 0.25f, 1f));
            Place(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(360f, 6f));

            _knob = NewImage("Knob", _sliderGroup, TextBright).rectTransform;
            ((Image)_knob.GetComponent<Image>()).sprite = PixelArt.Disc;
            Place(_knob, new Vector2(0.5f, 1f), new Vector2(KnobSlots[1], -35f), new Vector2(20f, 20f));

            string[] names = ActionLabels; // darkroom vocab first; swaps after the Flash pickup
            for (int i = 0; i < 3; i++)
            {
                _stateLabels[i] = NewText("State" + i, _sliderGroup, names[i], 20, TextDim, TextAnchor.MiddleCenter);
                Place(_stateLabels[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(KnobSlots[i], -64f), new Vector2(150f, 26f));
            }

            _lockUnder = BuildMiniLock(_sliderGroup, new Vector2(KnobSlots[0] - 62f, -64f));
            _lockOver = BuildMiniLock(_sliderGroup, new Vector2(KnobSlots[2] + 50f, -64f));
            // (no state badge — the highlighted label is enough; the tutorial
            // card spells the state out while it still needs explaining)
        }

        GameObject BuildMiniLock(Transform parent, Vector2 pos)
        {
            var glyph = NewRect("Lock", parent);
            Place(glyph, new Vector2(0.5f, 1f), pos, new Vector2(18f, 20f));
            var body = NewImage("Body", glyph, TextDim);
            Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(14f, 10f));
            var shackle = NewImage("Shackle", glyph, TextDim);
            Place(shackle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(9f, 8f));
            return glyph.gameObject;
        }

        void BuildTrailsGroup()
        {
            var rt = NewRect("TrailsGroup", CanvasRoot);
            Place(rt, new Vector2(0.5f, 1f), new Vector2(310f, -118f), new Vector2(160f, 26f));
            var label = NewText("Label", rt, "TRAILS", 18, TextDim, TextAnchor.MiddleLeft);
            Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(80f, 24f));
            for (int i = 0; i < 3; i++)
            {
                _trailTicks[i] = NewImage("Tick" + i, rt, TextBright);
                Place(_trailTicks[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(86f + i * 20f, 0f), new Vector2(12f, 12f));
            }
            _trailsGroup = rt.gameObject;
            _trailsGroup.SetActive(false);
        }

        void BuildRoomInfo()
        {
            _roomTitle = NewText("RoomTitle", CanvasRoot, "", 24, TextBright, TextAnchor.MiddleLeft);
            Place(_roomTitle.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(800f, 32f));
            var objRT = NewRect("Objectives", CanvasRoot);
            Place(objRT, new Vector2(0f, 1f), new Vector2(42f, -74f), new Vector2(800f, 60f));
            _objGroup = objRT.gameObject.AddComponent<CanvasGroup>();
            for (int i = 0; i < 2; i++)
            {
                _objLines[i] = NewText("Objective" + i, objRT, "", 19, new Color(0.62f, 0.62f, 0.60f, 1f), TextAnchor.MiddleLeft);
                Place(_objLines[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, -i * 28f), new Vector2(800f, 26f));
            }
        }

        Coroutine _titleDevCo;

        /// The room title develops in like a print: two grain-flicker frames,
        /// then a steady fade up.
        IEnumerator RoomTitleDevelop()
        {
            var c = TextBright;
            _roomTitle.color = new Color(c.r, c.g, c.b, 0.35f);
            yield return new WaitForSeconds(0.05f);
            _roomTitle.color = new Color(c.r, c.g, c.b, 0.10f);
            yield return new WaitForSeconds(0.05f);
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                _roomTitle.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.10f, 1f, Mathf.Clamp01(t / 0.6f)));
                yield return null;
            }
            _roomTitle.color = c;
            _titleDevCo = null;
        }

        IEnumerator ObjectivesPeek()
        {
            _objGroup.alpha = 1f;
            yield return new WaitForSeconds(6f);
            yield return FadeGroup(_objGroup, 0f, 1.2f);
            _objCo = null;
        }

        void BuildControlsBlock()
        {
            _controlsText = NewText("Controls", CanvasRoot, "", 17, new Color(0.72f, 0.72f, 0.70f, 1f), TextAnchor.UpperRight);
            _controlsText.lineSpacing = 1.45f;
            Place(_controlsText.rectTransform, new Vector2(1f, 1f), new Vector2(-36f, -36f), new Vector2(480f, 120f));
            _controlsGroup = _controlsText.gameObject.AddComponent<CanvasGroup>();
            _controlsGroup.alpha = 0.65f; // present but quiet
            RebuildControls();
        }

        void BuildBubble()
        {
            _bubble = NewRect("HintBubble", CanvasRoot);
            Place(_bubble, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 84f));
            _bubble.pivot = new Vector2(0.5f, 0f);
            _bubbleGroup = _bubble.gameObject.AddComponent<CanvasGroup>();
            var bg = NewImage("Bg", _bubble, PanelBg);
            Stretch(bg.rectTransform);
            AddBorder(_bubble, PanelBorder);
            // tail: small rotated square poking out of the bottom edge
            var tail = NewImage("Tail", _bubble, PanelBg);
            Place(tail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -7f), new Vector2(16f, 16f));
            tail.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            _bubbleText = NewText("Text", _bubble, "", 21, TextBright, TextAnchor.MiddleCenter, display: true);
            Stretch(_bubbleText.rectTransform);
            _bubbleText.rectTransform.offsetMin = new Vector2(16f, 8f);
            _bubbleText.rectTransform.offsetMax = new Vector2(-16f, -8f);
            _bubble.gameObject.SetActive(false);
        }

        void BuildCard()
        {
            var rt = NewRect("ExposureCard", CanvasRoot);
            Place(rt, new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(620f, 124f));
            var bg = NewImage("Bg", rt, PanelBg);
            Stretch(bg.rectTransform);
            AddBorder(rt, PanelBorder);

            _cardTitle = NewText("Title", rt, "", 24, TextBright, TextAnchor.MiddleLeft);
            Place(_cardTitle.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -14f), new Vector2(480f, 30f));
            _cardBody = NewText("Body", rt, "", 19, new Color(0.66f, 0.66f, 0.64f, 1f), TextAnchor.UpperLeft);
            _cardBody.lineSpacing = 1.35f;
            Place(_cardBody.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -50f), new Vector2(480f, 64f));

            // aperture glyph: disc + 8 rays
            var glyph = NewRect("Aperture", rt);
            Place(glyph, new Vector2(1f, 0.5f), new Vector2(-56f, 0f), new Vector2(72f, 72f));
            var disc = NewImage("Disc", glyph, TextBright);
            disc.sprite = PixelArt.Disc;
            Place(disc.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f;
                var ray = NewImage("Ray" + i, glyph, TextBright);
                Place(ray.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 14f));
                ray.rectTransform.localEulerAngles = new Vector3(0f, 0f, ang);
                float rad = ang * Mathf.Deg2Rad;
                ray.rectTransform.anchoredPosition = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * 26f;
            }

            _cardGroup = rt.gameObject.AddComponent<CanvasGroup>();
            _cardGroup.alpha = 0f;
            rt.gameObject.SetActive(false);
        }

        void BuildPausePanel()
        {
            var rt = NewRect("PausePanel", CanvasRoot);
            Stretch(rt);
            var dim = rt.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            dim.raycastTarget = false;

            var title = NewText("PauseTitle", rt, "PAUSED", 64, TextBright, TextAnchor.MiddleCenter, display: true, shadow: true);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(600f, 90f));

            var help = NewText("PauseHelp", rt,
                "MOVE  A/D or arrows        JUMP  Space\n" +
                "EXPOSURE  1 Under   2 Normal   3 Over        CYCLE  E / Q\n" +
                "DRAW LIGHT  hold Shift, release to fix\n" +
                "RESTART FROM CHECKPOINT  R        MUTE  M\n\n" +
                "ESC  resume",
                24, new Color(0.72f, 0.72f, 0.70f, 1f), TextAnchor.MiddleCenter);
            help.lineSpacing = 1.5f;
            Place(help.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1200f, 300f));

            _pausePanel = rt.gameObject;
            _pausePanel.SetActive(false);
        }

        /// Boot card: only the promise, not the name. A single faint line on the
        /// black, glowing up out of the drips and the fan, dissolving as the room
        /// develops up. The name lands later (TitleDrop), when she steps inside.
        IEnumerator TitleCard()
        {
            var roll = NewText("TitleRoll", CanvasRoot, "one roll. eleven frames.", 24, new Color(0.62f, 0.62f, 0.60f, 0f), TextAnchor.MiddleCenter, display: true);
            Place(roll.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(1100f, 40f));

            // faint: glow up on black, hold, then dissolve as the world fades in
            yield return FadeText(roll, 0f, 0.55f, 1.2f);
            yield return new WaitForSeconds(1.4f);
            yield return FadeText(roll, 0.55f, 0f, 1.4f);
            Destroy(roll.gameObject);
        }

        bool _titleDropped;
        bool _firstUnderHandled;

        /// The game names itself — called when she steps into the photo (the
        /// prologue cinematic), not on the first press of 1.
        public void DropTitle()
        {
            if (_titleDropped) return;
            _titleDropped = true;
            StartCoroutine(TitleDrop());
        }

        public void FullFlash() { StartCoroutine(FullFlashRoutine(0.2f)); }

        /// A brief darkroom key-mapping note in the prologue ("1 — safelight" /
        /// "2 — work light"), surfaced once each by the PrologueDirector.
        public void ShowKeyHint(string text)
        {
            StartCoroutine(KeyHintRoutine(text));
        }

        IEnumerator KeyHintRoutine(string text)
        {
            var t = NewText("KeyHint", CanvasRoot, text, 22, new Color(0.82f, 0.81f, 0.77f, 0f), TextAnchor.MiddleCenter);
            Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(440f, 30f));
            yield return FadeText(t, 0f, 1f, 0.3f);
            yield return new WaitForSeconds(2.2f);
            yield return FadeText(t, 1f, 0f, 0.6f);
            Destroy(t.gameObject);
        }

        /// The game names itself at the moment the player is standing inside it:
        /// THE DARKROOM fades up out of the first real darkness.
        IEnumerator TitleDrop()
        {
            // let the switch itself land first (card, shutter click, grading)
            yield return new WaitForSeconds(1.35f);

            if (_objCo != null) { StopCoroutine(_objCo); _objCo = null; }
            _objGroup.alpha = 0f;

            var title = NewText("Title", CanvasRoot, "THE DARKROOM", 84, new Color(0.95f, 0.93f, 0.88f, 0f), TextAnchor.MiddleCenter, display: true, shadow: true);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1500f, 130f));
            if (AudioDirector.Instance != null) AudioDirector.Instance.NudgeHum(0.14f, 4.5f);

            float t = 0f;
            while (t < 0.9f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.9f);
                var c = title.color; c.a = k; title.color = c;
                if (!_controlsGone) _controlsGroup.alpha = Mathf.Lerp(0.65f, 0.2f, k);
                yield return null;
            }
            yield return new WaitForSeconds(2.2f);
            t = 0f;
            while (t < 1.4f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 1.4f);
                var c = title.color; c.a = 1f - k; title.color = c;
                if (!_controlsGone) _controlsGroup.alpha = Mathf.Lerp(0.2f, 0.65f, k);
                yield return null;
            }
            Destroy(title.gameObject);
        }

        // ---------- per-frame ----------

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return;
            float x = gm.Player.transform.position.x;

            // replay timer
            if (gm.HasEverWon && !gm.HasWon) _timerText.text = FormatTime(gm.RunTime);
            else if (!gm.HasEverWon) _timerText.text = "";

            // room title + objectives
            int room = LevelData.RoomIndexAt(x);
            if (room != _shownRoom)
            {
                int prev = _shownRoom;
                _shownRoom = room;
                if (AudioDirector.Instance != null) AudioDirector.Instance.SetRoomMood(room);
                var def = LevelData.Rooms[room];
                // rooms are frames on the roll — a story clock counting toward
                // the one frame that was never exposed
                // the prologue (room 0) is the blank, UNNUMBERED frame; the journey
                // counts 1..10, and frame 11 (the self-portrait) is taken at the finale.
                _roomTitle.text = room == 0
                    ? def.title
                    : "FRAME " + room + " OF 11 : " + def.title;
                for (int i = 0; i < 2; i++)
                    _objLines[i].text = i < def.objectives.Length ? "○ " + def.objectives[i] : "";
                if (_titleDevCo != null) StopCoroutine(_titleDevCo);
                _titleDevCo = StartCoroutine(RoomTitleDevelop());
                if (_objCo != null) StopCoroutine(_objCo);
                _objCo = StartCoroutine(ObjectivesPeek()); // show, then get out of the way
                // a brief ceremonial frame card on first forward entry (not at boot)
                if (_firstRoomShown && room > prev && room >= 1) ShowFrameCard(room);
                _firstRoomShown = true;
                // she's stepped into the first frame: the full control HUD arrives
                if (_prologueControls && room >= 1) { _prologueControls = false; RebuildControls(); }
            }

            // controls block retires after the drawing lesson (end of Room 5)
            if (!_controlsGone && x > 73f)
            {
                _controlsGone = true;
                if (_controlsCo != null) StopCoroutine(_controlsCo);
                _controlsCo = StartCoroutine(FadeGroup(_controlsGroup, 0f, 1.2f));
            }

            // hint bubble follows its trigger
            if (_bubble.gameObject.activeSelf)
            {
                if (_bubbleAnchor == null) { _bubble.gameObject.SetActive(false); }
                else
                {
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        Vector2 lp;
                        var sp = cam.WorldToScreenPoint(_bubbleAnchor.position + new Vector3(0f, 1.3f, 0f));
                        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)CanvasRoot, sp, null, out lp);
                        var half = ((RectTransform)CanvasRoot).rect.size * 0.5f;
                        lp.x = Mathf.Clamp(lp.x, -half.x + 250f, half.x - 250f);
                        lp.y = Mathf.Clamp(lp.y, -half.y + 60f, half.y - 220f);
                        _bubble.anchoredPosition = lp;
                    }
                }
            }
        }

        // ---------- exposure feedback ----------

        void HandleExposureChanged(Exposure e)
        {
            HighlightState(e);
            Color target = e == Exposure.Underexposed ? OverlayUnder
                         : e == Exposure.Overexposed ? OverlayOver : OverlayNormal;
            if (_overlayCo != null) StopCoroutine(_overlayCo);
            _overlayCo = StartCoroutine(LerpOverlay(target, 0.15f));

            // the first darkness is atmospheric — no white pop. The name (TitleDrop)
            // is withheld until she steps INTO the photo (the prologue cinematic),
            // not announced on the first press of 1.
            bool firstUnder = !_firstUnderHandled && e == Exposure.Underexposed;
            if (firstUnder) _firstUnderHandled = true;
            bool silent = ExposureManager.Instance != null && ExposureManager.Instance.LastChangeSilent;
            if (!silent && !firstUnder)
            {
                if (_flashCo != null) StopCoroutine(_flashCo);
                _flashCo = StartCoroutine(SwitchFlash(e));
            }

            if (_knobCo != null) StopCoroutine(_knobCo);
            _knobCo = StartCoroutine(MoveKnob(KnobSlots[(int)e]));
            ShowCard(e);
        }

        void HighlightState(Exposure e)
        {
            for (int i = 0; i < 3; i++)
                _stateLabels[i].color = (int)e == i ? TextBright : TextDim;
        }

        IEnumerator MoveKnob(float targetX)
        {
            float start = _knob.anchoredPosition.x;
            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.18f));
                _knob.anchoredPosition = new Vector2(Mathf.Lerp(start, targetX, k), _knob.anchoredPosition.y);
                yield return null;
            }
            _knob.anchoredPosition = new Vector2(targetX, _knob.anchoredPosition.y);
        }

        /// Tutorial-only exposure card: 4 s after each switch, before Room 4.
        void ShowCard(Exposure e)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null || gm.HasWon) return;
            if (gm.Player.transform.position.x < 5.5f) return; // the prologue stays note-only
            if (gm.Player.transform.position.x > 42f) return;
            if (_bubble.gameObject.activeSelf) return; // don't stack on a hint bubble
            if (_exposureVocab)
            {
                _cardTitle.text = "EXPOSURE : " + BadgeNames[(int)e];
                _cardBody.text = CardBodies[(int)e];
            }
            else
            {
                _cardTitle.text = ActionBadges[(int)e];
                _cardBody.text = ActionBodies[(int)e];
            }
            if (_cardCo != null) StopCoroutine(_cardCo);
            _cardCo = StartCoroutine(CardRoutine());
        }

        IEnumerator CardRoutine()
        {
            _cardGroup.gameObject.SetActive(true);
            yield return FadeGroup(_cardGroup, 1f, 0.15f);
            yield return new WaitForSeconds(4f);
            yield return FadeGroup(_cardGroup, 0f, 0.35f);
            _cardGroup.gameObject.SetActive(false);
            _cardCo = null;
        }

        IEnumerator FadeGroup(CanvasGroup g, float target, float dur)
        {
            float start = g.alpha;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                g.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / dur));
                yield return null;
            }
            g.alpha = target;
        }

        IEnumerator LerpOverlay(Color target, float dur)
        {
            Color start = _overlay.color;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                _overlay.color = Color.Lerp(start, target, k);
                yield return null;
            }
            _overlay.color = target;
        }

        // A restrained ~0.10s ease-out from a lowered peak, tinted toward the
        // destination state — not the rejected full-screen wash. StopCoroutine on
        // the prior flash (call site) means rapid mashing just holds a faint glow
        // rather than strobing.
        IEnumerator SwitchFlash(Exposure e)
        {
            Color tint = e == Exposure.Underexposed ? FlashUnder
                       : e == Exposure.Overexposed ? FlashOver : FlashNormal;
            const float dur = 0.10f, peak = 0.55f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(peak, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur)));
                _whiteFlash.color = new Color(tint.r, tint.g, tint.b, a);
                yield return null;
            }
            _whiteFlash.color = new Color(tint.r, tint.g, tint.b, 0f);
        }

        /// Jam: the exposure slider shakes (refused switch). The first PHYSICAL
        /// jam (matter would develop inside the player) also gets its one-time
        /// in-fiction explanation under the slider.
        public void JamFeedback(bool physical)
        {
            if (_shakeCo != null) StopCoroutine(_shakeCo);
            _shakeCo = StartCoroutine(ShakeSlider());
            if (physical && !_jamNoteShown)
            {
                _jamNoteShown = true;
                if (_jamCo != null) StopCoroutine(_jamCo);
                _jamCo = StartCoroutine(JamNoteRoutine());
            }
        }

        IEnumerator JamNoteRoutine()
        {
            _jamText.text = "nothing develops where you stand.";
            yield return new WaitForSeconds(2.8f);
            _jamText.text = "";
            _jamCo = null;
        }

        /// Post-respawn margin note: thematic (dim, lowercase) on the first
        /// burned print, terse cause line (REC red) afterwards.
        public void ShowDeathNote(string line, bool causeLine)
        {
            if (_checkpointCo != null) { StopCoroutine(_checkpointCo); _checkpointCo = null; _checkpointText.text = ""; _checkpointCaption.text = ""; }
            if (_deathCo != null) StopCoroutine(_deathCo);
            _deathText.fontSize = causeLine ? 22 : 20;
            _deathText.color = causeLine ? RecRed : new Color(0.62f, 0.62f, 0.60f, 1f);
            _deathCo = StartCoroutine(DeathNoteRoutine(line, causeLine ? 1.8f : 2.8f));
        }

        IEnumerator DeathNoteRoutine(string line, float hold)
        {
            _deathText.text = line;
            yield return new WaitForSeconds(hold);
            _deathText.text = "";
            _deathCo = null;
        }

        IEnumerator ShakeSlider()
        {
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                _sliderGroup.anchoredPosition = _sliderBasePos + Random.insideUnitCircle * 6f;
                yield return null;
            }
            _sliderGroup.anchoredPosition = _sliderBasePos;
        }

        // ---------- shutter open (drawing) ----------

        /// Viewfinder corners + REC appear only while the shutter is open.
        public void SetShutterOpen(bool open)
        {
            if (_shutterCo != null) StopCoroutine(_shutterCo);
            _shutterCo = StartCoroutine(FadeGroup(_shutterGroup, open ? 1f : 0f, 0.2f));
        }

        bool _recFast;

        /// Finale: the REC dot blinks double-time.
        public void SetRecFast(bool fast) { _recFast = fast; }

        IEnumerator RecBlink()
        {
            bool on = true;
            while (true)
            {
                on = !on;
                var c = RecRed;
                c.a = on ? 1f : 0.25f;
                _recDot.color = c;
                yield return new WaitForSeconds(_recFast ? 0.22f : 0.5f);
            }
        }

        // ---------- abilities ----------

        public void OnAbilityUnlocked(Ability a)
        {
            switch (a)
            {
                case Ability.Negative:
                    ShowBanner("NEGATIVE ACQUIRED — press 1: UNDEREXPOSED.");
                    StartCoroutine(PunchScale(_stateLabels[0].rectTransform));
                    break;
                case Ability.Flash:
                    ShowBanner("FLASH ACQUIRED — press 3: OVEREXPOSED.");
                    SetExposureVocab(true); // the enlarger flash is now the OVER exposure
                    StartCoroutine(PunchScale(_stateLabels[2].rectTransform));
                    break;
                case Ability.Shutter:
                    ShowBanner("SHUTTER ACQUIRED — hold SHIFT to draw light, release to fix.");
                    _trailsGroup.SetActive(true);
                    StartCoroutine(PunchScale((RectTransform)_trailsGroup.transform));
                    break;
            }
            ApplyLocks();
            RebuildControls();
            StartCoroutine(FullFlashRoutine(0.2f));
        }

        /// DEV: refresh ability-dependent HUD after a silent grant (boot, restart,
        /// room warp). Also re-seeds the slider vocab from the owned abilities so it
        /// never leaks across runs (safelight-vocab until the Flash is held).
        public void RefreshAbilityHud()
        {
            ApplyLocks();
            RebuildControls();
            var gm = GameManager.Instance;
            if (_trailsGroup != null) _trailsGroup.SetActive(gm != null && gm.HasShutter);
            SetExposureVocab(gm != null && gm.HasFlash);
        }

        /// Swap the slider labels between darkroom-action terms (safelight / work
        /// light / enlarger flash) and exposure terms. On once the Flash is owned.
        public void SetExposureVocab(bool on)
        {
            _exposureVocab = on;
            var names = on ? ExposureLabels : ActionLabels;
            for (int i = 0; i < 3; i++)
                if (_stateLabels[i] != null) _stateLabels[i].text = names[i];
            var em = ExposureManager.Instance;
            HighlightState(em != null ? em.Current : Exposure.Normal);
            ApplyLocks();
        }

        void ApplyLocks()
        {
            var gm = GameManager.Instance;
            bool hasNegative = gm != null && gm.HasNegative;
            bool hasFlash = gm != null && gm.HasFlash;
            _lockUnder.SetActive(!hasNegative);
            _lockOver.SetActive(!hasFlash);
            if (!hasNegative) _stateLabels[0].color = new Color(0.3f, 0.3f, 0.3f, 1f);
            if (!hasFlash) _stateLabels[2].color = new Color(0.3f, 0.3f, 0.3f, 1f);
        }

        void RebuildControls()
        {
            var gm = GameManager.Instance;
            // the prologue HUD shows ONLY move/jump, on two lines and nothing else —
            // no pause/retry, no exposure — until she steps into the first frame.
            if (_prologueControls)
            {
                _controlsText.text = "A / D — move\nSPACE — jump";
                return;
            }
            string s = "A/D ←→ move · SPACE jump";
            // the exposure controls surface with the enlarger flash (R2).
            // Safelight/work-light are taught by the slider label + the
            // "1 — safelight" key hint instead.
            if (gm != null && gm.HasFlash)
                s += "\n1/2/3 · Q/E exposure";
            if (gm != null && gm.HasShutter)
                s += "\nhold SHIFT draw · release fix";
            s += "\nESC pause · R retry";
            _controlsText.text = s;
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
                _trailTicks[i].color = i < remaining ? TextBright : new Color(0.25f, 0.25f, 0.25f, 1f);
        }

        /// Film-advance feedback: the trails group jolts as the oldest stroke is wound away.
        public void PunchTrails()
        {
            if (_trailsGroup.activeSelf)
                StartCoroutine(PunchScale((RectTransform)_trailsGroup.transform));
        }

        // ---------- hints / banner / checkpoint ----------

        public void ShowHint(string text, object key)
        {
            _hintKey = key;
            if (_hintHideCo != null) { StopCoroutine(_hintHideCo); _hintHideCo = null; }
            if (_hintDeferCo != null) { StopCoroutine(_hintDeferCo); _hintDeferCo = null; }
            // one teaching prompt at a time: if a pickup banner is up, wait it out
            if (_bannerBox != null && _bannerBox.activeSelf)
                _hintDeferCo = StartCoroutine(ShowHintAfterBanner(text, key));
            else
                ShowHintNow(text, key);
        }

        IEnumerator ShowHintAfterBanner(string text, object key)
        {
            while (_bannerBox != null && _bannerBox.activeSelf) yield return null;
            _hintDeferCo = null;
            if (_hintKey == key) ShowHintNow(text, key); // still the active trigger
        }

        void ShowHintNow(string text, object key)
        {
            _bubble.sizeDelta = new Vector2(460f, text.Length > 70 ? 112f : 84f);
            _bubbleText.text = text;
            var comp = key as Component;
            _bubbleAnchor = comp != null ? comp.transform : null;
            if (_bubbleAnchor != null)
            {
                _bubble.gameObject.SetActive(true);
                if (_bubbleFadeCo != null) StopCoroutine(_bubbleFadeCo);
                _bubbleFadeCo = StartCoroutine(FadeGroup(_bubbleGroup, 1f, 0.25f));
                // a tiny pop the first time each prompt surfaces (re-entry stays silent)
                if (key != null && _hintsHeard.Add(key) && AudioDirector.Instance != null)
                    AudioDirector.Instance.PlayHintPop();
            }
            else _bubble.gameObject.SetActive(false);
        }

        public void OnHintExit(object key)
        {
            if (_hintKey != key) return;
            // if the hint was still queued behind a banner, just drop it
            if (_hintDeferCo != null)
            {
                StopCoroutine(_hintDeferCo); _hintDeferCo = null;
                _hintKey = null; _bubbleAnchor = null;
                return;
            }
            if (_hintHideCo != null) StopCoroutine(_hintHideCo);
            _hintHideCo = StartCoroutine(HideHintAfter(2.5f, key));
        }

        IEnumerator HideHintAfter(float delay, object key)
        {
            yield return new WaitForSeconds(delay);
            if (_hintKey == key)
            {
                if (_bubbleFadeCo != null) StopCoroutine(_bubbleFadeCo);
                yield return FadeGroup(_bubbleGroup, 0f, 0.4f);
                _bubble.gameObject.SetActive(false);
                _hintKey = null;
                _bubbleAnchor = null;
            }
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
            _bannerBox.SetActive(true);
            yield return new WaitForSeconds(4f);
            _bannerBox.SetActive(false);
        }

        public void CheckpointFlash(string caption = "")
        {
            if (_checkpointCo != null) StopCoroutine(_checkpointCo);
            _checkpointCo = StartCoroutine(CheckpointRoutine(caption));
        }

        IEnumerator CheckpointRoutine(string caption)
        {
            bool noted = !string.IsNullOrEmpty(caption);
            StartCoroutine(SoftFlash(0.22f, 0.45f)); // the print develops
            _checkpointText.text = "CHECKPOINT DEVELOPED";
            _checkpointCaption.text = noted ? caption : "";
            // a margin note earns a longer look
            yield return new WaitForSeconds(noted ? 2.6f : 1.6f);
            _checkpointText.text = "";
            _checkpointCaption.text = "";
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

        /// A gentle white bloom (softer than the one-frame switch flash) — the
        /// print "developing" at a checkpoint.
        IEnumerator SoftFlash(float maxA, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                _whiteFlash.color = new Color(1f, 1f, 1f, Mathf.Lerp(maxA, 0f, Mathf.Clamp01(t / dur)));
                yield return null;
            }
            _whiteFlash.color = new Color(1f, 1f, 1f, 0f);
        }

        // ---------- cinematic flow: letterbox, boot, frame cards ----------

        /// Slide the cinematic bars in/out. Used for the boot open and the finale.
        public const float LetterboxHeight = 92f;

        public void SetLetterbox(bool on, float dur = 0.7f)
        {
            if (_letterboxCo != null) StopCoroutine(_letterboxCo);
            _letterboxCo = StartCoroutine(LetterboxRoutine(on ? LetterboxHeight : 0f, dur));
        }

        IEnumerator LetterboxRoutine(float target, float dur)
        {
            float start = _barTop.sizeDelta.y, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float h = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur)));
                _barTop.sizeDelta = new Vector2(0f, h);
                _barBottom.sizeDelta = new Vector2(0f, h);
                yield return null;
            }
            _barTop.sizeDelta = new Vector2(0f, target);
            _barBottom.sizeDelta = new Vector2(0f, target);
        }

        /// Boot open: the darkroom in the dark — a drip, the fan, a distant
        /// shutter — then the room develops up out of black under a brief letterbox.
        IEnumerator BootIn()
        {
            _blackFade.color = new Color(0f, 0f, 0f, 1f);
            _barTop.sizeDelta = new Vector2(0f, 56f);
            _barBottom.sizeDelta = new Vector2(0f, 56f);
            var ad = AudioDirector.Instance;
            if (ad != null) { ad.SetFan(0.16f); ad.NudgeHum(0.06f, 4f); } // the ventilation fan, a quiet darkroom
            yield return new WaitForSeconds(0.4f);
            if (ad != null) ad.PlayDrip();  // a drop in the dark
            yield return new WaitForSeconds(1.2f);
            if (ad != null) ad.PlayClick(); // a distant shutter
            yield return new WaitForSeconds(0.5f);
            if (ad != null) ad.PlayDrip();  // another drop
            yield return new WaitForSeconds(0.5f);
            if (ad != null) ad.SetFan(0.05f); // the fan recedes as the room develops up
            yield return FadeBlackRoutine(false, 1.6f);
            yield return new WaitForSeconds(1.6f);
            SetLetterbox(false, 1.3f);
        }

        /// A brief ceremonial "FRAME N" as the player crosses into a new frame.
        void ShowFrameCard(int frame)
        {
            if (_frameCardCo != null) StopCoroutine(_frameCardCo);
            _frameCardCo = StartCoroutine(FrameCardRoutine(frame));
        }

        IEnumerator FrameCardRoutine(int frame)
        {
            string title = frame >= 0 && frame < LevelData.Rooms.Length ? LevelData.Rooms[frame].title : "";
            _frameCard.text = "FRAME " + frame + " OF 11 — " + title;
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayFrameCardChime();
            Color c = _frameCard.color;
            yield return FadeText(_frameCard, 0f, 1f, 0.6f);
            yield return new WaitForSeconds(1.7f);
            yield return FadeText(_frameCard, 1f, 0f, 0.9f);
            c.a = 0f; _frameCard.color = c;
        }

        static IEnumerator FadeText(Text txt, float from, float to, float dur)
        {
            float t = 0f;
            Color c = txt.color;
            while (t < dur)
            {
                t += Time.deltaTime;
                c.a = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                txt.color = c;
                yield return null;
            }
            c.a = to; txt.color = c;
        }

        // ---------- pause / mute ----------

        public void ShowPause(bool paused) { _pausePanel.SetActive(paused); }

        public void SetMuted(bool muted) { _mutedText.text = muted ? "MUTED" : ""; }

        public static string FormatTime(float t)
        {
            int m = (int)(t / 60f);
            return m + ":" + (t - m * 60f).ToString("00.0");
        }

        // ---------- restart ----------

        public void ResetForRestart()
        {
            var win = CanvasRoot.Find("WinScreen");
            if (win != null) Destroy(win.gameObject);
            // a replay re-enters the prologue: darkroom vocab + the name un-dropped
            _exposureVocab = false;
            _titleDropped = false;
            _firstUnderHandled = false;
            for (int i = 0; i < 3; i++)
                if (_stateLabels[i] != null) _stateLabels[i].text = ActionLabels[i];
            ApplyLocks();
            HighlightState(Exposure.Normal);
            ApplyLocks(); // re-dim locked labels after highlight reset
            _knob.anchoredPosition = new Vector2(KnobSlots[1], _knob.anchoredPosition.y);
            _trailsGroup.SetActive(false);
            SetStrokeDots(3);
            _overlay.color = OverlayNormal;
            _bannerBox.SetActive(false);
            _checkpointText.text = "";
            _checkpointCaption.text = "";
            if (_deathCo != null) { StopCoroutine(_deathCo); _deathCo = null; }
            _deathText.text = "";
            if (_jamCo != null) { StopCoroutine(_jamCo); _jamCo = null; }
            _jamText.text = "";
            _blackFade.color = new Color(0f, 0f, 0f, 0f);
            _whiteFlash.color = new Color(1f, 1f, 1f, 0f);
            if (_hintHideCo != null) { StopCoroutine(_hintHideCo); _hintHideCo = null; }
            if (_hintDeferCo != null) { StopCoroutine(_hintDeferCo); _hintDeferCo = null; }
            _bubble.gameObject.SetActive(false);
            _bubbleGroup.alpha = 1f;
            _hintKey = null;
            _bubbleAnchor = null;
            _shownRoom = -1;
            _firstRoomShown = false;
            if (_frameCardCo != null) { StopCoroutine(_frameCardCo); _frameCardCo = null; }
            { var fc = _frameCard.color; fc.a = 0f; _frameCard.color = fc; }
            SetLetterbox(false, 0.01f);
            _controlsGone = false;
            _prologueControls = true; // a replay re-enters the prologue: HUD back to move/jump
            _controlsGroup.alpha = 0.65f;
            RebuildControls();
            if (_cardCo != null) { StopCoroutine(_cardCo); _cardCo = null; }
            _cardGroup.alpha = 0f;
            _cardGroup.gameObject.SetActive(false);
            _shutterGroup.alpha = 0f;
            _recFast = false;
            _jamNoteShown = false; // per-run, like the first-death note
            // (_titleDropped stays per-session: the name lands only once)
        }
    }
}
