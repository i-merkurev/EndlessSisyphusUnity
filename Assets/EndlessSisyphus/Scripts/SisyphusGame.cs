using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;   // проект с активным новым Input System
#endif

namespace EndlessSisyphus
{
    public enum GState { Start, Settings, Playing, Falling, Over }
    public enum ObKind { None, Wind, Rain, Ice, Steep }
    public enum ObPhase { Calm, Warn, Active }
    public enum CritterKind { Eagle, Goat, Raven, Lizard, Snake, Butterfly }

    public class Critter
    {
        public CritterKind kind;
        public float x, y, ty, dir, sp, w, head, headT, life, sit, bx, pause;
        public int phase;      // raven: 0 in, 1 sit, 2 out
        public bool dead, hasLife;
    }

    public class Particle { public bool wind; public float x, y, vx, vy, life; public bool hasLife; }
    public class Ambient { public string t; public float x, y, vx, vy, sw, life; public bool hasLife; public Color32 col; public bool hasCol; }
    public class Cloud { public float x, y, s, sp; }
    public class Erupt { public float x, y, vx, vy, life; public bool lava; }

    /// <summary>
    /// Центральный контроллер: конечный автомат + физика + стамина + планировщик
    /// препятствий + позиционный лёд + сезоны + рекорд. Прямой порт логики из game.js.
    /// Отрисовку ведёт WorldRenderer, звук — AudioEngine, UI — GameUI.
    /// </summary>
    public class SisyphusGame : MonoBehaviour
    {
        // ---- размеры виртуального экрана ----
        public int VW { get; private set; }
        public int VH { get; private set; }

        // ---- состояние ----
        public GState State = GState.Start;
        public int Best;
        public float Clock;

        public float Height, Momentum, Stamina;
        public float LastTapTime, PushAnim;
        public bool SpaceDown, ShiftDown, Careful, CarefulBad;

        public ObKind Obstacle = ObKind.None;
        public ObPhase Phase = ObPhase.Calm;
        public float ObTimer;
        public float IWind, IRain, IIce, ISteep;

        public float SlipRisk, Shake, Scroll, SfxTimer;
        public float StoneAngle, StoneSpinVel;

        // лёд как позиционный участок в мировых координатах
        public bool IceActive;
        public float IceStart, IceLen;

        public float FallT; public string FallReason = "exhausted";

        public readonly List<Particle> Particles = new List<Particle>();
        public readonly List<Cloud> Clouds = new List<Cloud>();
        public readonly List<Critter> Critters = new List<Critter>();
        public readonly List<Erupt> Erupts = new List<Erupt>();
        public readonly List<Ambient> Ambient = new List<Ambient>();

        float nextEagle = 3f, nextCritter = 1.5f, nextErupt = 4f, seasonTimer = 0f;

        public DifficultySettings Set;
        WorldRenderer world;
        AudioEngine audioEngine;

        // ---- утилиты ----
        static float Rand(float a, float b) => Random.Range(a, b);
        static float Clamp01f(float v, float a, float b) => Mathf.Clamp(v, a, b);
        public static float Hash(float n) { float x = Mathf.Sin(n * 127.1f) * 43758.5453f; return x - Mathf.Floor(x); }
        public float Difficulty() => 1f + Height / GameConfig.RampHeight;

        void Awake()
        {
            VH = GameConfig.VirtualH;
            float aspect = (Screen.height > 0) ? (float)Screen.width / Screen.height : 16f / 9f;
            VW = Mathf.RoundToInt(VH * aspect);
            Set = DifficultySettings.Load();
            Best = PlayerPrefs.GetInt("sisyphus_best", 0);
            Stamina = GameConfig.StaminaMax;

            for (int i = 0; i < 5; i++)
                Clouds.Add(new Cloud { x = Rand(0, 1), y = Rand(0.06f, 0.36f), s = Rand(0.6f, 1.4f), sp = Rand(1, 3) });

            audioEngine = new AudioEngine(gameObject);
            world = new WorldRenderer(this);
        }

