using UnityEngine;

namespace CozyAnimalTown
{
    public static class AnimalSpriteFactory
    {
        const int Sz = 128;
        const int Cx = Sz / 2, Cy = Sz / 2;

        static readonly Sprite[] Cache = new Sprite[10]; // 0-7 животные, 8 радуга, 9 бомба
        static Color32[] px;

        // Имена PNG-арта в Resources/Animals/ по id: если файл есть — заменяет процедурную
        // отрисовку 1:1, нет файла — рисуем кодом.
        static readonly string[] Names =
            { "cat", "bear", "frog", "bunny", "fox", "chick", "panda", "dog", "rainbow", "bomb" };

        static readonly Color32 Transparent = new Color32(0, 0, 0, 0);
        static readonly Color32 Dark    = new Color32( 38,  26,  18, 255);
        static readonly Color32 Black   = new Color32( 18,  10,   6, 255);
        static readonly Color32 White   = new Color32(255, 255, 255, 255);
        static readonly Color32 Pink    = new Color32(255, 158, 182, 255);
        static readonly Color32 Orange  = new Color32(248, 128,  22, 255);
        static readonly Color32 Blush   = new Color32(255, 148, 148, 110);
        static readonly Color32 Yellow  = new Color32(255, 215,  50, 255);

        static readonly Color32[] Rainbow6 =
        {
            new Color32(235,  70,  70, 255),
            new Color32(240, 150,  40, 255),
            new Color32(230, 210,  40, 255),
            new Color32( 80, 190,  80, 255),
            new Color32( 70, 140, 220, 255),
            new Color32(170,  80, 220, 255),
        };

        public static Sprite Get(int id, Color body)
        {
            if (id < 0 || id >= Cache.Length) return SpriteFactory.Circle();
            if (Cache[id] != null) return Cache[id];

            // Готовый PNG-арт рендерим как есть, без пост-обработки (ободок/тень/блик
            // давали «рамку в рамке»); даунскейл — через мипмапы текстуры (enableMipMap в .meta).
            var artTex = Resources.Load<Texture2D>("Animals/" + Names[id]);
            if (artTex != null)
            {
                var clean = StripShadow(artTex);
                Cache[id] = Sprite.Create(clean, new Rect(0, 0, clean.width, clean.height),
                                          Vector2.one * 0.5f, clean.width);
                return Cache[id];
            }

            px = new Color32[Sz * Sz];

            if (id == GameConfig.RainbowId) { BuildRainbow(); }
            else if (id == GameConfig.BombId) { BuildBomb(); }
            else
            {
                Color32 bc    = ToC32(body);
                Color32 light = Lighten(bc, 0.26f);
                Color32 dark2 = Darken(bc, 0.18f);

                // мягкая тёмная обводка
                Circ(Cx, Cy, 61, new Color32(55, 38, 24, 255));

                switch (id)
                {
                    case 0: Cat(bc, light);    break;
                    case 1: Bear(bc, light);   break;
                    case 2: Frog(bc, light);   break;
                    case 3: Bunny(bc, light);  break;
                    case 4: Fox(bc, light);    break;
                    case 5: Chick(bc);         break;
                    case 6: Panda(bc, light);  break;
                    case 7: Dog(bc, light, dark2); break;
                }

                // глянец: широкая мягкая засветка + крисповый блик-точка (сочный шар)
                Oval(46, 99, 26, 14, new Color32(255, 255, 255, 95));
                Circ(43, 103, 5, new Color32(255, 255, 255, 175));
            }

            var tex = new Texture2D(Sz, Sz, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(px);
            tex.Apply();
            px = null;   // буфер больше не нужен — не держим ~64КБ до конца сессии
            Cache[id] = Sprite.Create(tex, new Rect(0, 0, Sz, Sz), Vector2.one * 0.5f, Sz);
            return Cache[id];
        }

        // Делает текстуру читаемой (blit в RT — обходит isReadable/сжатие) и вырезает
        // круг: центр/радиус по bbox пикселей с alpha≥250 (тело шара). Полупрозрачная
        // drop-тень лежит вне круга → обнуляется.
        static Texture2D StripShadow(Texture2D src)
        {
            int w = src.width, h = src.height;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var p = tex.GetPixels32();
            int minX = w, minY = h, maxX = 0, maxY = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (p[y * w + x].a >= 250)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            if (maxX < minX) { tex.Apply(); return tex; }   // нет непрозрачного тела

            float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
            float r  = Mathf.Min(maxX - minX, maxY - minY) * 0.5f + 1f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float m = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 1f);
                    if (m < 1f)
                    {
                        int i = y * w + x;
                        var c = p[i]; c.a = (byte)(c.a * m); p[i] = c;
                    }
                }
            tex.SetPixels32(p); tex.Apply();
            return tex;
        }

