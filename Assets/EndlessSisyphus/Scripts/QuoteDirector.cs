using System.Collections.Generic;
using UnityEngine;

namespace EndlessSisyphus
{
    /// <summary>
    /// Управляет игровыми цитатами: выдерживает паузу между ними, привязывает
    /// часть фраз к событиям и постепенно проводит игрока через остальной набор.
    /// Стартовая цитата и цитата поражения рисуются GameUI отдельно.
    /// </summary>
    public sealed class QuoteDirector : MonoBehaviour
    {
        public const float GapBetweenQuotes = 21f;
        const float FadeIn = 0.8f;
        const float FadeOut = 1.2f;

        struct QuoteCue
        {
            public readonly string Text;
            public readonly float Duration;

            public QuoteCue(string text, float duration)
            {
                Text = text;
                Duration = duration;
            }
        }

        // Цитата экрана поражения намеренно отсутствует в этом списке.
        static readonly QuoteCue[] Cues =
        {
            new QuoteCue("Если этот миф трагичен, то лишь потому, что его герой наделён сознанием", 10f),
            new QuoteCue("Каждая крупица этого камня, каждый отблеск минерала на этой горе, окутанной ночью, составляет для него целый мир", 15f),
            new QuoteCue("Самой борьбы к вершине достаточно, чтобы наполнить сердце человека. Сизифа нужно представлять себе счастливым", 15f),
            new QuoteCue("Абсурд говорит «да», и его усилие отныне не знает остановки", 10f),
            new QuoteCue("На вершине, куда он стремится, он достигает исполнения своего удела. И Сизиф видит, как камень в несколько мгновений скатывается к подножию, откуда его снова нужно поднимать к вершине", 20f),
            new QuoteCue("Камень — его достояние. Равно как и абсурдная вселенная, в которой он живёт", 15f),
            new QuoteCue("Нет солнца без тени, и необходимо познать ночь", 10f),
            new QuoteCue("Сизиф твёрже своего камня", 10f),
            new QuoteCue("Нет судьбы, которую не превозмогло бы презрение", 10f),
            new QuoteCue("Одного презрения к собственной участи оказывается достаточно, чтобы превратить её в победу, — и это, быть может, самое большое чудо, на какое способен человек", 20f),
            new QuoteCue("Сизиф — абсурдный герой. Он абсурден настолько же благодаря своим страстям, насколько и благодаря своим мучениям", 15f),
            new QuoteCue("Для человека без шор нет зрелища прекраснее, чем борьба интеллекта с превосходящей его реальностью", 15f),
            new QuoteCue("Нет кары более ужасной, чем бесполезный и безнадёжный труд", 10f),
            new QuoteCue("Разве была бы его кара столь ужасной, если бы на каждом шагу его поддерживала надежда на успех?", 15f),
            new QuoteCue("Свою ношу всегда находишь вновь", 10f),
        };

        // Фразы, не закреплённые за одним событием, идут по кругу между забегами.
        static readonly int[] AmbientOrder = { 3, 4, 6, 9, 10, 11, 12, 13 };

        public SisyphusGame game;

        readonly Queue<int> pending = new Queue<int>();
        readonly HashSet<int> queuedOrShown = new HashSet<int>();
        // Сохраняется между забегами в рамках сессии: уже показанная фраза
        // больше не возвращается ни как событийная, ни как фоновая.
        readonly HashSet<int> shownInSession = new HashSet<int>();

        int observedRunId;
        int ambientCursor;
        int currentCue = -1;
        int runBest;
        float currentTime;
        float cooldown;
        bool firstEffort;
        bool heightOneQueued;
        bool heightTwoQueued;
        bool firstObstacleQueued;
        bool steepQueued;
        bool recordQueued;
        ObKind previousObstacle;

        public bool IsVisible => currentCue >= 0 && Opacity > 0.001f;
        public string CurrentText => currentCue >= 0 ? Cues[currentCue].Text : string.Empty;

