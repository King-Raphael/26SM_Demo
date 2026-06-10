using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Darkroom
{
    /// "Taking the final photograph": 0.1 s white flash, slight desaturation,
    /// film border (4 dark bars + inner white frame) and the win text.
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

        void Start()
        {
            StartCoroutine(Sequence());
        }

        IEnumerator Sequence()
        {
            // full white flash, held 0.1 s
            var flash = HUDController.NewImage("WinFlash", transform, Color.white);
            HUDController.Stretch(flash.rectTransform);
            yield return new WaitForSeconds(0.1f);

            BuildFrame();

            // fade the flash out to reveal the framed final image
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                flash.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / 0.35f)));
                yield return null;
            }
            flash.color = new Color(1f, 1f, 1f, 0f);
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

            var text = HUDController.NewText("WinText", transform,
                "DEVELOPED.\nThe final image holds.\nPress R to restart.",
                40, new Color(0.96f, 0.94f, 0.90f, 1f), TextAnchor.MiddleCenter);
            text.lineSpacing = 1.4f;
            HUDController.Place(text.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 320f));

            var gm = GameManager.Instance;
            if (gm != null)
            {
                var time = HUDController.NewText("WinTime", transform,
                    "TIME  " + HUDController.FormatTime(gm.RunTime),
                    26, new Color(0.80f, 0.78f, 0.74f, 1f), TextAnchor.MiddleCenter);
                HUDController.Place(time.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -210f), new Vector2(600f, 40f));
            }

            // photo caption, like the margin print on a contact sheet
            var caption = HUDController.NewText("WinCaption", transform,
                "f/1.4 · 1/125 · ISO 400 — THE DARKROOM, frame 11 of 11",
                20, new Color(0.55f, 0.53f, 0.50f, 1f), TextAnchor.MiddleCenter);
            HUDController.Place(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(900f, 30f));
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
