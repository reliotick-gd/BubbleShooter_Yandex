using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Игровой фон под ВСЕМ кадром. Раньше поля вокруг колонки заливала фоновая камера с
    /// cullingMask = 0 — ровный крем на 69% кадра. Яндекс это запрещает: п.5.9 разрешает
    /// поля, только если они «часть игры, а не заливка при масштабировании», а п.5.1.1.2
    /// требует, чтобы кадр качественно демонстрировал механику и графику.
    ///
    /// ПОЧЕМУ МИРОВЫЕ СПРАЙТЫ, А НЕ UI. Первая версия рисовала фон двумя оверлейными
    /// полосами по бокам колонки. Оверлейный канвас всегда поверх вывода камеры, поэтому
    /// фон приходилось обрывать ровно на границах колонки — и в кадре появлялись
    /// вертикальные швы: по бокам живой фон, внутри колонки плоская заливка камеры.
    /// Вторая попытка (отдельная фоновая камера + clearFlags = Depth) не сработала: проект
    /// на URP, где базовая камера всё равно очищает свой вьюпорт.
    /// Итог: на широком экране вьюпорт основной камеры растянут на весь кадр
    /// (CameraFitter.LateUpdate) и она же рисует этот фон — швов нет по построению.
    /// Масштаб при этом не меняется: орто-размер — полувысота, а высота вьюпорта та же.
    /// Середину кадра закрывает белая карта поля — как в макете.
    /// </summary>
    public class CozyBackdrop : MonoBehaviour
    {
        public const string LayerName = "Backdrop";

        const int   TexW = 384, TexH = 216;
        const int   Circles = 9;

        struct Floater
        {
            public Transform tr;
            public float x, y;            // −1..1 по ширине, 0..1 по высоте
            public float speed, swayAmp, swayFreq, swayPhase, size;
        }

        SpriteRenderer _bg;
        Floater[] _float;

        public static CozyBackdrop Create(GameConfig cfg)
        {
            var go = new GameObject("CozyBackdrop");
            DontDestroyOnLoad(go);
            return go.AddComponent<CozyBackdrop>().Init(cfg);
        }

        CozyBackdrop Init(GameConfig cfg)
        {
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0) layer = 0;      // слой не заведён — рисуем хотя бы на Default
            gameObject.layer = layer;

            var tex = BuildTexture(cfg);
            var bgSprite = Sprite.Create(tex, new Rect(0, 0, TexW, TexH), new Vector2(0.5f, 0.5f), 100f);

            var bgGo = new GameObject("Sky") { layer = layer };
            bgGo.transform.SetParent(transform, false);
            _bg = bgGo.AddComponent<SpriteRenderer>();
            _bg.sprite = bgSprite;
            _bg.drawMode = SpriteDrawMode.Sliced;      // тянем под размер кадра
            _bg.sortingOrder = -200;

            var circle = SpriteFactory.Circle();
            var pal = cfg.palette;
            // Тёплая выборка палитры: яркий голубой в фоне читался холодным пятном.
            int[] warm = { 1, 0, 5, 4, 2, 7 };

            _float = new Floater[Circles];
            for (int i = 0; i < Circles; i++)
            {
                var go = new GameObject("Blob") { layer = layer };
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = circle;
                sr.sortingOrder = -190;

                // Детерминированный «псевдослучай» от индекса: раскладка одинакова при
                // каждом запуске — промо-кадры повторяемы.
                float r1 = Frac(i * 0.6180339f);
                float r2 = Frac(i * 0.3819660f + 0.37f);
                float r3 = Frac(i * 0.7548776f + 0.11f);

                Color c = Color.Lerp(pal[warm[i % warm.Length]], Color.white, 0.62f);
                c.a = Mathf.Lerp(0.30f, 0.55f, r3);
                sr.color = c;

                _float[i] = new Floater
                {
                    tr        = go.transform,
                    x         = r2 * 2f - 1f,
                    y         = r3,
                    speed     = Mathf.Lerp(0.008f, 0.020f, r1),
                    swayAmp   = Mathf.Lerp(0.15f, 0.45f, r2),
                    swayFreq  = Mathf.Lerp(0.07f, 0.17f, r3),
                    swayPhase = r1 * 6.283f,
                    size      = Mathf.Lerp(2.2f, 5.0f, r1),
                };
            }
            return this;
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        void LateUpdate()
        {
            // Размер берём у основной камеры: в широком режиме её вьюпорт — весь экран,
            // и фон обязан покрыть ровно то, что она видит.
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;
            float halfH = cam.orthographicSize;
            float halfW = halfH * Mathf.Max(0.1f, cam.aspect);
            transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);

            if (_bg != null) _bg.size = new Vector2(halfW * 2f, halfH * 2f);

            float t = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;      // фон живёт и на паузе, и во время рекламы

            for (int i = 0; i < _float.Length; i++)
            {
                ref var f = ref _float[i];
                if (f.tr == null) continue;

                f.y += f.speed * dt;
                if (f.y > 1.3f) f.y -= 1.6f;        // ушёл за верх — заходит снизу

                float sway = Mathf.Sin(t * f.swayFreq * 6.283f + f.swayPhase) * f.swayAmp;
                f.tr.localPosition = new Vector3(f.x * halfW + sway, (f.y - 0.15f) * halfH * 2f - halfH, 0f);

                // Лёгкое «дыхание» — движение читается, даже когда пятно почти не сместилось.
                float pulse = f.size * (1f + 0.05f * Mathf.Sin(t * 0.21f + f.swayPhase));
                f.tr.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        /// <summary>Процедурный фон: тёплый градиент и холмы понизу. Цвет дают круги поверх.</summary>
        static Texture2D BuildTexture(GameConfig cfg)
        {
            var tex = new Texture2D(TexW, TexH, TextureFormat.RGB24, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

            Color top = cfg.bgColor;
            Color bot = new Color(cfg.bgColor.r - 0.07f, cfg.bgColor.g - 0.10f, cfg.bgColor.b - 0.14f);

            var px = new Color[TexW * TexH];
            for (int y = 0; y < TexH; y++)
            {
                float v = (float)y / (TexH - 1);            // 0 — низ, 1 — верх
                Color baseCol = Color.Lerp(bot, top, v);
                for (int x = 0; x < TexW; x++)
                {
                    float u = (float)x / (TexW - 1);
                    Color c = baseCol;

                    // Два холма понизу — «земля», как на титуле.
                    float h1 = 0.20f + 0.07f * Mathf.Sin((u + 0.15f) * 3.6f);
                    float h2 = 0.13f + 0.05f * Mathf.Sin((1f - u) * 4.4f + 1.2f);
                    if (v < h1) c = Color.Lerp(c, new Color(0.862f, 0.898f, 0.780f), 0.62f);
                    if (v < h2) c = Color.Lerp(c, new Color(0.812f, 0.867f, 0.729f), 0.62f);

                    px[y * TexW + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
