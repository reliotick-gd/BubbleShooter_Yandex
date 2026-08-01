using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Держит доску в центрированной колонке 9:16 (игра всегда вертикальная).
    /// На широких экранах (десктоп) по бокам — поля, залитые небом отдельной
    /// фоновой камерой. Внутри колонки сверху/снизу зарезервированы полосы HUD.
    /// Поддерживает camera shake.
    /// </summary>
    public class CameraFitter : MonoBehaviour
    {
        Camera cam;
        GameConfig cfg;

        /// <summary>
        /// Сдвиг камеры вверх → поле визуально ниже. В портрете он резервирует полосу под
        /// HUD над доской. На десктопе HUD уехал в боковые панели, резервировать нечего —
        /// иначе доска липнет к низу экрана, а сверху зияет пустая полоса.
        /// 0.05 — центр содержимого доски (верх ряда 5.06, низ пушки −4.95).
        /// </summary>
        public static float BoardYOffset => ScreenColumn.IsWide ? 0.05f : 1.0f;

        /// <summary>Мировая точка → координаты дизайн-макета (центр = 0,0; ±540×±960),
        /// т.к. камера рендерит поле ровно в колонку 9:16 (см. ColumnFitter/LateUpdate).</summary>
        public static Vector2 WorldToDesign(Vector3 world, GameConfig cfg)
        {
            float halfW = cfg.boardWidth * 0.5f + 0.6f;
            float ortho = halfW / ScreenColumn.Aspect;
            return new Vector2(world.x / halfW * (ColumnFitter.DesignW * 0.5f),
                               (world.y - BoardYOffset) / ortho * (ColumnFitter.DesignHeight * 0.5f));
        }

        float _shakeIntensity, _shakeDuration, _shakeElapsed;

        public static CameraFitter Instance { get; private set; }

        public void Init(GameConfig cfg)
        {
            Instance = this;
            this.cfg = cfg;
            cam = GetComponent<Camera>();

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cfg.bgColor;

            // Отдельной фоновой камеры больше нет. Она заливала поля при letterbox, но
            // основная камера теперь всегда полноэкранная (см. LateUpdate), а её
            // clearFlags = SolidColor и так закрывает кадр. В URP каждая базовая камера —
            // это отдельный проход с culling, setup рендер-таргета и clear; держать такой
            // проход ради полностью перекрытой заливки незачем.
        }

        public void TriggerShake(float intensity = 0.18f, float duration = 0.25f)
        {
            _shakeIntensity = intensity;
            _shakeDuration  = duration;
            _shakeElapsed   = 0f;
        }

        void LateUpdate()
        {
            if (cam == null || cfg == null) return;

            // Тот же центрированный 9:16-прямоугольник, что и UI (ScreenColumn). Орто-размер
            // фиксирован по ширине колонки — поле центрируется по вертикали, место под HUD остаётся.
            // Орто-размер задаёт ШИРИНА колонки — доска обязана влезать по горизонтали
            // целиком (11 ячеек + поля на рикошет).
            // Камера ВСЕГДА занимает весь кадр, а орто-размер считается по более узкой из
            // двух пропорций — колонки и самого экрана.
            //
            // Раньше в портрете вьюпорт равнялся ScreenColumn.Column(), и на телефонах
            // выше 9:16 (а это почти все современные: 390×844 — это 0.462) сверху и снизу
            // оставались полосы по 75-80 px, которые заливала SkyCamera плоским кремом.
            // Фон CozyBackdrop снизу темнее и с зелёными «холмами», так что полоса
            // обрывала их ровной горизонтальной линией — ровно тот случай, который
            // запрещает п.5.9 («поля как заливка при масштабировании»).
            //
            // Размер доски в пикселях от правки не меняется: и там, и там он равен
            // Screen.width / (2 * halfW) на юнит. Меняется только то, что в кадре теперь
            // видно больше игрового фона вместо заливки.
            float halfW = cfg.boardWidth * 0.5f + 0.6f;
            float sa    = (float)Screen.width / Mathf.Max(1, Screen.height);
            cam.orthographicSize = halfW / Mathf.Min(ScreenColumn.Aspect, sa);
            cam.rect = new Rect(0f, 0f, 1f, 1f);


            if (_shakeElapsed < _shakeDuration)
            {
                _shakeElapsed += Time.deltaTime;
                float pct    = 1f - _shakeElapsed / _shakeDuration;
                float offset = Mathf.Sin(_shakeElapsed * 60f) * _shakeIntensity * pct;
                cam.transform.localPosition = new Vector3(offset, BoardYOffset, -10f);
            }
            else
            {
                cam.transform.localPosition = new Vector3(0f, BoardYOffset, -10f);
            }
        }
    }
}
