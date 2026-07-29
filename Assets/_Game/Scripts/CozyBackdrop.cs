using UnityEngine;
using UnityEngine.UI;

namespace CozyAnimalTown
{
    /// <summary>
    /// Игровой фон в полях вокруг колонки (десктоп). Раньше их заливала фоновая камера
    /// с cullingMask = 0 — ровный крем на 69% кадра. Яндекс это запрещает: п.5.9 разрешает
    /// поля, только если они «часть игры, а не заливка при масштабировании», а п.5.1.1.2
    /// требует, чтобы кадр качественно демонстрировал механику и графику.
    ///
    /// Устройство: оверлейный Canvas из двух полос по бокам колонки. В каждой — одна и та же
    /// процедурная текстура, обрезанная по uvRect, поэтому градиент и «боке» проходят через
    /// колонку без шва. Поверх очень медленно дрейфуют мягкие пастельные пятна — нейтральное
    /// боке, а не игровые мордочки: те спорили бы с доской. Полосы обрезают содержимое
    /// (RectMask2D), так что пятно уплывает ЗА доску, а не поверх неё.
    /// </summary>
    public class CozyBackdrop : MonoBehaviour
    {
        const int   TexW = 384, TexH = 216;   // картинка мягкая, тянется фильтрацией
        const int   BubblesPerSide = 5;
        const float DesignH = 1200f;          // условная высота «мира» дрейфа

        struct Floater
        {
            public RectTransform rt;
            public float x, y;        // 0..1 по полосе, 0..1 по высоте
            public float speed;       // доля высоты в секунду
            public float swayAmp, swayFreq, swayPhase;
        }

        Canvas _canvas;
        RectTransform _leftStrip, _rightStrip;
        RawImage _leftBg, _rightBg;
        Floater[] _float;

        public static CozyBackdrop Create(GameConfig cfg)
        {
            var go = new GameObject("CozyBackdrop");
            DontDestroyOnLoad(go);
            return go.AddComponent<CozyBackdrop>().Init(cfg);
        }

