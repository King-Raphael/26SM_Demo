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
        CanvasGroup _sliderCg;     // faded out during cinematics (clean prologue/finale reveal)
        CanvasGroup _topCg;        // the top header scrim (so the HUD reads on any background)
        float _chromeAlpha = 1f;
        Vector2 _sliderBasePos;
        bool _sliderDrag;
        int _sliderLastIdx = -1;
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
        Image _objRule;   // the warm margin rule — sized to the live objective count
        Coroutine _objCo;
        int _shownRoom = -1;

        // top-right controls (tutorial only)
        Text _controlsText;
        CanvasGroup _controlsGroup;
        bool _controlsGone;
        bool _prologueControls = true; // prologue HUD shows ONLY move/jump until Frame 1
        // control-hint highlight: the operation the current frame leans on (e.g. SHIFT-draw)
        // pulses warm in the top-right list, so the player reads "this is what to do here".
        enum ControlHint { None, Move, Exposure, Draw, System }
        readonly string[] _ctrlTexts = new string[6];
        readonly ControlHint[] _ctrlKinds = new ControlHint[6];
        int _ctrlCount;
        ControlHint _ctrlHighlight = ControlHint.None;

        // bottom exposure card (tutorial only)
        CanvasGroup _cardGroup;
        Text _cardTitle, _cardBody;
        readonly Image[] _cardDots = new Image[3]; // under / normal / over indicator
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
        RectTransform _pauseGallery;
        bool _jamNoteShown;
        Coroutine _overlayCo, _shakeCo, _bannerCo, _checkpointCo, _flashCo, _knobCo, _shutterCo, _controlsCo, _deathCo, _jamCo;

        // diegetic film gate: sprocket margins + edge code, revealed transiently as the
        // film "advances" at each checkpoint, then wound away (not a permanent border)
        Text _edgeCode;
        CanvasGroup _gateGroup;
        Coroutine _gateCo;

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

        // ONE consistent vocabulary everywhere — UNDER / NORMAL / OVER — matching the
        // keys (1/2/3), the solidity logic and the acquire-banners. The darkroom flavour
        // (safelight, the enlarger, the trays) lives in the art, audio and narrative,
        // not in the labels you read mid-jump.
        static readonly string[] ExposureLabels = { "UNDER", "NORMAL", "OVER" };

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

            BuildTopScrim();  // header gradient so the top HUD reads on any background
            BuildFilmGate(); // built early -> low sibling index -> sits behind the readable HUD
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

        /// A soft dark gradient down from the top edge so the header (room title, exposure
        /// slider, controls) reads on ANY background — bright Over included — and the three
        /// top elements feel like one designed strip instead of floating chrome. Faded out
        /// during cinematics with the rest of the chrome.
        void BuildTopScrim()
        {
            var rt = NewRect("TopScrim", CanvasRoot);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 230f); // full width, top header strip
            rt.anchoredPosition = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = PixelArt.TopGradient;
            img.type = Image.Type.Simple;
            img.color = new Color(0.03f, 0.03f, 0.05f, 0.5f); // dark cool; alpha = scrim strength
            img.raycastTarget = false;
            _topCg = rt.gameObject.AddComponent<CanvasGroup>();
        }

        /// The diegetic FILM GATE: two near-void film-base margins down the screen edges,
        /// stacked 35mm sprocket perforations, and an engraved edge-code in the bottom
        /// margin (the frame number, updated per room). Reframes the whole game as one
        /// frame of film inside the gate. Pure overlay, monochrome, introduces no red —
        /// it cannot violate the safelight-red rule. Kept thin + low-value so it frames
        /// without occluding footing (§5 'a hard film-base border the camera never crosses').
        void BuildFilmGate()
        {
            var gate = NewRect("FilmGate", CanvasRoot);
            Stretch(gate);
            _gateGroup = gate.gameObject.AddComponent<CanvasGroup>();
            _gateGroup.alpha = 0f; // hidden by default; RevealFilmGate() winds it in at checkpoints
            var baseC = new Color(0.04f, 0.04f, 0.05f, 1f);
            const float margin = 26f;

            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? 0f : 1f; // 0 = left edge, 1 = right edge
                var strip = NewImage(s == 0 ? "GateL" : "GateR", gate, baseC);
                var rt = strip.rectTransform;
                rt.anchorMin = new Vector2(side, 0f);
                rt.anchorMax = new Vector2(side, 1f);
                rt.pivot = new Vector2(side, 0.5f);
                rt.sizeDelta = new Vector2(margin, 0f);
                rt.anchoredPosition = Vector2.zero;

                // stack perforations from screen-centre out (extras off-screen are harmless,
                // so it fills any aspect ratio without runtime measuring)
                for (int i = -22; i <= 22; i++)
                {
                    var cell = NewImage("Perf", rt, Color.white);
                    cell.sprite = PixelArt.SprocketCell;
                    var crt = cell.rectTransform;
                    crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
                    crt.sizeDelta = new Vector2(13f, 21f);
                    crt.anchoredPosition = new Vector2(0f, i * 30f);
                }
            }

            // engraved edge-code in the bottom-left margin; the "frame number" updates per room
            _edgeCode = NewText("EdgeCode", gate, "KODAK  5222", 15,
                new Color(0.5f, 0.5f, 0.52f, 0.62f), TextAnchor.LowerLeft);
            _edgeCode.font = FontLoader.Mono;
            Place(_edgeCode.rectTransform, new Vector2(0f, 0f), new Vector2(36f, 14f), new Vector2(420f, 22f));
        }

        /// Wind the film gate in (sprockets + edge-code fade up), hold a beat, then wind it
        /// away — the frame advancing through the gate. Fired by a Checkpoint on arrival.
        public void RevealFilmGate()
        {
            if (_gateGroup == null) return;
            if (_gateCo != null) StopCoroutine(_gateCo);
            _gateCo = StartCoroutine(FilmGateRoutine());
        }

        IEnumerator FilmGateRoutine()
        {
            yield return FadeGroup(_gateGroup, 1f, 0.4f);
            yield return new WaitForSeconds(1.6f);
            yield return FadeGroup(_gateGroup, 0f, 0.9f);
            _gateCo = null;
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
            // CENTER: the exposure bar — the interactive focal point. Its caption is the
            // same letter-spaced mono eyebrow as the tutorial card, so every label across
            // the HUD speaks in one voice.
            _sliderGroup = NewRect("ExposureSlider", CanvasRoot);
            Place(_sliderGroup, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(460f, 120f));
            _sliderCg = _sliderGroup.gameObject.AddComponent<CanvasGroup>();
            _sliderBasePos = _sliderGroup.anchoredPosition;

            var caption = NewText("Caption", _sliderGroup, "E X P O S U R E", 15, new Color(0.74f, 0.74f, 0.70f, 1f), TextAnchor.MiddleCenter);
            Place(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(320f, 22f));

            // The slider track is a real glass ROD, composed in layers so the light
            // reads with depth instead of one flat baked gradient:
            //   drop shadow → glass body (refraction/baked) → underside shade →
            //   crisp top highlight → a soft OFF-CENTRE specular glint → cap sparkles.
            const float trackW = 360f, trackH = 22f;
            var trackRT = NewRect("Track", _sliderGroup);
            Place(trackRT, new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(trackW, trackH));

            // 1) soft drop shadow — lifts the rod off the scrim
            var trShadow = NewImage("Shadow", trackRT, new Color(0f, 0f, 0f, 0.42f));
            trShadow.sprite = PixelArt.SoftGlow;
            Place(trShadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), new Vector2(trackW + 24f, trackH + 8f));

            // 2) the glass body — the baked GlassBar CAPSULE: rounded ends (soft
            //    corners) and a near-transparent body (baked alpha ~0.10), so the
            //    scene reads THROUGH the rod and it sits IN the frame rather than on
            //    it. (Replaces the rectangular refraction grab — its hard corners +
            //    frosted fill read as a sticker pasted over the background.)
            var body = NewImage("Body", trackRT, new Color(0.78f, 0.84f, 0.93f, 1f));
            body.sprite = PixelArt.GlassBar; // a translucent cool clear-glass cast
            Place(body.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(trackW, trackH));

            // 3) underside shade — the rod's lower half sits in its own shadow
            var lowShade = NewImage("LowShade", trackRT, new Color(0.02f, 0.03f, 0.05f, 0.30f));
            lowShade.sprite = PixelArt.RoundedRect;
            Place(lowShade.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(trackW - 14f, 8f));

            // 4) specular highlight line just under the top edge — soft, so the rod
            //    reads as lit glass, not a bright bar painted over the scene
            var hiLine = NewImage("HiLine", trackRT, new Color(0.95f, 0.97f, 1f, 0.40f));
            hiLine.sprite = PixelArt.RoundedRect;
            Place(hiLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(trackW - 30f, 3f));

            // 5) a soft, OFF-CENTRE glint — the "real glass" cue (one gentle bloom,
            //    not a uniform shine)
            var glint = NewImage("Glint", trackRT, new Color(1f, 1f, 1f, 0.42f));
            glint.sprite = PixelArt.SoftGlow;
            Place(glint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-86f, 4f), new Vector2(150f, 16f));

            // 6) tiny sparkles where light catches the rounded caps
            for (int s = 0; s < 2; s++)
            {
                var cap = NewImage("Cap" + s, trackRT, new Color(1f, 1f, 1f, 0.42f));
                cap.sprite = PixelArt.SoftGlow;
                Place(cap.rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2((s == 0 ? -1f : 1f) * (trackW / 2f - 7f), 1f), new Vector2(13f, 13f));
            }

            // a clean glass knob: a soft cast shadow, a faint dark rim, a bright
            // NEUTRAL disc and a crisp specular highlight — it reads as a lit bead
            _knob = NewRect("Knob", _sliderGroup);
            Place(_knob, new Vector2(0.5f, 1f), new Vector2(KnobSlots[1], -38f), new Vector2(22f, 22f));
            var kshadow = NewImage("KnobShadow", _knob, new Color(0f, 0f, 0f, 0.5f));
            kshadow.sprite = PixelArt.SoftGlow;
            Place(kshadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -2.5f), new Vector2(30f, 30f));
            var krim = NewImage("KnobRim", _knob, new Color(0.20f, 0.22f, 0.26f, 0.9f));
            krim.sprite = PixelArt.Disc;
            Place(krim.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
            var kdisc = NewImage("KnobDisc", _knob, new Color(0.97f, 0.97f, 0.97f, 0.98f));
            kdisc.sprite = PixelArt.Disc;
            Place(kdisc.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(21f, 21f));
            var kspec = NewImage("KnobSpec", _knob, new Color(1f, 1f, 1f, 0.95f));
            kspec.sprite = PixelArt.Disc;
            Place(kspec.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-4f, 4f), new Vector2(7f, 7f));

            string[] names = ExposureLabels;
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
            // bottom-centre budget readout: three round pips above a small caption,
            // low enough to clear the tutorial card (which lives bottom-centre too)
            var rt = NewRect("TrailsGroup", CanvasRoot);
            Place(rt, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(160f, 54f));
            for (int i = 0; i < 3; i++)
            {
                _trailTicks[i] = NewImage("Tick" + i, rt, TextBright);
                _trailTicks[i].sprite = PixelArt.Disc; // round pips read cleaner than squares
                Place(_trailTicks[i].rectTransform, new Vector2(0.5f, 0f), new Vector2((i - 1) * 22f, 36f), new Vector2(11f, 11f));
            }
            var label = NewText("Label", rt, "T R A I L S", 14, TextDim, TextAnchor.MiddleCenter);
            Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(160f, 18f));
            _trailsGroup = rt.gameObject;
            _trailsGroup.SetActive(false);
        }

        void BuildRoomInfo()
        {
            // LEFT column: cursive frame title at the header top, objective beneath (mono,
            // matching the right-hand controls so the two columns share one functional voice)
            _roomTitle = NewText("RoomTitle", CanvasRoot, "", 38, TextBright, TextAnchor.MiddleLeft);
            _roomTitle.font = FontLoader.Title; // a cursive hand — the level name reads as a title, not UI
            Place(_roomTitle.rectTransform, new Vector2(0f, 1f), new Vector2(52f, -40f), new Vector2(880f, 50f));

            // objectives as a tidy checklist beneath a faint warm margin rule — the
            // title, rule and list share one left edge for a clean, deliberate column
            var objRT = NewRect("Objectives", CanvasRoot);
            Place(objRT, new Vector2(0f, 1f), new Vector2(54f, -110f), new Vector2(760f, 70f));
            _objGroup = objRT.gameObject.AddComponent<CanvasGroup>();
            _objRule = NewImage("Rule", objRT, new Color(0.85f, 0.80f, 0.66f, 0.26f));
            _objRule.sprite = PixelArt.RoundedRect;
            Place(_objRule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -3f), new Vector2(2f, 56f));
            for (int i = 0; i < 2; i++)
            {
                _objLines[i] = NewText("Objective" + i, objRT, "", 19, new Color(0.66f, 0.66f, 0.63f, 1f), TextAnchor.MiddleLeft);
                Place(_objLines[i].rectTransform, new Vector2(0f, 1f), new Vector2(16f, -i * 30f), new Vector2(800f, 28f));
            }
        }

        Coroutine _titleDevCo;

        static string ToTitle(string s) =>
            System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

        /// Click or drag the exposure bar to switch modes (players reach for it).
        /// Polls the mouse directly — this runtime HUD has no EventSystem — and maps
        /// the pointer to the nearest mode label, switching via the same gated path as
        /// the keys (locks + the jam rule still apply). Overlay canvas: null camera.
        void HandleSliderPointer()
        {
            if (_sliderGroup == null) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null || gm.HasWon || gm.IsCinematic || gm.IsRespawning || PauseController.IsPaused)
            { _sliderDrag = false; return; }

            if (DarkroomInput.PointerPressed)
            {
                _sliderDrag = RectTransformUtility.RectangleContainsScreenPoint(_sliderGroup, DarkroomInput.PointerPos, null);
                _sliderLastIdx = -1;
            }
            if (!DarkroomInput.PointerHeld) { _sliderDrag = false; return; }
            if (!_sliderDrag) return;

            float mx = DarkroomInput.PointerPos.x;
            int idx = 1; float best = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                if (_stateLabels[i] == null) continue;
                float lx = RectTransformUtility.WorldToScreenPoint(null, _stateLabels[i].rectTransform.position).x;
                float d = Mathf.Abs(mx - lx);
                if (d < best) { best = d; idx = i; }
            }
            if (idx == _sliderLastIdx) return;
            _sliderLastIdx = idx;
            var em = ExposureManager.Instance;
            if (em != null && em.Current != (Exposure)idx) em.TrySetExposure((Exposure)idx);
        }

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
            // RIGHT column: controls, right-aligned, top-aligned with the title (y -36),
            // 48px margin to mirror the left. Mono + dim — the reference voice.
            _controlsText = NewText("Controls", CanvasRoot, "", 15, new Color(0.46f, 0.46f, 0.45f, 1f), TextAnchor.UpperRight);
            _controlsText.lineSpacing = 1.5f;
            Place(_controlsText.rectTransform, new Vector2(1f, 1f), new Vector2(-48f, -36f), new Vector2(440f, 130f));
            _controlsGroup = _controlsText.gameObject.AddComponent<CanvasGroup>();
            _controlsGroup.alpha = 0.55f; // a faded hint, not eye-catching
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

        /// The tutorial exposure card, restyled as a darkroom contact-card: a film-base
        /// panel with a thin warm hairline, a sprocket accent down the left, an "EXPOSURE"
        /// eyebrow over the state name (display face), the explanation, and three state dots
        /// (under/normal/over) with the current one lit.
        void BuildCard()
        {
            var rt = NewRect("ExposureCard", CanvasRoot);
            // taller than the content needs: the body is top-anchored, so the extra height is
            // pure bottom padding — the second body line no longer kisses the bottom hairline.
            Place(rt, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(560f, 150f));
            var bg = NewImage("Bg", rt, new Color(0.05f, 0.05f, 0.06f, 0.93f)); // film base
            Stretch(bg.rectTransform);
            AddBorder(rt, new Color(0.85f, 0.80f, 0.66f, 0.42f));               // thin warm hairline

            // symmetric sprocket accents down BOTH edges (film identity + balance)
            for (int s = 0; s < 2; s++)
            {
                float ex = s == 0 ? 13f : -13f;
                var anchor = new Vector2(s == 0 ? 0f : 1f, 0.5f);
                for (int i = 0; i < 4; i++)
                {
                    var perf = NewImage("Perf", rt, Color.white);
                    perf.sprite = PixelArt.SprocketCell;
                    Place(perf.rectTransform, anchor, new Vector2(ex, 42f - i * 28f), new Vector2(10f, 16f));
                }
            }

            // everything centred for a tidy, balanced card
            var eyebrow = NewText("Eyebrow", rt, "E X P O S U R E", 12, new Color(0.58f, 0.56f, 0.51f, 1f), TextAnchor.MiddleCenter);
            eyebrow.font = FontLoader.Mono;
            Place(eyebrow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(360f, 16f));

            _cardTitle = NewText("Title", rt, "", 27, new Color(1f, 0.95f, 0.86f, 1f), TextAnchor.MiddleCenter, display: true);
            Place(_cardTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(440f, 34f));

            // a centred row of three state dots under the name (current lit in ShowCard)
            for (int i = 0; i < 3; i++)
            {
                var dot = NewImage("Dot" + i, rt, new Color(0.32f, 0.32f, 0.34f, 1f));
                dot.sprite = PixelArt.Disc;
                Place(dot.rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 22f, -68f), new Vector2(10f, 10f));
                _cardDots[i] = dot;
            }

            _cardBody = NewText("Body", rt, "", 18, new Color(0.68f, 0.68f, 0.65f, 1f), TextAnchor.UpperCenter);
            _cardBody.lineSpacing = 1.3f;
            Place(_cardBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(480f, 48f));

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

            // the darkroom gallery — lost frames developed so far, rebuilt from
            // PhotoAlbum each time the panel opens (captures happen during play)
            var galRT = NewRect("PauseGallery", rt);
            Place(galRT, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1400f, 150f));
            _pauseGallery = galRT;

            _pausePanel = rt.gameObject;
            _pausePanel.SetActive(false);
        }

        /// A brief top-of-screen note when a hidden lost frame is developed.
        public void ShowLostFrame()
        {
            int n = GameManager.Instance != null ? GameManager.Instance.LostFound : 0;
            StartCoroutine(KeyHintRoutine("LOST FRAME DEVELOPED   " + n + " / " + LevelData.LostFrameTotal));
        }

        /// Rebuild the pause-menu darkroom wall: a count line + a thumbnail of each
        /// lost frame surfaced so far (a separate roll from the eleven).
        void RebuildPauseGallery()
        {
            if (_pauseGallery == null) return;
            for (int i = _pauseGallery.childCount - 1; i >= 0; i--)
                Destroy(_pauseGallery.GetChild(i).gameObject);

            int total = LevelData.LostFrameTotal;
            if (total <= 0) return;
            var album = PhotoAlbum.Instance;
            int found = GameManager.Instance != null ? GameManager.Instance.LostFound : 0;

            var label = NewText("GalLabel", _pauseGallery,
                "LOST FRAMES DEVELOPED   " + found + " / " + total,
                22, new Color(0.72f, 0.72f, 0.70f, 1f), TextAnchor.UpperCenter, display: true);
            Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(700f, 30f));

            int shots = album != null ? album.LostCount : 0;
            const float w = 120f, h = 80f, gap = 12f;
            float tw = shots > 0 ? shots * w + (shots - 1) * gap : 0f;
            float x0 = -tw / 2f + w / 2f;
            for (int i = 0; i < shots; i++)
            {
                var tex = album.LostShot(i);
                if (tex == null) continue;
                var slot = NewRect("Lost" + i, _pauseGallery);
                Place(slot, new Vector2(0.5f, 1f), new Vector2(x0 + i * (w + gap), -42f), new Vector2(w, h));
                var border = slot.gameObject.AddComponent<Image>();
                border.color = new Color(0.55f, 0.6f, 0.7f, 1f); // cool: these are negatives
                var imgRT = NewRect("Shot", slot);
                Place(imgRT, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w - 6f, h - 6f));
                var raw = imgRT.gameObject.AddComponent<RawImage>();
                raw.texture = tex;
                raw.raycastTarget = false;
            }
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

        // the prologue mode-hint text lives on CanvasRoot, OUTSIDE the chrome groups, with a
        // long hold — tracked so a cinematic can clear it instead of letting it float over
        // the paper reveal.
        Text _keyHint;
        Coroutine _keyHintCo;

        /// A brief darkroom key-mapping note in the prologue ("1 — safelight" /
        /// "2 — work light"), surfaced once each by the PrologueDirector.
        public void ShowKeyHint(string text)
        {
            if (_keyHintCo != null) StopCoroutine(_keyHintCo);
            if (_keyHint != null) Destroy(_keyHint.gameObject);
            _keyHintCo = StartCoroutine(KeyHintRoutine(text));
        }

        IEnumerator KeyHintRoutine(string text)
        {
            var t = NewText("KeyHint", CanvasRoot, text, 22, new Color(0.82f, 0.81f, 0.77f, 0f), TextAnchor.MiddleCenter);
            Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(440f, 30f));
            _keyHint = t;
            yield return FadeText(t, 0f, 1f, 0.3f);
            yield return new WaitForSeconds(2.2f);
            yield return FadeText(t, 1f, 0f, 0.6f);
            Destroy(t.gameObject);
            _keyHint = null;
            _keyHintCo = null;
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
                if (!_controlsGone) _controlsGroup.alpha = Mathf.Lerp(0.55f, 0.2f, k);
                yield return null;
            }
            yield return new WaitForSeconds(2.2f);
            t = 0f;
            while (t < 1.4f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 1.4f);
                var c = title.color; c.a = 1f - k; title.color = c;
                if (!_controlsGone) _controlsGroup.alpha = Mathf.Lerp(0.2f, 0.55f, k);
                yield return null;
            }
            Destroy(title.gameObject);
        }

        // ---------- per-frame ----------

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return;
            HandleSliderPointer();
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
                    ? ToTitle(def.title)
                    : "Frame " + room + " of 11 :  " + ToTitle(def.title);
                if (_edgeCode != null) // the gate's edge-code counts the roll (prologue = unnumbered)
                    _edgeCode.text = "KODAK  5222      " + (room == 0 ? "—" : room.ToString("00") + "A");
                for (int i = 0; i < 2; i++)
                    _objLines[i].text = i < def.objectives.Length ? "○ " + def.objectives[i] : "";
                // the margin rule spans only as many lines as there actually are
                // (a one-objective room no longer shows a two-line rule)
                if (_objRule != null)
                {
                    int nObj = Mathf.Clamp(def.objectives.Length, 0, 2);
                    _objRule.enabled = nObj > 0;
                    _objRule.rectTransform.sizeDelta = new Vector2(2f, nObj <= 1 ? 26f : 56f);
                }
                if (_titleDevCo != null) StopCoroutine(_titleDevCo);
                _titleDevCo = StartCoroutine(RoomTitleDevelop());
                if (_objCo != null) StopCoroutine(_objCo);
                _objCo = StartCoroutine(ObjectivesPeek()); // show, then get out of the way
                // a brief ceremonial frame card on first forward entry (not at boot)
                if (_firstRoomShown && room > prev && room >= 1) ShowFrameCard(room);
                _firstRoomShown = true;
                // she's stepped into the first frame: the full control HUD arrives
                if (_prologueControls && room >= 1) { _prologueControls = false; RebuildControls(); }
                HighlightControl(FeaturedControl(room)); // pulse the op this frame leans on
            }

            // controls block retires after the drawing lesson (end of Room 5)
            if (!_controlsGone && x > 73f)
            {
                _controlsGone = true;
                if (_controlsCo != null) StopCoroutine(_controlsCo);
                _controlsCo = StartCoroutine(FadeGroup(_controlsGroup, 0f, 1.2f));
            }

            // fade the gameplay HUD chrome out during cinematics (the prologue paper reveal,
            // the finale) and the win screen, so they read clean — no slider/hint clutter
            float chromeT = (gm.IsCinematic || gm.HasWon) ? 0f : 1f;
            _chromeAlpha = Mathf.MoveTowards(_chromeAlpha, chromeT, Time.deltaTime * 4f);
            if (_sliderCg != null) _sliderCg.alpha = _chromeAlpha;
            if (_topCg != null) _topCg.alpha = _chromeAlpha;
            // the exposure mode-hint card lingers (hold + slow fade) after a switch, so a
            // cinematic can begin while it's still up. Cancel any in-flight card and fade it
            // out WITH the chrome instead of letting it float over the paper reveal.
            if ((gm.IsCinematic || gm.HasWon) && _cardGroup != null && _cardGroup.gameObject.activeSelf)
            {
                if (_cardCo != null) { StopCoroutine(_cardCo); _cardCo = null; }
                _cardGroup.alpha = Mathf.MoveTowards(_cardGroup.alpha, 0f, Time.deltaTime * 4f);
                if (_cardGroup.alpha <= 0.001f) _cardGroup.gameObject.SetActive(false);
            }
            // the prologue key-hint ("2 — work light") also lives outside the chrome groups —
            // fade it out + destroy so it can't float over the prologue paper reveal
            if ((gm.IsCinematic || gm.HasWon) && _keyHint != null)
            {
                if (_keyHintCo != null) { StopCoroutine(_keyHintCo); _keyHintCo = null; }
                var kc = _keyHint.color;
                kc.a = Mathf.MoveTowards(kc.a, 0f, Time.deltaTime * 4f);
                _keyHint.color = kc;
                if (kc.a <= 0.001f) { Destroy(_keyHint.gameObject); _keyHint = null; }
            }
            // keep the featured-control hint gently pulsing while it's highlighted + visible
            if (_ctrlHighlight != ControlHint.None && !_controlsGone) ApplyControlsText();

            // hint bubble follows its trigger (and is hidden during cinematics)
            if (_bubble.gameObject.activeSelf)
            {
                if (_bubbleAnchor == null || gm.IsCinematic) { _bubble.gameObject.SetActive(false); }
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

        /// Tutorial exposure card. Only once exposure is a REAL three-state choice — i.e.
        /// after the Flash is acquired (R2/R3) — so it never pops in R1 just for the
        /// boot-granted Under/Normal toggle. Stops after the tutorial stretch (x > 48).
        void ShowCard(Exposure e)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null || gm.HasWon || gm.IsCinematic) return;
            if (!gm.HasFlash) return;                          // not until exposure is fully unlocked
            if (gm.Player.transform.position.x > 48f) return;  // tutorial stretch only
            if (_bubble.gameObject.activeSelf) return;         // don't stack on a hint bubble
            _cardTitle.text = BadgeNames[(int)e];
            _cardTitle.color = Color.Lerp(new Color(1f, 0.95f, 0.86f, 1f), StateDotLit(e), 0.5f); // subtle state tint
            _cardBody.text = CardBodies[(int)e];
            for (int i = 0; i < 3; i++)
                if (_cardDots[i] != null)
                    _cardDots[i].color = i == (int)e ? StateDotLit((Exposure)i) : new Color(0.32f, 0.32f, 0.34f, 1f);
            if (_cardCo != null) StopCoroutine(_cardCo);
            _cardCo = StartCoroutine(CardRoutine());
        }

        static Color StateDotLit(Exposure e) =>
            e == Exposure.Underexposed ? new Color(0.62f, 0.74f, 1f, 1f)     // cold blue
          : e == Exposure.Overexposed ? new Color(1f, 0.84f, 0.62f, 1f)      // warm
          : new Color(0.92f, 0.92f, 0.90f, 1f);                              // neutral

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
                    RefreshExposureLabels(); // un-grey the OVER label now the Flash is owned
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
            RefreshExposureLabels();
        }

        /// Refresh slider colours after an ability unlock (the OVER label un-greys
        /// once the Flash is owned). Labels are always UNDER / NORMAL / OVER.
        public void RefreshExposureLabels()
        {
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
                _ctrlCount = 0; // no per-line highlight in the prologue
                return;
            }
            _ctrlCount = 0;
            AddCtrl(ControlHint.Move, "A/D ←→ move · SPACE jump");
            // the exposure controls surface with the enlarger flash (R2). Safelight/work-light
            // are taught by the slider label + the "1 — safelight" key hint instead.
            if (gm != null && gm.HasFlash) AddCtrl(ControlHint.Exposure, "1/2/3 · Q/E exposure");
            if (gm != null && gm.HasShutter) AddCtrl(ControlHint.Draw, "hold SHIFT draw · release fix");
            AddCtrl(ControlHint.System, "ESC pause · R retry");
            ApplyControlsText();
        }

        void AddCtrl(ControlHint k, string t) { _ctrlKinds[_ctrlCount] = k; _ctrlTexts[_ctrlCount] = t; _ctrlCount++; }

        /// Compose the controls text, wrapping the highlighted line in a gently pulsing warm
        /// colour (rich text) so the op this frame leans on draws the eye without clutter.
        void ApplyControlsText()
        {
            if (_ctrlCount == 0) return;
            string hi = null;
            if (_ctrlHighlight != ControlHint.None && !_controlsGone)
            {
                float p = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);                 // slow, gentle
                Color c = Color.Lerp(new Color(0.78f, 0.72f, 0.50f), new Color(1f, 0.93f, 0.72f), p);
                hi = ColorUtility.ToHtmlStringRGB(c);
            }
            string s = "";
            for (int i = 0; i < _ctrlCount; i++)
            {
                if (i > 0) s += "\n";
                s += (hi != null && _ctrlKinds[i] == _ctrlHighlight)
                    ? "<color=#" + hi + ">" + _ctrlTexts[i] + "</color>"
                    : _ctrlTexts[i];
            }
            _controlsText.text = s;
        }

        /// The operation the current FRAME leans on → its top-right hint pulses. Controls fade
        /// out after R5 (x>73), so only the early frames matter; edit this map to taste.
        static ControlHint FeaturedControl(int room)
        {
            switch (room)
            {
                case 2: case 3: case 4: return ControlHint.Exposure;
                case 5: return ControlHint.Draw;
                default: return ControlHint.None;
            }
        }

        void HighlightControl(ControlHint kind)
        {
            _ctrlHighlight = kind;
            ApplyControlsText();
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
            var gm = GameManager.Instance;
            if (gm != null && (gm.IsCinematic || gm.HasWon)) { _bubble.gameObject.SetActive(false); return; }
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

        public void ShowPause(bool paused) { _pausePanel.SetActive(paused); if (paused) RebuildPauseGallery(); }

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
            // a replay re-enters the prologue: the name un-drops (labels stay UNDER/NORMAL/OVER)
            _titleDropped = false;
            _firstUnderHandled = false;
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
            _controlsGroup.alpha = 0.55f;
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
