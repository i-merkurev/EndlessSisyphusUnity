using System.Collections.Generic;
using UnityEngine;

namespace EndlessSisyphus
{
    /// <summary>
    /// Порт модуля Sound из game.js на Unity. Музыка разделена на дрон E+B и мелодию,
    /// чтобы во время поражения они могли затихать независимо. SFX и кинематографические
    /// акценты синтезируются процедурно и не требуют внешних аудиофайлов.
    /// Мягкий приглушённый push, отдельные ice/friction, ветер-луп — как в web-версии.
    /// </summary>
    public class AudioEngine
    {
        const int SR = 44100;
        const float Beat = 0.62f;

        readonly AudioSource drone, melody, sfx, wind, cinematic;
        bool musicOn, muted;
        float droneTarget = 0.4f, melodyTarget = 0.4f;

        AudioClip droneClip, melodyClip, windClip;
        AudioClip cPush, cSteepPush, cWindPush, cIce, cFriction, cError, cRumble, cGameover;
        AudioClip cDefeatRoll, cDefeatLook, cDefeatDescent;

        static readonly System.Random rng = new System.Random(1234);

        static readonly Dictionary<string, float> Note = new Dictionary<string, float>
        {
            {"E2",82.41f},{"B2",123.47f},{"E3",164.81f},{"F3",174.61f},{"G3",196.0f},
            {"A3",220.0f},{"B3",246.94f},{"C4",261.63f},{"D4",293.66f},{"E4",329.63f},
        };
        static readonly (string n, int d)[] Melody =
        {
            ("E3",3),("F3",1),("E3",2),("",2),("G3",2),("F3",1),("E3",3),("",1),("A3",2),
            ("G3",2),("F3",2),("E3",2),("",2),("D4",1),("C4",1),("B3",2),("A3",2),("",2),
        };

        public AudioEngine(GameObject host)
        {
            drone = host.AddComponent<AudioSource>(); drone.loop = true; drone.playOnAwake = false; drone.volume = 0.4f;
            melody = host.AddComponent<AudioSource>(); melody.loop = true; melody.playOnAwake = false; melody.volume = 0.4f;
            sfx = host.AddComponent<AudioSource>(); sfx.playOnAwake = false;
            wind = host.AddComponent<AudioSource>(); wind.loop = true; wind.playOnAwake = false; wind.volume = 0f;
            cinematic = host.AddComponent<AudioSource>(); cinematic.playOnAwake = false; cinematic.volume = 1f;
            BuildSfx();
        }

        // ---- управление ----
        public bool ToggleMute() { muted = !muted; AudioListener.volume = muted ? 0f : 1f; return muted; }

        public void StartMusic()
        {
            if (musicOn) return;
            musicOn = true;
            if (droneClip == null) droneClip = Make("music_drone", BuildDroneBuffer());
            if (melodyClip == null) melodyClip = Make("music_melody", BuildMelodyBuffer());
            drone.clip = droneClip;
            melody.clip = melodyClip;
            double startTime = AudioSettings.dspTime + 0.04;
            drone.PlayScheduled(startTime);
            melody.PlayScheduled(startTime);
        }

        public void Tick(float dt)
        {
            drone.volume = Mathf.MoveTowards(drone.volume, droneTarget, dt * 0.18f);
            melody.volume = Mathf.MoveTowards(melody.volume, melodyTarget, dt * 0.28f);
        }

        public void RestoreGameplayMusic()
        {
            droneTarget = 0.4f;
            melodyTarget = 0.4f;
            cinematic.Stop();
        }

        public void BeginDefeat()
        {
            StartMusic();
            droneTarget = 0.22f;
            melodyTarget = 0.08f;
            cinematic.Stop();
            if (!muted) cinematic.PlayOneShot(cFriction, 0.55f);
        }

        public void EnterDefeatPhase(DefeatPhase phase)
        {
            switch (phase)
            {
                case DefeatPhase.StoneRoll:
                    if (!muted) cinematic.PlayOneShot(cDefeatRoll, 0.62f);
                    break;
                case DefeatPhase.Look:
                    droneTarget = 0.12f;
                    melodyTarget = 0f;
                    if (!muted) cinematic.PlayOneShot(cDefeatLook, 0.48f);
                    break;
                case DefeatPhase.Descend:
                    droneTarget = 0.16f;
                    melodyTarget = 0f;
                    if (!muted) cinematic.PlayOneShot(cDefeatDescent, 0.52f);
                    break;
            }
        }

