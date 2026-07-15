using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Дизайн-рут фиксированного макета 1080×1920 (9:16). Держит его по центру экрана
    /// и масштабирует так, чтобы он ровно вписался в колонку 9:16 (ScreenColumn) —
    /// совпадая с вьюпортом камеры. Всё вне колонки добивается кремом (фон-камера).
    /// Один и тот же макет на любом экране: узкий/широкий — просто поля по краям.
    /// </summary>
    public class ColumnFitter : MonoBehaviour
    {
        public const float DesignW = 1080f, DesignH = 1920f;
        RectTransform rt;

        void Awake() { rt = (RectTransform)transform; Apply(); }
        void LateUpdate() => Apply();

        void Apply()
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(DesignW, DesignH);

            Rect c = ScreenColumn.Column();
            float colWpx = Screen.width * c.width, colHpx = Screen.height * c.height;
            float s = Mathf.Min(colWpx / DesignW, colHpx / DesignH);
            rt.localScale = new Vector3(s, s, 1f);
            rt.anchoredPosition = new Vector2(
                (c.center.x - 0.5f) * Screen.width,
                (c.center.y - 0.5f) * Screen.height);
        }
    }
}