        CozyBackdrop Init(GameConfig cfg)
        {
            _canvas = UiKit.CreateCanvas("BackdropCanvas", -100);   // под всем UI
            var tex = Build(cfg);

            _leftStrip  = Strip(out _leftBg,  tex);
            _rightStrip = Strip(out _rightBg, tex);

            var pal = cfg.palette;
            _float = new Floater[BubblesPerSide * 2];
            for (int i = 0; i < _float.Length; i++)
            {
                var parent = i < BubblesPerSide ? _leftStrip : _rightStrip;

                var go = new GameObject("Float", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var img = go.AddComponent<Image>();

                // Нейтральное боке, а НЕ шарики-зверята: мордочки из игры на фоне спорят
                // с доской и читаются как мусор. GlowSprite — мягкое радиальное пятно.
                img.sprite = UiKit.GlowSprite;
                img.raycastTarget = false;

                // Детерминированный «псевдослучай» от индекса: одинаковая раскладка при
                // каждом запуске (важно для повторяемых промо-скриншотов).
                float r1 = Frac(i * 0.6180339f);
                float r2 = Frac(i * 0.3819660f + 0.37f);
                float r3 = Frac(i * 0.7548776f + 0.11f);

                // Пастель: цвет палитры, разбавленный до фона, и очень низкая альфа.
                Color c = Color.Lerp(pal[i % pal.Length], Color.white, 0.42f);
                c.a = Mathf.Lerp(0.22f, 0.38f, r3);
                img.color = c;

                float size = Mathf.Lerp(210f, 470f, r1);
                img.rectTransform.sizeDelta = new Vector2(size, size);

                _float[i] = new Floater
                {
                    rt        = img.rectTransform,
                    x         = 0.10f + 0.80f * r2,
                    y         = r3,
                    speed     = Mathf.Lerp(0.008f, 0.020f, r1),   // очень медленно — это фон
                    swayAmp   = Mathf.Lerp(18f, 54f, r2),
                    swayFreq  = Mathf.Lerp(0.07f, 0.17f, r3),
                    swayPhase = r1 * 6.283f,
                };
            }

            Apply();
            return this;
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        RectTransform Strip(out RawImage bg, Texture2D tex)
        {
            var go = new GameObject("Strip", typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            go.AddComponent<RectMask2D>();      // шарики уплывают ЗА доску, а не поверх неё

            var bgGo = new GameObject("Bg", typeof(RectTransform));
            bgGo.transform.SetParent(go.transform, false);
            bg = bgGo.AddComponent<RawImage>();
            bg.texture = tex;
            bg.raycastTarget = false;
            UiKit.Stretch(bg.rectTransform);

            return (RectTransform)go.transform;
        }

        void LateUpdate()
        {
            Apply();
            Drift(Time.unscaledDeltaTime);   // фон живёт даже когда игра на паузе
        }

        void Drift(float dt)
        {
            if (_float == null) return;
            float t = Time.unscaledTime;

            for (int i = 0; i < _float.Length; i++)
            {
                ref var f = ref _float[i];
                if (f.rt == null) continue;

                f.y += f.speed * dt;
                if (f.y > 1.25f) f.y -= 1.5f;      // ушёл за верх — заходит снизу

                var parent = (RectTransform)f.rt.parent;
                float w = parent.rect.width, h = parent.rect.height;

                float sway = Mathf.Sin(t * f.swayFreq * 6.283f + f.swayPhase) * f.swayAmp;
                f.rt.anchorMin = f.rt.anchorMax = new Vector2(0f, 0f);
                f.rt.pivot = new Vector2(0.5f, 0.5f);
                f.rt.anchoredPosition = new Vector2(f.x * w + sway, (f.y - 0.125f) * h);

                // Лёгкое «дыхание» масштаба — движение читается даже когда пятно почти
                // не сместилось. Амплитуда маленькая: фон не должен притягивать взгляд.
                float pulse = 1f + 0.05f * Mathf.Sin(t * 0.21f + f.swayPhase);
                f.rt.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        void Apply()
        {
            Rect c = ScreenColumn.Column();
            float sw = Screen.width, sh = Screen.height;

            float leftW  = c.xMin * sw;
            float rightW = (1f - c.xMax) * sw;
            bool  show   = leftW > 1f || rightW > 1f;
            if (_leftStrip.gameObject.activeSelf != show)
            {
                _leftStrip.gameObject.SetActive(show);
                _rightStrip.gameObject.SetActive(show);
            }
            if (!show) return;

            Place(_leftStrip,  _leftBg,  -sw * 0.5f,          leftW,  sh, 0f,     c.xMin);
            Place(_rightStrip, _rightBg,  sw * 0.5f - rightW, rightW, sh, c.xMax, 1f);
        }

        // x — левый край полосы в координатах overlay-канваса (центр экрана = 0).
        // u0..u1 — кусок текстуры, чтобы рисунок был сквозным через колонку.
        static void Place(RectTransform strip, RawImage bg, float x, float w, float h, float u0, float u1)
        {
            strip.anchorMin = strip.anchorMax = new Vector2(0.5f, 0.5f);
            strip.pivot     = new Vector2(0f, 0.5f);
            strip.sizeDelta = new Vector2(w, h);
            strip.anchoredPosition = new Vector2(x, 0f);
            bg.uvRect = new Rect(u0, 0f, u1 - u0, 1f);
        }

        /// <summary>Процедурный фон: тёплый градиент, мягкие цветные пятна, холмы понизу.</summary>
        static Texture2D Build(GameConfig cfg)
        {
            var tex = new Texture2D(TexW, TexH, TextureFormat.RGB24, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

            Color top = cfg.bgColor;
            Color bot = new Color(cfg.bgColor.r - 0.07f, cfg.bgColor.g - 0.10f, cfg.bgColor.b - 0.14f);

            // Пятна берут цвета из игровой палитры — те же, что у шариков.
            var blobs = new[]
            {
                new Vector4(0.09f, 0.82f, 0.16f, 1f), new Vector4(0.17f, 0.30f, 0.11f, 2f),
                new Vector4(0.06f, 0.52f, 0.08f, 0f), new Vector4(0.91f, 0.77f, 0.17f, 4f),
                new Vector4(0.85f, 0.34f, 0.12f, 5f), new Vector4(0.96f, 0.13f, 0.09f, 3f),
                new Vector4(0.23f, 0.09f, 0.07f, 6f), new Vector4(0.78f, 0.90f, 0.07f, 7f),
            };

            var px = new Color[TexW * TexH];
            for (int y = 0; y < TexH; y++)
            {
                float v = (float)y / (TexH - 1);            // 0 — низ, 1 — верх
                Color baseCol = Color.Lerp(bot, top, v);
                for (int x = 0; x < TexW; x++)
                {
                    float u = (float)x / (TexW - 1);
                    Color c = baseCol;

                    foreach (var b in blobs)
                    {
                        float dx = (u - b.x) * (TexW / (float)TexH);   // круги, а не эллипсы
                        float dy = v - b.y;
                        float d  = Mathf.Sqrt(dx * dx + dy * dy) / b.z;
                        if (d >= 1f) continue;
                        float k = (1f - d) * (1f - d) * 0.32f;         // мягкий спад
                        c = Color.Lerp(c, cfg.palette[(int)b.w], k);
                    }

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
