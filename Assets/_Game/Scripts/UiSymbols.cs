using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.TextCore;
using TMPro;

namespace CozyAnimalTown
{
    /// <summary>
    /// Иконки ★☆♥✕✓▶≡→, которых нет в шрифте LiberationSans (рисовались как □).
    /// Рисуем процедурно, пакуем в один TMP Sprite Asset и ставим дефолтным, чтобы
    /// инлайн-теги &lt;sprite name="…"&gt; работали в любом тексте. Цветные иконки
    /// (звезда/сердце) держат свой цвет; белые идут с tint=1 и берут цвет текста.
    /// </summary>
    public static class UiSymbols
    {
        public const string Star  = "<sprite name=\"star_full\">";
        public const string StarO = "<sprite name=\"star_empty\">";
        public const string Heart = "<sprite name=\"heart\">";
        public const string Cross = "<sprite name=\"cross\" tint=1>";
        public const string Check = "<sprite name=\"check\" tint=1>";
        public const string Play  = "<sprite name=\"play\" tint=1>";
        public const string Menu  = "<sprite name=\"menu\" tint=1>";
        public const string Arrow = "<sprite name=\"arrow\" tint=1>";
        public const string Bolt  = "<sprite name=\"bolt\" tint=1>";
        /// <summary>Режиссёрская хлопушка — маркер «сейчас будет ролик» на кнопках rewarded.</summary>
        public const string Clap  = "<sprite name=\"clap\" tint=1>";

        const int Cell = 64;
        const int SS   = 4;         // супер-сэмплинг для сглаживания краёв

        static bool _done;

        public static void Init()
        {
            if (_done) return;
            _done = true;
            try { Build(); }
            catch (Exception e) { Debug.LogWarning($"[UiSymbols] init failed: {e.Message}"); }
        }

        // Цветные иконки = глянцевый вертикальный градиент (top -> bot). Белые = top==bot
        // (флэт, берут цвет текста через tint=1).
        static readonly (string name, Color top, Color bot, Func<float, float, bool> shape)[] Icons =
        {
            ("star_full",  new Color(1.00f, 0.95f, 0.62f), new Color(0.98f, 0.58f, 0.10f), (x, y) => InStar(x, y, 1.00f)),
            ("star_empty", new Color(1.00f, 0.95f, 0.62f), new Color(0.98f, 0.58f, 0.10f), (x, y) => InStar(x, y, 0.98f) && !InStar(x, y, 0.55f)),
            ("heart",      new Color(1.00f, 0.50f, 0.55f), new Color(0.80f, 0.14f, 0.22f), InHeart),
            ("cross",      Color.white, Color.white, InCross),
            ("check",      Color.white, Color.white, InCheck),
            ("play",       Color.white, Color.white, InPlay),
            ("menu",       Color.white, Color.white, InMenu),
            ("arrow",      Color.white, Color.white, InArrow),
            ("bolt",       Color.white, Color.white, InBolt),
            ("clap",       Color.white, Color.white, InClap),
        };

        static void Build()
        {
            int w = Cell * Icons.Length, h = Cell;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "UiSymbols Atlas" };

            var px = new Color32[w * h];
            for (int i = 0; i < Icons.Length; i++)
            {
                // Цветным иконкам (звезда/сердце) — градиент-объём + тёмная обводка
                // (иначе золотая звезда сливается с золотыми кнопками). Белые иконки
                // берут цвет текста (tint=1), обводка/градиент им не нужны.
                bool shaded = Icons[i].top != Icons[i].bot;
                DrawIcon(px, w, i * Cell, Icons[i].top, Icons[i].bot, Icons[i].shape, outline: shaded);
            }
            tex.SetPixels32(px);
            tex.Apply(false);

            var sprite = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            sprite.name = "UiSymbols";
            sprite.spriteSheet = tex;

