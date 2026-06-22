using UnityEngine;
using UnityEngine.Networking;

namespace Darkroom
{
    /// Procedurally synthesized audio — no external assets (spec stretch #1).
    /// Shutter click on every switch, low hum while Underexposed, bright hiss
    /// while Overexposed, dull jam click, pickup chime, win shutter.
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        const int SR = 44100;

        AudioSource _sfx, _foot, _hum, _hiss, _draw, _room, _wind, _pedal, _fan;
        AudioClip _click, _jam, _pickup, _win, _advance, _beep, _chord, _fix, _drip;
        AudioClip _footstep, _jump, _land, _death, _develop, _developLong, _checkpoint, _door;
        bool _pedalCut;
        float _humTarget, _hissTarget, _drawTarget, _windTarget, _fanTarget;

        // sound-design pass: new mechanic / ambience / UI beds + one-shots
        AudioSource _burn, _lift, _mood, _preview, _charge, _expSfx;
        AudioClip _burnThrough, _umbraOpen, _umbraSeal, _fixPlatform;
        AudioClip _enemyWake, _enemyFreeze, _frameCard, _hintPop, _deathEnemy, _deathSoft;
        float _burnReq, _moodTarget, _moodPitch = 1f, _previewTarget, _chargeTarget;
        bool _liftActive;
        float _nextDripTime;

        // background music: a melodic track loaded from StreamingAssets/music,
        // looped over the evolving ambient bed. Ducks with the scripted hush,
        // holds back through the prologue and the finale's silence.
        AudioSource _music;
        bool _musicLoaded;
        public float MusicVolume = 0.20f;
        readonly System.Random _rng = new System.Random(20260611);

        // Single owner of the ambience level: every bed (room tone, hum, hiss)
        // is scaled by one duck factor so scripted moments can't fight each other.
        float _duck = 1f, _duckTarget = 1f, _duckSpeed = 1.2f;

        /// Pull the whole ambience bed toward `level` (0..1). The Room 9
        /// blackout sinks it to near-silence; pass 1 to let it back up.
        public void DuckAmbience(float level, float speed = 1.2f)
        {
            _duckTarget = Mathf.Clamp01(level);
            _duckSpeed = speed;
        }

        /// Footsteps read louder when everything else has gone quiet.
        public float FootstepBoost = 1f;

        void Awake()
        {
            Instance = this;
            _sfx = NewSource(false);
            _foot = NewSource(false);
            _click = BuildShutterClick(0.14f, false);
            _win = BuildShutterClick(0.4f, true);
            _jam = BuildJamClick();
            _pickup = BuildPickupChime();
            _footstep = BuildFootstep();
            _jump = BuildJump();
            _land = BuildLand();
            _death = BuildDeath();
            _develop = BuildDevelop();
            _developLong = BuildDevelopLong();
            _checkpoint = BuildCheckpoint();
            _door = BuildDoor();
            _advance = BuildFilmAdvance();
            _beep = BuildBeep();
            _chord = BuildFinaleChord();
            _fix = BuildFixBlip();
            _drip = BuildDrip();

            _hum = NewSource(true);
            _hum.clip = BuildHumLoop();
            _hum.volume = 0f;
            _hum.Play();

            _hiss = NewSource(true);
            _hiss.clip = BuildHissLoop();
            _hiss.volume = 0f;
            _hiss.Play();

            _draw = NewSource(true);
            _draw.clip = BuildDrawLoop();
            _draw.volume = 0f;
            _draw.Play();

            // room tone: a barely-there noise floor so silence isn't dead
            _room = NewSource(true);
            _room.clip = BuildRoomTone();
            _room.volume = 0.05f;
            _room.Play();

            // wind: only audible during the Room 9 drop
            _wind = NewSource(true);
            _wind.clip = BuildWindLoop();
            _wind.volume = 0f;
            _wind.Play();

            // fan: the darkroom's ventilation bed, leaned on during the black-screen
            // open and dropped to a faint hush once the world develops up
            _fan = NewSource(true);
            _fan.clip = BuildFanLoop();
            _fan.volume = 0f;
            _fan.Play();

            // pedal tone: a near-subliminal throb that climbs through Room 10
            // and cuts to dead silence the instant the finale begins
            _pedal = NewSource(true);
            _pedal.clip = BuildPedalLoop();
            _pedal.volume = 0f;
            _pedal.Play();

            // --- sound-design pass: mechanic / ambience / UI beds + one-shots ---
            _burn = NewSource(true);    _burn.clip = BuildBurnLoop();       _burn.volume = 0f; _burn.Play();
            _lift = NewSource(true);    _lift.clip = BuildLiftLoop();       _lift.volume = 0f; _lift.Play();
            _mood = NewSource(true);    _mood.clip = BuildMoodDrone();      _mood.volume = 0f; _mood.Play();
            _preview = NewSource(true); _preview.clip = BuildPreviewTone();  _preview.volume = 0f; _preview.Play();
            _charge = NewSource(true);  _charge.clip = BuildChargeTone();   _charge.volume = 0f; _charge.Play();
            _expSfx = NewSource(false); // the exposure-switch click, pitched per target state
            _music = NewSource(true);   _music.volume = 0f;                 // clip loaded async in Start()

            _burnThrough = BuildBurnThrough();
            _umbraOpen   = BuildUmbraOpen();
            _umbraSeal   = BuildUmbraSeal();
            _fixPlatform = BuildFixPlatform();
            _enemyWake   = BuildEnemyWake();
            _enemyFreeze = BuildEnemyFreeze();
            _frameCard   = BuildFrameCardChime();
            _hintPop     = BuildHintPop();
            _deathEnemy  = BuildDeathEnemy();
            _deathSoft   = BuildDeathSoft();
            ScheduleNextDrip();
        }

        AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        void Start()
        {
            if (ExposureManager.Instance != null)
                ExposureManager.Instance.OnExposureChanged += HandleExposure;
            StartCoroutine(LoadMusic());
        }

        void OnDestroy()
        {
            if (ExposureManager.Instance != null)
                ExposureManager.Instance.OnExposureChanged -= HandleExposure;
        }

        void HandleExposure(Exposure e)
        {
            bool silent = ExposureManager.Instance != null && ExposureManager.Instance.LastChangeSilent;
            if (!silent)
            {
                // same shutter, faintly coloured by where you're going: darker
                // toward Under, brighter toward Over. Its own source so the pitch
                // never bleeds into the other one-shots.
                _expSfx.pitch = e == Exposure.Underexposed ? 0.92f
                              : e == Exposure.Overexposed ? 1.08f : 1.0f;
                _expSfx.PlayOneShot(_click, 0.5f);
            }
            _humTarget = e == Exposure.Underexposed ? 0.22f : 0f;
            _hissTarget = e == Exposure.Overexposed ? 0.12f : 0f;
        }

        void Update()
        {
            _duck = Mathf.MoveTowards(_duck, _duckTarget, Time.deltaTime * _duckSpeed);
            _hum.volume = Mathf.MoveTowards(_hum.volume, Mathf.Min(0.4f, _humTarget + _humNudge) * _duck, Time.deltaTime * 1.2f);
            _hiss.volume = Mathf.MoveTowards(_hiss.volume, _hissTarget * _duck, Time.deltaTime * 1.2f);
            _room.volume = 0.05f * _duck;
            _draw.volume = Mathf.MoveTowards(_draw.volume, _drawTarget, Time.deltaTime * 3f);
            _wind.volume = Mathf.MoveTowards(_wind.volume, _windTarget, Time.deltaTime * 0.9f);
            _fan.volume = Mathf.MoveTowards(_fan.volume, _fanTarget * _duck, Time.deltaTime * 0.8f);

            // pedal rises with x through the final room — too slow to notice
            // arriving, impossible to miss when it stops
            float pedalTarget = 0f;
            var gm = GameManager.Instance;
            if (!_pedalCut && gm != null && gm.Player != null && !gm.HasWon)
            {
                float x = gm.Player.transform.position.x;
                if (x > 142.5f)
                    pedalTarget = Mathf.Lerp(0f, 0.10f, Mathf.Clamp01((x - 142.5f) / 26.5f));
            }
            _pedal.volume = Mathf.MoveTowards(_pedal.volume, pedalTarget * _duck, Time.deltaTime * 0.15f);

            // --- sound-design pass beds ---
            // burn: max-request consumed each frame (requesters are Update-driven,
            // so a 1-frame-stale read is fine — no FixedUpdate gutter risk)
            _burn.volume = Mathf.MoveTowards(_burn.volume, _burnReq, Time.deltaTime * 2.5f);
            _burnReq = 0f;
            // lift: edge-driven on/off (requesters are in FixedUpdate)
            _lift.volume = Mathf.MoveTowards(_lift.volume, _liftActive ? 0.09f : 0f, Time.deltaTime * 4f);
            // mood + charge are part of the ambience bed: they duck with everything else
            _mood.volume = Mathf.MoveTowards(_mood.volume, _moodTarget * _duck, Time.deltaTime * 0.5f);
            _mood.pitch  = Mathf.MoveTowards(_mood.pitch, _moodPitch, Time.deltaTime * 0.6f);
            _charge.volume = Mathf.MoveTowards(_charge.volume, _chargeTarget * _duck, Time.deltaTime * 3f);

            // preview tone is player-initiated UI — not ducked — but must die if a
            // death/win swallows the release that would otherwise stop it
            if (gm != null && (gm.IsRespawning || gm.HasWon)) _previewTarget = 0f;
            _preview.volume = Mathf.MoveTowards(_preview.volume, _previewTarget, Time.deltaTime * 6f);

            // sparse darkroom drips during calm exploration — never the prologue's
            // deliberate first drip, never during a scripted hush
            if (Time.time >= _nextDripTime)
            {
                ScheduleNextDrip();
                bool calm = _duck > 0.9f && gm != null && !gm.HasWon && !gm.IsRespawning && !gm.IsCinematic;
                int dripRoom = calm && gm.Player != null ? LevelData.RoomIndexAt(gm.Player.transform.position.x) : -1;
                if (dripRoom >= 1) _sfx.PlayOneShot(_drip, 0.22f);
            }

            // background music: present through the journey, but it lets the
            // prologue's intimate open breathe, ducks for the R9 blackout, and
            // falls silent for the finale's held breath / the win screen
            if (_musicLoaded)
            {
                float mt = MusicVolume;
                int mroom = gm != null && gm.Player != null ? LevelData.RoomIndexAt(gm.Player.transform.position.x) : 0;
                if (mroom < 1) mt = 0f;                 // music joins at Frame 1
                mt *= _duck;                            // ride the ambience duck
                if (gm != null && (gm.IsCinematic || gm.HasWon)) mt = 0f;
                _music.volume = Mathf.MoveTowards(_music.volume, mt, Time.deltaTime * 0.3f);
            }
        }

