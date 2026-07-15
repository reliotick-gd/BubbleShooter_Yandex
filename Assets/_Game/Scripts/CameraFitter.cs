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

        // Сдвиг камеры вверх → поле визуально ниже (ближе к бонусам, место под HUD сверху).
        public const float BoardYOffset = 1.0f;

        /// <summary>Мировая точка → координаты дизайн-макета (центр = 0,0; ±540×±960),
        /// т.к. камера рендерит поле ровно в колонку 9:16 (см. ColumnFitter/LateUpdate).</summary>
        public static Vector2 WorldToDesign(Vector3 world, GameConfig cfg)
        {
            float halfW = cfg.boardWidth * 0.5f + 0.6f;
            float ortho = halfW / ScreenColumn.TargetAspect;
            return new Vector2(world.x / halfW * 540f, (world.y - BoardYOffset) / ortho * 960f);
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

            // Заливает весь экран небом (поля вокруг колонки 9:16); cullingMask=0 — ничего
            // не рендерит, только clear. Глубина ниже основной камеры.
            var go = new GameObject("SkyCamera");
            bgCam = go.AddComponent<Camera>();
            bgCam.orthographic     = true;
            bgCam.depth            = cam.depth - 10f;
            bgCam.cullingMask      = 0;
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
            cam.rect = ScreenColumn.Column();
            cam.orthographicSize = (cfg.boardWidth * 0.5f + 0.6f) / ScreenColumn.TargetAspect;

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
