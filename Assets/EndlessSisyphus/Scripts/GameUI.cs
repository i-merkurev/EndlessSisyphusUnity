using UnityEngine;

namespace EndlessSisyphus
{
    /// <summary>
    /// Экраны и HUD на IMGUI (OnGUI) — без зависимости от uGUI/TMP и без настройки сцены.
    /// Порт HTML-оверлеев: старт «Как преодолевать препятствия», HUD, настройки, Game Over.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public SisyphusGame game;
        Texture2D white;
        GUIStyle title, h2, body, hint, btn, banner;

        void Awake()
        {
            white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply();
        }

        void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.8f, 0.2f) } };
            h2 = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.84f, 0.29f, 0.23f) } };
            body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, normal = { textColor = new Color(0.96f, 0.91f, 0.82f) } };
            hint = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.6f, 0.55f, 0.42f) } };
            btn = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            banner = new GUIStyle(GUI.skin.box) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        void Rect2(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }

        void OnGUI()
        {
            if (game == null) return;
            EnsureStyles();
            switch (game.State)
            {
                case GState.Start: DrawStart(); break;
                case GState.Settings: DrawSettings(); break;
                case GState.Playing:
                case GState.Falling: DrawHUD(); break;
                case GState.Over: DrawOver(); break;
            }
        }

        void DrawHUD()
        {
            float w = Screen.width, h = Screen.height;
            // шкала сил
            float bx = 20, by = 20, bw = Mathf.Min(w * 0.32f, 340), bh = 20;
            GUI.Label(new Rect(bx, by - 16, 200, 16), "СИЛЫ", hint);
            Rect2(new Rect(bx, by, bw, bh), new Color(0.05f, 0.04f, 0.09f));
            float pct = game.Stamina / GameConfig.StaminaMax;
            Color fill = pct > 0.5f ? new Color(0.34f, 0.79f, 0.23f) : pct > 0.22f ? new Color(1f, 0.8f, 0.2f) : new Color(0.84f, 0.29f, 0.23f);
            Rect2(new Rect(bx + 3, by + 3, (bw - 6) * pct, bh - 6), fill);

            // высота / рекорд
            var right = new GUIStyle(body) { alignment = TextAnchor.UpperRight, fontSize = 20 };
            GUI.Label(new Rect(w - 320, 16, 300, 30), "ВЫСОТА  " + Roman.To(game.Height), right);
            var right2 = new GUIStyle(hint) { alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(w - 320, 48, 300, 20), "РЕКОРД  " + Roman.To(game.Best), right2);

            // баннер препятствия
            string bt = BannerText();
            if (bt != null) GUI.Label(new Rect(w / 2 - 200, 84, 400, 34), bt, banner);

            // индикатор осторожного режима
            if (game.Careful)
            {
                var cs = new GUIStyle(banner) { normal = { textColor = game.CarefulBad ? new Color(0.84f, 0.29f, 0.23f) : new Color(0.49f, 0.94f, 0.82f) } };
                string ct = game.CarefulBad ? "ОТКЛЮЧИ CTRL — НЕТ ДОЖДЯ" : "ОСТОРОЖНО (Ctrl)";
                GUI.Label(new Rect(w / 2 - 200, h - 60, 400, 30), ct, cs);
            }
        }

        string BannerText()
        {
            switch (game.Obstacle)
            {
                case ObKind.Wind: return "ВЕТЕР — НЕ ЖМИ КЛАВИШИ";
                case ObKind.Rain: return "ДОЖДЬ — CTRL + SPACE";
                case ObKind.Ice: return "ЛЁД — ДЕРЖИ SPACE";
                case ObKind.Steep: return "КРУТОЙ СКЛОН — SHIFT + SPACE";
                default: return null;
            }
        }

        Rect Center(float cw, float ch) => new Rect(Screen.width / 2 - cw / 2, Screen.height / 2 - ch / 2, cw, ch);

        void DrawStart()
        {
            Rect2(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.03f, 0.07f, 0.82f));
            var r = Center(620, 470);
            GUILayout.BeginArea(r);
            GUILayout.Label("БЕСКОНЕЧНЫЙ СИЗИФ", title);
            GUILayout.Space(6);
            GUILayout.Label("Толкай камень вверх по бесконечному склону — так высоко, как хватит сил.", hint);
            GUILayout.Space(10);
            GUILayout.Label("Как преодолевать препятствия", new GUIStyle(body) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.8f, 0.2f) } });
            GUILayout.Label(
                "• Обычный склон — ритмично нажимайте Space.\n" +
                "• Крутой склон — удерживайте Shift и нажимайте Space.\n" +
                "• Встречный ветер — не нажимайте клавиш, дождитесь конца порыва.\n" +
                "• Дождь — удерживайте Ctrl и продолжайте Space; после дождя отпустите Ctrl.\n" +
                "• Лёд — удерживайте Space, постоянное усилие.", body);
            GUILayout.Space(6);
            GUILayout.Label("Неправильные действия расходуют силы Сизифа. Когда силы заканчиваются — попытка завершается.", new GUIStyle(hint) { normal = { textColor = new Color(0.88f, 0.48f, 0.17f) } });
            GUILayout.Space(12);
            if (GUILayout.Button("SPACE — НАЧАТЬ", btn, GUILayout.Height(44))) game.StartGame();
            GUILayout.Space(6);
            if (GUILayout.Button("НАСТРОЙКИ СЛОЖНОСТИ", btn, GUILayout.Height(34))) game.OpenSettings();
            GUILayout.Label("M звук   Esc меню   R заново", hint);
            GUILayout.EndArea();
        }

        void DrawSettings()
        {
            Rect2(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.03f, 0.07f, 0.9f));
            var s = game.Set;
            var r = Center(560, 430);
            GUILayout.BeginArea(r);
            GUILayout.Label("НАСТРОЙКИ СЛОЖНОСТИ", h2);
            GUILayout.Space(14);
            s.DrainMul = Slider("Расход сил", s.DrainMul, 0.5f, 2f, "×" + s.DrainMul.ToString("0.0"));
            s.FreqMul = Slider("Частота явлений", s.FreqMul, 0.5f, 2f, "×" + s.FreqMul.ToString("0.0"));
            s.WindDurMul = Slider("Длительность ветра", s.WindDurMul, 0.5f, 2f, "×" + s.WindDurMul.ToString("0.0"));
            s.RainProb = Slider("Вероятность дождя", s.RainProb, 0f, 3f, RainLabel(s.RainProb));
            s.SteepMul = Slider("Крутизна склонов", s.SteepMul, 0.5f, 2f, "×" + s.SteepMul.ToString("0.0"));
            GUILayout.Space(12);
            if (GUILayout.Button("НАЗАД", btn, GUILayout.Height(38))) { s.Save(); game.CloseSettings(); }
            if (GUILayout.Button("СБРОС", btn, GUILayout.Height(30))) { s.Reset(); s.Save(); }
            GUILayout.EndArea();
        }

        float Slider(string name, float val, float lo, float hi, string valText)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, body, GUILayout.Width(220));
            GUILayout.Label(valText, new GUIStyle(body) { alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(1f, 0.8f, 0.2f) } }, GUILayout.Width(90));
            GUILayout.EndHorizontal();
            return GUILayout.HorizontalSlider(val, lo, hi);
        }

        static string RainLabel(float v) => v == 0 ? "выкл" : v < 0.75f ? "редко" : v <= 1.25f ? "обычная" : v <= 2f ? "часто" : "очень часто";

        void DrawOver()
        {
            Rect2(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.03f, 0.07f, 0.82f));
            var r = Center(560, 320);
            GUILayout.BeginArea(r);
            GUILayout.Label(game.FallReason == "slip" ? "СИЗИФ ПОСКОЛЬЗНУЛСЯ" : "СИЛЫ ИССЯКЛИ", h2);
            GUILayout.Space(8);
            GUILayout.Label(game.FallReason == "slip" ? "Камень вырвался — и покатился вниз." : "Руки опустились — камень скатился к подножию.", hint);
            GUILayout.Space(14);
            GUILayout.Label("ВЫСОТА  " + Roman.To(game.Height), new GUIStyle(body) { fontSize = 26, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.8f, 0.2f) } });
            GUILayout.Label("РЕКОРД  " + Roman.To(game.Best), hint);
            if (game.IsRecordScreen) GUILayout.Label("★ НОВЫЙ РЕКОРД ★", new GUIStyle(hint) { normal = { textColor = new Color(1f, 0.8f, 0.2f) } });
            GUILayout.Space(14);
            if (GUILayout.Button("SPACE — ЗАНОВО", btn, GUILayout.Height(44))) game.StartGame();
            GUILayout.Space(6);
            if (GUILayout.Button("В МЕНЮ", btn, GUILayout.Height(34))) game.GoMenu();
            GUILayout.EndArea();
        }
    }
}