        public void HoldDefeatMusic()
        {
            droneTarget = 0.14f;
            melodyTarget = 0f;
        }

        public void WindStart()
        {
            if (muted) return;
            if (windClip == null) windClip = BuildWind();
            wind.clip = windClip; wind.volume = 0.2f; if (!wind.isPlaying) wind.Play();
        }
        public void WindStop() { wind.Stop(); wind.volume = 0f; }

        // ---- SFX ----
        public void Push(float s) { if (!muted) sfx.PlayOneShot(cPush, Mathf.Clamp01(0.22f + 0.20f * s)); }
        public void SteepPush(float s) { if (!muted) sfx.PlayOneShot(cSteepPush, Mathf.Clamp01(0.25f + 0.21f * s)); }
        public void WindResistance(float s) { if (!muted) sfx.PlayOneShot(cWindPush, Mathf.Clamp01(0.36f + 0.28f * s)); }
        public void Ice() { if (!muted) sfx.PlayOneShot(cIce, 0.30f); }
        public void Friction() { if (!muted) sfx.PlayOneShot(cFriction, 0.30f); }
        public void Error() { if (!muted) sfx.PlayOneShot(cError, 0.8f); }
        public void Rumble() { if (!muted) sfx.PlayOneShot(cRumble, 0.9f); }
        public void GameOver() { if (!muted) sfx.PlayOneShot(cGameover, 0.9f); }

        // ============================================================ Синтез
        static AudioClip Make(string name, float[] buf)
        {
            var clip = AudioClip.Create(name, buf.Length, 1, SR, false);
            clip.SetData(buf, 0);
            return clip;
        }

        static float Saw(float freq, int i) { float p = (i * freq / SR) % 1f; return 2f * p - 1f; }
        static float Square(float freq, int i) { float p = (i * freq / SR) % 1f; return p < 0.5f ? 1f : -1f; }
        static float Tri(float freq, int i) { float p = (i * freq / SR) % 1f; return 4f * Mathf.Abs(p - 0.5f) - 1f; }
        static float Noise() { return (float)(rng.NextDouble() * 2.0 - 1.0); }

        // однополюсный ФНЧ
        static void LowPass(float[] buf, float cutoff)
        {
            float dt = 1f / SR, rc = 1f / (2f * Mathf.PI * cutoff), a = dt / (rc + dt), y = 0f;
            for (int i = 0; i < buf.Length; i++) { y += a * (buf[i] - y); buf[i] = y; }
        }

        void BuildSfx()
        {
            cPush = Make("push", SynthPush());
            cSteepPush = Make("steep_push", SynthSteepPush());
            cWindPush = Make("wind_resistance", SynthWindPush());
            cIce = Make("ice", SynthIce());
            cFriction = Make("friction", SynthFriction());
            cError = Make("error", SynthError());
            cRumble = Make("rumble", SynthRumble());
            cGameover = Make("gameover", SynthGameover());
            cDefeatRoll = Make("defeat_roll", SynthDefeatRoll());
            cDefeatLook = Make("defeat_look", SynthDefeatLook());
            cDefeatDescent = Make("defeat_descent", SynthDefeatDescent());
        }

        // Мягкий каменный импульс строится на тех же E–B, что и музыкальный дрон.
        float[] SynthPush()
        {
            int n = (int)(0.26f * SR);
            var texture = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++) texture[i] = Noise();
            LowPass(texture, 320f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float bodyEnv = Mathf.Min(t / 0.026f, 1f) * Mathf.Exp(-t * 8.5f);
                float contactEnv = Mathf.Min(t / 0.008f, 1f) * Mathf.Exp(-t * 22f);
                float body = Mathf.Sin(2f * Mathf.PI * 82.41f * t) +
                    0.28f * Mathf.Sin(2f * Mathf.PI * 123.47f * t) +
                    0.10f * Mathf.Sin(2f * Mathf.PI * 164.81f * t);
                b[i] = body * bodyEnv * 0.30f + texture[i] * contactEnv * 0.12f;
            }
            LowPass(b, 430f);
            return b;
        }