        static void Cat(Color32 bc, Color32 light)
        {
            // ушки
            Tri(22, 104, 6,  76, 44, 76, bc);
            Tri(106, 104, 84, 76, 122, 76, bc);
            Sphere(Cx, Cy, 58, bc);
            Tri(22, 98, 14, 80, 38, 80, Pink);
            Tri(106, 98, 90, 80, 116, 80, Pink);
            // глаза — увеличенные
            Oval(46, 72, 13, 9, Dark);
            Oval(82, 72, 13, 9, Dark);
            Circ(50, 76, 6, White); Circ(86, 76, 6, White);
            Circ(47, 74, 3, new Color32(255,255,255,160));
            Circ(83, 74, 3, new Color32(255,255,255,160));
            // нос
            Tri(60, 62, 68, 62, 64, 56, Pink);
            // рот
            Circ(57, 51, 2, Dark); Circ(64, 48, 2, Dark); Circ(71, 51, 2, Dark);
            // усы
            Circ(30, 64, 2, Dark); Circ(36, 59, 2, Dark); Circ(33, 54, 2, Dark);
            Circ(94, 64, 2, Dark); Circ(88, 59, 2, Dark); Circ(91, 54, 2, Dark);
            // румянец
            Oval(38, 60, 10, 6, Blush); Oval(90, 60, 10, 6, Blush);
        }

        static void Bear(Color32 bc, Color32 light)
        {
            Circ(30, 100, 18, bc); Circ(98, 100, 18, bc);
            Sphere(Cx, Cy, 58, bc);
            Circ(30, 100, 10, light); Circ(98, 100, 10, light);
            Oval(Cx, 54, 22, 15, light);
            Circ(44, 76, 11, Dark); Circ(84, 76, 11, Dark);
            Circ(48, 80, 6, White); Circ(88, 80, 6, White);
            Circ(45, 77, 3, new Color32(255,255,255,160));
            Circ(85, 77, 3, new Color32(255,255,255,160));
            Oval(Cx, 58, 7, 5, Dark);
            Oval(38, 62, 9, 6, Blush); Oval(90, 62, 9, 6, Blush);
        }

        static void Frog(Color32 bc, Color32 light)
        {
            Sphere(Cx, Cy, 58, bc);
            Circ(40, 100, 18, bc); Circ(88, 100, 18, bc);
            Circ(40, 100, 11, Dark); Circ(88, 100, 11, Dark);
            Circ(44, 104, 5, White); Circ(92, 104, 5, White);
            Circ(42, 102, 2, new Color32(255,255,255,160));
            Circ(90, 102, 2, new Color32(255,255,255,160));
            // широкая улыбка
            for (int i = 0; i <= 12; i++)
            {
                float a = Mathf.PI * 0.18f + i * (Mathf.PI * 0.64f / 12f);
                int x = Cx + (int)(Mathf.Cos(a) * 30);
                int y = 46 - (int)(Mathf.Sin(a) * 10);
                Circ(x, y, 3, Dark);
            }
            Circ(58, 70, 3, Dark); Circ(70, 70, 3, Dark);
            // светлое брюшко
            Oval(Cx, 54, 18, 22, light);
        }

        static void Bunny(Color32 bc, Color32 light)
        {
            Oval(45, 106, 10, 24, bc); Oval(83, 106, 10, 24, bc);
            Sphere(Cx, Cy, 58, bc);
            Oval(45, 106, 5, 17, Pink); Oval(83, 106, 5, 17, Pink);
            Circ(46, 73, 12, Dark); Circ(82, 73, 12, Dark);
            Circ(50, 78, 6, White); Circ(86, 78, 6, White);
            Circ(47, 75, 3, new Color32(255,255,255,160));
            Circ(83, 75, 3, new Color32(255,255,255,160));
            Circ(Cx, 58, 6, Pink);
            Circ(57, 51, 2, Dark); Circ(64, 49, 2, Dark); Circ(71, 51, 2, Dark);
            Oval(38, 62, 10, 6, Blush); Oval(90, 62, 10, 6, Blush);
        }

