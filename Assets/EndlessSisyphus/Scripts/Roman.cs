using System.Text;

namespace EndlessSisyphus
{
    /// <summary>Порт roman() из game.js — целые числа римскими цифрами.</summary>
    public static class Roman
    {
        static readonly int[] Vals = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
        static readonly string[] Syms = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };

        public static string To(float value)
        {
            int n = (int)value;
            if (n <= 0) return "—";
            var sb = new StringBuilder();
            for (int i = 0; i < Vals.Length; i++)
                while (n >= Vals[i]) { sb.Append(Syms[i]); n -= Vals[i]; }
            return sb.ToString();
        }
    }
}
