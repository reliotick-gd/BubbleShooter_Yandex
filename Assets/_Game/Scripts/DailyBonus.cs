using System;
using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Ежедневный подарок: заход раз в сутки — заряды бустеров.
    ///
    /// Дата берётся по UTC, а не по локальной: иначе перевод часов на устройстве
    /// открывает подарок сколько угодно раз. Хранится строкой «ггггММдд» — сравнение
    /// строк тут надёжнее арифметики с датами и переживает смену часового пояса.
    /// </summary>
    public static class DailyBonus
    {
        const string Key = "cat_daily";

        public const int RainbowReward = 2;
        public const int BombReward    = 2;

        static string Today => DateTime.UtcNow.ToString("yyyyMMdd");

        /// <summary>Подарок ещё не забирали сегодня.</summary>
        public static bool Available => PlayerPrefs.GetString(Key, "") != Today;

        /// <summary>Начисляет подарок и запоминает день. Повторный вызов за сутки — no-op.</summary>
        public static bool Claim()
        {
            if (!Available) return false;
            PlayerPrefs.SetString(Key, Today);
            PlayerPrefs.SetInt("cat_rainbow", PlayerPrefs.GetInt("cat_rainbow", 0) + RainbowReward);
            PlayerPrefs.SetInt("cat_bomb",    PlayerPrefs.GetInt("cat_bomb", 0)    + BombReward);
            PlayerPrefs.Save();
            return true;
        }
    }
}
