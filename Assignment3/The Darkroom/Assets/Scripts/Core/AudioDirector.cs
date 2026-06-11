using UnityEngine;

namespace Darkroom
{
    /// Procedurally synthesized audio — no external assets (spec stretch #1).
    /// Shutter click on every switch, low hum while Underexposed, bright hiss
    /// while Overexposed, dull jam click, pickup chime, win shutter.
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        const int SR = 44100;

        AudioSource _sfx, _foot, _hum, _hiss, _draw, _room, _wind, _pedal;
        AudioClip _click, _jam, _pickup, _win, _advance, _beep, _chord;
        AudioClip _footstep, _jump, _land, _death, _develop, _developLong, _checkpoint, _door;
        bool _pedalCut;
        float _humTarget, _hissTarget, _drawTarget, _windTarget;
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

            // pedal tone: a near-subliminal throb that climbs through Room 10
            // and cuts to dead silence the instant the finale begins
            _pedal = NewSource(true);
            _pedal.clip = BuildPedalLoop();
            _pedal.volume = 0f;
            _pedal.Play();
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
        }

        void OnDestroy()
        {
            if (ExposureManager.Instance != null)
                ExposureManager.Instance.OnExposureChanged -= HandleExposure;
        }

        void HandleExposure(Exposure e)
        {
            bool silent = ExposureManager.Instance != null && ExposureManager.Instance.LastChangeSilent;
            if (!silent) _sfx.PlayOneShot(_click, 0.5f);
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
        public void PlayDevelop() { _sfx.PlayOneShot(_develop, 0.4f); }
        /// The win screen's slow print-surfacing swell.
        public void PlayDevelopLong() { _sfx.PlayOneShot(_developLong, 0.45f); }
        public void PlayCheckpoint() { _sfx.PlayOneShot(_checkpoint, 0.35f); }
        public void PlayDoor() { _sfx.PlayOneShot(_door, 0.55f); }
        /// The roll ratchets forward: the oldest stroke is wound away.
        public void PlayFilmAdvance() { _sfx.PlayOneShot(_advance, 0.4f); }

        /// The "exposing" crackle while a stroke is being drawn.
        public void SetDrawing(bool drawing) { _drawTarget = drawing ? 0.2f : 0f; }

        public void PlayWin()
        {
            _humTarget = 0f;
            _hissTarget = 0f;
            _sfx.PlayOneShot(_win, 0.8f);
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

        /// Two quiet notes, distinct from the pickup chime.
        AudioClip BuildCheckpoint()
        {
            int n = (int)(SR * 0.3f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float v = 0.4f * Mathf.Sin(2f * Mathf.PI * 600f * t) * Mathf.Exp(-t * 18f);
                if (t >= 0.09f)
                {
                    float u = t - 0.09f;
                    v += 0.4f * Mathf.Sin(2f * Mathf.PI * 900f * u) * Mathf.Exp(-u * 14f);
                }
                d[i] = Mathf.Clamp(v, -1f, 1f) * 0.6f;
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
    }
}