        static void Fox(Color32 bc, Color32 light)
        {
            Tri(28, 114, 6, 80, 52, 80, bc); Tri(100, 114, 76, 80, 122, 80, bc);
            Sphere(Cx, Cy, 58, bc);
            Tri(28, 108, 14, 83, 46, 83, Pink); Tri(100, 108, 82, 83, 116, 83, Pink);
            Oval(Cx, 56, 25, 19, light);
            Oval(46, 76, 12, 9, Dark); Oval(82, 76, 12, 9, Dark);
            Circ(50, 80, 5, White); Circ(86, 80, 5, White);
            Circ(47, 77, 3, new Color32(255,255,255,160));
            Circ(83, 77, 3, new Color32(255,255,255,160));
            Circ(Cx, 61, 6, Dark);
        }

        static void Chick(Color32 bc)
        {
            Circ(50, 116, 11, bc); Circ(64, 120, 13, bc); Circ(78, 116, 11, bc);
            Sphere(Cx, Cy, 58, bc);
            Tri(56, 64, 72, 64, 64, 53, Orange);
            Circ(44, 76, 13, Dark); Circ(84, 76, 13, Dark);
            Circ(49, 81, 7, White); Circ(89, 81, 7, White);
            Circ(46, 78, 3, new Color32(255,255,255,160));
            Circ(86, 78, 3, new Color32(255,255,255,160));
            Oval(34, 62, 12, 8, Blush); Oval(94, 62, 12, 8, Blush);
            // крылышки-намёки
            Oval(18, 50, 9, 14, bc); Oval(110, 50, 9, 14, bc);
        }

        static void Panda(Color32 bc, Color32 light)
        {
            // чёрные круглые ушки
            Circ(30, 102, 18, Black); Circ(98, 102, 18, Black);
            // белое тело
            Sphere(Cx, Cy, 58, bc);
            // чёрные пятна вокруг глаз
            Oval(44, 74, 18, 14, Black); Oval(84, 74, 18, 14, Black);
            // глаза
            Circ(44, 74, 9, Dark); Circ(84, 74, 9, Dark);
            Circ(48, 78, 5, White); Circ(88, 78, 5, White);
            Circ(45, 75, 2, new Color32(255,255,255,180));
            Circ(85, 75, 2, new Color32(255,255,255,180));
            // нос
            Oval(Cx, 60, 8, 6, new Color32(40,28,20,255));
            // рот
            Circ(57, 52, 2, Dark); Circ(64, 50, 2, Dark); Circ(71, 52, 2, Dark);
            // чёрные лапки намёком (нижний полукруг)
            Oval(Cx, 20, 30, 14, Black);
            // румянец
            Oval(38, 60, 9, 6, Blush); Oval(90, 60, 9, 6, Blush);
        }

        static void Dog(Color32 bc, Color32 light, Color32 dark2)
        {
            // висячие уши (широкие овалы по бокам)
            Oval(24, 72, 16, 28, dark2); Oval(104, 72, 16, 28, dark2);
            // тело
            Sphere(Cx, Cy, 58, bc);
            // пятно на одном глазу
            Oval(80, 76, 18, 15, dark2);
            // морда чуть светлее
            Oval(Cx, 58, 26, 20, light);
            // глаза
            Circ(44, 75, 11, Dark); Circ(84, 75, 11, Dark);
            Circ(48, 79, 6, White); Circ(88, 79, 6, White);
            Circ(45, 76, 3, new Color32(255,255,255,160));
            Circ(85, 76, 3, new Color32(255,255,255,160));
            // нос — большой чёрный овал
            Oval(Cx, 62, 10, 7, Dark);
            // язычок
            Oval(Cx, 50, 8, 6, Pink);
            // рот
            Circ(57, 52, 2, Dark); Circ(71, 52, 2, Dark);
            // румянец
            Oval(36, 62, 10, 6, Blush); Oval(92, 62, 10, 6, Blush);
        }

