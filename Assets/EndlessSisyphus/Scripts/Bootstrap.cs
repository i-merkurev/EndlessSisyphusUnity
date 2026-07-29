using UnityEngine;

namespace EndlessSisyphus
{
    /// <summary>
    /// Автозапуск без настройки сцены: после загрузки сцены создаёт камеру (если нет),
    /// объект игры (SisyphusGame) и UI (GameUI). Импортировал .unitypackage → нажал Play.
    /// Если хочешь ручную настройку — удали этот файл и повесь SisyphusGame + GameUI на объект сам.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindAnyObjectByType<SisyphusGame>() != null) return;

            if (Camera.main == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            var go = new GameObject("EndlessSisyphus");
            var game = go.AddComponent<SisyphusGame>();   // Awake создаёт WorldRenderer/AudioEngine и настраивает камеру
            var quotes = go.AddComponent<QuoteDirector>();
            quotes.game = game;
            var ui = go.AddComponent<GameUI>();
            ui.game = game;
            ui.quotes = quotes;
        }
    }
}
