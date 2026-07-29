using UnityEngine;

namespace EndlessSisyphus
{
    /// <summary>Порт блока PAL из game.js — палитра пиксель-арта.</summary>
    public static class Palette
    {
        static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        public static readonly Color32 SkyDayTop = C(64, 100, 156);
        public static readonly Color32 SkyDayBot = C(158, 196, 226);
        public static readonly Color32 SkyNightTop = C(10, 9, 32);
        public static readonly Color32 SkyNightBot = C(38, 26, 72);

        public static readonly Color32 MountFar = C(52, 46, 86);
        public static readonly Color32 MountMid = C(40, 34, 66);
        public static readonly Color32 MountNear = C(30, 26, 52);

        public static readonly Color32 Volcano = C(58, 40, 52);
        public static readonly Color32 Lava = C(255, 120, 40);
        public static readonly Color32 Smoke = C(90, 84, 96);

        public static readonly Color32 Dirt1 = C(112, 82, 74);
        public static readonly Color32 Dirt2 = C(82, 61, 62);
        public static readonly Color32 Dirt3 = C(61, 45, 52);
        public static readonly Color32 Rock = C(92, 88, 90);
        public static readonly Color32 Grass = C(96, 140, 60);

        public static readonly Color32 Ice1 = C(196, 232, 244);
        public static readonly Color32 Ice2 = C(140, 196, 220);
        public static readonly Color32 IceEdge = C(235, 250, 255);

        public static readonly Color32 Skin = C(214, 168, 120);
        public static readonly Color32 Cloth = C(222, 208, 176);
        public static readonly Color32 ClothSh = C(180, 162, 128);
        public static readonly Color32 Beard = C(232, 228, 214);
        public static readonly Color32 Hair = C(212, 206, 190);

        public static readonly Color32 StoneLo = C(92, 84, 76);
        public static readonly Color32 StoneMid = C(124, 116, 106);
        public static readonly Color32 StoneHi = C(158, 150, 138);
        public static readonly Color32 StoneCr = C(64, 58, 52);

        public static readonly Color32 Sun = C(255, 214, 96);
        public static readonly Color32 Moon = C(222, 224, 240);
        public static readonly Color32 Wood = C(105, 73, 62);
        public static readonly Color32 WoodDk = C(70, 50, 48);
        public static readonly Color32 Chasm = C(10, 8, 16);

        public static readonly Color32 DeadTree = C(66, 53, 52);
        public static readonly Color32 Shrub = C(78, 68, 52);
        public static readonly Color32 Goat = C(86, 74, 66);
        public static readonly Color32 GoatDk = C(54, 46, 40);
        public static readonly Color32 Horn = C(188, 176, 150);
        public static readonly Color32 Snake = C(96, 128, 58);
        public static readonly Color32 SnakeDk = C(64, 92, 40);
        public static readonly Color32 Raven = C(16, 14, 24);
        public static readonly Color32 Lizard = C(122, 150, 60);
        public static readonly Color32 Flutter = C(230, 184, 77);

        /// <summary>Линейное смешение двух цветов (порт mix()).</summary>
        public static Color32 Mix(Color32 a, Color32 b, float t)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.r, b.r, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.g, b.g, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.b, b.b, t)),
                255);
        }
    }
}