            // Иначе UpdateLookupTables() при наличии материала запускает
            // UpgradeSpriteAsset(), который чистит наши таблицы и падает на
            // пустом legacy spriteInfoList. Версия = пропускаем апгрейд.
            typeof(TMP_Asset).GetField("m_Version", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(sprite, "1.1.0");

            // TMP для инлайн-спрайта берёт pointSize/scale/ascentLine/descentLine.
            sprite.faceInfo = new FaceInfo
            {
                pointSize   = Cell,
                scale       = 1f,
                lineHeight  = Cell,
                ascentLine  = 56f,
                baseline    = 0f,
                descentLine = -8f,
            };

            // Таблицы уже проинициализированы пустыми списками (сеттеры internal) —
            // просто наполняем их.
            for (int i = 0; i < Icons.Length; i++)
            {
                var metrics = new GlyphMetrics(Cell, Cell, 0f, 52f, 66f);
                var rect    = new GlyphRect(i * Cell, 0, Cell, Cell);
                var glyph   = new TMP_SpriteGlyph((uint)i, metrics, rect, 1f, 0);
                sprite.spriteGlyphTable.Add(glyph);
                sprite.spriteCharacterTable.Add(new TMP_SpriteCharacter(0, glyph) { name = Icons[i].name, scale = 1f });
            }

            // ВАЖНО: TextMeshPro/Sprite должен быть в Project Settings → Graphics →
            // Always Included Shaders, иначе в билде (WebGL/IL2CPP) шейдер стрипается,
            // Shader.Find вернёт null, материал не назначится и спрайты НЕ РИСУЮТСЯ
            // (в редакторе все шейдеры доступны, поэтому там всё видно, а в билде — нет).
            var shader = Shader.Find("TextMeshPro/Sprite");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetTexture(ShaderUtilities.ID_MainTex, tex);
                sprite.material = mat;
            }
            else
            {
                Debug.LogError("[UiSymbols] shader 'TextMeshPro/Sprite' not found — " +
                               "add it to Graphics → Always Included Shaders, иначе иконки невидимы в билде.");
            }

            sprite.UpdateLookupTables();

            TMP_Settings.defaultSpriteAsset = sprite;
        }

        // тёмная обводка цветных иконок (тёмно-коричневая, читается на золоте и на свете)
        static readonly Color OutlineCol = new Color(0.12f, 0.08f, 0.05f);
        const int OutlineR = 3;     // толщина обводки в пикселях (Cell=64)

        static void DrawIcon(Color32[] px, int texW, int x0, Color top, Color bot, Func<float, float, bool> inside, bool outline)
        {
            // 1) покрытие формы (анти-алиас через супер-сэмплинг) в локальном буфере
            var cov = new float[Cell * Cell];
            for (int y = 0; y < Cell; y++)
            for (int x = 0; x < Cell; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < SS; sy++)
                for (int sx = 0; sx < SS; sx++)
                {
                    float nx = (x + (sx + 0.5f) / SS) / Cell * 2f - 1f;
                    float ny = (y + (sy + 0.5f) / SS) / Cell * 2f - 1f;
                    if (inside(nx, ny)) hits++;
                }
                cov[y * Cell + x] = (float)hits / (SS * SS);
            }

