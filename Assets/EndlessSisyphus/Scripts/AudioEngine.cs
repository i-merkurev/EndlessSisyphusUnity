using System.Collections.Generic;
using UnityEngine;

namespace EndlessSisyphus
{
    /// <summary>
    /// Порт модуля Sound из game.js на Unity. Музыка (дрон E+B во фригийском ладу + мелодия)
    /// синтезируется в один зацикленный AudioClip; SFX — короткие процедурные клипы через PlayOneShot.
    /// Мягкий приглушённый push, отдельные ice/friction, ветер-луп — как в web-версии.
    /// </summary>
    public class AudioEngine
    {
        const int SR = 44100;
        const float Beat = 0.62f;

        readonly AudioSource music, sfx, wind;
        bool musicOn, muted;

        AudioClip musicClip, windClip;
        AudioClip cPush, cIce, cFriction, cError, cRumble, cGameover;

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
            music = host.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false; music.volume = 0.4f;
            sfx = host.AddComponent<AudioSource>(); sfx.playOnAwake = false;
            wind = host.AddComponent<AudioSource>(); wind.loop = true; wind.playOnAwake = false; wind.volume = 0f;
            BuildSfx();
        }

        // ---- управление ----
        public bool ToggleMute() { muted = !muted; AudioListener.volume = muted ? 0f : 1f; return muted; }

        public void StartMusic()
        {
            if (musicOn) return;
            musicOn = true;
            if (musicClip == null) musicClip = BuildMusic();
            music.clip = musicClip; music.Play();
        }

        public void WindStart()
        {
            if (muted) return;
            if (windClip == null) windClip = BuildWind();
            wind.clip = windClip; wind.volume = 0.2f; if (!wind.isPlaying) wind.Play();
        }
        public void WindStop() { wind.Stop(); wind.volume = 0f; }

        // ---- SFX ----
        public void Push(float s) { if (!muted) sfx.PlayOneShot(cPush, Mathf.Clamp01(0.5f + 0.4f * s)); }
        public void Ice() { if (!muted) sfx.PlayOneShot(cIce, 0.7f); }
        public void Friction() { if (!muted) sfx.PlayOneShot(cFriction, 0.8f); }
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
            cIce = Make("ice", SynthIce());
            cFriction = Make("friction", SynthFriction());
            cError = Make("error", SynthError());
            cRumble = Make("rumble", SynthRumble());
            cGameover = Make("gameover", SynthGameover());
        }

        // мягкий приглушённый толчок на тонике E
        float[] SynthPush()
        {
            int n = (int)(0.26f * SR); var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Min(t / 0.015f, 1f) * Mathf.Exp(-t * 16f);
                b[i] = (Mathf.Sin(2 * Mathf.PI * 82.41f * t) + 0.35f * Tri(164.81f, i)) * env * 0.5f;
            }
            LowPass(b, 480f);
            return b;
        }

        float[] SynthIce()
        {
            int n = (int)(0.09f * SR); var b = new float[n]; var lp = new float[n];
            for (int i = 0; i < n; i++) b[i] = Noise();
            System.Array.Copy(b, lp, n); LowPass(lp, 3600f);
            for (int i = 0; i < n; i++) { float t = i / (float)SR, env = Mathf.Min(t / 0.004f, 1f) * Mathf.Exp(-t * 40f); b[i] = (b[i] - lp[i]) * env * 0.5f; } // highpass≈noise-lowpass
            return b;
        }

        float[] SynthFriction()
        {
            int n = (int)(0.22f * SR); var b = new float[n];
            for (int i = 0; i < n; i++) b[i] = Noise();
            LowPass(b, 320f);
            for (int i = 0; i < n; i++) { float t = i / (float)SR, env = Mathf.Min(t / 0.02f, 1f) * Mathf.Exp(-t * 12f); b[i] *= env * 1.2f; }
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

        float[] BuildMusicBuffer()
        {
            int totalBeats = 0; foreach (var m in Melody) totalBeats += m.d;
            int n = (int)(totalBeats * Beat * SR);
            var b = new float[n];

            // дрон E2 + B2
            for (int i = 0; i < n; i++) b[i] = (Saw(82.41f, i) + Saw(123.47f, i)) * 0.09f;
            LowPass(b, 320f);

            // мелодия
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

        AudioClip BuildMusic() => Make("music", BuildMusicBuffer());
    }
}
