using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Процедурные спрайты, чтобы прототип не зависел от арт-ассетов.
    /// Один белый кружок тинтуется в N цветов (art-saver из GDD §13).
    /// </summary>
    public static class SpriteFactory
    {
        static Sprite _circle, _sparkle, _ring;

        public static Sprite Circle()
        {
            if (_circle == null) _circle = BuildCircle(128);
            return _circle;
        }

        public static Sprite Sparkle()
        {
            if (_sparkle == null) _sparkle = BuildSparkle(128);
            return _sparkle;
        }

        public static Sprite Ring()
        {
            if (_ring == null) _ring = BuildRing(128);
            return _ring;
        }

        static Sprite BuildSparkle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float c = (size - 1) * 0.5f, R = size * 0.5f - 1.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Atan2(dy, dx);
                float m = Mathf.Abs(Mathf.Cos(2f * a));          // 1 на осях, 0 на диагоналях
                float boundary = R * (0.10f + 0.90f * Mathf.Pow(m, 1.35f)); // острые 4 луча
                float alpha = Mathf.Clamp01(boundary - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite BuildRing(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float c = (size - 1) * 0.5f, Rout = size * 0.5f - 1.5f;
            float Rmid = Rout * 0.80f, thick = Rout * 0.20f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - Rmid) / thick); // мягкая полоса-кольцо
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite BuildCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float c = (size - 1) * 0.5f;
            float r = size * 0.5f - 1.5f;
            var px = new Color32[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d + 0.5f); // мягкий край ~1px
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }

            tex.SetPixels32(px);
            tex.Apply();

            // pixelsPerUnit = size  =>  спрайт = 1 мировая единица, масштабируем трансформом.
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
