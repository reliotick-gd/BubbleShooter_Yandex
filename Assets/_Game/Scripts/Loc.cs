using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace CozyAnimalTown
{
    /// <summary>
    /// Локализация RU/EN. Язык — из Yandex SDK (environment.i18n.lang, требование 2.14):
    /// ru/be/kk/uk → русский, остальные → английский. Чтение синхронное — index.html
    /// инициализирует SDK до загрузки Unity. В редакторе — по языку системы.
    /// </summary>
    public static class Loc
    {
        static bool _init;
        static bool _ru;
        static string _raw = "en";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern string YG_GetLang();
#endif

        public static bool Ru { get { EnsureInit(); return _ru; } }

        /// <summary>Сырой код языка из SDK ("ru"/"en"/"tr"…) — для аналитики.</summary>
        public static string LangCode { get { EnsureInit(); return _raw; } }

        /// <summary>Выбор строки по языку: en-вариант первым (исходный текст игры).</summary>
        public static string T(string en, string ru) => Ru ? ru : en;

        /// <summary>
        /// Русское числительное: 1 выстрел / 2 выстрела / 5 выстрелов.
        ///
        /// Нужно потому, что все награды подставляются из констант (MidLevelShots и т.п.),
        /// а их крутят под A/B. Захардкоженное «выстрелов» правильно только для 5 и 11-20:
        /// смена константы на 2 молча дала бы «2 выстрелов» на кнопке, которую по п.4.5.1
        /// читает модерация.
        /// </summary>
        public static string Plural(int n, string one, string few, string many)
        {
            int a = Mathf.Abs(n) % 100;
            if (a >= 11 && a <= 14) return many;
            switch (a % 10)
            {
                case 1:              return one;
                case 2: case 3: case 4: return few;
                default:             return many;
            }
        }

        /// <summary>Английское множественное: 1 shot / N shots.</summary>
        public static string PluralEn(int n, string one, string many) => n == 1 ? one : many;

        static void EnsureInit()
        {
            if (_init) return;
            _init = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            try { _raw = YG_GetLang() ?? "en"; } catch { _raw = "en"; }
#else
            _raw = Application.systemLanguage == SystemLanguage.Russian ? "ru" : "en";
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD || PROMO_CAPTURE
            // Промоматериалы снимаются на обоих языках с одной машины, поэтому язык
            // должен задаваться извне: -lang ru|en. В релизном WebGL-билде этого кода нет.
            var argv = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length - 1; i++)
                if (argv[i] == "-lang") { _raw = argv[i + 1]; break; }
#endif
            // ru + be/kk/uk получают русский интерфейс, остальной каталог — английский
            _ru = _raw == "ru" || _raw == "be" || _raw == "kk" || _raw == "uk";
        }
    }
}