        public float Opacity
        {
            get
            {
                if (currentCue < 0) return 0f;
                float duration = Cues[currentCue].Duration;
                float fadeIn = Mathf.Clamp01(currentTime / FadeIn);
                float fadeOut = Mathf.Clamp01((duration - currentTime) / FadeOut);
                return Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut));
            }
        }

        void Awake()
        {
            if (game == null) game = GetComponent<SisyphusGame>();
            if (game != null) observedRunId = game.RunId;
        }

        void LateUpdate()
        {
            if (game == null) return;

            if (observedRunId != game.RunId)
            {
                observedRunId = game.RunId;
                BeginRun();
            }

            if (game.State != GState.Playing)
            {
                HideImmediately();
                previousObstacle = game.Obstacle;
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            ObserveEvents();

            if (currentCue >= 0)
            {
                currentTime += dt;
                if (currentTime >= Cues[currentCue].Duration)
                {
                    currentCue = -1;
                    currentTime = 0f;
                    cooldown = GapBetweenQuotes;
                }
                return;
            }

            if (cooldown > 0f)
            {
                cooldown -= dt;
                return;
            }

            if (!firstEffort || !IsCalmMoment()) return;

            if (pending.Count > 0)
                Show(pending.Dequeue());
            else
            {
                int ambient = NextAmbientCue();
                if (ambient >= 0) Show(ambient);
            }
        }

        void BeginRun()
        {
            pending.Clear();
            queuedOrShown.Clear();
            currentCue = -1;
            currentTime = 0f;
            cooldown = 0f;
            firstEffort = false;
            heightOneQueued = false;
            heightTwoQueued = false;
            firstObstacleQueued = false;
            steepQueued = false;
            recordQueued = false;
            previousObstacle = ObKind.None;
            runBest = game.Best;

            // После поражения новый подъём начинается с осознания повторения.
            if (game.RunId > 1) Enqueue(14);
        }

        void ObserveEvents()
        {
            if (!firstEffort && (game.PushAnim > 0.01f || game.SpaceDown || game.Height > 0.5f))
            {
                firstEffort = true;
                if (game.RunId == 1) Enqueue(0);
            }

            if (!heightOneQueued && game.Height >= 50f)
            {
                heightOneQueued = true;
                Enqueue(1);
            }

            if (!heightTwoQueued && game.Height >= 150f)
            {
                heightTwoQueued = true;
                Enqueue(5);
            }

            if (previousObstacle != ObKind.None && game.Obstacle == ObKind.None)
            {
                if (!firstObstacleQueued)
                {
                    firstObstacleQueued = true;
                    Enqueue(7);
                }

                if (!steepQueued && previousObstacle == ObKind.Steep)
                {
                    steepQueued = true;
                    Enqueue(8);
                }
            }

            if (!recordQueued && runBest > 0 && game.Height > runBest)
            {
                recordQueued = true;
                Enqueue(2);
            }

            previousObstacle = game.Obstacle;
        }

        bool IsCalmMoment()
        {
            return game.Obstacle == ObKind.None &&
                   game.Phase == ObPhase.Calm &&
                   game.Momentum > -0.35f;
        }

        void Enqueue(int cue)
        {
            if (shownInSession.Contains(cue)) return;
            if (queuedOrShown.Add(cue)) pending.Enqueue(cue);
        }

        int NextAmbientCue()
        {
            for (int i = 0; i < AmbientOrder.Length; i++)
            {
                int cue = AmbientOrder[ambientCursor];
                ambientCursor = (ambientCursor + 1) % AmbientOrder.Length;
                if (shownInSession.Contains(cue)) continue;
                if (queuedOrShown.Add(cue)) return cue;
            }
            return -1;
        }

        void Show(int cue)
        {
            if (!shownInSession.Add(cue)) return;
            currentCue = cue;
            currentTime = 0f;
        }

        void HideImmediately()
        {
            currentCue = -1;
            currentTime = 0f;
        }
    }
}