        static void BuildRainbow()
        {
            // белое тело (перламутровый объём)
            Circ(Cx, Cy, 61, new Color32(55, 38, 24, 200)); // тёмная обводка
            Sphere(Cx, Cy, 58, new Color32(245, 245, 250, 255));

            // радужное кольцо: 6 цветных сегментов по внешней дуге
            const float innerR = 40f, outerR = 57f;
            for (int seg = 0; seg < 6; seg++)
            {
                float a0 = seg / 6f * Mathf.PI * 2f;
                float a1 = (seg + 1) / 6f * Mathf.PI * 2f;
                var col = Rainbow6[seg];
                for (float a = a0; a <= a1; a += 0.04f)
                {
                    for (float r = innerR; r <= outerR; r += 1f)
                    {
                        int x = Cx + (int)(Mathf.Cos(a) * r);
                        int y = Cy + (int)(Mathf.Sin(a) * r);
                        Blend(x, y, col);
                    }
                }
            }

            // четыре искорки (маленькие звёздочки)
            Sparkle(Cx,     Cy + 30, 6, Yellow);
            Sparkle(Cx + 28, Cy,     5, Yellow);
            Sparkle(Cx - 22, Cy + 10, 4, Yellow);
            Sparkle(Cx + 14, Cy - 26, 4, Yellow);

            // глянец
            Oval(48, 97, 20, 11, new Color32(255, 255, 255, 80));
        }

        static void BuildBomb()
        {
            // тёмно-серое тело
            var body = new Color32(52, 42, 38, 255);
            Circ(Cx, Cy, 61, new Color32(20, 14, 10, 255)); // обводка
            Sphere(Cx, Cy, 58, body);

            // 8-лучевая звезда взрыва по периметру (тонкие лучи)
            var burst = new Color32(240, 160, 40, 220);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                for (float r = 44f; r <= 62f; r += 1f)
                {
                    int x = Cx + (int)(Mathf.Cos(a) * r);
                    int y = Cy + (int)(Mathf.Sin(a) * r);
                    Circ(x, y, 3, burst);
                }
            }

            // чёрный кружок поверх тела (чтобы лучи были только по краям)
            Circ(Cx, Cy, 43, body);

            // фитиль наверху: оранжевая дуга
            for (float a = Mathf.PI * 0.6f; a <= Mathf.PI * 1.0f; a += 0.08f)
            {
                int x = Cx + (int)(Mathf.Cos(a) * 52f);
                int y = Cy + (int)(Mathf.Sin(a) * 52f);
                Circ(x, y, 4, Orange);
            }

            // искра на фитиле
            Sparkle(Cx - 44, Cy + 14, 7, Yellow);
            Sparkle(Cx - 44, Cy + 14, 4, White);

