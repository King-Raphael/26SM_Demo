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

        AudioSource _sfx, _hum, _hiss;
        AudioClip _click, _jam, _pickup, _win;
        float _humTarget, _hissTarget;
        readonly System.Random _rng = new System.Random(20260611);

        void Awake()
        {
            Instance = this;
            _sfx = NewSource(false);
            _click = BuildShutterClick(0.14f, false);
            _win = BuildShutterClick(0.4f, true);
            _jam = BuildJamClick();
            _pickup = BuildPickupChime();

            _hum = NewSource(true);
            _hum.clip = BuildHumLoop();
            _hum.volume = 0f;
            _hum.Play();

            _hiss = NewSource(true);
            _hiss.clip = BuildHissLoop();
            _hiss.volume = 0f;
            _hiss.Play();
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
        }

        public void PlayJam() { _sfx.PlayOneShot(_jam, 0.6f); }
        public void PlayPickup() { _sfx.PlayOneShot(_pickup, 0.45f); }

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
