using UnityEngine;
using UnityEngine.UI;

namespace CozyAnimalTown
{
    /// <summary>
    /// Рисует игровой фон в полях вокруг колонки (десктоп). Раньше их заливала фоновая
    /// камера с cullingMask = 0 — то есть ровный крем на 69% кадра. Яндекс это запрещает:
    /// п.5.9 разрешает поля, только если они «часть игры, а не заливка при масштабировании»,
    /// а п.5.1.1.2 требует, чтобы кадр качественно демонстрировал механику и графику.
    ///
    /// Фон — оверлейный Canvas из двух RawImage по бокам колонки; в них одна и та же
    /// процедурная текстура, обрезанная по uvRect, поэтому градиент и «боке» переходят
    /// через колонку без шва. Полосы не накрывают саму доску — она рендерится камерой под ними.
    /// </summary>
    public class CozyBackdrop : MonoBehaviour
    {
        const int TexW = 384, TexH = 216;   // достаточно: картинка мягкая, тянется фильтрацией

        RawImage _left, _right;
        Canvas   _canvas;

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

            _left  = Strip(tex);
            _right = Strip(tex);
            Apply();
            return this;
        }

        RawImage Strip(Texture2D tex)
        {
            var go = new GameObject("Strip", typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            var img = go.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            return img;
        }

        void LateUpdate() => Apply();

        void Apply()
        {
            Rect c = ScreenColumn.Column();
            float sw = Screen.width, sh = Screen.height;

            // Поля только по бокам (портрет на телефоне полей не имеет — полосы схлопнутся).
            float leftW  = c.xMin * sw;
            float rightW = (1f - c.xMax) * sw;
            bool  show   = leftW > 1f || rightW > 1f;
            if (_left.enabled != show)  { _left.enabled = show; _right.enabled = show; }
            if (!show) return;

            Place(_left,  -sw * 0.5f,           leftW,  sh, 0f,      c.xMin);
            Place(_right,  sw * 0.5f - rightW,  rightW, sh, c.xMax,  1f);
        }

        // x — левый край полосы в координатах overlay-канваса (центр экрана = 0).
        // u0..u1 — какой кусок текстуры показать, чтобы рисунок был сквозным через колонку.
        static void Place(RawImage img, float x, float w, float h, float u0, float u1)
        {
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, 0f);
            img.uvRect = new Rect(u0, 0f, u1 - u0, 1f);
        }

        /// <summary>Процедурный фон: тёплый градиент, мягкие цветные пятна, холмы понизу.</summary>
        static Texture2D Build(GameConfig cfg)
        {
            var tex = new Texture2D(TexW, TexH, TextureFormat.RGB24, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

            Color top  = cfg.bgColor;
            Color bot  = new Color(cfg.bgColor.r - 0.06f, cfg.bgColor.g - 0.08f, cfg.bgColor.b - 0.12f);

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
                        float k = (1f - d) * (1f - d) * 0.16f;         // мягкий спад
                        c = Color.Lerp(c, cfg.palette[(int)b.w], k);
                    }

                    // Два холма понизу — «земля», как на титуле.
                    float h1 = 0.20f + 0.07f * Mathf.Sin((u + 0.15f) * 3.6f);
                    float h2 = 0.13f + 0.05f * Mathf.Sin((1f - u) * 4.4f + 1.2f);
                    if (v < h1) c = Color.Lerp(c, new Color(0.886f, 0.910f, 0.816f), 0.55f);
                    if (v < h2) c = Color.Lerp(c, new Color(0.847f, 0.886f, 0.780f), 0.55f);

                    px[y * TexW + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
