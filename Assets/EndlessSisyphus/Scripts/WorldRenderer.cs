using System.Collections.Generic;
using UnityEngine;

namespace EndlessSisyphus
{
    /// <summary>
    /// Порт render() из game.js. Весь процедурный мир рисуется в PixelCanvas (координаты
    /// y-вниз, как в web-canvas) и выводится на фоновый SpriteRenderer. Дополнительно
    /// создаются РЕАЛЬНЫЕ спрайт-объекты Сизифа и валуна (пустые по умолчанию) — они трекают
    /// позицию/поворот героя, чтобы подменить процедурную отрисовку на арт: назначь им Sprite
    /// и выключи DrawHeroesInBackdrop.
    /// </summary>
    public class WorldRenderer
    {
        readonly SisyphusGame g;
        readonly int VW, VH;
        readonly PixelCanvas bg;

        Transform root;
        SpriteRenderer bgSR, sisSR, bouSR;
        bool spriteReady;

        /// <summary>true — герои рисуются в процедурном слое (по умолчанию, гарантированно верно).
        /// false — рисуй их своими спрайтами через sisSR/bouSR.</summary>
        public bool DrawHeroesInBackdrop = true;

        // геометрия склона текущего кадра
        float ang, baseY;
        float TerrainWave(float worldX) =>
            Mathf.Sin(worldX * 0.024f) * 6.2f +
            Mathf.Sin(worldX * 0.067f + 1.7f) * 2.4f;
        float SlopeY(float x)
        {
            float worldX = x + g.Scroll;
            float heroWorldX = g.Scroll + VW * 0.40f;
            float localSteepRise = g.SteepElevationAt(worldX) - g.SteepElevationAt(heroWorldX);
            return baseY - x * ang + TerrainWave(worldX) - localSteepRise;
        }
        float SlopeRotation(float x) => Mathf.Atan((SlopeY(x + 2f) - SlopeY(x - 2f)) * 0.25f);

        // силуэт валуна (порт BOULDER)
        const int BoulderRadius = 19;
        const float BoulderOffset = 25f;
        const float HeroScale = 1.18f;
        readonly Vector2[] boulder = new Vector2[20];