        public AudioEngine Audio => audioEngine;

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Clock += dt;
            HandleInput();
            Tick(dt);
            UpdateAmbience(dt);
            world.Render();
        }

        // ================= Ввод =================
        // Дуальный ввод: работает и с legacy Input Manager, и с новым Input System —
        // не нужно менять Active Input Handling в Player Settings.
        void HandleInput()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = Keyboard.current; var ms = Mouse.current;
            bool spaceDownNow = kb != null && kb.spaceKey.wasPressedThisFrame;
            bool mouseDownNow = ms != null && ms.leftButton.wasPressedThisFrame;
            bool spaceHeld = kb != null && kb.spaceKey.isPressed;
            bool mouseHeld = ms != null && ms.leftButton.isPressed;
            bool shift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            bool ctrlDown = kb != null && (kb.leftCtrlKey.wasPressedThisFrame || kb.rightCtrlKey.wasPressedThisFrame);
            bool escDown = kb != null && kb.escapeKey.wasPressedThisFrame;
            bool rDown = kb != null && kb.rKey.wasPressedThisFrame;
            bool mDown = kb != null && kb.mKey.wasPressedThisFrame;
#else
            bool spaceDownNow = Input.GetKeyDown(KeyCode.Space);
            bool mouseDownNow = Input.GetMouseButtonDown(0);
            bool spaceHeld = Input.GetKey(KeyCode.Space);
            bool mouseHeld = Input.GetMouseButton(0);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrlDown = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl);
            bool escDown = Input.GetKeyDown(KeyCode.Escape);
            bool rDown = Input.GetKeyDown(KeyCode.R);
            bool mDown = Input.GetKeyDown(KeyCode.M);
