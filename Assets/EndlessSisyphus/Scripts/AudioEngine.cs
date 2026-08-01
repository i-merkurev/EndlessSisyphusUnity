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
        const float GameplaySfxGain = 1.48f;
        const float NatureSfxGain = 1.68f;
        const float CinematicSfxGain = 1.18f;

        readonly AudioSource drone, melody, sfx, wind, rain, cinematic;
        bool musicOn, muted;
        float droneTarget = 0.4f, melodyTarget = 0.4f;

        AudioClip droneClip, melodyClip, windClip, rainClip;
        AudioClip cPush, cSteepPush, cWindPush, cRainPush, cIce, cFriction;
        AudioClip cSlipFall, cExhaustedBreath, cError, cRumble, cGameover;
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

        static float Gain(float value, float multiplier) =>
            Mathf.Clamp01(value * multiplier);

        public AudioEngine(GameObject host)
        {
            drone = host.AddComponent<AudioSource>(); drone.loop = true; drone.playOnAwake = false; drone.volume = 0.4f;
            melody = host.AddComponent<AudioSource>(); melody.loop = true; melody.playOnAwake = false; melody.volume = 0.4f;
            sfx = host.AddComponent<AudioSource>(); sfx.playOnAwake = false;
            wind = host.AddComponent<AudioSource>(); wind.loop = true; wind.playOnAwake = false; wind.volume = 0f;
            rain = host.AddComponent<AudioSource>(); rain.loop = true; rain.playOnAwake = false; rain.volume = 0f;
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

        public void BeginDefeat(string reason)
        {
            StartMusic();
            droneTarget = 0.22f;
            melodyTarget = 0.08f;
            cinematic.Stop();
            if (!muted)
                cinematic.PlayOneShot(reason == "slip" ? cSlipFall : cExhaustedBreath,
                    Gain(reason == "slip" ? 0.72f : 0.78f, CinematicSfxGain));
        }

        public void EnterDefeatPhase(DefeatPhase phase)
        {
            switch (phase)
            {
                case DefeatPhase.StoneRoll:
                    if (!muted) cinematic.PlayOneShot(cDefeatRoll,
                        Gain(0.62f, CinematicSfxGain));
                    break;
                case DefeatPhase.Look:
                    droneTarget = 0.12f;
                    melodyTarget = 0f;
                    if (!muted) cinematic.PlayOneShot(cDefeatLook,
                        Gain(0.48f, CinematicSfxGain));
                    break;
                case DefeatPhase.Descend:
                    droneTarget = 0.16f;
                    melodyTarget = 0f;
                    if (!muted) cinematic.PlayOneShot(cDefeatDescent,
                        Gain(0.52f, CinematicSfxGain));
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
            wind.clip = windClip;
            wind.volume = Gain(0.34f, NatureSfxGain);
            if (!wind.isPlaying) wind.Play();
        }
        public void WindStop() { wind.Stop(); wind.volume = 0f; }

        public void RainStart(int variant)
        {
            if (muted) return;
            if (rainClip == null) rainClip = BuildRain();
            rain.clip = rainClip;
            rain.pitch = 0.94f + Mathf.Clamp(variant, 0, 2) * 0.04f;
            rain.volume = Gain(0.20f + Mathf.Clamp(variant, 0, 2) * 0.025f, NatureSfxGain);
            if (!rain.isPlaying) rain.Play();
        }
        public void RainStop() { rain.Stop(); rain.volume = 0f; }

        // ---- SFX ----
        public void Push(float s) { if (!muted) sfx.PlayOneShot(cPush, Gain(0.38f + 0.32f * s, GameplaySfxGain)); }
        public void SteepPush(float s) { if (!muted) sfx.PlayOneShot(cSteepPush, Gain(0.44f + 0.34f * s, GameplaySfxGain)); }
        public void WindResistance(float s) { if (!muted) sfx.PlayOneShot(cWindPush, Gain(0.28f + 0.24f * s, NatureSfxGain)); }
        public void RainPush(float s) { if (!muted) sfx.PlayOneShot(cRainPush, Gain(0.34f + 0.28f * s, NatureSfxGain)); }
        public void Ice() { if (!muted) sfx.PlayOneShot(cIce, Gain(0.18f, NatureSfxGain)); }
        public void Friction() { if (!muted) sfx.PlayOneShot(cFriction, Gain(0.48f, NatureSfxGain)); }
        public void Error() { if (!muted) sfx.PlayOneShot(cError, Gain(0.8f, CinematicSfxGain)); }
        public void Rumble() { if (!muted) sfx.PlayOneShot(cRumble, Gain(0.9f, CinematicSfxGain)); }
        public void GameOver() { if (!muted) sfx.PlayOneShot(cGameover, Gain(0.9f, CinematicSfxGain)); }

        // ============================================================ Синтез
        static AudioClip Make(string name, float[] buf)
        {
            var clip = AudioClip.Create(name, buf.Length, 1, SR, false);
            clip.SetData(buf, 0);
            return clip;
        }

        static float[] NormalizePeak(float[] buffer, float targetPeak, float maxBoost = 5f)
        {
            float peak = 0f;
            for (int i = 0; i < buffer.Length; i++)
                peak = Mathf.Max(peak, Mathf.Abs(buffer[i]));
            if (peak < 0.0001f) return buffer;

            float boost = Mathf.Min(maxBoost, targetPeak / peak);
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = Mathf.Clamp(buffer[i] * boost, -1f, 1f);
            return buffer;
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
            cPush = Make("push", NormalizePeak(SynthPush(), 0.72f));
            cSteepPush = Make("steep_push", NormalizePeak(SynthSteepPush(), 0.78f));
            cWindPush = Make("wind_resistance", NormalizePeak(SynthWindPush(), 0.70f));
            cRainPush = Make("rain_push", NormalizePeak(SynthRainPush(), 0.70f));
            cIce = Make("ice", NormalizePeak(SynthIce(), 0.62f));
            cFriction = Make("friction", NormalizePeak(SynthFriction(), 0.66f));
            cSlipFall = Make("slip_fall", SynthSlipFall());
            cExhaustedBreath = Make("exhausted_breath", SynthExhaustedBreath());
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
            int n = (int)(0.44f * SR);
            var contact = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++) contact[i] = Noise();
            LowPass(contact, 180f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float bodyEnv = Mathf.Min(t / 0.042f, 1f) * Mathf.Exp(-t * 5.3f);
                float contactEnv = Mathf.Min(t / 0.016f, 1f) * Mathf.Exp(-t * 10f);
                float body = Mathf.Sin(2f * Mathf.PI * 36.71f * t) +
                    0.55f * Mathf.Sin(2f * Mathf.PI * 55f * t) +
                    0.20f * Mathf.Sin(2f * Mathf.PI * 82.41f * t);
                float weightPulse = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 7.5f * t));
                b[i] = body * bodyEnv * (0.37f + 0.07f * weightPulse) +
                    contact[i] * contactEnv * 0.14f;
            }
            LowPass(b, 220f);
            return b;
        }

        // Сопротивление ветра — глухой низкий напор без резкого воздушного шипения.
        float[] SynthWindPush()
        {
            int n = (int)(0.52f * SR);
            var air = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++) air[i] = Noise();
            LowPass(air, 180f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Min(t / 0.08f, 1f) * Mathf.Exp(-t * 3.7f);
                float breath = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * 2.4f * t);
                float hum = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.22f +
                    Mathf.Sin(2f * Mathf.PI * 82.41f * t) * 0.08f;
                b[i] = (air[i] * breath * 0.24f + hum) * env;
            }
            LowPass(b, 210f);
            return b;
        }

        // Дождевой толчок — отдельный влажный, приглушённый удар без звонких капель.
        float[] SynthRainPush()
        {
            int n = (int)(0.40f * SR);
            var rain = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++) rain[i] = Noise();
            LowPass(rain, 260f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Min(t / 0.038f, 1f) * Mathf.Exp(-t * 5.8f);
                float wetNoise = rain[i] * (0.58f + 0.42f * Mathf.Sin(2f * Mathf.PI * 5.2f * t));
                float body = Mathf.Sin(2f * Mathf.PI * 65.41f * t) +
                    0.26f * Mathf.Sin(2f * Mathf.PI * 98f * t);
                b[i] = (body * 0.24f + wetNoise * 0.22f) * env;
            }
            LowPass(b, 280f);
            return b;
        }

        float[] SynthIce()
        {
            const float duration = 0.44f;
            int n = (int)(duration * SR);
            var snow = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++) snow[i] = Noise();
            LowPass(snow, 620f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Min(t / 0.06f, 1f) * Mathf.Exp(-t * 4.8f);
                float glide = Mathf.Sin(2f * Mathf.PI * 125f * t) * 0.06f;
                b[i] = (snow[i] * 0.22f + glide) * env;
            }
            LowPass(b, 650f);
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
            const float duration = 0.42f;
            int n = (int)(duration * SR);
            var gravel = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++) gravel[i] = Noise();
            LowPass(gravel, 340f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Min(t / 0.025f, 1f) * Mathf.Exp(-t * 5.5f);
                float grains = 0.30f + 0.70f *
                    Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 13f * t)), 7f);
                float body = Mathf.Sin(2f * Mathf.PI * 72f * t) * 0.09f;
                b[i] = (gravel[i] * grains * 0.34f + body) * env;
            }
            LowPass(b, 360f);
            return b;
        }

        // Подскальзывание — короткое глухое падение тела на каменистую землю.
        float[] SynthSlipFall()
        {
            int n = (int)(0.58f * SR);
            var soil = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++) soil[i] = Noise();
            LowPass(soil, 190f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float impact = Mathf.Exp(-t * 12f);
                float settleT = Mathf.Max(0f, t - 0.13f);
                float settle = t >= 0.13f ? Mathf.Exp(-settleT * 15f) : 0f;
                float body = Mathf.Sin(2f * Mathf.PI * 46f * t) * impact +
                    0.42f * Mathf.Sin(2f * Mathf.PI * 68f * settleT) * settle;
                b[i] = body * 0.38f + soil[i] * (impact * 0.30f + settle * 0.16f);
            }
            LowPass(b, 230f);
            return b;
        }

        // Иссякание сил — длинный низкий выдох, а не удар или сигнал ошибки.
        float[] SynthExhaustedBreath()
        {
            const float duration = 1.45f;
            int n = (int)(duration * SR);
            var breath = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++) breath[i] = Noise();
            LowPass(breath, 520f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float p = t / duration;
                float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.16f)) *
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((duration - t) / 0.42f));
                float frequency = Mathf.Lerp(78f, 48f, p);
                float chest = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.15f;
                float exhale = breath[i] * (0.28f + 0.10f * Mathf.Sin(2f * Mathf.PI * 1.7f * t));
                b[i] = (exhale + chest) * env;
            }
            LowPass(b, 480f);
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
            const float duration = 5.6f;
            int n = (int)(duration * SR);
            var air = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++) air[i] = Noise();
            LowPass(air, 230f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float p = t / duration;
                float mainGust = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * p)), 1.65f);
                float secondGust = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * p)), 2.2f);
                float gust = 0.06f + mainGust * 0.68f + secondGust * 0.15f;
                float windowHum = (Mathf.Sin(2f * Mathf.PI * 48f * t) +
                    0.32f * Mathf.Sin(2f * Mathf.PI * 72f * t)) * mainGust * 0.07f;
                b[i] = air[i] * gust * 0.34f + windowHum;
            }
            LowPass(b, 260f);
            int fade = (int)(0.22f * SR);
            for (int i = 0; i < fade; i++)
            {
                float k = Mathf.SmoothStep(0f, 1f, i / (float)fade);
                b[i] *= k;
                b[n - 1 - i] *= k;
            }
            return Make("wind", NormalizePeak(b, 0.48f));
        }

        AudioClip BuildRain()
        {
            const float duration = 4.8f;
            int n = (int)(duration * SR);
            var water = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++) water[i] = Noise();
            LowPass(water, 680f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float swell = 0.72f + 0.18f * Mathf.Sin(2f * Mathf.PI * 0.34f * t) +
                    0.10f * Mathf.Sin(2f * Mathf.PI * 0.71f * t + 1.2f);
                float lowBody = Mathf.Sin(2f * Mathf.PI * 58f * t) * 0.035f;
                b[i] = water[i] * swell * 0.30f + lowBody;
            }
            LowPass(b, 720f);
            int fade = (int)(0.20f * SR);
            for (int i = 0; i < fade; i++)
            {
                float k = Mathf.SmoothStep(0f, 1f, i / (float)fade);
                b[i] *= k;
                b[n - 1 - i] *= k;
            }
            return Make("rain_ambience", NormalizePeak(b, 0.44f));
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
