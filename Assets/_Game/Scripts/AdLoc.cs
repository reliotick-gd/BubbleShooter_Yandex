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

        /// <summary>Слово «выстрел» в нужном числе — количество приходит из константы,
        /// а её крутят под A/B, поэтому склонять надо, а не хардкодить.</summary>
        static string Shots(int n) => Loc.T(
            Loc.PluralEn(n, "shot", "shots"),
            Loc.Plural(n, "выстрел", "выстрела", "выстрелов"));

        /// <summary>Оффер прямо в игре, когда выстрелы на исходе.</summary>
        public static string MidLevelBtn =>
            UiSymbols.Clap + " AD  +" + GameManager.MidLevelShots + " " + Shots(GameManager.MidLevelShots);

        // Экран победы и ежедневный подарок.
        public static string ScoreWord  => Loc.T("Score", "Счёт");
        public static string RecordWord => Loc.T("New best!", "Новый рекорд!");

        public static string DailyTitle  => Loc.T("Daily gift", "Ежедневный подарок");
        public static string DailyReward
        {
            get
            {
                int r = DailyBonus.RainbowReward, b = DailyBonus.BombReward;
                string rw = Loc.T(Loc.PluralEn(r, "rainbow", "rainbows"),
                                  Loc.Plural(r, "радуга", "радуги", "радуг"));
                string bw = Loc.T(Loc.PluralEn(b, "bomb", "bombs"),
                                  Loc.Plural(b, "бомба", "бомбы", "бомб"));
                return Loc.T($"+{r} {rw} and +{b} {bw}", $"+{r} {rw} и +{b} {bw}");
            }
        }
        public static string DailyTake => Loc.T("Take it!", "Забрать!");

        /// <summary>
        /// Подсказка в боковой панели. Формулировка про «усложнение», а не про «нового
        /// зверя»: новый цвет — это не награда, а рост сложности (больше цветов на доске
        /// = труднее собирать тройки), и игрок должен понимать это заранее.
        /// </summary>
        public static string NextAnimal(int levels) =>
            Loc.T($"Harder in {levels} lvl", $"Усложнение через {levels} ур.");

        public static string AllAnimals => Loc.T("Max difficulty", "Максимальная сложность");

        // Кнопки на экране поражения — награду называем прямо в подписи кнопки.
        // Награда за «второй шанс» зависит от причины проигрыша (см. GameManager.OnRewarded),
        // поэтому подписи две: обещать «+5 выстрелов» при переполнении было бы неправдой.
        public static string SecondChanceShotsBtn
        {
            get
            {
                int n = GameManager.SecondChanceShots;
                return Loc.T($"Second chance\n+{n} {Shots(n)}", $"Второй шанс\n+{n} {Shots(n)}");
            }
        }

        public static string SecondChanceClearBtn
        {
            get
            {
                int n = GameManager.OverflowClearRows;
                string rows = Loc.T(Loc.PluralEn(n, "bottom row", "bottom rows"),
                                    Loc.Plural(n, "нижний ряд", "нижних ряда", "нижних рядов"));
                return Loc.T($"Second chance\nclear {n} {rows}", $"Второй шанс\nубрать {n} {rows}");
            }
        }

        public static string SkipLevelBtn =>
            Loc.T("Skip level\nand move on", "Пропустить уровень\nи идти дальше");
    }
}