        public WorldRenderer(SisyphusGame game)
        {
            g = game; VW = g.VW; VH = g.VH;
            bg = new PixelCanvas(VW, VH);
            for (int i = 0; i < boulder.Length; i++)
            {
                float a = (i / (float)boulder.Length) * Mathf.PI * 2f;
                float r = 0.97f + (SisyphusGame.Hash(i * 3.3f) - 0.5f) * 0.08f;
                boulder[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            SetupObjects();
            SetupCamera();
        }

        void SetupObjects()
        {
            root = new GameObject("EndlessSisyphus_World").transform;
            bgSR = MakeSR("Backdrop", 0);
            bouSR = MakeSR("Boulder", 10);
            sisSR = MakeSR("Sisyphus", 11);
        }

        SpriteRenderer MakeSR(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = true;
            cam.orthographicSize = VH / 2f;
            cam.transform.position = new Vector3(VW / 2f, VH / 2f, -10f);
            cam.backgroundColor = Color.black;
        }

        // ============================================================ РЕНДЕР
        public void Render()
        {
            float night = Nightness();
            DrawSky(night); DrawCelestial(night); DrawStars(night); DrawClouds();
            DrawFarRanges(); DrawVolcano();
            DrawMountainCritters(); DrawEagles();
            DrawSlope();
            if (DrawHeroesInBackdrop) DrawActor();
            DrawAmbient();
            DrawWeather();
            DrawFog();
            DrawSeasonTint();

            var tex = bg.ToTexture();
            if (!spriteReady)
            {
                bgSR.sprite = Sprite.Create(tex, new Rect(0, 0, VW, VH), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
                spriteReady = true;
            }

            // тряска — сдвиг всего мира
            float sh = g.Shake > 0 ? g.Shake * 3f : 0f;
            root.position = sh > 0 ? new Vector3(Mathf.Round(Random.Range(-sh, sh)), Mathf.Round(Random.Range(-sh, sh)), 0) : Vector3.zero;

            UpdateHeroTransforms();
        }

        float Nightness() => (1 - Mathf.Cos((g.Clock / GameConfig.DayLen) * Mathf.PI * 2f)) / 2f;

        // canvas-точка (y-вниз) → миров. координата (y-вверх) для спрайт-объектов
        Vector3 ToWorld(float cx, float cyDown) => new Vector3(cx, VH - cyDown, 0);

        void ActorLayout(out float stoneX, out float heroX, out float tumble, out bool showStone)
        {
            float px = VW * 0.40f;
            stoneX = px + BoulderOffset;
            heroX = px;
            tumble = 0f;
            showStone = true;

            if (g.IntroActive)
            {
                float approach = Mathf.SmoothStep(0f, 1f, g.IntroProgress);
                heroX = Mathf.Lerp(-24f, px, approach);
                return;
            }

            bool cinematic = g.DefeatStage != DefeatPhase.None &&
                (g.State == GState.Falling || g.State == GState.Over);
            if (!cinematic) return;

            float rawProgress = g.DefeatPhaseProgress;
            float p = Mathf.SmoothStep(0f, 1f, rawProgress);
            const float fallenRotation = 0.82f;
            switch (g.DefeatStage)
            {
                case DefeatPhase.Slip:
                    stoneX -= 10f * p;
                    heroX -= 7f * p;
                    tumble = fallenRotation * p;
                    break;
                case DefeatPhase.StoneRoll:
                {
                    float rollingProgress = rawProgress * rawProgress;
                    stoneX -= Mathf.Lerp(10f, VW * 0.72f, rollingProgress);
                    heroX -= 7f;
                    tumble = fallenRotation;
                    break;
                }
                case DefeatPhase.Pause:
                    stoneX -= VW * 0.72f;
                    heroX -= 7f;
                    tumble = fallenRotation;
                    showStone = false;
                    break;
                case DefeatPhase.Rise:
                    stoneX -= VW * 0.72f;
                    heroX -= Mathf.Lerp(7f, 3f, p);
                    tumble = Mathf.Lerp(fallenRotation, 0f, p);
                    showStone = false;
                    break;
                case DefeatPhase.Look:
                    stoneX -= VW * 0.72f;
                    heroX -= 3f;
                    showStone = false;
                    break;
                case DefeatPhase.Descend:
                    stoneX -= VW * 0.72f;
                    heroX -= 3f + VW * 0.56f * p;
                    showStone = false;
                    break;
            }
        }

        void UpdateHeroTransforms()
        {
            ActorLayout(out float sx, out float hpx, out float tumble, out bool showStone);
            int R = BoulderRadius;
            float sy = SlopeY(sx) + BoulderOffset * Mathf.Sin(SlopeRotation(sx)) - R + 2;
            bouSR.transform.position = ToWorld(sx, sy);
            bouSR.transform.rotation = Quaternion.Euler(0, 0, -g.StoneAngle * Mathf.Rad2Deg);
            bouSR.enabled = showStone;
            sisSR.transform.position = ToWorld(hpx, SlopeY(hpx));
            sisSR.transform.rotation = Quaternion.Euler(0, 0, (-SlopeRotation(hpx) - tumble) * Mathf.Rad2Deg);
        }

        // ---------- Небо / светила / звёзды / облака ----------
        void DrawSky(float night)
        {
            Color32 top = Palette.Mix(Palette.SkyDayTop, Palette.SkyNightTop, night);
            Color32 bot = Palette.Mix(Palette.SkyDayBot, Palette.SkyNightBot, night);
            int b = 12;
            for (int i = 0; i < b; i++)
                bg.FillRect(0, Mathf.FloorToInt(i * VH / (float)b), VW, Mathf.CeilToInt(VH / (float)b) + 1, Palette.Mix(top, bot, i / (float)(b - 1)));
        }

        void DrawCelestial(float night)
        {
            float ph = (g.Clock / GameConfig.DayLen) % 1f, a = ph * Mathf.PI * 2f - Mathf.PI / 2f;
            float cx = VW * 0.5f + Mathf.Cos(a) * VW * 0.42f, cy = VH * 0.55f + Mathf.Sin(a) * VH * 0.5f;
            bool moon = night > 0.5f;
            if (!moon) bg.FillCircle(cx, cy, 13, new Color32(255, 230, 140, 255), 0.25f);
            float r = moon ? 7 : 9;
            bg.FillCircle(cx, cy, r, moon ? Palette.Moon : Palette.Sun);
            if (moon) bg.FillCircle(cx + 3, cy - 2, r - 1, Palette.Mix(Palette.SkyNightTop, Palette.SkyNightBot, 0.3f));
        }

        void DrawStars(float night)
        {
            if (night < 0.15f) return;
            float alpha = Mathf.Clamp01((night - 0.15f) * 1.3f);
            for (int i = 0; i < 55; i++)
            {
                int x = (int)((i * 71.3f) % VW), y = (int)((i * 47.9f) % (VH * 0.55f));
                if ((i * 13 + Mathf.FloorToInt(g.Clock * 2)) % 19 != 0) bg.PlotBlend(x, y, new Color32(255, 255, 255, 255), alpha);
            }
        }

        void DrawClouds()
        {
            var c = new Color32(230, 230, 245, 255);
            foreach (var cl in g.Clouds)
            {
                float cx = Mathf.Floor(cl.x * VW), cy = Mathf.Floor(cl.y * VH), r = 6 * cl.s;
                bg.FillCircle(cx, cy, r, c, 0.16f); bg.FillCircle(cx + r, cy + 2, r * 0.8f, c, 0.16f); bg.FillCircle(cx - r, cy + 2, r * 0.7f, c, 0.16f);
            }
        }

        void DrawFarRanges()
        {
            (Color32 col, float h, float amp, float w, float sp)[] layers =
            {
                (Palette.MountFar, 0.40f, 0.10f, 90f, 0.10f),
                (Palette.MountMid, 0.48f, 0.13f, 70f, 0.16f),
            };
            foreach (var L in layers)
            {
                float off = (g.Scroll * L.sp) % L.w;
                var pts = new List<Vector2> { new Vector2(0, VH) };
                for (float x = -L.w; x <= VW + L.w; x += L.w)
                {
                    pts.Add(new Vector2(x - off + L.w / 2, VH * (L.h - L.amp)));
                    pts.Add(new Vector2(x - off + L.w, VH * (L.h + L.amp * 0.4f)));
                }
                pts.Add(new Vector2(VW, VH));
                bg.FillPolygon(pts.ToArray(), L.col);
            }
        }

        void DrawVolcano()
        {
            float vx = VW * 0.72f, baseB = VH * 0.5f, peak = VH * 0.28f;
            bg.FillPolygon(new[] { new Vector2(vx - 26, baseB), new Vector2(vx - 6, peak), new Vector2(vx + 6, peak), new Vector2(vx + 26, baseB) }, Palette.Volcano);
            bg.FillRect((int)(vx - 5), (int)peak, 10, 2, Palette.Lava);
            bg.FillRectBlend((int)(vx - 2), (int)peak, 3, Mathf.RoundToInt(baseB - peak) - 4, Palette.Lava, 0.5f);
            foreach (var p in g.Erupts)
            {
                if (p.lava) bg.FillRect(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), 2, 2, Palette.Lava);
                else bg.FillRectBlend(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), 2, 2, new Color32(120, 114, 126, 255), Mathf.Clamp01(p.life));
            }
        }

        void DrawEagles()
        {
            var c = new Color32(20, 16, 30, 255);
            foreach (var cr in g.Critters)
            {
                if (cr.kind != CritterKind.Eagle) continue;
                int flap = Mathf.RoundToInt(Mathf.Sin(cr.w) * 3), x = Mathf.RoundToInt(cr.x), y = Mathf.RoundToInt(cr.y);
                bg.FillRectBlend(x - 1, y, 2, 2, c, 0.85f); bg.FillRectBlend(x - 5, y - flap - 1, 4, 2, c, 0.85f); bg.FillRectBlend(x + 1, y - flap - 1, 4, 2, c, 0.85f);
            }
        }

        void DrawMountainCritters()
        {
            foreach (var cr in g.Critters)
            {
                if (cr.kind == CritterKind.Eagle) continue;
                int x = Mathf.RoundToInt(cr.x), y = Mathf.RoundToInt(cr.y);
                switch (cr.kind)
                {
                    case CritterKind.Goat:
                        bg.FillRect(x - 3, y, 6, 3, Palette.Goat); bg.FillRect(x - 3, y + 2, 6, 1, Palette.GoatDk);
                        int hx = cr.dir > 0 ? x + 3 : x - 4;
                        bg.FillRect(hx, y - 2, 2, 3, Palette.Goat);
                        bg.FillRect(hx + (cr.dir > 0 ? 0 : 1), y - 4, 1, 2, Palette.Horn); bg.FillRect(hx + (cr.dir > 0 ? -1 : 2), y - 5, 1, 2, Palette.Horn);
                        bg.FillRect(hx, y + 1, 1, 1, Palette.GoatDk);
                        int leg = Mathf.RoundToInt(Mathf.Sin(cr.w) * 1);
                        bg.FillRect(x - 2, y + 3, 1, 2 + leg, Palette.GoatDk); bg.FillRect(x + 1, y + 3, 1, 2 - leg, Palette.GoatDk);
                        break;
                    case CritterKind.Snake:
                        // Змеи исключены из окружения: тип оставлен для совместимости
                        // с уже сохранёнными состояниями, но визуально не выводится.
                        break;
                    case CritterKind.Raven:
                        int fl = Mathf.RoundToInt(Mathf.Sin(cr.w) * 2); bg.FillRect(x, y - 3, 2, 4, Palette.Raven);
                        if (cr.phase != 1) { bg.FillRect(x - 3, y - 3 - fl, 3, 2, Palette.Raven); bg.FillRect(x + 2, y - 3 - fl, 3, 2, Palette.Raven); }
                        else { int rhx = cr.head >= 0 ? x + 2 : x - 1; bg.FillRect(rhx, y - 4, 1, 1, Palette.Raven); }
                        bg.FillRect(x + (cr.head < 0 ? -1 : 2), y - 3, 1, 1, new Color32(224, 160, 32, 255));
                        break;
                    case CritterKind.Lizard:
                        bg.FillRect(x, y, 4, 1, Palette.Lizard); bg.FillRect(x + (cr.dir > 0 ? 4 : -3), y, 3, 1, Palette.Lizard); bg.FillRect(x + 1, y - 1, 1, 1, Palette.Lizard);
                        break;
                    case CritterKind.Butterfly:
                        int open = Mathf.Sin(cr.w) > 0 ? 2 : 1; bg.FillRect(x - open, y - 1, open, 2, Palette.Flutter); bg.FillRect(x + 1, y - 1, open, 2, Palette.Flutter); bg.FillRect(x, y - 1, 1, 2, new Color32(58, 42, 16, 255));
                        break;
                }
            }
        }

        // ---------- Склон ----------
        float SlopeAngle() => Mathf.Min(0.30f + (g.Difficulty() - 1) * 0.05f, 0.58f);

        void DrawSlope()
        {
            ang = SlopeAngle(); baseY = VH * 0.80f;

            bg.FillPolygon(new[] { new Vector2(0, SlopeY(0)), new Vector2(VW, SlopeY(VW)), new Vector2(VW, VH), new Vector2(0, VH) }, Palette.Dirt2);

            int cell = 8; float off = g.Scroll % cell;
            for (float sx = -cell; sx < VW + cell; sx += cell)
            {
                float wx = sx - off; int wi = Mathf.FloorToInt((g.Scroll + sx) / cell); float sy = SlopeY(wx);
                float h1 = SisyphusGame.Hash(wi), h2 = SisyphusGame.Hash(wi * 1.7f + 3);
                Color32 col = h1 < 0.33f ? Palette.Dirt1 : h1 < 0.66f ? Palette.Dirt2 : Palette.Dirt3;
                bg.FillRect(Mathf.RoundToInt(wx), Mathf.RoundToInt(sy), cell + 1, VH - Mathf.RoundToInt(sy), col);
                bg.FillRect(Mathf.RoundToInt(wx), Mathf.RoundToInt(sy), cell + 1, 2, Palette.Dirt1);
                if (h2 < 0.25f) bg.FillRect(Mathf.RoundToInt(wx) + Mathf.FloorToInt(h1 * 4), Mathf.RoundToInt(sy) + 3 + Mathf.FloorToInt(h2 * 8), 2, 2, Palette.Rock);
                if (h2 > 0.85f) bg.FillRect(Mathf.RoundToInt(wx) + 2, Mathf.RoundToInt(sy) + 6, 3, 2, Palette.StoneCr);
                if (h1 > 0.92f) { var gr = g.GrassTone(); bg.FillRect(Mathf.RoundToInt(wx) + 1, Mathf.RoundToInt(sy) - 2, 1, 2, gr); bg.FillRect(Mathf.RoundToInt(wx) + 3, Mathf.RoundToInt(sy) - 3, 1, 3, gr); }
            }

            DrawSnowCover();
            DrawProps();
            DrawIce();
            DrawBridges();
        }

        void DrawSnowCover()
        {
            float w = g.SeasonWeight(3); if (w < 0.04f) return;
            float a = Mathf.Clamp(0.65f * w, 0, 0.65f);
            int cell = 10; float off = ((g.Scroll % cell) + cell) % cell;
            for (float sx = -cell; sx < VW + cell; sx += cell)
            {
                float wx = sx - off; int cx = Mathf.RoundToInt((g.Scroll + sx) / cell); float y = SlopeY(wx);
                int th = 2 + Mathf.RoundToInt(SisyphusGame.Hash(cx * 3.1f) * 2);
                bg.FillRectBlend(Mathf.RoundToInt(wx), Mathf.RoundToInt(y), cell + 1, th, new Color32(236, 244, 255, 255), a);
                if (SisyphusGame.Hash(cx * 1.9f + 2) > 0.6f) bg.FillRectBlend(Mathf.RoundToInt(wx), Mathf.RoundToInt(y) + th, cell + 1, 1, new Color32(236, 244, 255, 255), a);
            }
        }

        void DrawProps()
        {
            int period = 46, kS = Mathf.FloorToInt((g.Scroll - 20) / period), kE = Mathf.CeilToInt((g.Scroll + VW + 20) / period);
            for (int k = kS; k <= kE; k++)
            {
                float h = SisyphusGame.Hash(k * 5.1f); if (h > 0.62f) continue;
                int x = Mathf.RoundToInt(k * period - g.Scroll), y = Mathf.RoundToInt(SlopeY(x));
                if (x < -10 || x > VW + 10) continue;
                if (IsOverGorge(k * period)) continue;
                if (h < 0.22f)
                {
                    bg.FillRect(x, y - 12, 2, 12, Palette.DeadTree);
                    bg.FillRect(x - 3, y - 9, 3, 1, Palette.DeadTree); bg.FillRect(x + 2, y - 11, 3, 1, Palette.DeadTree); bg.FillRect(x - 2, y - 6, 2, 1, Palette.DeadTree); bg.FillRect(x + 2, y - 5, 2, 1, Palette.DeadTree);
                    bg.FillRect(x - 4, y - 10, 1, 2, Palette.DeadTree); bg.FillRect(x + 4, y - 12, 1, 2, Palette.DeadTree);
                }
                else if (h < 0.44f)
                {
                    bg.FillRect(x - 2, y - 4, 6, 4, Palette.Shrub); bg.FillRect(x - 1, y - 6, 4, 2, Palette.Shrub);
                    bg.FillRect(x, y - 3, 1, 3, Palette.Dirt3); bg.FillRect(x + 2, y - 2, 1, 2, Palette.Dirt3);
                }
                else
                {
                    bg.FillRect(x - 2, y - 4, 7, 5, Palette.Rock); bg.FillRect(x - 1, y - 4, 3, 2, Palette.StoneHi); bg.FillRect(x + 3, y - 1, 2, 2, Palette.StoneCr);
                }
            }
        }

        void DrawIce()
        {
            for (int i = 0; i < g.IcePatches.Count; i++)
                DrawIcePatch(g.IcePatches[i].x, g.IcePatches[i].y);
        }

        void DrawIcePatch(float start, float length)
        {
            float x0 = start - g.Scroll, x1 = start + length - g.Scroll;
            if (x1 < -6 || x0 > VW + 6) return;
            int xa = Mathf.Max(-6, Mathf.FloorToInt(x0)), xb = Mathf.Min(VW + 6, Mathf.CeilToInt(x1));
            var pts = new List<Vector2>();
            for (int x = xa; x <= xb; x += 4) pts.Add(new Vector2(x, SlopeY(x)));
            pts.Add(new Vector2(xb, VH)); pts.Add(new Vector2(xa, VH));
            bg.FillPolygon(pts.ToArray(), Palette.Ice1);
            for (int x = xa; x < xb; x += 10) { float y = SlopeY(x); if (Mathf.FloorToInt((x + g.Scroll) / 10) % 2 != 0) bg.FillRect(x, Mathf.RoundToInt(y) + 4, 6, 3, Palette.Ice2); }
            for (int x = xa + 6; x < xb; x += 20) bg.FillRectBlend(x, Mathf.RoundToInt(SlopeY(x)) + 2, 3, 1, new Color32(255, 255, 255, 255), 0.6f);
            if (x1 <= VW + 6) bg.Line(x1, SlopeY(x1) - 3, x1, VH, Palette.IceEdge, 2);
            if (x0 >= -6) bg.Line(x0, SlopeY(x0) - 2, x0, VH, Palette.IceEdge, 2);
        }

        // ---------- Ущелья и мосты ----------
        const float GorgePeriod = 300f;
        static bool GorgeAt(int k) => SisyphusGame.Hash(k * 1.3f) < 0.42f;
        static int GorgeWidth(int k) => 40 + Mathf.FloorToInt(SisyphusGame.Hash(k * 2.1f) * 26);
        bool IsOverGorge(float worldX)
        {
            int k = Mathf.RoundToInt(worldX / GorgePeriod);
            for (int d = -1; d <= 1; d++)
            {
                int kk = k + d; if (!GorgeAt(kk)) continue;
                float gx0 = kk * GorgePeriod, gw = GorgeWidth(kk);
                if (worldX > gx0 - 6 && worldX < gx0 + gw + 6) return true;
            }
            return false;
        }

        void DrawBridges()
        {
            int kS = Mathf.FloorToInt((g.Scroll - 40) / GorgePeriod), kE = Mathf.CeilToInt((g.Scroll + VW + 40) / GorgePeriod);
            for (int k = kS; k <= kE; k++)
            {
                if (!GorgeAt(k)) continue;
                int gw = GorgeWidth(k), gx = Mathf.RoundToInt(k * GorgePeriod - g.Scroll);
                if (gx + gw < -10 || gx > VW + 10) continue;
                float yL = SlopeY(gx), yR = SlopeY(gx + gw);
                // расщелина вниз
                int bands = 8;
                for (int i = 0; i < bands; i++)
                    bg.FillRect(gx, Mathf.RoundToInt(yL + i * (VH - yL) / bands), gw, Mathf.RoundToInt((VH - yL) / bands) + 1, Palette.Mix(Palette.Dirt3, Palette.Chasm, i / (float)(bands - 1)));
                bg.FillRect(gx, Mathf.RoundToInt(yL), 2, 3, Palette.Rock); bg.FillRect(gx + gw - 2, Mathf.RoundToInt(yR), 2, 3, Palette.Rock);
                int type = Mathf.FloorToInt(SisyphusGame.Hash(k * 7.7f) * 3) % 3;
                DrawBridge(type, gx, gw);
            }
        }

        void DrawBridge(int type, int gx, int gw)
        {
            if (type == 1) // каменный арочный
            {
                float midX = gx + gw / 2f, yL = SlopeY(gx - 3), yR = SlopeY(gx + gw + 3), yM = SlopeY(midX), rise = 7;
                var topA = PixelCanvas.Quadratic(new Vector2(gx - 3, yL + 2), new Vector2(midX, yM - rise + 2), new Vector2(gx + gw + 3, yR + 2));
                var botA = PixelCanvas.Quadratic(new Vector2(gx + gw + 3, yR + 6), new Vector2(midX, yM - rise + 8), new Vector2(gx - 3, yL + 6));
                var poly = new List<Vector2>(); poly.AddRange(topA); poly.AddRange(botA);
                bg.FillPolygon(poly.ToArray(), Palette.Rock);
                bg.StrokePolyline(PixelCanvas.Quadratic(new Vector2(gx - 3, yL), new Vector2(midX, yM - rise), new Vector2(gx + gw + 3, yR)), Palette.StoneHi, 2);
                for (float x = gx; x <= gx + gw; x += 6) { float t = (x - gx) / gw, y = SlopeY(x) - Mathf.Sin(t * Mathf.PI) * rise; bg.Line(x, y + 1, x, y + 6, Palette.StoneCr, 1); }
                bg.FillRect(gx - 4, Mathf.RoundToInt(yL), 3, 8, Palette.StoneCr); bg.FillRect(gx + gw + 1, Mathf.RoundToInt(yR), 3, 8, Palette.StoneCr);
            }
            else if (type == 2) // подвесной
            {
                float sag = 6, yL = SlopeY(gx - 2), yR = SlopeY(gx + gw + 2);
                System.Func<float, float> cableY = (x) => { float t = (x - (gx - 2)) / (gw + 4); return SlopeY(x) + Mathf.Sin(t * Mathf.PI) * sag; };
                bg.FillRect(gx - 3, Mathf.RoundToInt(yL) - 10, 2, 11, Palette.WoodDk); bg.FillRect(gx + gw + 1, Mathf.RoundToInt(yR) - 10, 2, 11, Palette.WoodDk);
                var top = new List<Vector2> { new Vector2(gx - 3, yL - 9) }; for (float x = gx - 2; x <= gx + gw + 2; x += 3) top.Add(new Vector2(x, cableY(x) - 8)); top.Add(new Vector2(gx + gw + 1, yR - 9));
                bg.StrokePolyline(top.ToArray(), Palette.Wood, 1);
                var deck = new List<Vector2>(); for (float x = gx - 2; x <= gx + gw + 2; x += 3) deck.Add(new Vector2(x, cableY(x)));
                bg.StrokePolyline(deck.ToArray(), Palette.Wood, 1);
                for (float x = gx; x <= gx + gw; x += 4) { float y = cableY(x); bg.FillRect(Mathf.RoundToInt(x), Mathf.RoundToInt(y) - 1, 3, 2, Palette.Wood); }
                for (float x = gx + 2; x < gx + gw; x += 8) { float y = cableY(x); bg.Line(x, y - 8, x, y, Palette.WoodDk, 1); }
            }
            else // деревянный дощатый
            {
                bg.Line(gx - 3, SlopeY(gx - 3), gx + gw + 3, SlopeY(gx + gw + 3), Palette.Wood, 3);
                for (float x = gx; x <= gx + gw; x += 4) { float y = SlopeY(x); bg.Line(x, y - 1, x, y + 2, Palette.WoodDk, 1); }
                bg.Line(gx - 2, SlopeY(gx - 2) - 6, gx + gw + 2, SlopeY(gx + gw + 2) - 6, Palette.WoodDk, 1);
                for (float x = gx + 4; x < gx + gw; x += 12) { float y = SlopeY(x); bg.Line(x, y - 6, x, y, Palette.WoodDk, 1); }
                bg.FillRect(gx - 2, Mathf.RoundToInt(SlopeY(gx)) - 6, 2, 7, Palette.WoodDk); bg.FillRect(gx + gw, Mathf.RoundToInt(SlopeY(gx + gw)) - 6, 2, 7, Palette.WoodDk);
            }
        }

        // ---------- Сизиф и валун (процедурный слой) ----------
        void DrawActor()
        {
            ActorLayout(out float sx, out float heroX, out float tumble, out bool showStone);
            int R = BoulderRadius;
            if (showStone)
            {
                float sy = SlopeY(sx) + BoulderOffset * Mathf.Sin(SlopeRotation(sx)) - R + 2;
                bg.FillEllipse(sx, SlopeY(sx) + 1, R, 4, new Color32(0, 0, 0, 255), 0.38f);
                DrawBoulder(sx, sy, R, g.StoneAngle);
            }
            DrawSisyphus(heroX, SlopeY(heroX), SlopeRotation(heroX) + tumble,
                g.PushAnim, g.DefeatStage, g.DefeatPhaseProgress, g.IRain, g.IntroActive);
        }

        void DrawBoulder(float x, float y, float R, float aRot)
        {
            float c = Mathf.Cos(aRot), s = Mathf.Sin(aRot);
            Vector2 T(float lx, float ly) => new Vector2(x + lx * c - ly * s, y + lx * s + ly * c);
            var poly = new Vector2[boulder.Length];
            for (int i = 0; i < boulder.Length; i++) poly[i] = T(boulder[i].x * R, boulder[i].y * R);
            bg.FillPolygon(poly, Palette.StoneMid);
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                bg.Line(a.x, a.y, b.x, b.y, Palette.StoneCr, 1);
            }
            var hi = T(-R * 0.34f, -R * 0.34f);
            bg.FillEllipse(hi.x, hi.y, R * 0.34f, R * 0.20f, Palette.StoneHi, 0.38f);
            var lo = T(R * 0.34f, R * 0.38f);
            bg.FillEllipse(lo.x, lo.y, R * 0.32f, R * 0.18f, Palette.StoneLo, 0.48f);

            // Две асимметричные трещины занимают разные зоны камня и не
            // пересекаются: никаких геометрических «крестов» и «плюсов».
            var crackA = new[]
            {
                T(-R * 0.44f, -R * 0.24f),
                T(-R * 0.27f, -R * 0.17f),
                T(-R * 0.16f, -R * 0.02f),
                T(-R * 0.03f, R * 0.05f)
            };
            var crackB = new[]
            {
                T(R * 0.39f, -R * 0.11f),
                T(R * 0.24f, R * 0.01f),
                T(R * 0.27f, R * 0.18f),
                T(R * 0.11f, R * 0.31f)
            };
            bg.StrokePolyline(crackA, Palette.StoneCr, 1);
            bg.StrokePolyline(crackB, Palette.StoneCr, 1);

            for (int i = 0; i < 7; i++)
            {
                float aa = i * 2.399f + 0.4f;
                float rr = R * (0.35f + SisyphusGame.Hash(i * 4.7f) * 0.28f);
                var p = T(Mathf.Cos(aa) * rr, Mathf.Sin(aa) * rr);
                bg.FillRect(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), 1, 1,
                    i % 3 == 0 ? Palette.StoneHi : Palette.StoneCr);
            }
        }