        // Тот же жест, что у обычного толчка, но ниже, тяжелее и без высокого скрежета.
        float[] SynthSteepPush()
        {
            int n = (int)(0.36f * SR);
            var contact = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++) contact[i] = Noise();
            LowPass(contact, 180f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float bodyEnv = Mathf.Min(t / 0.036f, 1f) * Mathf.Exp(-t * 6.2f);
                float contactEnv = Mathf.Min(t / 0.012f, 1f) * Mathf.Exp(-t * 13f);
                float body = Mathf.Sin(2f * Mathf.PI * 41.20f * t) +
                    0.48f * Mathf.Sin(2f * Mathf.PI * 61.74f * t) +
                    0.16f * Mathf.Sin(2f * Mathf.PI * 82.41f * t);
                b[i] = body * bodyEnv * 0.34f + contact[i] * contactEnv * 0.13f;
            }
            LowPass(b, 245f);
            return b;
        }

        // Сопротивление ветра — мягкий воздушный выброс с тихим E2 внутри.
        float[] SynthWindPush()
        {
            int n = (int)(0.38f * SR);
            var air = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++) air[i] = Noise();
            LowPass(air, 760f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Min(t / 0.045f, 1f) * Mathf.Exp(-t * 4.8f);
                float breath = 0.68f + 0.32f * Mathf.Sin(2f * Mathf.PI * 3.2f * t);
                float tone = Mathf.Sin(2f * Mathf.PI * 82.41f * t) * 0.11f;
                b[i] = (air[i] * breath * 0.46f + tone) * env;
            }
            return b;
        }

        float[] SynthIce()
        {
            int n = (int)(0.30f * SR);
            var frost = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++) frost[i] = Noise();
            LowPass(frost, 920f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Min(t / 0.018f, 1f) * Mathf.Exp(-t * 9.5f);
                float glass = Mathf.Sin(2f * Mathf.PI * 329.63f * t) +
                    0.34f * Mathf.Sin(2f * Mathf.PI * 493.88f * t) +
                    0.12f * Mathf.Sin(2f * Mathf.PI * 659.26f * t);
                float granular = frost[i] * (0.62f + 0.38f * Mathf.Sin(2f * Mathf.PI * 7f * t));
                b[i] = (glass * 0.16f + granular * 0.12f) * env;
            }
            LowPass(b, 1100f);
            return b;
        }

        static float IceCrack(float t, float start, float frequency, float decay)
        {
            float local = t - start;
            if (local < 0f) return 0f;
            float env = Mathf.Exp(-local * decay);
            float brittle = Mathf.Sin(2f * Mathf.PI * frequency * local) +
                0.42f * Mathf.Sin(2f * Mathf.PI * frequency * 1.71f * local);
            return brittle * env;
        }

        float[] SynthFriction()
        {
            int n = (int)(0.24f * SR); var b = new float[n];
            for (int i = 0; i < n; i++) b[i] = Noise();
            LowPass(b, 240f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Min(t / 0.025f, 1f) * Mathf.Exp(-t * 9f);
                float stoneTone = Mathf.Sin(2f * Mathf.PI * 82.41f * t) * 0.12f;
                b[i] = (b[i] * 0.52f + stoneTone) * env;
            }
            return b;
        }

        float[] SynthError()
        {
            int n = (int)(0.16f * SR); var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR, freq = Mathf.Lerp(120f, 70f, t / 0.16f);
                float env = Mathf.Min(t / 0.005f, 1f) * Mathf.Exp(-t * 12f);
                b[i] = Square(freq, i) * env * 0.5f;
            }
            return b;
        }

        float[] SynthRumble()
        {
            int n = (int)(1.2f * SR); var b = new float[n];
            for (int i = 0; i < n; i++) b[i] = Noise();
            LowPass(b, 400f);
            for (int i = 0; i < n; i++) { float t = i / (float)SR, env = Mathf.Min(t / 0.02f, 1f) * Mathf.Exp(-t * 3f); b[i] *= env; }
            return b;
        }

        float[] SynthGameover()
        {
            int n = (int)(1.1f * SR); var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR, freq = 300f * Mathf.Pow(0.2f, t / 1f);
                float env = Mathf.Min(t / 0.01f, 1f) * Mathf.Exp(-t * 2.5f);
                b[i] = Saw(freq, i) * env * 0.5f;
            }
            return b;
        }

        float[] SynthDefeatRoll()
        {
            const float duration = 1.8f;
            int n = (int)(duration * SR);
            var noise = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++) noise[i] = Noise();
            LowPass(noise, 260f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float p = t / duration;
                float fade = Mathf.Min(t / 0.08f, 1f) * Mathf.Clamp01((duration - t) / 0.18f);
                float rollPhase = 2f * Mathf.PI * (1.05f * t + 1.35f * t * t);
                float contact = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(rollPhase)), 9f);
                float body = Mathf.Sin(2f * Mathf.PI * 54f * t) * contact * (0.18f + 0.34f * p);
                b[i] = (noise[i] * (0.24f + 0.42f * p * p) + body) * fade;
            }
            LowPass(b, 330f);
            return b;
        }

        float[] SynthDefeatLook()
        {
            const float duration = 2f;
            int n = (int)(duration * SR);
            var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.55f)) *
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((duration - t) / 0.65f));
                float openFifth = Mathf.Sin(2f * Mathf.PI * 82.41f * t) +
                    0.45f * Mathf.Sin(2f * Mathf.PI * 123.47f * t);
                b[i] = openFifth * env * 0.16f;
            }
            return b;
        }

        float[] SynthDefeatDescent()
        {
            const float duration = 5.5f;
            int n = (int)(duration * SR);
            var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                b[i] =
                    TonePulse(t, 0.35f, 164.81f, 1.45f) * 0.20f +
                    TonePulse(t, 2.25f, 123.47f, 1.55f) * 0.17f +
                    TonePulse(t, 4.20f, 82.41f, 1.25f) * 0.15f;
            }
            return b;
        }

        static float TonePulse(float t, float start, float frequency, float duration)
        {
            float local = t - start;
            if (local < 0f || local > duration) return 0f;
            float env = Mathf.Min(local / 0.18f, 1f) * Mathf.Exp(-local * 1.55f);
            return (Mathf.Sin(2f * Mathf.PI * frequency * local) +
                0.18f * Mathf.Sin(4f * Mathf.PI * frequency * local)) * env;
        }

        AudioClip BuildWind()
        {
            int n = (int)(1.2f * SR); var b = new float[n];
            for (int i = 0; i < n; i++) b[i] = Noise();
            LowPass(b, 700f);
            // сгладить края для бесшовного лупа
            int fade = 2000;
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; b[i] *= k; b[n - 1 - i] *= k; }
            return Make("wind", b);
        }

        static int MusicSampleCount()
        {
            int totalBeats = 0; foreach (var m in Melody) totalBeats += m.d;
            return (int)(totalBeats * Beat * SR);
        }

        float[] BuildDroneBuffer()
        {
            int n = MusicSampleCount();
            var b = new float[n];
            for (int i = 0; i < n; i++) b[i] = (Saw(82.41f, i) + Saw(123.47f, i)) * 0.09f;
            LowPass(b, 320f);
            return b;
        }

        float[] BuildMelodyBuffer()
        {
            int n = MusicSampleCount();
            var b = new float[n];
            int cursor = 0;
            foreach (var (name, d) in Melody)
            {
                int len = (int)(d * Beat * SR);
                if (!string.IsNullOrEmpty(name) && Note.TryGetValue(name, out float f))
                {
                    for (int i = 0; i < len && cursor + i < n; i++)
                    {
                        float t = i / (float)SR, dur = d * Beat;
                        float env = Mathf.Min(t / 0.04f, 1f) * Mathf.Exp(-t / (dur * 0.4f));
                        b[cursor + i] += (Tri(f, cursor + i) + 0.18f * Square(f, cursor + i)) * env * 0.14f;
                    }
                }
                cursor += len;
            }
            return b;
        }
    }
}
