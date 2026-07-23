namespace EndlessSisyphus
{
    /// <summary>
    /// Порт блока CFG из game.js — баланс игры.
    /// Держим как static-поля, чтобы легко твикать (при желании легко превратить в ScriptableObject).
    /// </summary>
    public static class GameConfig
    {
        public const int VirtualH = 240;          // виртуальная высота рендера (пиксели)

        public const float IdealInterval = 0.34f;
        public const float MashInterval = 0.13f;
        public const float TapImpulse = 2.2f;
        public const float HoldPush = 4.0f;
        public const float ShiftBoost = 1.7f;

        public const float Gravity = 3.4f;
        public const float Drag = 1.15f;
        public const float RampHeight = 900f;
        public const float GraceHeight = 10f;

        public const float StaminaMax = 100f;

        public const float DrainWind = 18f;
        public const float DrainIce = 18f;
        public const float DrainSteep = 22f;
        public const float DrainRollback = 8f;
        public const float ErrWindTap = 4f;
        public const float DrainCareful = 12f;     // осторожный режим вне дождя — ошибка

        public const float RainSlipRate = 0.55f;
        public const float RainPushMul = 0.5f;     // дождь: импульс тапа вдвое ниже
        public const float RainHoldMul = 0.5f;     // дождь: удержание вдвое слабее
        public const float SteepDrift = 1.1f;      // крутой склон: постоянное сползание

        public const float CalmMin = 4.5f;
        public const float CalmMax = 8.0f;
        public const float WarnLead = 1.8f;
        public const float ObstacleMin = 3.6f;
        public const float ObstacleMax = 5.6f;

        public const float IceLeadDist = 0.75f;    // доля VW: как далеко впереди появляется лёд
        public const float IceLenMin = 90f;
        public const float IceLenMax = 160f;

        public const float DayLen = 90f;           // длина цикла суток (сек)
        public const float SeasonLen = 70f;        // длина одного сезона (сек)
    }
}