#endif
            if (spaceDownNow) OnPushStart();                                   // старт/толчок с клавиатуры
            if (mouseDownNow && State == GState.Playing) RegisterTap();        // мышь тапает только в игре (не мешает кнопкам UI)
            SpaceDown = spaceHeld || (mouseHeld && State == GState.Playing);
            ShiftDown = shift;
            if (ctrlDown) ToggleCareful();
            if (escDown) { if (State == GState.Settings) CloseSettings(); else GoMenu(); }
            if (rDown) { if (State == GState.Playing || State == GState.Over || State == GState.Falling) StartGame(); }
            if (mDown) ToggleSound();
        }

        public void OnPushStart()
        {
            if (State == GState.Settings) return;
            audioEngine.StartMusic();
            if (State == GState.Start || State == GState.Over) { StartGame(); return; }
            if (State == GState.Playing) RegisterTap();
        }

        public void ToggleSound() { audioEngine.ToggleMute(); }
        public void ToggleCareful() { if (State != GState.Playing) return; Careful = !Careful; }

        // ================= Экраны =================
        public void StartGame()
        {
            audioEngine.StartMusic(); audioEngine.WindStop();
            State = GState.Playing;
            Height = 0; Momentum = 0; Stamina = GameConfig.StaminaMax;
            LastTapTime = Time.time; PushAnim = 0;
            Careful = false; CarefulBad = false;
            Obstacle = ObKind.None; Phase = ObPhase.Calm; ObTimer = Rand(3f, 4.5f);
            IceActive = false; SlipRisk = 0; Scroll = 0; Shake = 0; SfxTimer = 0;
            StoneAngle = 0; StoneSpinVel = 0;
            IWind = IRain = IIce = ISteep = 0;
            Particles.Clear();
        }

        public void GoMenu() { State = GState.Start; audioEngine.WindStop(); }
        public void OpenSettings() { State = GState.Settings; }
        public void CloseSettings() { State = GState.Start; }

        // ================= Толчок =================
        public void RegisterTap()
        {
            float now = Time.time, interval = now - LastTapTime; LastTapTime = now; PushAnim = 1;
            float factor;
            if (interval < GameConfig.MashInterval) factor = 0.3f;
            else { float diff = Mathf.Abs(interval - GameConfig.IdealInterval); factor = Mathf.Clamp(1 - diff * 1.5f, 0.35f, 1f); }
            ObKind active = ActiveObstacle();
            float impulse = GameConfig.TapImpulse * factor;
            if (ShiftDown) impulse *= GameConfig.ShiftBoost;
            if (active == ObKind.Wind) { impulse *= 0.1f; Stamina -= GameConfig.ErrWindTap * Set.DrainMul; audioEngine.Error(); }
            if (active == ObKind.Rain) impulse *= GameConfig.RainPushMul;
            Momentum += impulse; audioEngine.Push(factor);
        }

        public ObKind ActiveObstacle()
        {
            if (IWind > 0.5f) return ObKind.Wind;
            if (IRain > 0.5f) return ObKind.Rain;
            if (IIce > 0.5f) return ObKind.Ice;
            if (ISteep > 0.5f) return ObKind.Steep;
            return ObKind.None;
        }

        // ================= Планировщик препятствий =================
        void UpdateObstacles(float dt)
        {
            ObTimer -= dt;
            if (Obstacle != ObKind.Ice && ObTimer <= 0f)
            {
                if (Phase == ObPhase.Calm)
                {
                    Obstacle = PickObstacle();
                    bool telegraph = (Obstacle == ObKind.Ice || Obstacle == ObKind.Steep);
                    if (Obstacle == ObKind.Ice) StartIce();
                    else if (telegraph) { Phase = ObPhase.Warn; ObTimer = GameConfig.WarnLead; }
                    else { Phase = ObPhase.Active; ObTimer = ObstacleDuration(); if (Obstacle == ObKind.Wind) audioEngine.WindStart(); }
                }
                else if (Phase == ObPhase.Warn) { Phase = ObPhase.Active; ObTimer = ObstacleDuration(); }
                else { if (Obstacle == ObKind.Wind) audioEngine.WindStop(); Obstacle = ObKind.None; Phase = ObPhase.Calm; ObTimer = Rand(GameConfig.CalmMin, GameConfig.CalmMax) / Mathf.Sqrt(Difficulty()) / Set.FreqMul; }
            }

            // ветер / дождь / крутизна — интенсивность по фазе
            IWind += (TargetFor(ObKind.Wind) - IWind) * Mathf.Clamp(5f * dt, 0, 1);
            IRain += (TargetFor(ObKind.Rain) - IRain) * Mathf.Clamp(5f * dt, 0, 1);
            ISteep += (TargetFor(ObKind.Steep) - ISteep) * Mathf.Clamp(1.1f * dt, 0, 1);

            UpdateIce(dt);
        }

        float TargetFor(ObKind k)
        {
            if (Obstacle != k) return 0f;
            return Phase == ObPhase.Warn ? 0.35f : (Phase == ObPhase.Active ? 1f : 0f);
        }

        void StartIce()
        {
            float sisX = Scroll + VW * 0.40f;
            IceStart = sisX + VW * GameConfig.IceLeadDist;
            IceLen = Rand(GameConfig.IceLenMin, GameConfig.IceLenMax);
            IceActive = true;
            Obstacle = ObKind.Ice; Phase = ObPhase.Active; ObTimer = 16f;
        }

        void UpdateIce(float dt)
        {
            float sisX = Scroll + VW * 0.40f;
            bool onIce = IceActive && sisX >= IceStart && sisX <= IceStart + IceLen;
            IIce += ((onIce ? 1f : 0f) - IIce) * Mathf.Clamp(1.4f * dt, 0, 1);
            if (Obstacle == ObKind.Ice)
            {
                bool passed = IceActive && (sisX > IceStart + IceLen + 6f || (IceStart + IceLen - Scroll) < -24f);
                if (passed || ObTimer <= 0f)
                {
                    Obstacle = ObKind.None; Phase = ObPhase.Calm; IceActive = false;
                    ObTimer = Rand(GameConfig.CalmMin, GameConfig.CalmMax) / Mathf.Sqrt(Difficulty()) / Set.FreqMul;
                }
            }
        }

        float ObstacleDuration()
        {
            float d = Rand(GameConfig.ObstacleMin, GameConfig.ObstacleMax);
            if (Obstacle == ObKind.Wind) d *= Set.WindDurMul;
            return d;
        }

        ObKind PickObstacle()
        {
            float wWind = 1f, wRain = Set.RainProb, wIce = 1f, wSteep = 0.8f + Height / GameConfig.RampHeight;
            float total = wWind + wRain + wIce + wSteep;
            if (total <= 0) return ObKind.Wind;
            float r = Random.value * total;
            if ((r -= wWind) <= 0) return ObKind.Wind;
            if ((r -= wRain) <= 0) return ObKind.Rain;
            if ((r -= wIce) <= 0) return ObKind.Ice;
            return ObKind.Steep;
        }

        // ================= Главный апдейт =================
        void Tick(float dt)
        {
            if (State == GState.Falling) { UpdateFall(dt); return; }
            if (State != GState.Playing) return;

            UpdateObstacles(dt);

            float gravity = GameConfig.Gravity * (1 + (Difficulty() - 1) * 0.35f) * (1 + 0.8f * ISteep * Set.SteepMul);
            if (ISteep > 0.5f && !(SpaceDown && ShiftDown)) gravity *= 1.6f;
            Momentum -= gravity * dt;
            Momentum -= Momentum * GameConfig.Drag * dt;
            if (ISteep > 0.5f) Momentum -= GameConfig.SteepDrift * ISteep * Set.SteepMul * dt;
            if (SpaceDown) Momentum += GameConfig.HoldPush * (1 - 0.9f * IWind) * (1 - GameConfig.RainHoldMul * IRain) * dt;
            Momentum -= 1.5f * IWind * dt;

            Height += Momentum * dt;
            if (Height < 0) { Height = 0; if (Momentum < 0) Momentum = 0; }

            float drain = 0;
            if (IWind > 0.5f && SpaceDown) drain += GameConfig.DrainWind;
            if (IIce > 0.5f && !SpaceDown) drain += GameConfig.DrainIce;
            if (ISteep > 0.5f && SpaceDown && !ShiftDown) drain += GameConfig.DrainSteep;
            if (Careful && IRain < 0.5f) drain += GameConfig.DrainCareful;
            if (IWind < 0.5f && ISteep < 0.5f && Height > GameConfig.GraceHeight && Momentum < -0.05f) drain += GameConfig.DrainRollback;
            Stamina = Mathf.Clamp(Stamina - drain * dt * Set.DrainMul, 0, GameConfig.StaminaMax);
            if (Stamina <= 0) { StartFall("exhausted"); return; }
            CarefulBad = Careful && IRain < 0.5f;

            // звуки поверхности
            SfxTimer -= dt;
            if (SfxTimer <= 0f)
            {
                if (IIce > 0.5f && SpaceDown && Mathf.Abs(Momentum) > 0.15f) { audioEngine.Ice(); SfxTimer = Rand(0.09f, 0.16f); }
                else if (ISteep > 0.5f && SpaceDown) { audioEngine.Friction(); SfxTimer = Rand(0.16f, 0.24f); }
                else SfxTimer = 0.1f;
            }

            if (IRain > 0.5f && !Careful) { SlipRisk += GameConfig.RainSlipRate * dt * (0.7f + Random.value * 0.6f); if (SlipRisk >= 1) { StartFall("slip"); return; } }
            else SlipRisk = Mathf.Max(0, SlipRisk - dt);

            float targetSpin = Momentum * 0.05f;
            StoneSpinVel += (targetSpin - StoneSpinVel) * Mathf.Clamp(0.9f * dt, 0, 1);
            StoneAngle += StoneSpinVel * dt;

            PushAnim = Mathf.Max(0, PushAnim - dt * 5f);
            Shake = Mathf.Max(0, Shake - dt * 2.5f);
            Scroll += Momentum * dt * 26f;
            UpdateParticles(dt);
        }

        void StartFall(string reason)
        {
            State = GState.Falling; FallT = 0; FallReason = reason;
            Shake = 1.4f; StoneSpinVel = -3f;
            audioEngine.WindStop(); audioEngine.Rumble();
        }

        void UpdateFall(float dt)
        {
            FallT += dt; Shake = Mathf.Max(0, Shake - dt * 1.2f);
            StoneAngle += StoneSpinVel * dt; StoneSpinVel -= 2f * dt;
            Scroll -= dt * 120f;
            if (FallT > 1.5f) EndGame(FallReason);
        }

        void EndGame(string reason)
        {
            State = GState.Over; audioEngine.GameOver();
            int h = Mathf.FloorToInt(Height);
            if (h > Best) { Best = h; PlayerPrefs.SetInt("sisyphus_best", Best); PlayerPrefs.Save(); }
        }

        public bool IsRecordScreen => State == GState.Over && Mathf.FloorToInt(Height) >= Best && Best > 0;

        // ================= Частицы погоды =================
        void UpdateParticles(float dt)
        {
            if (IRain > 0.3f)
                for (int i = 0; i < Mathf.Ceil(IRain * 2); i++)
                    Particles.Add(new Particle { wind = false, x = Rand(0, VW), y = -4, vy = Rand(220, 300), vx = -40 });
            if (IWind > 0.3f)
                Particles.Add(new Particle { wind = true, x = VW + 6, y = Rand(0, VH * 0.7f), vx = Rand(-260, -200), vy = Rand(-8, 8), life = 0.9f, hasLife = true });
            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                var p = Particles[i]; p.x += p.vx * dt; p.y += p.vy * dt; if (p.hasLife) p.life -= dt;
                if (!(p.y < VH + 6 && p.x > -20 && (!p.hasLife || p.life > 0))) Particles.RemoveAt(i);
            }
        }

        // ================= Атмосфера: облака, вулкан, живность, сезоны =================
        float MountY(float f) => VH * (0.40f + 0.14f * f);

        void UpdateAmbience(float dt)
        {
            foreach (var c in Clouds) { c.x += (c.sp / VW) * dt; if (c.x > 1.2f) { c.x = -0.2f; c.y = Rand(0.06f, 0.36f); } }

            nextErupt -= dt;
            if (nextErupt <= 0) { nextErupt = Rand(4, 9); float vx = VW * 0.72f, vy = VH * 0.30f; for (int i = 0; i < 5; i++) Erupts.Add(new Erupt { x = vx + Rand(-3, 3), y = vy, vx = Rand(-8, 8), vy = Rand(-30, -18), life = Rand(1.2f, 2.2f), lava = Random.value < 0.5f }); }
            for (int i = Erupts.Count - 1; i >= 0; i--) { var p = Erupts[i]; p.x += p.vx * dt; p.y += p.vy * dt; p.vy += 14 * dt; p.life -= dt; if (p.life <= 0) Erupts.RemoveAt(i); }

            nextEagle -= dt;
            if (nextEagle <= 0) { nextEagle = Rand(8, 16); float dir = Random.value < 0.5f ? 1 : -1; Critters.Add(new Critter { kind = CritterKind.Eagle, x = dir > 0 ? -20 : VW + 20, y = Rand(VH * 0.10f, VH * 0.38f), dir = dir, sp = Rand(14, 24), w = 0 }); }

            nextCritter -= dt;
            if (nextCritter <= 0) { nextCritter = Rand(3, 6); SpawnCritter((CritterKind)new int[] { 1, 1, 2, 3, 4, 5 }[Mathf.FloorToInt(Rand(0, 6))]); }
            for (int i = Critters.Count - 1; i >= 0; i--) { UpdateCritter(Critters[i], dt); if (Critters[i].dead) Critters.RemoveAt(i); }

            // сезонные частицы
            seasonTimer -= dt;
            if (seasonTimer <= 0) { SpawnSeasonParticle(); seasonTimer = SeasonSpawnInterval(); }
            for (int i = Ambient.Count - 1; i >= 0; i--)
            {
                var p = Ambient[i]; p.sw += dt * 2.2f;
                float sway = p.t == "leaf" ? 16 : p.t == "snow" ? 9 : p.t == "petal" ? 8 : 4;
                p.x += (p.vx + Mathf.Sin(p.sw) * sway) * dt; p.y += p.vy * dt; if (p.hasLife) p.life -= dt;
                if (!(p.y < VH + 8 && p.x > -24 && p.x < VW + 24 && (!p.hasLife || p.life > 0))) Ambient.RemoveAt(i);
            }
        }

        void SpawnCritter(CritterKind t)
        {
            float x = Rand(VW * 0.12f, VW * 0.9f), dir = Random.value < 0.5f ? 1 : -1;
            switch (t)
            {
                case CritterKind.Goat: Critters.Add(new Critter { kind = t, x = x, y = MountY(Hash(x)), dir = dir, sp = Rand(1.5f, 3.5f), w = Rand(0, 6), head = 0, headT = Rand(1, 2), life = Rand(10, 16), hasLife = true }); break;
                case CritterKind.Raven: Critters.Add(new Critter { kind = t, x = x, y = -6, ty = MountY(Hash(x)), phase = 0, w = 0, head = 0, headT = Rand(0.8f, 1.6f), sit = Rand(3, 6) }); break;
                case CritterKind.Lizard: Critters.Add(new Critter { kind = t, x = dir > 0 ? VW * 0.1f : VW * 0.9f, y = MountY(Hash(x)) + 3, dir = dir, sp = Rand(24, 40), pause = 0, life = Rand(6, 10), hasLife = true }); break;
                case CritterKind.Snake: Critters.Add(new Critter { kind = t, x = x, y = MountY(Hash(x)) + 4, w = Rand(0, 6), head = 0, life = Rand(8, 12), hasLife = true }); break;
                case CritterKind.Butterfly: Critters.Add(new Critter { kind = t, x = x, y = MountY(Hash(x)) - 6, w = Rand(0, 6), bx = Rand(0, 6), life = Rand(6, 10), hasLife = true }); break;
            }
        }

        void UpdateCritter(Critter c, float dt)
        {
            if (c.kind == CritterKind.Eagle) { c.x += c.dir * c.sp * dt; c.w += dt * 6; if (c.x < -30 || c.x > VW + 30) c.dead = true; return; }
            if (c.hasLife) { c.life -= dt; if (c.life <= 0 && c.kind != CritterKind.Raven) c.dead = true; }
            switch (c.kind)
            {
                case CritterKind.Goat:
                    c.headT -= dt; if (c.headT <= 0) { c.headT = Rand(1.2f, 2.6f); c.head = c.head != 0 ? 0 : (Random.value < 0.5f ? 1 : -1); }
                    if (c.head == 0) c.x += c.dir * c.sp * dt; c.w += dt * 3;
                    if (c.x < VW * 0.05f || c.x > VW * 0.95f) c.dir *= -1;
                    break;
                case CritterKind.Raven:
                    c.w += dt * 8;
                    if (c.phase == 0) { c.y += (c.ty - c.y) * Mathf.Clamp(3 * dt, 0, 1); if (Mathf.Abs(c.y - c.ty) < 1) c.phase = 1; }
                    else if (c.phase == 1) { c.sit -= dt; c.headT -= dt; if (c.headT <= 0) { c.headT = Rand(0.7f, 1.5f); c.head = c.head != 0 ? 0 : (Random.value < 0.5f ? 1 : -1); } if (c.sit <= 0) c.phase = 2; }
                    else { c.y -= 34 * dt; c.x += 22 * dt; if (c.y < -8) c.dead = true; }
                    break;
                case CritterKind.Lizard:
                    c.pause -= dt; if (c.pause <= 0) { c.x += c.dir * c.sp * dt; if (Random.value < 0.02f) c.pause = Rand(0.4f, 1.1f); }
                    if (c.x < -6 || c.x > VW + 6) c.dead = true;
                    break;
                case CritterKind.Snake: c.w += dt * 2.2f; c.head = Mathf.Sin(c.w * 0.5f); break;
                case CritterKind.Butterfly: c.w += dt * 5; c.bx += dt; c.x += Mathf.Sin(c.bx * 2) * 10 * dt; c.y += Mathf.Cos(c.bx * 3) * 7 * dt; break;
            }
        }

        // ---- сезоны ----
        public void SeasonData(out int i, out float t, out int next)
        {
            float sp = Clock / GameConfig.SeasonLen;
            i = ((Mathf.FloorToInt(sp) % 4) + 4) % 4;
            t = sp - Mathf.Floor(sp);
            next = (i + 1) % 4;
        }

        public float SeasonWeight(int idx) { SeasonData(out int i, out float t, out int next); return (i == idx ? 1 - t : 0) + (next == idx ? t : 0); }

        public Color32 GrassTone()
        {
            Color32[] cols = { new Color32(110, 170, 70, 255), new Color32(96, 140, 60, 255), new Color32(176, 132, 52, 255), new Color32(200, 214, 224, 255) };
            SeasonData(out int i, out float t, out int next);
            return Palette.Mix(cols[i], cols[next], t);
        }

        void SpawnSeasonParticle()
        {
            SeasonData(out int i, out float t, out int next);
            int pick = Random.value < (1 - t) ? i : next;
            if (pick == 0) Ambient.Add(new Ambient { t = "petal", x = Rand(0, VW), y = -4, vy = Rand(14, 26), vx = Rand(-6, 6), sw = Rand(0, 6.28f), col = Random.value < 0.5f ? new Color32(248, 196, 214, 255) : new Color32(236, 240, 220, 255), hasCol = true });
            else if (pick == 1) Ambient.Add(new Ambient { t = "pollen", x = Rand(0, VW), y = Rand(0, VH * 0.7f), vy = Rand(-3, 4), vx = Rand(-4, 4), sw = Rand(0, 6.28f), life = Rand(3, 6), hasLife = true });
            else if (pick == 2) { Color32[] lc = { new Color32(200, 110, 40, 255), new Color32(176, 84, 40, 255), new Color32(150, 120, 50, 255), new Color32(190, 140, 50, 255) }; Ambient.Add(new Ambient { t = "leaf", x = Rand(0, VW), y = -4, vy = Rand(18, 30), vx = Rand(-10, 2), sw = Rand(0, 6.28f), col = lc[Mathf.FloorToInt(Rand(0, 4))], hasCol = true }); }
            else Ambient.Add(new Ambient { t = "snow", x = Rand(0, VW), y = -4, vy = Rand(22, 40), vx = Rand(-8, 6), sw = Rand(0, 6.28f) });
        }

        float SeasonSpawnInterval()
        {
            SeasonData(out int i, out float t, out int next);
            int dom = (1 - t) > 0.5f ? i : next;
            return dom == 1 ? Rand(0.45f, 0.95f) : dom == 3 ? Rand(0.05f, 0.12f) : Rand(0.1f, 0.22f);
        }
    }
}
