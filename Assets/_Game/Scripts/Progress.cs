using System.Text;
using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Счёт и звёзды за уровни.
    ///
    /// ЗАЧЕМ. Лидерборд раньше хранил максимальный уровень — такая таблица мертва: все,
    /// кто дошёл до 30-го, равны между собой, обгонять некого и переигрывать нечего.
    /// Теперь в таблицу уходит сумма ЛУЧШИХ результатов по уровням, поэтому есть смысл
    /// возвращаться и переигрывать пройденное.
    ///
    /// ФОРМАТ ХРАНЕНИЯ. Класть по ключу PlayerPrefs на уровень — это сотни ключей и
    /// столько же записей в IndexedDB на WebGL. Вместо этого одна строка вида
    /// "уровень:счёт:звёзды;…" — компактно, читаемо и целиком влезает в облачный сейв
    /// (лимит Яндекса 200 КБ на игрока с огромным запасом).
    /// </summary>
    public static class Progress
    {
        const string KeyBest = "cat_best";     // "1:1200:3;2:980:2;…"

        // Очки. Держим в константах: их же показывает экран победы.
        public const int PointsPerBubble  = 10;   // за лопнувший шар (умножается на комбо)
        public const int PointsPerShotLeft = 50;  // за каждый неизрасходованный выстрел

        static bool _loaded;
        static readonly System.Collections.Generic.Dictionary<int, (int score, int stars)> _best = new();

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            Parse(PlayerPrefs.GetString(KeyBest, ""));
        }

        static void Parse(string raw)
        {
            _best.Clear();
            if (string.IsNullOrEmpty(raw)) return;
            foreach (var part in raw.Split(';'))
            {
                if (part.Length == 0) continue;
                var f = part.Split(':');
                if (f.Length != 3) continue;
                if (int.TryParse(f[0], out int lvl) && int.TryParse(f[1], out int sc) && int.TryParse(f[2], out int st))
                    _best[lvl] = (sc, st);
            }
        }

        static string Serialize()
        {
            var sb = new StringBuilder();
            foreach (var kv in _best)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append(':').Append(kv.Value.score).Append(':').Append(kv.Value.stars);
            }
            return sb.ToString();
        }

        /// <summary>Сырая строка рекордов — уезжает в облако вместе с уровнем.</summary>
        public static string Raw { get { EnsureLoaded(); return Serialize(); } }

        /// <summary>Сливает облачную строку с локальной: по каждому уровню побеждает больший счёт.</summary>
        public static void MergeRaw(string cloudRaw)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(cloudRaw)) return;

            foreach (var part in cloudRaw.Split(';'))
            {
                if (part.Length == 0) continue;
                var f = part.Split(':');
                if (f.Length != 3) continue;
                if (!int.TryParse(f[0], out int lvl) || !int.TryParse(f[1], out int sc) || !int.TryParse(f[2], out int st))
                    continue;
                if (!_best.TryGetValue(lvl, out var cur) || sc > cur.score) _best[lvl] = (sc, st);
            }
            PlayerPrefs.SetString(KeyBest, Serialize());
            PlayerPrefs.Save();
        }

        public static int TotalScore
        {
            get
            {
                EnsureLoaded();
                int sum = 0;
                foreach (var kv in _best) sum += kv.Value.score;
                return sum;
            }
        }

        public static int StarsOf(int level)
        {
            EnsureLoaded();
            return _best.TryGetValue(level, out var v) ? v.stars : 0;
        }

        public static int BestScoreOf(int level)
        {
            EnsureLoaded();
            return _best.TryGetValue(level, out var v) ? v.score : 0;
        }

        /// <summary>Записывает результат уровня. Возвращает true, если это новый рекорд.</summary>
        public static bool Submit(int level, int score, int stars)
        {
            EnsureLoaded();
            bool record = !_best.TryGetValue(level, out var cur) || score > cur.score;
            if (record)
            {
                _best[level] = (score, Mathf.Max(stars, cur.stars));
                PlayerPrefs.SetString(KeyBest, Serialize());
                PlayerPrefs.Save();
            }
            return record;
        }

        /// <summary>Звёзды по остатку выстрелов: 3 — прошёл с запасом, 1 — впритык.</summary>
        public static int StarsForShots(int shotsLeft, int startingShots)
        {
            if (startingShots <= 0) return 1;
            float k = (float)shotsLeft / startingShots;
            if (k >= 0.40f) return 3;
            if (k >= 0.18f) return 2;
            return 1;
        }
    }
}
