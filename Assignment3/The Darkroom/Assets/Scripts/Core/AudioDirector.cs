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

        AudioSource _sfx, _foot, _hum, _hiss, _draw, _room;
        AudioClip _click, _jam, _pickup, _win;
        AudioClip _footstep, _jump, _land, _death, _develop, _checkpoint, _door;
        float _humTarget, _hissTarget, _drawTarget;
        readonly System.Random _rng = new System.Random(20260611);

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
            _checkpoint = BuildCheckpoint();
            _door = BuildDoor();

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
            _sfx.PlayOneShot(_click, 0.5f);
            _humTarget = e == Exposure.Underexposed ? 0.22f : 0f;
            _hissTarget = e == Exposure.Overexposed ? 0.12f : 0f;
        }

        void Update()
        {
            _hum.volume = Mathf.MoveTowards(_hum.volume, _humTarget, Time.deltaTime * 1.2f);
            _hiss.volume = Mathf.MoveTowards(_hiss.volume, _hissTarget, Time.deltaTime * 1.2f);
            _draw.volume = Mathf.MoveTowards(_draw.volume, _drawTarget, Time.deltaTime * 3f);
        }

        public void PlayJam() { _sfx.PlayOneShot(_jam, 0.6f); }
        public void PlayPickup() { _sfx.PlayOneShot(_pickup, 0.45f); }

        public void PlayFootstep()
        {
            _foot.pitch = 0.9f + (float)_rng.NextDouble() * 0.25f;
            _foot.PlayOneShot(_footstep, 0.22f);
        }

        public void PlayJumpSound() { _sfx.PlayOneShot(_jump, 0.3f); }
        public void PlayLand(float strength01) { _sfx.PlayOneShot(_land, 0.15f + 0.35f * Mathf.Clamp01(strength01)); }
        public void PlayDeath() { _sfx.PlayOneShot(_death, 0.6f); }
        public void PlayDevelop() { _sfx.PlayOneShot(_develop, 0.4f); }
        public void PlayCheckpoint() { _sfx.PlayOneShot(_checkpoint, 0.35f); }
        public void PlayDoor() { _sfx.PlayOneShot(_door, 0.55f); }

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