        /// The held breath before the final photograph.
        public void CutPedal()
        {
            _pedalCut = true;
            _pedal.volume = 0f;
        }

        public void ResetFinaleAudio() { _pedalCut = false; }

        /// Rushing air during the Room 9 drop (0 to cut).
        public void SetWind(float v) { _windTarget = Mathf.Clamp01(v); }

        /// The darkroom ventilation bed (the fan), leaned on in the black-screen
        /// open and dropped to a hush once the room develops up (0 to cut).
        public void SetFan(float v) { _fanTarget = Mathf.Clamp01(v); }

        /// A lone water drop in the dark — the prologue's first sound.
        public void PlayDrip() { _sfx.PlayOneShot(_drip, 0.5f); }

        /// Bare shutter click outside an exposure change (the blackout's relight).
        public void PlayClick() { _sfx.PlayOneShot(_click, 0.5f); }

        /// Soft tick for each contact-sheet thumbnail developing in.
        public void PlayTick() { _sfx.PlayOneShot(_click, 0.16f); }

        /// Viewfinder countdown beep (three before the final shot).
        public void PlayBeep() { _sfx.PlayOneShot(_beep, 0.3f); }

        /// The only melody in the game: the checkpoint's two notes plus one
        /// new one, sustained — everything she saved, resolving.
        public void PlayFinaleChord() { _sfx.PlayOneShot(_chord, 0.45f); }

        /// The journey arrives at the door: shutter clicks from far to near,
        /// faster and brighter until they meet the present.
        public System.Collections.IEnumerator FinaleBursts()
        {
            float interval = 0.5f, pitch = 0.8f, vol = 0.12f;
            for (int i = 0; i < 9; i++)
            {
                _foot.pitch = pitch;
                _foot.PlayOneShot(_click, vol);
                yield return new WaitForSeconds(interval);
                interval = Mathf.Max(0.09f, interval * 0.78f);
                pitch = Mathf.Min(1.6f, pitch * 1.09f);
                vol = Mathf.Min(0.5f, vol * 1.25f);
            }
            _foot.pitch = 1f;
        }

        float _humNudge;

        /// Momentarily lean on the under-hum (the title drop rides it), then let go.
        public void NudgeHum(float amount, float dur)
        {
            StartCoroutine(HumNudgeRoutine(amount, dur));
        }

