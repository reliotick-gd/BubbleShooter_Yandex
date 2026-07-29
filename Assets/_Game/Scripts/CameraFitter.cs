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
        Camera bgCam;
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

            // Фон рисует САМА основная камера (см. LateUpdate: на широком экране её вьюпорт
            // растянут на весь кадр). Отдельная камера тут не годится: проект на URP, где
            // базовая камера всё равно очищает свой вьюпорт, и clearFlags = Depth не
            // композитит — из-за этого на границах колонки были вертикальные швы.
            var go = new GameObject("SkyCamera");
            bgCam = go.AddComponent<Camera>();
            bgCam.orthographic     = true;
            bgCam.depth            = cam.depth - 10f;
            bgCam.cullingMask      = 0;         // только заливка полей при letterbox в портрете
            bgCam.clearFlags       = CameraClearFlags.SolidColor;
            bgCam.backgroundColor  = cfg.bgColor;
            bgCam.rect             = new Rect(0f, 0f, 1f, 1f);
            bgCam.allowMSAA        = false;
            bgCam.useOcclusionCulling = false;
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
            cam.orthographicSize = (cfg.boardWidth * 0.5f + 0.6f) / ScreenColumn.Aspect;

            // На широком экране камера занимает ВЕСЬ кадр, а не колонку. Масштаб от этого
            // не меняется (орто-размер — полувысота, а высота вьюпорта та же): просто
            // становится видно мир по бокам от доски, и фон рисуется без швов.
            // В портрете оставляем колонку — там поля добивает SkyCamera.
            cam.rect = ScreenColumn.IsWide ? new Rect(0f, 0f, 1f, 1f) : ScreenColumn.Column();

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
