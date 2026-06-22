using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Darkroom
{
    /// "Taking the final photograph", staged like a print in the tray:
    /// white flash -> film border -> the contact strip develops frame by
    /// frame -> the self-portrait surfaces on warm paper -> the margin
    /// lines -> the DEVELOPED block.
    public class WinScreen : MonoBehaviour
    {
        public static void Show()
        {
            var hud = HUDController.Instance;
            if (hud == null) return;
            var rt = HUDController.NewRect("WinScreen", hud.CanvasRoot);
            HUDController.Stretch(rt);
            rt.gameObject.AddComponent<WinScreen>();
        }

        Image _flash;
        Image _figure;
        CanvasGroup _folioGroup, _textGroup;
        Text[] _marginLines;
        CanvasGroup[] _thumbGroups;

        // ("you never took it." was cut when the finale arrived — she takes
        // this one herself, by her own hand, at the door.)
        static readonly string[] MarginCopy =
        {
            "frame 11 — the last on the roll.",
            "the only frame with you in it.",
        };

        void Start()
        {
            StartCoroutine(Sequence());
        }

        IEnumerator Sequence()
        {
            // full white flash, held 0.1 s
            _flash = HUDController.NewImage("WinFlash", transform, Color.white);
            HUDController.Stretch(_flash.rectTransform);
            yield return new WaitForSeconds(0.1f);

            BuildFrame();

            // fade the flash out to reveal the framed, still-blank paper
            yield return Fade(t => _flash.color = new Color(1f, 1f, 1f, 1f - t), 0.35f);
            yield return new WaitForSeconds(0.45f);

            // contact sheet: the roll develops along the strip, frame by frame —
            // ten photographs of this exact run
            if (_thumbGroups != null)
            {
                for (int i = 0; i < _thumbGroups.Length; i++)
                {
                    if (_thumbGroups[i] == null) continue;
                    var g = _thumbGroups[i];
                    if (AudioDirector.Instance != null) AudioDirector.Instance.PlayTick();
                    yield return Fade(t => g.alpha = t, 0.22f);
                    yield return new WaitForSeconds(0.1f);
                }
                yield return new WaitForSeconds(0.3f);
            }

            // the print develops: her silhouette surfaces over ~2 s
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayDevelopLong();
            yield return Fade(t =>
            {
                var c = _figure.color; c.a = t; _figure.color = c;
            }, 1.9f);
            yield return Fade(t => _folioGroup.alpha = t, 0.4f);
            yield return new WaitForSeconds(0.35f);

            // margin notes, one truth at a time
            foreach (var line in _marginLines)
            {
                yield return Fade(t =>
                {
                    var c = line.color; c.a = t; line.color = c;
                }, 0.45f);
                yield return new WaitForSeconds(0.75f);
            }

            yield return Fade(t => _textGroup.alpha = t, 0.6f);
        }

        static IEnumerator Fade(System.Action<float> apply, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                apply(Mathf.Clamp01(t / dur));
                yield return null;
            }
            apply(1f);
        }

        void BuildFrame()
        {
            // slight desaturation wash
            var desat = HUDController.NewImage("Desat", transform, new Color(0.8f, 0.8f, 0.78f, 0.12f));
            HUDController.Stretch(desat.rectTransform);

            // 4 dark film bars
            var barColor = new Color(0.04f, 0.04f, 0.04f, 0.92f);
            var top = HUDController.NewImage("BarTop", transform, barColor);
            SetBar(top.rectTransform, new Vector2(0.5f, 1f), new Vector2(1920f, 110f));
            var bottom = HUDController.NewImage("BarBottom", transform, barColor);
            SetBar(bottom.rectTransform, new Vector2(0.5f, 0f), new Vector2(1920f, 110f));
            var left = HUDController.NewImage("BarLeft", transform, barColor);
            SetBar(left.rectTransform, new Vector2(0f, 0.5f), new Vector2(130f, 1080f));
            var right = HUDController.NewImage("BarRight", transform, barColor);
            SetBar(right.rectTransform, new Vector2(1f, 0.5f), new Vector2(130f, 1080f));

            // thin inner white frame
            var frameColor = new Color(0.95f, 0.95f, 0.93f, 0.9f);
            var fTop = HUDController.NewImage("FrameTop", transform, frameColor);
            HUDController.Place(fTop.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1664f, 4f));
            var fBottom = HUDController.NewImage("FrameBottom", transform, frameColor);
            HUDController.Place(fBottom.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1664f, 4f));
            var fLeft = HUDController.NewImage("FrameLeft", transform, frameColor);
            HUDController.Place(fLeft.rectTransform, new Vector2(0f, 0.5f), new Vector2(130f, 0f), new Vector2(4f, 864f));
            var fRight = HUDController.NewImage("FrameRight", transform, frameColor);
            HUDController.Place(fRight.rectTransform, new Vector2(1f, 0.5f), new Vector2(-130f, 0f), new Vector2(4f, 864f));

            // the eleventh frame: warm photo paper, her silhouette still latent
            var paper = HUDController.NewImage("PhotoPaper", transform, new Color(0.93f, 0.90f, 0.84f, 1f));
            HUDController.Place(paper.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(340f, 460f));
            HUDController.AddBorder(paper.rectTransform, new Color(0.10f, 0.10f, 0.11f, 0.85f));

            // BOOKEND: the once-blank eleventh frame was never empty — she was always
            // LATENT in it. A faint ghost sits on the warm paper from the moment it is
            // revealed; the full self-portrait then develops in over it. (The prologue's
            // blank-paper door, answered — and it lands even for players who skipped it.)
            var latent = HUDController.NewImage("SelfPortraitLatent", paper.transform, new Color(1f, 1f, 1f, 0.12f));
            latent.sprite = SilhouetteArt.PlayerIdle;
            latent.preserveAspect = true;
            HUDController.Place(latent.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(180f, 330f));

            _figure = HUDController.NewImage("SelfPortrait", paper.transform, new Color(1f, 1f, 1f, 0f));
            _figure.sprite = SilhouetteArt.PlayerIdle;
            _figure.preserveAspect = true;
            HUDController.Place(_figure.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(180f, 330f));

            var folioRT = HUDController.NewRect("Folio", paper.transform);
            _folioGroup = folioRT.gameObject.AddComponent<CanvasGroup>();
            _folioGroup.alpha = 0f;
            var folio = HUDController.NewText("PaperCaption", folioRT, "self-portrait.",
                19, new Color(0.32f, 0.30f, 0.28f, 1f), TextAnchor.MiddleCenter, display: true);
            HUDController.Place(folioRT, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(300f, 26f));
            HUDController.Stretch(folio.rectTransform);

            // margin notes under the paper (surface one by one)
            _marginLines = new Text[MarginCopy.Length];
            for (int i = 0; i < MarginCopy.Length; i++)
            {
                _marginLines[i] = HUDController.NewText("Margin" + i, transform, MarginCopy[i],
                    21, new Color(0.78f, 0.76f, 0.72f, 0f), TextAnchor.MiddleCenter, display: true);
                HUDController.Place(_marginLines[i].rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -104f - i * 36f), new Vector2(900f, 30f));
            }

            // the DEVELOPED block (arrives last, as a group)
            var blockRT = HUDController.NewRect("WinBlock", transform);
            HUDController.Stretch(blockRT);
            _textGroup = blockRT.gameObject.AddComponent<CanvasGroup>();
            _textGroup.alpha = 0f;

            var text = HUDController.NewText("WinText", blockRT,
                "DEVELOPED.\nThe final image holds.\nPress R to restart.",
                34, new Color(0.96f, 0.94f, 0.90f, 1f), TextAnchor.MiddleCenter, display: true, shadow: true);
            text.lineSpacing = 1.4f;
            HUDController.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -256f), new Vector2(1200f, 160f));

            var gm = GameManager.Instance;
            if (gm != null)
            {
                var time = HUDController.NewText("WinTime", blockRT,
                    "TIME  " + HUDController.FormatTime(gm.RunTime),
                    24, new Color(0.80f, 0.78f, 0.74f, 1f), TextAnchor.MiddleCenter);
                HUDController.Place(time.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -356f), new Vector2(600f, 36f));
            }

            // photo caption, like the margin print on a contact sheet:
            // every burned print was a retake
            int deaths = gm != null ? gm.Deaths : 0;
            string take = deaths == 0 ? "first take." : "take " + (deaths + 1) + ".";
            var caption = HUDController.NewText("WinCaption", blockRT,
                "f/1.4 · 1/125 · ISO 400 — SELF PORTRAIT, frame 11 of 11 · " + take,
                20, new Color(0.55f, 0.53f, 0.50f, 1f), TextAnchor.MiddleCenter);
            HUDController.Place(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(1000f, 30f));

            BuildContactStrip();
        }

        /// Eleven slots along the bottom film bar. Captured frames show the
        /// player's own run; a frame the album missed stays a dark slot.
        /// If nothing was captured at all, the strip is skipped entirely.
        void BuildContactStrip()
        {
            var album = PhotoAlbum.Instance;
            if (album == null || !album.HasAny) return;

            const int count = 11;
            const float slotW = 134f, slotH = 96f, gap = 8f;
            float total = count * slotW + (count - 1) * gap;
            float x0 = -total / 2f + slotW / 2f;

            _thumbGroups = new CanvasGroup[count];
            for (int i = 0; i < count; i++)
            {
                var slotRT = HUDController.NewRect("Frame" + (i + 1), transform);
                // pivot sits at the rect bottom: y=7 centers the 96px slot
                // inside the 110px film bar (and clear of the caption at 130)
                HUDController.Place(slotRT, new Vector2(0.5f, 0f),
                    new Vector2(x0 + i * (slotW + gap), 7f), new Vector2(slotW, slotH));
                var g = slotRT.gameObject.AddComponent<CanvasGroup>();
                g.alpha = 0f;
                _thumbGroups[i] = g;

                // strip position i (0..10) reads as frame i+1. Frames 1..10 are the
                // journey (album slots 1..10); frame 11 is the self-portrait, which
                // the finale printed into slot 0 (the once-blank unprinted frame).
                var shot = album.Shot(i < 10 ? i + 1 : 0);
                // white print border (dim if the frame was never caught)
                var border = HUDController.NewImage("Border", slotRT,
                    shot != null ? new Color(0.90f, 0.88f, 0.83f, 1f) : new Color(0.30f, 0.29f, 0.27f, 1f));
                HUDController.Stretch(border.rectTransform);

                if (shot != null)
                {
                    var imgRT = HUDController.NewRect("Shot", slotRT);
                    HUDController.Place(imgRT, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(slotW - 8f, slotH - 26f));
                    var raw = imgRT.gameObject.AddComponent<RawImage>();
                    raw.texture = shot;
                    raw.raycastTarget = false;
                }
                else
                {
                    var dark = HUDController.NewImage("Missing", slotRT, new Color(0.07f, 0.07f, 0.08f, 1f));
                    HUDController.Place(dark.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(slotW - 8f, slotH - 26f));
                }

                // frame number on the white margin, contact-sheet style
                var num = HUDController.NewText("Num", slotRT, (i + 1).ToString(),
                    12, new Color(0.25f, 0.24f, 0.22f, 1f), TextAnchor.MiddleCenter);
                HUDController.Place(num.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(40f, 16f));
            }
        }

        static void SetBar(RectTransform rt, Vector2 anchor, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }
    }
}
