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
        public static string AdWord => Loc.T("AD", "Реклама");

        /// <summary>
        /// Подпись на бейдже у иконки бонуса: «🎬 AD +3». Держит оба факта, которых требует
        /// п.4.5.1 — что сейчас будет ролик (хлопушка + маркер AD) и сколько за него дадут.
        /// Раньше здесь был голый треугольник, за это черновик и сняли.
        ///
        /// «AD» намеренно не переводится: маркер короткий и узнаваемый, а «Реклама +3» на
        /// кнопке читается как объявление, а не как награда. Если модерация придерётся по
        /// п.8.2.3 («предупреждения о рекламе переводятся») — заменить на AdWord здесь.
        /// </summary>
        public static string RefillBadge =>
            UiSymbols.Clap + " AD +" + GameManager.RefillAmount;

        /// <summary>Оффер прямо в игре, когда выстрелы на исходе.</summary>
        public static string MidLevelBtn =>
            UiSymbols.Clap + " AD  " + Loc.T($"+{GameManager.MidLevelShots} shots",
                                             $"+{GameManager.MidLevelShots} выстрелов");

        // Экран победы и ежедневный подарок.
        public static string ScoreWord  => Loc.T("Score", "Счёт");
        public static string RecordWord => Loc.T("New best!", "Новый рекорд!");

        public static string DailyTitle  => Loc.T("Daily gift", "Ежедневный подарок");
        public static string DailyReward =>
            Loc.T($"+{DailyBonus.RainbowReward} rainbows and +{DailyBonus.BombReward} bombs",
                  $"+{DailyBonus.RainbowReward} радуги и +{DailyBonus.BombReward} бомбы");
        public static string DailyTake => Loc.T("Take it!", "Забрать!");

        /// <summary>«До нового зверя — N уровней» в боковой панели.</summary>
        public static string NextAnimal(int levels) =>
            Loc.T($"New animal in {levels} lvl", $"Новый зверь через {levels} ур.");

        public static string AllAnimals => Loc.T("All animals unlocked", "Все зверята открыты");

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