            // блик
            Oval(48, 97, 18, 10, new Color32(255, 255, 255, 45));
        }

        static void Sparkle(int cx, int cy, int size, Color32 col)
        {
            Circ(cx, cy, size / 2, col);
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2f + Mathf.PI / 4f;
                int x = cx + (int)(Mathf.Cos(a) * size);
                int y = cy + (int)(Mathf.Sin(a) * size);
                Circ(x, y, size / 3, col);
            }
        }

        static void Circ(int cx, int cy, int r, Color32 c, float soft = 1.5f)
        {
            int x0 = Mathf.Max(0, cx - r - 2), x1 = Mathf.Min(Sz - 1, cx + r + 2);
            int y0 = Mathf.Max(0, cy - r - 2), y1 = Mathf.Min(Sz - 1, cy + r + 2);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float a = Mathf.Clamp01((r - Mathf.Sqrt(dx * dx + dy * dy)) / soft);
                    if (a > 0) Blend(x, y, new Color32(c.r, c.g, c.b, (byte)(c.a * a)));
                }
        }

        static void Oval(int cx, int cy, int rx, int ry, Color32 c, float soft = 1.5f)
        {
            int x0 = Mathf.Max(0, cx - rx - 2), x1 = Mathf.Min(Sz - 1, cx + rx + 2);
            int y0 = Mathf.Max(0, cy - ry - 2), y1 = Mathf.Min(Sz - 1, cy + ry + 2);
            float sr = soft / Mathf.Max(1, Mathf.Min(rx, ry));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float nx = (x - cx) / (float)rx, ny = (y - cy) / (float)ry;
                    float a = Mathf.Clamp01((1f - Mathf.Sqrt(nx * nx + ny * ny)) / sr);
                    if (a > 0) Blend(x, y, new Color32(c.r, c.g, c.b, (byte)(c.a * a)));
                }
        }

        // Объёмный шар: радиальный градиент со светом сверху-слева + тёмный ободок —
        // даёт «сочный» 3D-объём вместо плоской заливки (топ-казуалки 2026).
        static void Sphere(int cx, int cy, int r, Color32 baseCol)
        {
            const float Lx = -0.62f, Ly = 0.62f; // свет сверху-слева (y вверх)
            Color32 hi = Lighten(baseCol, 0.44f);
            Color32 lo = Darken(baseCol, 0.30f);
            int x0 = Mathf.Max(0, cx - r - 2), x1 = Mathf.Min(Sz - 1, cx + r + 2);
            int y0 = Mathf.Max(0, cy - r - 2), y1 = Mathf.Min(Sz - 1, cy + r + 2);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float ddx = x - cx, ddy = y - cy;
                    float dist = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                    float a = Mathf.Clamp01((r - dist) / 1.5f);
                    if (a <= 0f) continue;
                    float dx = ddx / r, dy = ddy / r;
                    float g = 0.5f + 0.62f * (dx * Lx + dy * Ly);
                    g *= 1f - 0.33f * Mathf.SmoothStep(0.55f, 1f, dist / r); // тёмный ободок
                    var col = Lerp(lo, hi, g);
                    Blend(x, y, new Color32(col.r, col.g, col.b, (byte)(255 * a)));
                }
        }

        static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t), 255);
        }

        static void Tri(int x0, int y0, int x1, int y1, int x2, int y2, Color32 c)
        {
            if (y0 > y1) { int t = x0; x0 = x1; x1 = t; t = y0; y0 = y1; y1 = t; }
            if (y0 > y2) { int t = x0; x0 = x2; x2 = t; t = y0; y0 = y2; y2 = t; }
            if (y1 > y2) { int t = x1; x1 = x2; x2 = t; t = y1; y1 = y2; y2 = t; }
            for (int y = y0; y <= y2; y++)
            {
                float t1 = y2 == y0 ? 1f : (float)(y - y0) / (y2 - y0);
                int xa = x0 + (int)((x2 - x0) * t1);
                int xb = y <= y1
                    ? x0 + (int)((x1 - x0) * (y1 == y0 ? 1f : (float)(y - y0) / (y1 - y0)))
                    : x1 + (int)((x2 - x1) * (y2 == y1 ? 1f : (float)(y - y1) / (y2 - y1)));
                if (xa > xb) { int t = xa; xa = xb; xb = t; }
                for (int x = xa; x <= xb; x++) Blend(x, y, c);
            }
        }

        static void Blend(int x, int y, Color32 src)
        {
            if ((uint)x >= Sz || (uint)y >= Sz) return;
            int i = y * Sz + x;
            Color32 dst = px[i];
            float sa = src.a / 255f, da = dst.a / 255f;
            float oa = sa + da * (1f - sa);
            if (oa < 0.001f) return;
            px[i] = new Color32(
                (byte)((src.r * sa + dst.r * da * (1f - sa)) / oa),
                (byte)((src.g * sa + dst.g * da * (1f - sa)) / oa),
                (byte)((src.b * sa + dst.b * da * (1f - sa)) / oa),
                (byte)(oa * 255f));
        }

        static Color32 ToC32(Color c) =>
            new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255);

        static Color32 Lighten(Color32 c, float f) =>
            new Color32(
                (byte)Mathf.Min(255, c.r + (int)(f * 255)),
                (byte)Mathf.Min(255, c.g + (int)(f * 255)),
                (byte)Mathf.Min(255, c.b + (int)(f * 255)), 255);

        static Color32 Darken(Color32 c, float f) =>
            new Color32(
                (byte)Mathf.Max(0, c.r - (int)(f * 255)),
                (byte)Mathf.Max(0, c.g - (int)(f * 255)),
                (byte)Mathf.Max(0, c.b - (int)(f * 255)), 255);
    }
}
