using System.Collections;
using UnityEngine;

namespace Darkroom
{
    /// A print pinned on the drying line. Arriving sets the respawn point, captures this
    /// room's frame, winds the film gate in (HUDController.RevealFilmGate), and DEVELOPS the
    /// hanging print: the cool negative warms to a finished print, a soft glow rises, the
    /// clothespin snaps. Built by LevelBuilder.CheckpointAt, which injects the part refs.
    public class Checkpoint : MonoBehaviour
    {
        /// Contact-sheet margin note shown when this photo develops.
        public string Caption = "";
        /// Authoritative owning room (CP_R5 sits 1 unit left of its room's
        /// boundary, so deriving the room from x misfiles its frame).
        public int RoomIndex = -1;

        // visual parts (injected by the builder; all null-guarded)
        public SpriteRenderer Frame, Glow;
        public Transform Clip;

        bool _developed;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.layer != Layers.Player) return;

            // always refresh the respawn point (re-touch after a backtrack is fine)
            if (GameManager.Instance != null)
                GameManager.Instance.SetCheckpoint(transform.position, Caption);

            if (_developed) return; // develop / capture / wind the gate only once
            _developed = true;

            // the developing photo IS the photograph: snap this room's frame. Room 0 (the
            // prologue) is the BLANK unprinted frame — never photographed here; the finale
            // prints the self-portrait into slot 0.
            int room = RoomIndex >= 0 ? RoomIndex : LevelData.RoomIndexAt(transform.position.x);
            if (room != 0 && PhotoAlbum.Instance != null)
                PhotoAlbum.Instance.CaptureRoom(room);

            if (HUDController.Instance != null) HUDController.Instance.RevealFilmGate();
            StartCoroutine(Develop());
        }

        IEnumerator Develop()
        {
            if (AudioDirector.Instance != null)
            {
                AudioDirector.Instance.PlayFilmAdvance(); // frame locked, next armed
                AudioDirector.Instance.PlayDevelop();      // the print surfaces
            }
            if (Frame != null)
                StrokeSparkle.Burst(Frame.transform.position, new Color(0.95f, 0.90f, 0.78f, 1f), 8);

            Color from = Frame != null ? Frame.color : Color.white;
            var to = new Color(0.96f, 0.90f, 0.80f, 1f); // a finished warm print
            var baseClip = Clip != null ? Clip.localScale : Vector3.one;

            float t = 0f; const float dur = 0.7f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                if (Frame != null) Frame.color = Color.Lerp(from, to, k);
                if (Glow != null) { var g = Glow.color; g.a = Mathf.Lerp(0f, 0.5f, k); Glow.color = g; }
                if (Clip != null) { float p = 1f + 0.25f * Mathf.Sin(k * Mathf.PI); Clip.localScale = new Vector3(baseClip.x * p, baseClip.y * p, 1f); }
                yield return null;
            }
            if (Frame != null) Frame.color = to;
            if (Glow != null) { var g = Glow.color; g.a = 0.5f; Glow.color = g; }
            if (Clip != null) Clip.localScale = baseClip;
        }
    }
}
