using UnityEngine;

namespace Darkroom
{
    public class Checkpoint : MonoBehaviour
    {
        /// Contact-sheet margin note shown when this photo develops.
        public string Caption = "";
        /// Authoritative owning room (CP_R5 sits 1 unit left of its room's
        /// boundary, so deriving the room from x misfiles its frame).
        public int RoomIndex = -1;

        SpriteRenderer _marker;

        void Awake() { _marker = GetComponentInChildren<SpriteRenderer>(); }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.layer != Layers.Player) return;
            if (GameManager.Instance != null)
                GameManager.Instance.SetCheckpoint(transform.position, Caption);
            if (_marker != null)
            {
                _marker.color = Color.white; // developed: the photo brightens
                StrokeSparkle.Burst(_marker.transform.position, new Color(0.92f, 0.92f, 0.86f, 1f), 8);
            }
            // the developing photo IS the photograph: snap this room's frame.
            // Room 0 (the prologue) is the BLANK unprinted frame — it is never
            // photographed here; the finale prints the self-portrait into slot 0.
            int room = RoomIndex >= 0 ? RoomIndex : LevelData.RoomIndexAt(transform.position.x);
            if (room != 0 && PhotoAlbum.Instance != null)
                PhotoAlbum.Instance.CaptureRoom(room);
        }
    }
}
