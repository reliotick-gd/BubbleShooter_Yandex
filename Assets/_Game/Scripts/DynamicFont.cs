using UnityEngine;
using TMPro;

namespace CozyAnimalTown
{
    /// <summary>
    /// Дефолтный LiberationSans SDF в этом проекте содержит только кириллицу — латиница/
    /// цифры/пунктуация рендерятся как □. Вместо правки атласа создаём при старте Dynamic-
    /// шрифт из TTF (в Resources, значит грузится и в WebGL) и назначаем на лейблы напрямую —
    /// TMP Settings не трогаем. Символы ★☆♥✕✓▶≡→⚡ идут спрайтами (UiSymbols).
    /// </summary>
    public static class DynamicFont
    {
        public static TMP_FontAsset Asset { get; private set; }
        static bool _tried;

        public static void Ensure()
        {
            if (_tried) return;
            _tried = true;

            var ttf = Resources.Load<Font>("CozyFont");
            if (ttf == null)
            {
                Debug.LogWarning("[DynamicFont] Resources/CozyFont.ttf не найден — останется дефолтный TMP-шрифт.");
                return;
            }

            // Дефолты CreateFontAsset(ttf): 90pt SDFAA, атлас 1024², Dynamic multi-atlas.
            var dyn = TMP_FontAsset.CreateFontAsset(ttf);
            if (dyn == null)
            {
                Debug.LogWarning("[DynamicFont] CreateFontAsset не смог (проверь 'Include Font Data' у TTF).");
                return;
            }

            dyn.name = "Cozy Dynamic";
            Asset = dyn;
        }

        public static void Apply(TMP_Text t)
        {
            if (Asset != null && t != null) t.font = Asset;
        }
    }
}
