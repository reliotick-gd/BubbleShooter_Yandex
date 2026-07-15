using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Спрайты препятствий (128×128), процедурно. Drop-in PNG в Resources/Obstacles/{name}.png
    /// заменит процедурку, если файл найден.
    /// </summary>
    public static class ObstacleFactory
    {
        const int Sz = 128;
        const int Cx = Sz / 2, Cy = Sz / 2;
        static readonly System.Collections.Generic.Dictionary<int, Sprite> Cache = new();
        static Color32[] px;

        static readonly string[] Names = { "rock", "ice", "slime" };

        public static Sprite Get(int id)
        {
            if (Cache.TryGetValue(id, out var s)) return s;

            int kind = id - GameConfig.RockId;
            if (kind < 0 || kind > 2) return SpriteFactory.Circle();

            var artTex = Resources.Load<Texture2D>("Obstacles/" + Names[kind]);
            if (artTex != null)
            {
                var sp = Sprite.Create(artTex, new Rect(0, 0, artTex.width, artTex.height),
                                       Vector2.one * 0.5f, artTex.width);
                Cache[id] = sp;
                return sp;
            }

            px = new Color32[Sz * Sz];
            switch (kind)
            {
                case 0: BuildRock();  break;
                case 1: BuildIce();   break;
                case 2: BuildSlime(); break;
            }
            var tex = new Texture2D(Sz, Sz, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply();
            px = null;
            var spr = Sprite.Create(tex, new Rect(0, 0, Sz, Sz), Vector2.one * 0.5f, Sz);
            Cache[id] = spr;
            return spr;
        }

        static void BuildRock()
        {
            var outline = new Color32(66, 62, 70, 255);
            var body    = new Color32(132, 128, 136, 255);
            Circ(64, 56, 52, outline);
            Circ(42, 62, 30, outline); Circ(88, 60, 30, outline); Circ(64, 40, 30, outline);
            Circ(64, 56, 47, body);
            Circ(44, 62, 26, body); Circ(86, 60, 26, body); Circ(64, 42, 26, body);
            Circ(52, 74, 22, Lighten(body, 0.16f));
            Oval(64, 34, 42, 16, Darken(body, 0.14f));
            for (int i = 0; i < 9; i++) Circ(60 + i * 3, 84 - i * 6, 2, Darken(body, 0.30f));
            for (int i = 0; i < 5; i++) Circ(60 + i * 3, 60 - i * 4, 2, Darken(body, 0.30f));
        }

        static void BuildIce()
        {
            var edge = new Color32(122, 178, 208, 255);
            var body = new Color32(200, 233, 249, 255);
            var core = new Color32(226, 245, 253, 255);
            Circ(64, 60, 60, edge);
            Sphere(64, 60, 56, body);
            Circ(64, 62, 42, core);

            var flake = new Color32(255, 255, 255, 240);
            for (int a = 0; a < 6; a++)
            {
                float ang = a * 60f * Mathf.Deg2Rad, dx = Mathf.Cos(ang), dy = Mathf.Sin(ang);
                LineTo(64, 62, 64 + (int)(dx * 34), 62 + (int)(dy * 34), flake);
                int bx = 64 + (int)(dx * 21), by = 62 + (int)(dy * 21);
                LineTo(bx, by, bx + (int)(Mathf.Cos(ang + 0.6f) * 12), by + (int)(Mathf.Sin(ang + 0.6f) * 12), flake);
                LineTo(bx, by, bx + (int)(Mathf.Cos(ang - 0.6f) * 12), by + (int)(Mathf.Sin(ang - 0.6f) * 12), flake);
            }
            Circ(64, 62, 6, flake);
            Oval(48, 92, 15, 9, new Color32(255, 255, 255, 170));
        }

        static void BuildSlime()
        {
            var body = new Color32(122, 212, 120, 255);
            Circ(64, 60, 60, new Color32(52, 130, 64, 255));
            Sphere(64, 60, 56, body);
            Circ(48, 64, 13, new Color32(26, 38, 26, 255));
            Circ(80, 64, 13, new Color32(26, 38, 26, 255));
            Circ(52, 69, 5, new Color32(255, 255, 255, 255));
            Circ(84, 69, 5, new Color32(255, 255, 255, 255));
            Oval(50, 94, 18, 10, new Color32(255, 255, 255, 95));
        }

        static void LineTo(int x0, int y0, int x1, int y1, Color32 c)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps;
                Circ((int)Mathf.Lerp(x0, x1, u), (int)Mathf.Lerp(y0, y1, u), 2, c);
            }
        }

        static void Tri(int x0, int y0, int x1, int y1, int x2, int y2, Color32 col)
        {
            int minx = Mathf.Max(0, Mathf.Min(x0, Mathf.Min(x1, x2)));
            int maxx = Mathf.Min(Sz - 1, Mathf.Max(x0, Mathf.Max(x1, x2)));
            int miny = Mathf.Max(0, Mathf.Min(y0, Mathf.Min(y1, y2)));
            int maxy = Mathf.Min(Sz - 1, Mathf.Max(y0, Mathf.Max(y1, y2)));
            for (int y = miny; y <= maxy; y++)
                for (int x = minx; x <= maxx; x++)
                {
                    float d1 = Sgn(x, y, x0, y0, x1, y1);
                    float d2 = Sgn(x, y, x1, y1, x2, y2);
                    float d3 = Sgn(x, y, x2, y2, x0, y0);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) Blend(x, y, col);
                }
        }
        static float Sgn(int px_, int py_, int ax, int ay, int bx, int by) =>
            (px_ - bx) * (ay - by) - (ax - bx) * (py_ - by);

        static void Circ(int cx, int cy, int r, Color32 c)
        {
            int x0 = Mathf.Max(0, cx - r - 1), x1 = Mathf.Min(Sz - 1, cx + r + 1);
            int y0 = Mathf.Max(0, cy - r - 1), y1 = Mathf.Min(Sz - 1, cy + r + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float a = Mathf.Clamp01((r - Mathf.Sqrt(dx * dx + dy * dy)) / 1.5f);
                    if (a > 0) Blend(x, y, new Color32(c.r, c.g, c.b, (byte)(c.a * a)));
                }
        }

        static void Blob(int cx, int cy, int r, Color32 c) => Circ(cx, cy, r, c);

        static void Oval(int cx, int cy, int rx, int ry, Color32 c)
        {
            for (int y = Mathf.Max(0, cy - ry); y <= Mathf.Min(Sz - 1, cy + ry); y++)
                for (int x = Mathf.Max(0, cx - rx); x <= Mathf.Min(Sz - 1, cx + rx); x++)
                {
                    float nx = (x - cx) / (float)rx, ny = (y - cy) / (float)ry;
                    float a = Mathf.Clamp01((1f - Mathf.Sqrt(nx * nx + ny * ny)) * 3f);
                    if (a > 0) Blend(x, y, new Color32(c.r, c.g, c.b, (byte)(c.a * a)));
                }
        }

        static void RoundRect(int cx, int cy, int half, int r, Color32 c)
        {
            for (int y = cy - half; y <= cy + half; y++)
                for (int x = cx - half; x <= cx + half; x++)
                {
                    float dx = Mathf.Abs(x - cx) - (half - r);
                    float dy = Mathf.Abs(y - cy) - (half - r);
                    float d = (dx > 0 && dy > 0) ? Mathf.Sqrt(dx * dx + dy * dy) : Mathf.Max(dx, dy);
                    float a = Mathf.Clamp01((r - d) / 1.5f + 1f);
                    if (a > 0) Blend(x, y, new Color32(c.r, c.g, c.b, (byte)(c.a * Mathf.Clamp01(a))));
                }
        }

        static void Sphere(int cx, int cy, int r, Color32 baseCol)
        {
            const float Lx = -0.6f, Ly = 0.6f;
            Color32 hi = Lighten(baseCol, 0.34f), lo = Darken(baseCol, 0.26f);
            for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(Sz - 1, cy + r); y++)
                for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(Sz - 1, cx + r); x++)
                {
                    float ddx = x - cx, ddy = y - cy;
                    float dist = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                    float a = Mathf.Clamp01((r - dist) / 1.5f);
                    if (a <= 0f) continue;
                    float g = 0.5f + 0.6f * ((ddx / r) * Lx + (ddy / r) * Ly);
                    var col = Lerp(lo, hi, g);
                    Blend(x, y, new Color32(col.r, col.g, col.b, (byte)(255 * a)));
                }
        }

        static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t),
                               (byte)(a.b + (b.b - a.b) * t), 255);
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

        static Color32 Lighten(Color32 c, float f) => new Color32(
            (byte)Mathf.Min(255, c.r + (int)(f * 255)),
            (byte)Mathf.Min(255, c.g + (int)(f * 255)),
            (byte)Mathf.Min(255, c.b + (int)(f * 255)), 255);

        static Color32 Darken(Color32 c, float f) => new Color32(
            (byte)Mathf.Max(0, c.r - (int)(f * 255)),
            (byte)Mathf.Max(0, c.g - (int)(f * 255)),
            (byte)Mathf.Max(0, c.b - (int)(f * 255)), 255);
    }
}
