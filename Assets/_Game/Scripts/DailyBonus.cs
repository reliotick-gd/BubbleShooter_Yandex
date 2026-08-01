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

        // Подарок доступен с 4-го уровня, а бомба открывается только на 7-м. Начислять
        // заряды заблокированному бустеру нельзя по двум причинам: игрок их не видит
        // (HUD держит бустер под замком) и, главное, само появление ключа cat_bomb
        // раньше засчитывалось как «стартовые 10 уже выдавали» — на 7-м уровне человек
        // получал 2 заряда вместо 10. Маркер выдачи теперь отдельный (см. GameManager),
        // но обещать в подарке то, что не дойдёт до игрока, всё равно неправильно.
        public static bool RainbowUnlocked(int level) => level >= GameManager.RainbowUnlockLevel;
        public static bool BombUnlocked(int level)    => level >= GameManager.BombUnlockLevel;

        /// <summary>Начисляет подарок и запоминает день. Повторный вызов за сутки — no-op.</summary>
        public static bool Claim(int level)
        {
            if (!Available) return false;
            PlayerPrefs.SetString(Key, Today);
            if (RainbowUnlocked(level))
                PlayerPrefs.SetInt("cat_rainbow", PlayerPrefs.GetInt("cat_rainbow", 0) + RainbowReward);
            if (BombUnlocked(level))
                PlayerPrefs.SetInt("cat_bomb", PlayerPrefs.GetInt("cat_bomb", 0) + BombReward);
            PlayerPrefs.Save();
            return true;
        }
    }
}
