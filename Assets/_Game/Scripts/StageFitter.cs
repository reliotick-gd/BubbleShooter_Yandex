using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Полноэкранный дизайн-рут в ТЕХ ЖЕ единицах и с тем же масштабом, что и колонка
    /// (ColumnFitter), но шириной во весь экран. Нужен на десктопе: колонка отдана доске,
    /// а HUD переезжает в поля по бокам — дотянуться до них можно только отсюда.
    /// В портрете сцена совпадает с колонкой, поэтому мобильная вёрстка не меняется.
    ///
    /// Координаты общие с колонкой: элемент на x = 0 стоит в центре в обоих рутах.
    /// </summary>
    public class StageFitter : MonoBehaviour
    {
        RectTransform rt;

        /// <summary>Половина ширины сцены в дизайн-единицах (в портрете = 540).</summary>
        public static float HalfWidth
        {
            get
            {
                float designH = ColumnFitter.DesignHeight;
                Rect c = ScreenColumn.Column();
                float s = Mathf.Min(Screen.width * c.width / ColumnFitter.DesignW,
                                    Screen.height * c.height / designH);
                return s > 0f ? Screen.width / s * 0.5f : ColumnFitter.DesignW * 0.5f;
            }
        }

        void Awake() { rt = (RectTransform)transform; Apply(); }
        void LateUpdate() => Apply();

        void Apply()
        {
            float designH = ColumnFitter.DesignHeight;
            Rect c = ScreenColumn.Column();
            float s = Mathf.Min(Screen.width * c.width / ColumnFitter.DesignW,
                                Screen.height * c.height / designH);
            if (s <= 0f) return;

            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Screen.width / s, Screen.height / s);
            rt.localScale = new Vector3(s, s, 1f);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
