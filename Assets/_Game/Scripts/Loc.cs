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

        static void EnsureInit()
        {
            if (_init) return;
            _init = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            try { _raw = YG_GetLang() ?? "en"; } catch { _raw = "en"; }
#else
            _raw = Application.systemLanguage == SystemLanguage.Russian ? "ru" : "en";
#endif
            // ru + be/kk/uk получают русский интерфейс, остальной каталог — английский
            _ru = _raw == "ru" || _raw == "be" || _raw == "kk" || _raw == "uk";
        }
    }
}