            // 2) композит: заливка поверх обводки (дилатация формы)
            for (int y = 0; y < Cell; y++)
            for (int x = 0; x < Cell; x++)
            {
                float fa = cov[y * Cell + x];            // альфа заливки

                float oa = fa;                           // альфа обводки = дилатированная форма
                if (outline && oa < 1f)
                    for (int dy = -OutlineR; dy <= OutlineR; dy++)
                    for (int dx = -OutlineR; dx <= OutlineR; dx++)
                    {
                        if (dx * dx + dy * dy > OutlineR * OutlineR) continue;
                        int ny = y + dy, nx = x + dx;
                        if ((uint)nx >= Cell || (uint)ny >= Cell) continue;
                        float c = cov[ny * Cell + nx];
                        if (c > oa) oa = c;
                    }

                if (oa <= 0f) continue;

                // цвет заливки: для цветных иконок — вертикальный градиент (низ→верх)
                // плюс глянцевая засветка сверху; для белых — ровный top.
                Color col = top;
                if (outline)
                {
                    float vN = (float)y / (Cell - 1);                 // 0 низ, 1 верх
                    col = Color.Lerp(bot, top, vN);
                    float sheen = Mathf.SmoothStep(0.60f, 1f, vN) * 0.40f;
                    col = Color.Lerp(col, Color.white, sheen);
                }

                // straight-alpha over: out.rgb = (fill*fa + outline*oa*(1-fa)) / oa, out.a = oa
                float k = oa * (1f - fa);
                float r = (col.r * fa + OutlineCol.r * k) / oa;
                float g = (col.g * fa + OutlineCol.g * k) / oa;
                float b = (col.b * fa + OutlineCol.b * k) / oa;
                px[y * texW + x0 + x] = new Color32(
                    (byte)(Mathf.Clamp01(r) * 255), (byte)(Mathf.Clamp01(g) * 255),
                    (byte)(Mathf.Clamp01(b) * 255), (byte)(oa * 255));
            }
        }

        // Координаты форм в [-1,1], y вверх.
        static bool InStar(float x, float y, float s)
        {
            const float R = 0.92f, r = 0.40f;
            // 10-вершинная звезда, вершина вверх
            int cross = 0;
            for (int k = 0; k < 10; k++)
            {
                float a0 = Mathf.PI / 2f + k       * Mathf.PI / 5f;
                float a1 = Mathf.PI / 2f + (k + 1) * Mathf.PI / 5f;
                float r0 = (k       % 2 == 0 ? R : r) * s;
                float r1 = ((k + 1) % 2 == 0 ? R : r) * s;
                float x0v = Mathf.Cos(a0) * r0, y0v = Mathf.Sin(a0) * r0;
                float x1v = Mathf.Cos(a1) * r1, y1v = Mathf.Sin(a1) * r1;
                if (((y0v > y) != (y1v > y)) &&
                    (x < (x1v - x0v) * (y - y0v) / (y1v - y0v) + x0v)) cross++;
            }
            return (cross & 1) == 1;
        }

        static bool InHeart(float x, float y)
        {
            float X = x * 1.25f;
            float Y = y * 1.25f + 0.25f; // лёгкий сдвиг вниз для центровки
            float a = X * X + Y * Y - 1f;
            return a * a * a - X * X * Y * Y * Y < 0f;
        }

        static bool InCross(float x, float y)
        {
            float d = Mathf.Min(
                SegDist(x, y, -0.62f, 0.62f, 0.62f, -0.62f),
                SegDist(x, y, -0.62f, -0.62f, 0.62f, 0.62f));
            return d < 0.16f;
        }

        static bool InCheck(float x, float y)
        {
            float d = Mathf.Min(
                SegDist(x, y, -0.58f, 0.02f, -0.18f, -0.42f),
                SegDist(x, y, -0.18f, -0.42f, 0.62f, 0.5f));
            return d < 0.16f;
        }

        static bool InPlay(float x, float y) =>
            InTri(x, y, -0.42f, 0.56f, -0.42f, -0.56f, 0.6f, 0f);

        static bool InMenu(float x, float y)
        {
            if (Mathf.Abs(x) > 0.68f) return false;
            return Mathf.Abs(y - 0.42f) < 0.13f ||
                   Mathf.Abs(y)         < 0.13f ||
                   Mathf.Abs(y + 0.42f) < 0.13f;
        }

        static bool InArrow(float x, float y)
        {
            bool shaft = Mathf.Abs(y) < 0.13f && x > -0.72f && x < 0.4f;
            bool head  = InTri(x, y, 0.3f, 0.42f, 0.3f, -0.42f, 0.78f, 0f);
            return shaft || head;
        }

        // молния-зигзаг (вершина внизу), 6-вершинный простой полигон
        static readonly float[] BoltX = { 0.10f, -0.52f, -0.08f, -0.28f, 0.52f, 0.08f };
        static readonly float[] BoltY = { 0.92f,  0.05f,  0.05f, -0.92f, 0.12f, 0.12f };
        static bool InBolt(float x, float y) => PointInPoly(x, y, BoltX, BoltY);

        /// <summary>
        /// Режиссёрская хлопушка: корпус-прямоугольник снизу + откинутая планка сверху
        /// с косыми полосами. Однозначно читается как «видео», в отличие от голого
        /// треугольника — п.4.5.1 требует, чтобы игрок понимал, что будет реклама.
        /// </summary>
        static bool InClap(float x, float y)
        {
            const float L = -0.70f, R = 0.70f;

            // корпус
            if (x >= L && x <= R && y >= -0.62f && y <= 0.12f)
            {
                // две светлые «ножки»-прорези внизу не рисуем — на 64px они замылятся
                return true;
            }

            // планка: наклонена, поэтому её нижняя и верхняя границы зависят от x
            float tilt = 0.16f;
            float bot  = 0.20f + tilt * x;
            float top  = 0.52f + tilt * x;
            if (x < L || x > R || y < bot || y > top) return false;

            // косые полосы: вырезаем каждую вторую диагональ
            float s = (x - y * 0.5f) * 4.2f;
            return Mathf.Repeat(s, 2f) < 1.15f;
        }

        static bool PointInPoly(float x, float y, float[] vx, float[] vy)
        {
            int n = vx.Length, cross = 0;
            for (int i = 0, j = n - 1; i < n; j = i++)
                if (((vy[i] > y) != (vy[j] > y)) &&
                    (x < (vx[j] - vx[i]) * (y - vy[i]) / (vy[j] - vy[i]) + vx[i]))
                    cross++;
            return (cross & 1) == 1;
        }

        static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy);
            t = Mathf.Clamp01(t);
            float cx = ax + t * dx, cy = ay + t * dy;
            return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        }

        static bool InTri(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = Sign(px, py, ax, ay, bx, by);
            float d2 = Sign(px, py, bx, by, cx, cy);
            float d3 = Sign(px, py, cx, cy, ax, ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static float Sign(float px, float py, float ax, float ay, float bx, float by) =>
            (px - bx) * (ay - by) - (ax - bx) * (py - by);
    }
}
