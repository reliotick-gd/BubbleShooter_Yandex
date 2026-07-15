using System.Collections.Generic;
using UnityEngine;

namespace CozyAnimalTown
{
    public static class IconFactory
    {
        const int Sz = 128;
        static readonly Dictionary<string, Sprite> Cache = new();
        static Color32[] px;
        static Color32 Ink;

        public static Sprite Get(string name, Color color)
        {
            string key = name + "_" + ColorUtility.ToHtmlStringRGBA(color);
            if (Cache.TryGetValue(key, out var s)) return s;

            // Drop-in: чистые PNG-иконки (Resources/Icons/*.png, Material Icons) —
            // перекрашиваем в нужный цвет, сохраняя альфу. Нет файла → процедурка.
            var png = Resources.Load<Texture2D>("Icons/" + name);
            if (png != null)
            {
                var tinted = Tint(png, color);
                Cache[key] = tinted;
                return tinted;
            }

            px = new Color32[Sz * Sz];
            Ink = color;
            switch (name)
            {
                case "gear":      Gear();     break;
                case "restart":   Restart();  break;
                case "back":      Back();     break;
                case "close":     Close();    break;
                case "hand":      Hand();     break;
                case "sound_on":  Speaker(true);  break;
                case "sound_off": Speaker(false); break;
                case "leaderboard": Leaderboard(); break;
            }
            var tex = new Texture2D(Sz, Sz, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px); tex.Apply(); px = null;
            var sp = Sprite.Create(tex, new Rect(0, 0, Sz, Sz), Vector2.one * 0.5f, Sz);
            Cache[key] = sp;
            return sp;
        }

        // Перекраска PNG-иконки: RGB → color, альфа из исходника. Blit делает
        // сжатую/нечитаемую текстуру доступной для GetPixels.
        static Sprite Tint(Texture2D src, Color color)
        {
            int w = src.width, h = src.height;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var p = tex.GetPixels32();
            var c = (Color32)color;
            for (int i = 0; i < p.Length; i++)
                p[i] = new Color32(c.r, c.g, c.b, p[i].a);
            tex.SetPixels32(p);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.one * 0.5f, w);
        }

        static void Gear()
        {
            const int cx = 64, cy = 64;
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Disc(cx + (int)(Mathf.Cos(a) * 46), cy + (int)(Mathf.Sin(a) * 46), 12);
            }
            Ring(cx, cy, 40, 18);
        }

        static void Restart()
        {
            const int cx = 64, cy = 66;
            Arc(cx, cy, 30, 120f, 400f, 7);           // почти полный круг
            float a = 40f * Mathf.Deg2Rad;            // конец дуги (400° мод 360°)
            int ex = cx + (int)(Mathf.Cos(a) * 30), ey = cy + (int)(Mathf.Sin(a) * 30);
            Tri(ex - 14, ey - 2, ex + 8, ey + 10, ex + 12, ey - 16);
        }

        static void Back()
        {
            ThickLine(84, 40, 44, 64, 9);
            ThickLine(84, 88, 44, 64, 9);
            ThickLine(44, 64, 92, 64, 9);
        }

        static void Close()
        {
            ThickLine(40, 40, 88, 88, 9);
            ThickLine(88, 40, 40, 88, 9);
        }

        static void Speaker(bool on)
        {
            RectFill(26, 52, 44, 76);
            Tri(44, 52, 66, 30, 66, 98);
            Tri(44, 52, 44, 76, 66, 98);              // второй треугольник добивает рупор до четырёхугольника
            if (on)
            {
                Arc(70, 64, 16, -50f, 50f, 8);
                Arc(70, 64, 32, -45f, 45f, 8);
            }
            else
            {
                ThickLine(78, 46, 108, 82, 9);
                ThickLine(108, 46, 78, 82, 9);
            }
        }

        static void Leaderboard()
        {
            RectFill(26, 36, 46, 74);  Disc(36, 74, 10);
            RectFill(54, 36, 74, 94);  Disc(64, 94, 10);
            RectFill(82, 36, 102, 60); Disc(92, 60, 10);
        }

        static void Hand()
        {
            Disc(64, 44, 27);
            RectFill(56, 44, 72, 92);
            Disc(64, 92, 8);
            RectFill(44, 40, 84, 52);
        }

        static void Disc(int cx, int cy, int r)
        {
            for (int y = cy - r - 1; y <= cy + r + 1; y++)
                for (int x = cx - r - 1; x <= cx + r + 1; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (a > 0) Blend(x, y, a);
                }
        }

        static void Ring(int cx, int cy, int rOut, int rIn)
        {
            for (int y = cy - rOut - 1; y <= cy + rOut + 1; y++)
                for (int x = cx - rOut - 1; x <= cx + rOut + 1; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = Mathf.Clamp01(Mathf.Min(rOut - d, d - rIn) + 0.5f);
                    if (a > 0) Blend(x, y, a);
                }
        }

        static void Arc(int cx, int cy, int r, float a0, float a1, int thick)
        {
            for (float a = a0; a <= a1; a += 2f)
            {
                float ar = a * Mathf.Deg2Rad;
                Disc(cx + (int)(Mathf.Cos(ar) * r), cy + (int)(Mathf.Sin(ar) * r), thick / 2);
            }
        }

        static void ThickLine(int x0, int y0, int x1, int y1, int thick)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Disc((int)Mathf.Lerp(x0, x1, t), (int)Mathf.Lerp(y0, y1, t), thick / 2);
            }
        }

        static void RectFill(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) Blend(x, y, 1f);
        }

        static void Tri(int x0, int y0, int x1, int y1, int x2, int y2)
        {
            int minx = Mathf.Min(x0, Mathf.Min(x1, x2)), maxx = Mathf.Max(x0, Mathf.Max(x1, x2));
            int miny = Mathf.Min(y0, Mathf.Min(y1, y2)), maxy = Mathf.Max(y0, Mathf.Max(y1, y2));
            for (int y = miny; y <= maxy; y++)
                for (int x = minx; x <= maxx; x++)
                    if (InTri(x, y, x0, y0, x1, y1, x2, y2)) Blend(x, y, 1f);
        }

        static bool InTri(int px_, int py_, int x0, int y0, int x1, int y1, int x2, int y2)
        {
            float d1 = Sign(px_, py_, x0, y0, x1, y1);
            float d2 = Sign(px_, py_, x1, y1, x2, y2);
            float d3 = Sign(px_, py_, x2, y2, x0, y0);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
        static float Sign(int px_, int py_, int ax, int ay, int bx, int by) =>
            (px_ - bx) * (ay - by) - (ax - bx) * (py_ - by);

        static void Blend(int x, int y, float a)
        {
            if ((uint)x >= Sz || (uint)y >= Sz) return;
            int i = y * Sz + x;
            // (byte)-каст дробной альфы давал 0 и убивал сглаживание краёв — считаем в 0..255
            byte na = (byte)(Mathf.Clamp01(a) * 255f);
            if (na <= px[i].a) return;   // берём максимум альфы (перекрытие штрихов)
            px[i] = new Color32(Ink.r, Ink.g, Ink.b, na);
        }
    }
}
