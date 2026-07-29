namespace CozyAnimalTown
{
    /// <summary>
    /// Тексты rewarded-офферов, RU/EN. Требование Яндекса п.4.5.1: кнопка вызова рекламы
    /// за вознаграждение привязана к тексту, из которого однозначно понятно, что игрок
    /// (а) сейчас посмотрит рекламу и (б) что именно и СКОЛЬКО он за это получит.
    /// Черновик уже отклоняли по этому пункту — бейдж был голым треугольником «▶».
    ///
    /// Количества подставляются из констант GameManager, чтобы обещание в тексте не
    /// разошлось с реальной выдачей в OnRewarded.
    /// </summary>
    public static class AdLoc
    {
        public static string AdWord  => Loc.T("AD", "Реклама");
        public static string WatchAd => Loc.T("Watch ad", "Смотреть рекламу");
        public static string Cancel  => Loc.T("Not now", "Не сейчас");

        // Диалог пополнения бонуса: заголовок — что кончилось, строка награды — что дадут.
        public static string RainbowTitle => Loc.T("Out of rainbows", "Радуги закончились");
        public static string BombTitle    => Loc.T("Out of bombs", "Бомбы закончились");

        public static string RainbowReward =>
            Loc.T($"Watch an ad and get +{GameManager.RefillAmount} rainbow bubbles",
                  $"Посмотри рекламу и получи +{GameManager.RefillAmount} радуги");

        public static string BombReward =>
            Loc.T($"Watch an ad and get +{GameManager.RefillAmount} bombs",
                  $"Посмотри рекламу и получи +{GameManager.RefillAmount} бомбы");

        /// <summary>Короткая подпись на самом бейдже у иконки бонуса: «▶ +3».</summary>
        public static string RefillBadge => UiSymbols.Play + " +" + GameManager.RefillAmount;

        // Кнопки на экране поражения — награду называем прямо в подписи кнопки.
        // Награда за «второй шанс» зависит от причины проигрыша (см. GameManager.OnRewarded),
        // поэтому подписи две: обещать «+5 выстрелов» при переполнении было бы неправдой.
        public static string SecondChanceShotsBtn =>
            Loc.T($"Second chance\n+{GameManager.SecondChanceShots} shots",
                  $"Второй шанс\n+{GameManager.SecondChanceShots} выстрелов");

        public static string SecondChanceClearBtn =>
            Loc.T($"Second chance\nclear {GameManager.OverflowClearRows} bottom rows",
                  $"Второй шанс\nубрать {GameManager.OverflowClearRows} нижних ряда");

        public static string SkipLevelBtn =>
            Loc.T("Skip level\nand move on", "Пропустить уровень\nи идти дальше");
    }
}
