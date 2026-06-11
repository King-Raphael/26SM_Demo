using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Darkroom
{
    /// Eleven frames, captured live. The first checkpoint of each room
    /// quietly photographs the player's own run (frame 11 is retaken at
    /// the finale). The win screen then develops them along the film
    /// strip — the answer to "what was she photographing" is this exact
    /// playthrough. Every step degrades gracefully: if no render path
    /// works the album stays empty and the ending simply skips the strip.
    public class PhotoAlbum : MonoBehaviour
    {
        public static PhotoAlbum Instance { get; private set; }

        const int W = 480, H = 270;

        readonly Texture2D[] _shots = new Texture2D[11];
        bool _renderRequestBroken;

        void Awake() { Instance = this; }

        public bool HasAny
        {
            get { foreach (var s in _shots) if (s != null) return true; return false; }
        }

        public Texture2D Shot(int frame) =>
            frame >= 0 && frame < _shots.Length ? _shots[frame] : null;

        public void CaptureRoom(int room, bool overwrite = false)
        {
            if (room < 0 || room >= _shots.Length) return;
            if (_shots[room] != null && !overwrite) return;
            StartCoroutine(CaptureRoutine(room));
        }

        IEnumerator CaptureRoutine(int room)
        {
            yield return new WaitForEndOfFrame(); // never mid-physics
            var cam = Camera.main;
            if (cam == null) yield break;

            var rt = RenderTexture.GetTemporary(W, H, 24);
            if (TryRender(cam, rt))
            {
                var tex = ReadAndGrade(rt);
                if (tex != null)
                {
                    if (_shots[room] != null) Destroy(_shots[room]);
                    _shots[room] = tex;
                }
            }
            RenderTexture.ReleaseTemporary(rt);
        }

        /// Unity 6 / URP 17 path first (SubmitRenderRequest), classic
        /// targetTexture render as the fallback. The screen-space-overlay
        /// HUD is naturally excluded from both.
        bool TryRender(Camera cam, RenderTexture rt)
        {
            if (!_renderRequestBroken)
            {
                try
                {
                    var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                    if (RenderPipeline.SupportsRenderRequest(cam, req))
                    {
                        RenderPipeline.SubmitRenderRequest(cam, req);
                        return true;
                    }
                    _renderRequestBroken = true;
                }
                catch (System.Exception)
                {
                    _renderRequestBroken = true;
                }
            }
            try
            {
                var prev = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prev;
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        /// Read back, validate (an all-black frame means the render path
        /// silently failed) and lift ~25% so Under shots stay readable on
        /// the dark film strip.
        Texture2D ReadAndGrade(RenderTexture rt)
        {
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            RenderTexture.active = prevActive;

            var px = tex.GetPixels32();
            int peak = 0;
            for (int i = 0; i < px.Length; i += 977)
                peak = Mathf.Max(peak, Mathf.Max(px[i].r, Mathf.Max(px[i].g, px[i].b)));
            if (peak < 3) { Destroy(tex); return null; }

            for (int i = 0; i < px.Length; i++)
            {
                px[i].r = (byte)Mathf.Min(255, px[i].r * 5 / 4);
                px[i].g = (byte)Mathf.Min(255, px[i].g * 5 / 4);
                px[i].b = (byte)Mathf.Min(255, px[i].b * 5 / 4);
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        public void Clear()
        {
            for (int i = 0; i < _shots.Length; i++)
                if (_shots[i] != null) { Destroy(_shots[i]); _shots[i] = null; }
        }
    }
}