        void DrawSisyphus(float ox, float oy, float rot, float push, DefeatPhase defeatPhase,
            float defeatProgress, float rain, bool approaching)
        {
            float c = Mathf.Cos(rot), s = Mathf.Sin(rot);
            bool cinematic = defeatPhase != DefeatPhase.None;
            bool grounded = defeatPhase == DefeatPhase.Slip ||
                defeatPhase == DefeatPhase.StoneRoll ||
                defeatPhase == DefeatPhase.Pause;
            bool descending = defeatPhase == DefeatPhase.Descend;
            float facing = descending ? -1f : 1f;
            float headAngle = 0f;
            if (defeatPhase == DefeatPhase.Look)
            {
                if (defeatProgress < 0.35f)
                    headAngle = Mathf.Lerp(0f, -0.28f, Mathf.SmoothStep(0f, 1f, defeatProgress / 0.35f));
                else if (defeatProgress < 0.60f)
                    headAngle = -0.28f;
                else if (defeatProgress < 0.90f)
                    headAngle = Mathf.Lerp(-0.28f, 0.12f, Mathf.SmoothStep(0f, 1f, (defeatProgress - 0.60f) / 0.30f));
                else
                    headAngle = 0.12f;
            }
            float headCos = Mathf.Cos(headAngle);
            float headSin = Mathf.Sin(headAngle);
            void RR(float lx, float ly, float w, float h, Color32 col)
            {
                Vector2 P(float px, float py)
                {
                    px *= facing * HeroScale; py *= HeroScale;
                    return new Vector2(ox + px * c - py * s, oy + px * s + py * c);
                }
                bg.FillPolygon(new[] { P(lx, ly), P(lx + w, ly), P(lx + w, ly + h), P(lx, ly + h) }, col);
            }
            void HR(float lx, float ly, float w, float h, Color32 col)
            {
                Vector2 P(float px, float py)
                {
                    const float pivotX = 2f;
                    const float pivotY = -17f;
                    float dx = px - pivotX;
                    float dy = py - pivotY;
                    float hx = pivotX + dx * headCos - dy * headSin;
                    float hy = pivotY + dx * headSin + dy * headCos;
                    hx *= facing * HeroScale;
                    hy *= HeroScale;
                    return new Vector2(ox + hx * c - hy * s, oy + hx * s + hy * c);
                }
                bg.FillPolygon(new[] { P(lx, ly), P(lx + w, ly), P(lx + w, ly + h), P(lx, ly + h) }, col);
            }
            int bend = cinematic ? 0 : Mathf.RoundToInt(push * 4);
            float strideAmp = 2 * (1 - 0.6f * rain);
            int stride = approaching
                ? Mathf.RoundToInt(Mathf.Sin(g.IntroT * 8f) * 2f)
                : descending
                ? Mathf.RoundToInt(Mathf.Sin(g.DefeatPhaseT * 8f) * 2f)
                : cinematic ? (grounded ? 2 : 0) : Mathf.RoundToInt(Mathf.Sin(g.Scroll * 0.12f) * strideAmp);
            RR(-4 + stride, -6, 2, 6, Palette.Skin); RR(1 - stride, -6, 2, 6, Palette.Skin);
            RR(-4 + bend, -16, 9, 11, Palette.Cloth);
            RR(2 + bend, -16, 3, 11, Palette.ClothSh);
            RR(-5 + bend, -8, 11, 3, Palette.Cloth);
            if (!cinematic && !approaching)
            {
                RR(3 + bend, -15, 9, 2, Palette.Skin);
                RR(3 + bend, -11, 9, 2, Palette.Skin);
            }
            else if (grounded)
            {
                RR(-2, -14, 9, 2, Palette.Skin);
                RR(0, -10, 8, 2, Palette.Skin);
            }
            else
            {
                int armSwing = approaching
                    ? Mathf.RoundToInt(Mathf.Sin(g.IntroT * 8f) * 1.5f)
                    : descending ? Mathf.RoundToInt(Mathf.Sin(g.DefeatPhaseT * 8f) * 1.5f) : 0;
                RR(-5 + armSwing, -15, 2, 8, Palette.Skin);
                RR(5 - armSwing, -15, 2, 8, Palette.Skin);
            }

            HR(2 + bend, -22, 5, 5, Palette.Skin);
            HR(2 + bend, -23, 6, 2, Palette.Hair);
            HR(1 + bend, -21, 2, 3, Palette.Hair);
            HR(6 + bend, -19, 3, 6, Palette.Beard);
            HR(5 + bend, -15, 3, 3, Palette.Beard);
            if (!cinematic && push > 0.4f && rain < 0.5f)
                HR(9 + bend, -24, 1, 2, new Color32(255, 255, 255, 255));
            else if (defeatPhase == DefeatPhase.Look && defeatProgress > 0.18f)
                HR(7, -21, 1, 1, new Color32(214, 208, 191, 255));
        }

