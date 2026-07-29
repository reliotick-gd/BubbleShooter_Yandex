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

        // На десктопе колонка 9:16 давала полосу в 31% ширины кадра, а остальные 69%
        // заливались кремом впустую. Яндекс это прямо запрещает: п.5.9 разрешает поля,
        // только если они «часть игры, а не заливка при масштабировании», а п.5.1.1.2
        // требует, чтобы кадр качественно показывал механику и графику.
        // Широкая колонка почти квадратная: доска 9×9.2 юнита сама по себе квадратная,
        // вертикаль 9:16 растягивал HUD, а не поле.
        // 0.915 — не «на глаз»: при нём карта поля на 1920×1080 получается 939×968 px,
        // ровно как на утверждённом референсе. Считается так: видимая высота мира
        // = 10.2 / Aspect, содержимое доски = 10.01 юнита, карта на экране
        // = 10.01 / (10.2/Aspect) * 1080. При 0.82 выходило 866 px — доска была мельче.
        public const float WideAspect    = 0.915f;
        public const float WideThreshold = 1.15f;     // с какого аспекта экрана считаем «широким»

        /// <summary>Широкий (десктопный) экран — раскладка landscape.</summary>
        public static bool IsWide =>
            (float)Screen.width / Mathf.Max(1, Screen.height) > WideThreshold;

        /// <summary>Аспект колонки под текущий экран.</summary>
        public static float Aspect => IsWide ? WideAspect : TargetAspect;

        /// <summary>Viewport-прямоугольник (0..1) центрированной колонки.</summary>
        public static Rect Column()
        {
            float sa = (float)Screen.width / Mathf.Max(1, Screen.height);
            float ta = Aspect;
            if (sa > ta)
            {
                float w = ta / sa;                     // pillarbox: уже экрана
                return new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            float h = sa / ta;                         // letterbox: ниже экрана (очень высокие)
            return new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }
    }
}
