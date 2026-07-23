using UnityEngine;

namespace EndlessSisyphus
{
    /// <summary>
    /// Порт объекта SET из game.js — множители сложности из UI.
    /// Сохраняются в PlayerPrefs (аналог localStorage 'sisyphus_set').
    /// </summary>
    [System.Serializable]
    public class DifficultySettings
    {
        public float DrainMul = 1f;
        public float FreqMul = 1f;
        public float WindDurMul = 1f;
        public float RainProb = 1f;
        public float SteepMul = 1f;

        const string Key = "sisyphus_set";

        public static DifficultySettings Load()
        {
            var s = new DifficultySettings();
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { JsonUtility.FromJsonOverwrite(json, s); } catch { }
            }
            return s;
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public void Reset()
        {
            DrainMul = FreqMul = WindDurMul = RainProb = SteepMul = 1f;
        }
    }
}