        // ---------- Сезонные частицы / погода / тинт ----------
        void DrawAmbient()
        {
            foreach (var p in g.Ambient)
            {
                int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
                if (p.t == "snow") { bg.PlotBlend(x, y, new Color32(255, 255, 255, 255), 0.85f); if (Mathf.Sin(p.sw) > 0.6f) bg.PlotBlend(x + 1, y, new Color32(255, 255, 255, 255), 0.85f); }
                else if (p.t == "petal") { bg.FillRect(x, y, Mathf.Sin(p.sw) > 0 ? 2 : 1, 1, p.col); }
                else if (p.t == "leaf") { bool o = Mathf.Sin(p.sw) > 0; bg.FillRect(x, y, o ? 2 : 1, o ? 1 : 2, p.col); }
                else if (p.t == "pollen") { bg.PlotBlend(x, y, new Color32(244, 228, 150, 255), 0.7f); }
            }
        }

        void DrawWeather()
        {
            foreach (var p in g.Particles)
            {
                if (!p.wind)
                {
                    float length = p.style == 0 ? 3f : p.style == 1 ? 5f : 8f;
                    float slant = p.style == 0 ? 0.5f : p.style == 1 ? 1.2f : 2.3f;
                    float alpha = p.style == 0 ? 0.34f : p.style == 1 ? 0.58f : 0.76f;
                    bg.Line(p.x, p.y, p.x - slant, p.y + length,
                        new Color32(150, 180, 255, 255), 1, alpha);
                }
                else
                {
                    float life = Mathf.Clamp01(p.hasLife ? p.life : 1f);
                    if (p.style == 0)
                        bg.Line(p.x, p.y, p.x + 18f, p.y, new Color32(210, 235, 255, 255), 1, 0.22f * life);
                    else if (p.style == 1)
                    {
                        bg.Line(p.x, p.y, p.x + 10f, p.y, new Color32(220, 238, 255, 255), 1, 0.38f * life);
                        bg.Line(p.x + 3f, p.y + 3f, p.x + 15f, p.y + 3f, new Color32(190, 218, 245, 255), 1, 0.20f * life);
                    }
                    else
                    {
                        bg.Line(p.x, p.y, p.x + 9f, p.y - 2f, new Color32(205, 230, 252, 255), 1, 0.30f * life);
                        bg.Line(p.x + 9f, p.y - 2f, p.x + 20f, p.y + 1f, new Color32(205, 230, 252, 255), 1, 0.25f * life);
                    }
                }
            }
            if (g.IRain > 0.2f)
            {
                float rainShade = g.RainVariant == 0 ? 0.10f : g.RainVariant == 1 ? 0.17f : 0.23f;
                bg.FillRectBlend(0, 0, VW, VH, new Color32(20, 30, 60, 255), rainShade * g.IRain);
            }
        }

