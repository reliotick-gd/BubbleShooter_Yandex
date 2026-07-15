using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Игра всегда «вертикальная» 9:16. Этот помощник считает центрированный
    /// прямоугольник-колонку 9:16 внутри реального экрана (viewport 0..1). Камера
    /// рендерит доску в эту колонку (pillarbox на десктопе), а поля заливаются
    /// небом (см. CameraFitter фоновой камерой). Один источник правды, чтобы
    /// колонка камеры и фон совпадали пиксель-в-пиксель.
    /// </summary>
    public static class ScreenColumn
    {
        public const float TargetAspect = 9f / 16f;   // ширина/высота портретной колонки

        /// <summary>Viewport-прямоугольник (0..1) центрированной колонки 9:16.</summary>
        public static Rect Column()
        {
            float sa = (float)Screen.width / Mathf.Max(1, Screen.height);
            if (sa > TargetAspect)
            {
                float w = TargetAspect / sa;            // pillarbox: уже экрана
                return new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            float h = sa / TargetAspect;                // letterbox: ниже экрана (очень высокие)
            return new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }
    }
}
