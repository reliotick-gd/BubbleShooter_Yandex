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
        /// Подпись на бейдже у иконки бонуса. Держим в ней ОБА факта, которых требует
        /// п.4.5.1: что сейчас будет ролик (треугольник «play» + слово «Реклама») и сколько
        /// дадут за него («+3»). Раньше здесь был голый треугольник — за это черновик и сняли.
        /// </summary>
        public static string RefillBadge =>
            UiSymbols.Play + " " + AdWord + " +" + GameManager.RefillAmount;

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