        void DrawFog()
        {
            if (g.FogAmount <= 0.004f) return;
            var fog = new Color32(164, 173, 193, 255);
            bg.FillRectBlend(0, Mathf.RoundToInt(VH * 0.08f), VW, Mathf.RoundToInt(VH * 0.80f),
                fog, g.FogAmount * 0.34f);
            for (int i = 0; i < 5; i++)
            {
                float driftX = (g.Clock * (3.2f + i * 0.45f) + i * 89f) % (VW + 120f) - 60f;
                float y = VH * (0.24f + i * 0.105f) + Mathf.Sin(g.Clock * 0.22f + i) * 5f;
                float radiusX = 72f + i * 16f;
                float radiusY = 10f + i * 2f;
                bg.FillEllipse(driftX, y, radiusX, radiusY, fog,
                    g.FogAmount * (0.22f - i * 0.018f));
                bg.FillEllipse(driftX + VW * 0.58f, y + 5f, radiusX * 0.82f, radiusY * 0.85f,
                    fog, g.FogAmount * (0.17f - i * 0.014f));
            }
        }

        void DrawSeasonTint()
        {
            g.SeasonData(out int i, out float t, out int next);
            (float r, float gg, float b, float a)[] tints =
            {
                (120, 200, 120, 0.05f), (255, 208, 120, 0.035f), (205, 120, 45, 0.06f), (200, 222, 255, 0.09f),
            };
            var A = tints[i]; var B = tints[next];
            var col = new Color32((byte)Mathf.Lerp(A.r, B.r, t), (byte)Mathf.Lerp(A.gg, B.gg, t), (byte)Mathf.Lerp(A.b, B.b, t), 255);
            bg.FillRectBlend(0, 0, VW, VH, col, Mathf.Lerp(A.a, B.a, t));
        }
    }
}
