using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkroom
{
    /// Room 9 set piece — the only time the world acts on its own.
    /// The drop gets a rising wind and a camera that can't keep up; the
    /// corridor lamps hold for a breath after touchdown, then die. In the
    /// dark the slider refuses input, the ambience sinks to near-silence,
    /// footsteps grow loud, and three sleeping shades — her old subjects —
    /// open their eyes a sliver as she passes. The lamp at the stairs
    /// snaps everything back with a shutter click.
    public class ScriptedBlackout : MonoBehaviour
    {
        // shaft (fall phase) and corridor extents, from LevelData Room 9
        const float ShaftMinX = 126.8f, ShaftMaxX = 129.6f;
        const float ShaftMinY = 0f, ShaftMaxY = 8f;
        const float CorridorMinX = 125f, CorridorMaxX = 142f;
        // the blackout must start only once she is on the SOLID corridor (past
        // the shadow lift's landing) — locking to NORMAL while she still stood
        // on the UNDER-only lift would vanish it and drop her into the void
        const float CorridorLandX = 129.6f;
        const float RelightX = 138.5f; // comfortably before the relocated stairs hint
        const float FuseSeconds = 25f; // absolute cap: the dark never outlives this

        static readonly float[] ShadeX = { 130.6f, 133.4f, 136.2f };
        const float ShadeY = -1.1f; // seated on the corridor floor (top at -1.5)

        bool _fallArmed = true;
        bool _mainArmed = true;
        bool _running;

        readonly List<Light2D> _lampLights = new List<Light2D>();
        readonly List<SpriteRenderer> _lampGlows = new List<SpriteRenderer>();
        SpriteRenderer[] _glints;
        float[] _glintAlpha;
        Transform[] _shades;
        SpriteRenderer _heldPhoto;   // middle shade cradles one of the player's own frames
        bool _heldBuilt;
        const int HeldFrame = 5;     // "First Stroke" — a frame from earlier in this very run
        CameraFollow _cam;
        Coroutine _co;

        void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnRespawn += Abort;
            var camGO = Camera.main;
            if (camGO != null) _cam = camGO.GetComponent<CameraFollow>();
            FindCorridorLamps();
            BuildShades();
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnRespawn -= Abort;
        }

        /// Corridor cone lamps live under the persistent backdrop; pick only
        /// the low ones (y &lt; 5) inside the corridor span so the ceiling
        /// lamps of the upper rooms are never touched.
        void FindCorridorLamps()
        {
            var lampsRoot = GameObject.Find("_Backdrop/Lamps");
            if (lampsRoot == null) return;
            foreach (Transform lamp in lampsRoot.transform)
            {
                var p = lamp.position;
                if (p.y >= 5f || p.x < CorridorMinX || p.x > CorridorMaxX) continue;
                _lampLights.AddRange(lamp.GetComponentsInChildren<Light2D>());
                foreach (var sr in lamp.GetComponentsInChildren<SpriteRenderer>())
                    if (sr.sharedMaterial == VisualFactory.GlowMat)
                        _lampGlows.Add(sr); // bulb + light cone read "lit"
            }
        }

        /// Three of her photo subjects, asleep along the corridor. Pure
        /// silhouettes: no colliders, no enemy logic — they never block or
        /// kill, they only watch.
        void BuildShades()
        {
            _shades = new Transform[ShadeX.Length];
            _glints = new SpriteRenderer[ShadeX.Length * 2];
            _glintAlpha = new float[ShadeX.Length];
            for (int i = 0; i < ShadeX.Length; i++)
            {
                var go = new GameObject("CorridorShade" + i);
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(ShadeX[i], ShadeY, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SilhouetteArt.EnemyAsleep;
                sr.sharedMaterial = VisualFactory.SpriteMat;
                sr.sortingOrder = VisualFactory.OrderEnemy;
                sr.flipX = i % 2 == 1;
                _shades[i] = go.transform;

                // two eye slits, the same white as her own glowing eye
                for (int e = 0; e < 2; e++)
                {
                    var eye = new GameObject("Glint" + e);
                    eye.transform.SetParent(go.transform, false);
                    eye.transform.localPosition = new Vector3(e == 0 ? -0.13f : 0.13f, -0.01f, 0f);
                    eye.transform.localScale = new Vector3(0.11f, 0.035f, 1f);
                    var esr = eye.AddComponent<SpriteRenderer>();
                    esr.sprite = VisualFactory.WhiteSprite;
                    esr.sharedMaterial = VisualFactory.GlowMat;
                    esr.color = new Color(1f, 1f, 1f, 0f);
                    esr.sortingOrder = VisualFactory.OrderEnemy + 1;
                    _glints[i * 2 + e] = esr;
                }

                // the middle shade (i==1) cradles a developed print — one of the
                // player's OWN earlier frames — so "the shades are her subjects" is a
                // SEEN truth, not just a code comment. Self-lit (GlowMat, like the
                // eyes) so it reads in the blackout; the texture is fetched lazily.
                if (i == 1)
                {
                    var photoGO = new GameObject("HeldPhoto");
                    photoGO.transform.SetParent(go.transform, false);
                    photoGO.transform.localPosition = new Vector3(0f, -0.04f, 0f);
                    photoGO.transform.localScale = new Vector3(0.10f, 0.10f, 1f);
                    var psr = photoGO.AddComponent<SpriteRenderer>();
                    psr.sharedMaterial = VisualFactory.GlowMat;
                    psr.sortingOrder = VisualFactory.OrderEnemy + 1;
                    psr.color = new Color(1f, 1f, 1f, 0f);
                    _heldPhoto = psr;
                }
            }
        }

        void Update()
        {
            if (PauseController.IsPaused) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null || gm.HasWon) return;
            var p = gm.Player.transform.position;

            // descent breath: wind up, camera trails the drop. Position-based
            // (not velocity) so it arms for the slow shadow-lift descent too.
            if (_fallArmed && p.x > ShaftMinX && p.x < ShaftMaxX
                && p.y > ShaftMinY && p.y < 6f)
            {
                _fallArmed = false;
                if (AudioDirector.Instance != null) AudioDirector.Instance.SetWind(0.3f);
                if (_cam != null) _cam.LagScale = 2.4f;
            }

            // touchdown on the solid corridor starts the blackout
            if (_mainArmed && !_fallArmed && p.y < 0f && gm.Player.IsGrounded
                && p.x > CorridorLandX && p.x < CorridorMaxX)
            {
                _mainArmed = false;
                _co = StartCoroutine(Run());
            }
        }

        IEnumerator Run()
        {
            _running = true;
            var em = ExposureManager.Instance;
            var ad = AudioDirector.Instance;
            var gm = GameManager.Instance;

            // touchdown: the wind dies, the camera catches up
            if (ad != null) ad.SetWind(0f);
            if (_cam != null) _cam.LagScale = 1f;

            // the lamps hold for a breath...
            yield return new WaitForSeconds(0.8f);

            // ...flicker once...
            SetLamps(false);
            yield return new WaitForSeconds(0.06f);
            SetLamps(true);
            yield return new WaitForSeconds(0.08f);

            // ...and the darkroom decides
            if (em != null)
            {
                em.ForceSet(Exposure.Normal, true);
                em.SetLocked(true);
            }
            SetLamps(false);
            if (LightDirector.Instance != null)
                LightDirector.Instance.SetOverride(new Color(0.30f, 0.34f, 0.46f), 0.05f);
            if (ad != null)
            {
                ad.DuckAmbience(0.1f, 2.5f);
                ad.FootstepBoost = 1.8f;
            }

            // walk in the dark until the stairs (or the fuse burns out)
            float t = 0f;
            while (t < FuseSeconds)
            {
                t += Time.deltaTime;
                var player = gm != null ? gm.Player : null;
                if (player != null && player.transform.position.x >= RelightX) break;
                UpdateGlints(player);
                yield return null;
            }

            Restore(true);
            _co = null;
        }

        void UpdateGlints(PlayerController player)
        {
            if (player == null) return;
            float px = player.transform.position.x;
            for (int i = 0; i < _shades.Length; i++)
            {
                bool near = Mathf.Abs(px - ShadeX[i]) < 1.4f;
                float a = _glintAlpha[i];
                a = near ? Mathf.MoveTowards(a, 1f, Time.deltaTime / 0.35f)
                         : Mathf.MoveTowards(a, 0f, Time.deltaTime / 0.6f);
                _glintAlpha[i] = a;
                SetGlint(i, a);
            }

            // develop the held print as she nears the middle shade. Fetched lazily
            // (frame 5 was captured back in R5); stays invisible if the album never
            // got it (skipped checkpoint or a failed render) — graceful by default.
            if (_heldPhoto != null)
            {
                if (!_heldBuilt && PhotoAlbum.Instance != null)
                {
                    var tex = PhotoAlbum.Instance.Shot(HeldFrame);
                    if (tex != null)
                    {
                        _heldPhoto.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f), 100f);
                        _heldBuilt = true;
                    }
                }
                float a = _heldBuilt ? _glintAlpha[1] * 0.5f : 0f;
                _heldPhoto.color = new Color(0.72f, 0.80f, 0.90f, a); // dim, cool — a developed ghost
            }
        }

        void SetGlint(int i, float a)
        {
            var c = new Color(1f, 1f, 1f, a * 0.9f);
            _glints[i * 2].color = c;
            _glints[i * 2 + 1].color = c;
        }

        void SetLamps(bool on)
        {
            foreach (var l in _lampLights)
                if (l != null) l.enabled = on;
            foreach (var sr in _lampGlows)
                if (sr != null) sr.enabled = on;
        }

        /// Atomic restore — also the abort path, written first and kept dumb.
        void Restore(bool withSnap)
        {
            if (!_running) return;
            _running = false;
            SetLamps(true);
            if (LightDirector.Instance != null) LightDirector.Instance.ClearOverride();
            if (ExposureManager.Instance != null) ExposureManager.Instance.SetLocked(false);
            var ad = AudioDirector.Instance;
            if (ad != null)
            {
                ad.DuckAmbience(1f, withSnap ? 3f : 6f);
                ad.FootstepBoost = 1f;
                ad.SetWind(0f);
                if (withSnap) ad.PlayClick(); // the world takes its picture back
            }
            for (int i = 0; i < _glintAlpha.Length; i++) { _glintAlpha[i] = 0f; SetGlint(i, 0f); }
            if (_heldPhoto != null) { var c = _heldPhoto.color; c.a = 0f; _heldPhoto.color = c; }
            if (_cam != null) _cam.LagScale = 1f;
        }

        /// Fired on EVERY respawn (GameManager.OnRespawn) — but only a death
        /// that touched the sequence may disarm it. A death anywhere else in
        /// the run must leave the set piece armed.
        void Abort()
        {
            // nothing has started: stay armed
            if (_fallArmed && _mainArmed) return;

            if (_mainArmed)
            {
                // died mid-fall: clean up the breath, let the drop replay
                // (respawn at CP_R9a puts the player back above the shaft)
                if (AudioDirector.Instance != null) AudioDirector.Instance.SetWind(0f);
                if (_cam != null) _cam.LagScale = 1f;
                _fallArmed = true;
                return;
            }

            // the blackout itself ran (or is running): restore, never re-arm
            if (_co != null) { StopCoroutine(_co); _co = null; }
            _fallArmed = false;
            if (AudioDirector.Instance != null) AudioDirector.Instance.SetWind(0f);
            if (_cam != null) _cam.LagScale = 1f;
            Restore(false);
        }
    }
}