        System.Collections.IEnumerator HumNudgeRoutine(float amount, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                _humNudge = Mathf.Lerp(amount, 0f, Mathf.Clamp01(t / dur));
                yield return null;
            }
            _humNudge = 0f;
        }

        public void PlayJam() { _sfx.PlayOneShot(_jam, 0.6f); }
        public void PlayPickup() { _sfx.PlayOneShot(_pickup, 0.45f); }

        public void PlayFootstep()
        {
            _foot.pitch = 0.9f + (float)_rng.NextDouble() * 0.25f;
            _foot.PlayOneShot(_footstep, 0.22f * FootstepBoost);
        }

        public void PlayJumpSound() { _sfx.PlayOneShot(_jump, 0.3f); }
        public void PlayLand(float strength01) { _sfx.PlayOneShot(_land, 0.15f + 0.35f * Mathf.Clamp01(strength01)); }
        public void PlayDeath() { _sfx.PlayOneShot(_death, 0.6f); }
        /// The print burns — coloured by cause: a sharper stab for the guard, a
        /// softer sigh for a deliberate restart (R is meant to be a quiet tool).
        public void PlayDeath(DeathCause cause)
        {
            switch (cause)
            {
                case DeathCause.Enemy:   _sfx.PlayOneShot(_deathEnemy, 0.6f); break;
                case DeathCause.Restart: _sfx.PlayOneShot(_deathSoft, 0.4f); break;
                default:                 _sfx.PlayOneShot(_death, 0.6f); break;
            }
        }
        public void PlayDevelop() { _sfx.PlayOneShot(_develop, 0.4f); }
        /// The win screen's slow print-surfacing swell.
        public void PlayDevelopLong() { _sfx.PlayOneShot(_developLong, 0.45f); }
        public void PlayCheckpoint() { _sfx.PlayOneShot(_checkpoint, 0.32f); }
        public void PlayDoor() { _sfx.PlayOneShot(_door, 0.55f); }
        /// The roll ratchets forward: the oldest stroke is wound away.
        public void PlayFilmAdvance() { _sfx.PlayOneShot(_advance, 0.4f); }
        /// The moment a drawn stroke fixes — a bright, higher "set" blip, distinct
        /// from the shutter click so creating terrain has its own voice.
        public void PlayFixStroke() { _sfx.PlayOneShot(_fix, 0.4f); }

        /// The "exposing" crackle while a stroke is being drawn.
        public void SetDrawing(bool drawing) { _drawTarget = drawing ? 0.2f : 0f; }

        public void PlayWin()
        {
            _humTarget = 0f;
            _hissTarget = 0f;
            _sfx.PlayOneShot(_win, 0.8f);
        }

        // ---------- sound-design pass: mechanic / ambience / UI API ----------

        /// Burning paper heat bed — each BurnPaper requests its char level every
        /// frame; the loudest wins and the bed falls silent when none is heating.
        public void RequestBurn(float level01) { _burnReq = Mathf.Max(_burnReq, Mathf.Clamp01(level01) * 0.10f); }
        /// Paper burns through: a rising whoosh + spark.
        public void PlayBurnThrough() { _sfx.PlayOneShot(_burnThrough, 0.7f); }

        /// A lift is carrying the player: bright/high pitch for the rising light
        /// slab, dark/low for the sinking shadow slab. Off when it stops moving.
        /// (One shared bed: this level never rides two lifts at once.)
        public void LiftOn(float pitch) { _liftActive = true; _lift.pitch = pitch; }
        public void LiftOff() { _liftActive = false; }

        /// Shadow recoils from delivered light (open) and floods back (reseal).
        public void PlayUmbraOpen() { _sfx.PlayOneShot(_umbraOpen, 0.5f); }
        public void PlayUmbraSeal() { _sfx.PlayOneShot(_umbraSeal, 0.5f); }

        /// A latent platform is printed permanently — a warm "set".
        public void PlayFixPlatform() { _sfx.PlayOneShot(_fixPlatform, 0.45f); }

        /// The guard wakes (a menacing swell) / freezes to stone (brittle crackle).
        public void PlayEnemyWake() { _sfx.PlayOneShot(_enemyWake, 0.5f); }
        public void PlayEnemyFreeze() { _sfx.PlayOneShot(_enemyFreeze, 0.4f); }

        /// A new frame on the roll: a delicate ceremonial sparkle.
        public void PlayFrameCardChime() { _sfx.PlayOneShot(_frameCard, 0.2f); }
        /// A teaching prompt surfaces (caller guards re-entry so it sounds once).
        public void PlayHintPop() { _sfx.PlayOneShot(_hintPop, 0.15f); }

        /// Per-room ambience tint: a faint unease drone, deliberately off the
        /// hum's pitch stack. Room 9 (the drop) is left to the scripted blackout.
        public void SetRoomMood(int room)
        {
            switch (room)
            {
                case 1:  _moodTarget = 0.03f;  _moodPitch = 1.00f; break;
                case 2:  _moodTarget = 0.05f;  _moodPitch = 0.95f; break;
                case 3:  _moodTarget = 0.035f; _moodPitch = 1.05f; break;
                case 4:  _moodTarget = 0.045f; _moodPitch = 1.10f; break;
                case 5:  _moodTarget = 0.03f;  _moodPitch = 1.00f; break;
                case 6:  _moodTarget = 0.035f; _moodPitch = 1.12f; break;
                case 7:  _moodTarget = 0.04f;  _moodPitch = 0.90f; break;
                case 8:  _moodTarget = 0.05f;  _moodPitch = 0.85f; break;
                case 9:  _moodTarget = 0f;     _moodPitch = 1.00f; break; // blackout owns it
                case 10: _moodTarget = 0.06f;  _moodPitch = 0.88f; break;
                default: _moodTarget = 0f;     _moodPitch = 1.00f; break; // prologue / unknown
            }
        }

        /// A soft sustained tone while holding to preview an exposure (pitched by
        /// the target state). Player-initiated, so it is NOT ducked.
        public void StartPreviewTone(Exposure cand)
        {
            _preview.pitch = cand == Exposure.Underexposed ? 0.7f
                           : cand == Exposure.Overexposed ? 1.4f : 1.0f;
            _previewTarget = 0.04f;
        }
        public void StopPreviewTone() { _previewTarget = 0f; }

        /// A LocalLux meter filling toward its threshold — a rising tone that
        /// resolves into the door. Pass 0 (on door-open) to silence it.
        public void SetSensorChargeTone(float fill01)
        {
            fill01 = Mathf.Clamp01(fill01);
            _chargeTarget = fill01 < 0.04f ? 0f : Mathf.Lerp(0.012f, 0.05f, fill01);
            _charge.pitch = Mathf.Lerp(0.8f, 1.5f, fill01);
        }

        void ScheduleNextDrip() { _nextDripTime = Time.time + 18f + (float)_rng.NextDouble() * 22f; }

        /// Background music: load the first audio track found in
        /// StreamingAssets/music (.ogg / .wav / .mp3) and loop it. A missing
        /// folder or file just means no music — the same graceful degrade the
        /// external art uses. The track is the melody over the evolving bed.
        System.Collections.IEnumerator LoadMusic()
        {
            string dir = System.IO.Path.Combine(Application.streamingAssetsPath, "music");
            if (!System.IO.Directory.Exists(dir)) yield break;

            var files = System.IO.Directory.GetFiles(dir);
            System.Array.Sort(files, System.StringComparer.OrdinalIgnoreCase);
            string file = null;
            foreach (var f in files)
            {
                string ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".ogg" || ext == ".wav" || ext == ".mp3") { file = f; break; }
            }
            if (file == null) yield break;

            AudioType type = file.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase) ? AudioType.OGGVORBIS
                           : file.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase) ? AudioType.MPEG
                           : AudioType.WAV;
            // new System.Uri(...).AbsoluteUri encodes the space in "The Darkroom"
            string uri = new System.Uri(file).AbsoluteUri;
            using (var req = UnityWebRequestMultimedia.GetAudioClip(uri, type))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[AudioDirector] music load failed: " + req.error);
                    yield break;
                }
                var clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip == null) yield break;
                clip.name = System.IO.Path.GetFileNameWithoutExtension(file);
                _music.clip = clip;
                _music.loop = true;
                _music.Play();
                _musicLoaded = true;
            }
        }

        // ---------- synthesis ----------

        float Noise() { return (float)(_rng.NextDouble() * 2.0 - 1.0); }

        static AudioClip ToClip(string name, float[] d)
        {
            var c = AudioClip.Create(name, d.Length, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        /// Two noise bursts (mirror up, curtain down) over a short tone body.
        AudioClip BuildShutterClick(float dur, bool heavy)
        {
            int n = (int)(SR * dur);
            var d = new float[n];
            float second = heavy ? 0.16f : 0.05f;
            float body = heavy ? 120f : 180f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float v = 0f;
                if (t < 0.03f) v += Noise() * Mathf.Exp(-t * 320f);
                if (t >= second) v += 0.7f * Noise() * Mathf.Exp(-(t - second) * 260f);
                v += 0.3f * Mathf.Sin(2f * Mathf.PI * body * t) * Mathf.Exp(-t * 40f);
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.8f;
            }
            return ToClip(heavy ? "win_shutter" : "shutter_click", d);
        }

        /// Dull, low refusal thud (the jam).
        AudioClip BuildJamClick()
        {
            int n = (int)(SR * 0.1f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float v = 0.8f * Mathf.Sin(2f * Mathf.PI * 105f * t) * Mathf.Exp(-t * 55f)
                        + 0.15f * Noise() * Mathf.Exp(-t * 400f);
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.7f;
            }
            return ToClip("jam_click", d);
        }

        /// Rising two-tone chime for ability pickups.
        AudioClip BuildPickupChime()
        {
            int n = (int)(SR * 0.4f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float v = 0.5f * Mathf.Sin(2f * Mathf.PI * 660f * t) * Mathf.Exp(-t * 14f);
                if (t >= 0.10f)
                {
                    float u = t - 0.10f;
                    v += 0.5f * Mathf.Sin(2f * Mathf.PI * 990f * u) * Mathf.Exp(-u * 11f);
                }
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.6f;
            }
            return ToClip("pickup_chime", d);
        }

        /// A short bright "set" blip when a stroke fixes: a quick upward sine
        /// chirp (1100→1500 Hz) with a fast decay — light snapping into place.
        AudioClip BuildFixBlip()
        {
            int n = (int)(SR * 0.12f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(1100f, 1500f, Mathf.Clamp01(t / 0.05f));
                float v = 0.6f * Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 26f);
                v += 0.18f * Mathf.Sin(2f * Mathf.PI * 2200f * t) * Mathf.Exp(-t * 60f); // glassy top
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.55f;
            }
            return ToClip("fix_blip", d);
        }

        /// 30 ms soft noise tick.
        AudioClip BuildFootstep()
        {
            int n = (int)(SR * 0.035f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                d[i] = Noise() * Mathf.Exp(-t * 280f) * 0.5f;
            }
            return ToClip("footstep", d);
        }

        /// Soft upward sweep.
        AudioClip BuildJump()
        {
            int n = (int)(SR * 0.09f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(200f, 330f, t / 0.09f);
                d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 30f) * 0.5f;
            }
            return ToClip("jump", d);
        }

        /// Low thud + tick.
        AudioClip BuildLand()
        {
            int n = (int)(SR * 0.13f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float v = 0.8f * Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t * 45f)
                        + 0.2f * Noise() * Mathf.Exp(-t * 350f);
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.7f;
            }
            return ToClip("land", d);
        }

        /// The image burns: noise rip + falling tone.
        AudioClip BuildDeath()
        {
            int n = (int)(SR * 0.35f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(300f, 70f, t / 0.35f);
                float v = 0.55f * Noise() * Mathf.Exp(-t * 14f)
                        + 0.5f * Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 9f);
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.7f;
            }
            return ToClip("death", d);
        }

        /// The final print surfaces: a long, patient version of the develop
        /// swell (two detuned sines so it shimmers slightly).
        AudioClip BuildDevelopLong()
        {
            int n = (int)(SR * 1.9f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float k = t / 1.9f;
                float f = Mathf.Lerp(160f, 440f, k * k); // slow start, confident finish
                float env = Mathf.Pow(Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI), 0.8f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * f * t)
                      + 0.4f * Mathf.Sin(2f * Mathf.PI * (f * 1.005f) * t)) * env * 0.26f;
            }
            return ToClip("develop_long", d);
        }

        /// The image re-develops: gentle rising swell.
        AudioClip BuildDevelop()
        {
            int n = (int)(SR * 0.4f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(220f, 440f, t / 0.4f);
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.4f) * Mathf.PI); // swell in & out
                d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.35f;
            }
            return ToClip("develop", d);
        }

        /// A soft "saved" confirm. The two seed notes (600 + 900 Hz — the finale
        /// chord grows from these) bloom with a gentle 12 ms attack, a faint
        /// detuned shimmer partner, and a breathy puff of air, so the photo
        /// settles into the tray instead of plinking. Softer than the pickup
        /// chime; the airy character reads as a camera confirming the frame.
        AudioClip BuildCheckpoint()
        {
            int n = (int)(SR * 0.5f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;

                // note 1 — 600 Hz: soft attack kills the onset click, gentle ~0.4 s tail
                float a1 = Mathf.Clamp01(t / 0.012f);
                float v = 0.30f * (Mathf.Sin(2f * Mathf.PI * 600f * t)
                                 + 0.3f * Mathf.Sin(2f * Mathf.PI * 600f * 1.006f * t))
                                * a1 * Mathf.Exp(-t * 5f);

                // note 2 — 900 Hz: enters ~60 ms later so the two breathe together
                if (t >= 0.06f)
                {
                    float u = t - 0.06f;
                    float a2 = Mathf.Clamp01(u / 0.012f);
                    v += 0.26f * (Mathf.Sin(2f * Mathf.PI * 900f * u)
                                + 0.3f * Mathf.Sin(2f * Mathf.PI * 900f * 1.006f * u))
                               * a2 * Mathf.Exp(-u * 5f);
                }

                // a breathy "confirm" puff of lowpassed air over the first ~80 ms
                lp += (Noise() - lp) * 0.18f;
                v += lp * 0.12f * Mathf.Exp(-t * 22f);

                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.5f;
            }
            return ToClip("checkpoint", d);
        }

        /// Heavy door slide: lowpassed noise rumble fading out.
        AudioClip BuildDoor()
        {
            int n = (int)(SR * 0.45f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                lp += (Noise() - lp) * 0.03f;
                float v = lp * 2.2f + 0.3f * Mathf.Sin(2f * Mathf.PI * 55f * t);
                float env = Mathf.Clamp01(t / 0.04f) * Mathf.Exp(-t * 6f);
                d[i] = Mathf.Clamp(v * env, -1f, 1f) * 0.8f;
            }
            return ToClip("door", d);
        }

        /// Two quick high shutter ticks 60 ms apart: the film-advance ratchet.
        AudioClip BuildFilmAdvance()
        {
            int n = (int)(SR * 0.16f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float v = 0f;
                if (t < 0.025f) v += Noise() * Mathf.Exp(-t * 500f);
                if (t >= 0.06f && t < 0.09f) { float u = t - 0.06f; v += 0.8f * Noise() * Mathf.Exp(-u * 500f); }
                v += 0.25f * Mathf.Sin(2f * Mathf.PI * 290f * t) * Mathf.Exp(-t * 60f);
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.7f;
            }
            return ToClip("film_advance", d);
        }

        /// Sparse crackle loop: the sound of light being written.
        AudioClip BuildDrawLoop()
        {
            int n = SR;
            var d = new float[n];
            float env = 0f;
            for (int i = 0; i < n; i++)
            {
                if (_rng.NextDouble() < 0.0009) env = 0.5f + (float)_rng.NextDouble() * 0.5f;
                env *= 0.9975f;
                d[i] = Noise() * env * 0.6f;
            }
            return ToClip("draw_loop", d);
        }

        /// Short viewfinder beep.
        AudioClip BuildBeep()
        {
            int n = (int)(SR * 0.07f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.07f) * Mathf.PI);
                d[i] = Mathf.Sin(2f * Mathf.PI * 900f * t) * env * 0.5f;
            }
            return ToClip("beep", d);
        }

        /// 600 + 900 + 1200 Hz arpeggiated into a held chord: the checkpoint
        /// notes the player has collected all run, plus one new note on top.
        AudioClip BuildFinaleChord()
        {
            const float dur = 2.2f;
            int n = (int)(SR * dur);
            var d = new float[n];
            float[] freqs = { 600f, 900f, 1200f };
            float[] starts = { 0f, 0.28f, 0.56f };
            float[] amps = { 0.36f, 0.30f, 0.24f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float v = 0f;
                for (int k = 0; k < 3; k++)
                {
                    if (t < starts[k]) continue;
                    float u = t - starts[k];
                    float attack = Mathf.Clamp01(u / 0.18f);
                    float release = Mathf.Clamp01((dur - t) / 0.7f);
                    v += amps[k] * Mathf.Sin(2f * Mathf.PI * freqs[k] * u) * attack * release;
                }
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.6f;
            }
            return ToClip("finale_chord", d);
        }

        /// 2 s loop with integer cycle counts (82 / 82.5 / 164 Hz -> 164 /
        /// 165 / 328 cycles): seamless seam, 0.5 Hz beat = a slow throb.
        AudioClip BuildPedalLoop()
        {
            int n = SR * 2;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                d[i] = (0.50f * Mathf.Sin(2f * Mathf.PI * 82f * t)
                      + 0.35f * Mathf.Sin(2f * Mathf.PI * 82.5f * t)
                      + 0.15f * Mathf.Sin(2f * Mathf.PI * 164f * t)) * 0.5f;
            }
            return ToClip("pedal", d);
        }

        /// Breathy band of filtered noise: rushing air for the long drop.
        AudioClip BuildWindLoop()
        {
            int n = SR;
            var d = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                lp += (Noise() - lp) * 0.12f;
                lp2 += (lp - lp2) * 0.12f;
                d[i] = Mathf.Clamp((lp - lp2) * 3.2f, -1f, 1f);
            }
            return ToClip("wind", d);
        }

        /// Barely audible lowpassed noise floor.
        AudioClip BuildRoomTone()
        {
            int n = SR;
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                lp += (Noise() - lp) * 0.02f;
                d[i] = lp * 1.6f;
            }
            return ToClip("room_tone", d);
        }

        /// 1 s loop, integer cycle counts (55/110/165 Hz) => seamless.
        AudioClip BuildHumLoop()
        {
            int n = SR;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                d[i] = (0.55f * Mathf.Sin(2f * Mathf.PI * 55f * t)
                      + 0.28f * Mathf.Sin(2f * Mathf.PI * 110f * t)
                      + 0.12f * Mathf.Sin(2f * Mathf.PI * 165f * t)) * 0.5f;
            }
            return ToClip("under_hum", d);
        }

        /// First-difference of white noise: bright hiss.
        AudioClip BuildHissLoop()
        {
            int n = SR;
            var d = new float[n];
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = Noise();
                d[i] = (w - prev) * 0.22f;
                prev = w;
            }
            return ToClip("over_hiss", d);
        }

        /// A single water drop: a short surface tick into a resonant "bloop"
        /// whose pitch rises as it rings out — a darkroom tap in the dark.
        AudioClip BuildDrip()
        {
            int n = (int)(SR * 0.22f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(420f, 920f, Mathf.Clamp01(t / 0.18f)); // upward bloop
                float v = 0.6f * Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 24f);
                if (t < 0.006f) v += 0.4f * Noise() * Mathf.Exp(-t * 600f); // surface tick
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.6f;
            }
            return ToClip("drip", d);
        }

        /// The ventilation fan: lowpassed noise (moving air) swelling on a slow
        /// rotational LFO over a faint motor tone. 1 s loop with integer LFO/tone
        /// cycle counts (4 Hz wobble, 60 Hz motor) so the seam stays seamless.
        AudioClip BuildFanLoop()
        {
            int n = SR;
            var d = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                lp += (Noise() - lp) * 0.05f;
                lp2 += (lp - lp2) * 0.05f;
                float air = lp2 * 4.0f;
                float wob = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 4f * t);  // rotational swell
                float motor = 0.12f * Mathf.Sin(2f * Mathf.PI * 60f * t);     // faint motor hum
                d[i] = Mathf.Clamp(air * wob + motor, -1f, 1f) * 0.5f;
            }
            return ToClip("fan_loop", d);
        }

        // ---------- sound-design pass: synthesis ----------

        /// Burning paper: two-pole lowpassed noise (heat, not static) with an
        /// integer-cycle 7 Hz flicker so the 1 s loop has no seam. Volume is
        /// driven by char level, so the buffer itself stays flat.
        AudioClip BuildBurnLoop()
        {
            int n = SR;
            var d = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                lp += (Noise() - lp) * 0.10f;
                lp2 += (lp - lp2) * 0.10f;
                float flick = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 7f * t); // 7 cycles → seamless
                d[i] = Mathf.Clamp((lp - lp2 * 0.6f) * 3.0f * flick, -1f, 1f) * 0.5f;
            }
            return ToClip("burn_loop", d);
        }

        /// The wall burns through: a rising low whoosh + noise rip + a spark.
        AudioClip BuildBurnThrough()
        {
            int n = (int)(SR * 0.45f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(80f, 240f, Mathf.Clamp01(t / 0.12f));
                float v = 0.5f * Noise() * Mathf.Exp(-t * 10f)
                        + 0.45f * Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 7f);
                if (t < 0.008f) v += 0.35f * Noise() * Mathf.Exp(-t * 500f); // spark transient
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.7f;
            }
            return ToClip("burn_through", d);
        }

        /// Lift motor: a soft tri-sine whir (90 / 90.5 / 180 Hz over a 2 s buffer
        /// -> 180 / 181 / 360 integer cycles, a 0.5 Hz throb) over a faint air
        /// bed. Pitched up for the rising light slab, down for the sinking shadow.
        AudioClip BuildLiftLoop()
        {
            int n = SR * 2;
            var d = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                lp += (Noise() - lp) * 0.06f;
                lp2 += (lp - lp2) * 0.06f;
                float whir = 0.5f * Mathf.Sin(2f * Mathf.PI * 90f * t)
                           + 0.3f * Mathf.Sin(2f * Mathf.PI * 90.5f * t)
                           + 0.18f * Mathf.Sin(2f * Mathf.PI * 180f * t);
                d[i] = Mathf.Clamp(whir * 0.4f + lp2 * 1.5f, -1f, 1f) * 0.5f;
            }
            return ToClip("lift_loop", d);
        }

        /// Per-room mood drone: 49 / 49.5 / 98 Hz over a 2 s buffer (98 / 99 / 196
        /// integer cycles, 0.5 Hz beat), deliberately off the hum's 55 Hz stack so
        /// it adds unease without a boomy pileup. Faint; per-room pitch + volume.
        AudioClip BuildMoodDrone()
        {
            int n = SR * 2;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                d[i] = (0.5f * Mathf.Sin(2f * Mathf.PI * 49f * t)
                      + 0.3f * Mathf.Sin(2f * Mathf.PI * 49.5f * t)
                      + 0.12f * Mathf.Sin(2f * Mathf.PI * 98f * t)) * 0.5f;
            }
            return ToClip("mood_drone", d);
        }

        /// A soft sustained sine (+ a quiet fifth), pitched per exposure state for
        /// the hold-to-preview tone. Integer cycles (330 / 660 Hz over 1 s).
        AudioClip BuildPreviewTone()
        {
            int n = SR;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                d[i] = (Mathf.Sin(2f * Mathf.PI * 330f * t)
                      + 0.3f * Mathf.Sin(2f * Mathf.PI * 660f * t)) * 0.4f;
            }
            return ToClip("preview_tone", d);
        }

        /// The sensor charge tone: a sine + octave (440 / 880 Hz, integer cycles),
        /// pitch-ramped up as a light meter fills toward its threshold.
        AudioClip BuildChargeTone()
        {
            int n = SR;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                d[i] = (Mathf.Sin(2f * Mathf.PI * 440f * t)
                      + 0.25f * Mathf.Sin(2f * Mathf.PI * 880f * t)) * 0.4f;
            }
            return ToClip("charge_tone", d);
        }

        /// Shadow recoils from light: an airy upward inhale (filtered noise + a
        /// rising sine).
        AudioClip BuildUmbraOpen()
        {
            int n = (int)(SR * 0.35f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(140f, 360f, Mathf.Clamp01(t / 0.18f));
                lp += (Noise() - lp) * 0.10f;
                float v = 0.4f * Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 6f)
                        + lp * 0.25f * Mathf.Exp(-t * 9f);
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.55f;
            }
            return ToClip("umbra_open", d);
        }

        /// Shadow floods back: the darker inverse — a downward sweep + a dull tick.
        AudioClip BuildUmbraSeal()
        {
            int n = (int)(SR * 0.4f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(220f, 70f, Mathf.Clamp01(t / 0.2f));
                float v = 0.5f * Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 8f)
                        + 0.15f * Noise() * Mathf.Exp(-t * 200f);
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.6f;
            }
            return ToClip("umbra_seal", d);
        }

        /// A latent platform prints in: a warm sweep with a soft swell + a body
        /// thunk — fuller and lower than the bright stroke-fix blip.
        AudioClip BuildFixPlatform()
        {
            int n = (int)(SR * 0.32f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(300f, 520f, Mathf.Clamp01(t / 0.1f));
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.32f) * Mathf.PI);
                float v = 0.6f * Mathf.Sin(2f * Mathf.PI * f * t) * env
                        + 0.2f * Mathf.Sin(2f * Mathf.PI * 150f * t) * Mathf.Exp(-t * 12f);
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.5f;
            }
            return ToClip("fix_platform", d);
        }

        /// The guard wakes: a low growl that swells (soft ~20 ms attack — dread,
        /// not a jump-scare) with a detuned octave and a dissonant top.
        AudioClip BuildEnemyWake()
        {
            int n = (int)(SR * 0.5f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float attack = Mathf.Clamp01(t / 0.02f);
                float f = Mathf.Lerp(60f, 150f, Mathf.Clamp01(t / 0.25f));
                float v = 0.5f * Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 4f)
                        + 0.18f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * t) * Mathf.Exp(-t * 5f)
                        + 0.12f * Mathf.Sin(2f * Mathf.PI * 233f * t) * Mathf.Exp(-t * 6f);
                d[i] = Mathf.Clamp(v * attack, -1f, 1f) * 0.55f;
            }
            return ToClip("enemy_wake", d);
        }

        /// The guard freezes to stone: three brittle ticks (matching the visual
        /// crackle's 0 / 0.10 / 0.20 s flips) under a glassy "ice" overtone.
        AudioClip BuildEnemyFreeze()
        {
            int n = (int)(SR * 0.3f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float v = 0.15f * Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-t * 30f);
                for (int k = 0; k < 3; k++)
                {
                    float ts = k * 0.10f;
                    if (t >= ts) { float u = t - ts; v += 0.5f * Noise() * Mathf.Exp(-u * 220f); }
                }
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.5f;
            }
            return ToClip("enemy_freeze", d);
        }

        /// A new frame on the roll: three quick rising glints, soft-attacked.
        AudioClip BuildFrameCardChime()
        {
            int n = (int)(SR * 0.45f);
            var d = new float[n];
            float[] f = { 880f, 1320f, 1760f };
            float[] s = { 0f, 0.06f, 0.12f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float v = 0f;
                for (int k = 0; k < 3; k++)
                {
                    if (t < s[k]) continue;
                    float u = t - s[k];
                    float a = Mathf.Clamp01(u / 0.008f);
                    v += (0.3f - k * 0.06f) * Mathf.Sin(2f * Mathf.PI * f[k] * u) * a * Mathf.Exp(-u * 7f);
                }
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.5f;
            }
            return ToClip("frame_card", d);
        }

        /// A teaching prompt surfaces: a tiny soft up-blip.
        AudioClip BuildHintPop()
        {
            int n = (int)(SR * 0.08f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(500f, 760f, Mathf.Clamp01(t / 0.04f));
                d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 28f) * 0.5f;
            }
            return ToClip("hint_pop", d);
        }

        /// Death by the guard: the burn rip with a sharper dissonant stab.
        AudioClip BuildDeathEnemy()
        {
            int n = (int)(SR * 0.4f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(300f, 70f, t / 0.4f);
                float v = 0.55f * Noise() * Mathf.Exp(-t * 14f)
                        + 0.5f * Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 9f)
                        + 0.3f * Mathf.Sin(2f * Mathf.PI * 196f * t) * Mathf.Exp(-t * 7f);
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.7f;
            }
            return ToClip("death_enemy", d);
        }

        /// A deliberate restart (R): a soft downward sigh, no violent rip.
        AudioClip BuildDeathSoft()
        {
            int n = (int)(SR * 0.3f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(260f, 120f, Mathf.Clamp01(t / 0.3f));
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI);
                d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.3f;
            }
            return ToClip("death_soft", d);
        }
    }
}
