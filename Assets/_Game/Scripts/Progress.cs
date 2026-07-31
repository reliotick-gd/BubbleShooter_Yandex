using System.Text;
using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Звёзды за уровни — единственная валюта прогресса.
    ///
    /// ЗАЧЕМ. Лидерборд раньше хранил максимальный уровень: все, кто дошёл до 30-го,
    /// оказывались равны, обгонять некого. Систему очков (за шары и остаток выстрелов)
    /// тоже убрали — она была непрозрачной: игрок не понимал, откуда взялось число.
    /// Звёзды видно прямо на экране победы, их максимум 3 за уровень, и сумма звёзд —
    /// честный и понятный критерий для таблицы: чтобы обогнать, надо перепройти уровни
    /// чище, а не «накрутить» очки.
    ///
    /// ФОРМАТ ХРАНЕНИЯ. Ключ PlayerPrefs на уровень — это сотни записей в IndexedDB на
    /// WebGL. Вместо этого одна строка «уровень:звёзды;…» — компактно и целиком влезает
    /// в облачный сейв.
    /// </summary>
    public static class Progress
    {
        const string KeyBest = "cat_best";     // "1:3;2:2;…"

        static bool _loaded;
        static readonly System.Collections.Generic.Dictionary<int, int> _stars = new();

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            Parse(PlayerPrefs.GetString(KeyBest, ""));
        }

        static void Parse(string raw)
        {
            _stars.Clear();
            Absorb(raw);
        }

        /// <summary>
        /// Разбирает строку рекордов, беря максимум по каждому уровню.
        /// Понимает и старый формат «уровень:очки:звёзды» — сейвы игроков, успевших
        /// поиграть до отказа от очков, не должны обнуляться.
        /// </summary>
        static void Absorb(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            foreach (var part in raw.Split(';'))
            {
                if (part.Length == 0) continue;
                var f = part.Split(':');
                if (f.Length < 2) continue;
                if (!int.TryParse(f[0], out int lvl)) continue;

                // 2 поля — новый формат (уровень:звёзды); 3 — старый (уровень:очки:звёзды).
                string starsField = f.Length >= 3 ? f[2] : f[1];
                if (!int.TryParse(starsField, out int st)) continue;

                st = Mathf.Clamp(st, 0, 3);
                if (!_stars.TryGetValue(lvl, out int cur) || st > cur) _stars[lvl] = st;
            }
        }

        static string Serialize()
        {
            var sb = new StringBuilder();
            foreach (var kv in _stars)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append(':').Append(kv.Value);
            }
            return sb.ToString();
        }

        /// <summary>Сырая строка рекордов — уезжает в облако вместе с уровнем.</summary>
        public static string Raw { get { EnsureLoaded(); return Serialize(); } }

        /// <summary>Сливает облачную строку с локальной: по каждому уровню побеждает больше звёзд.</summary>
        public static void MergeRaw(string cloudRaw)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(cloudRaw)) return;
            Absorb(cloudRaw);
            PlayerPrefs.SetString(KeyBest, Serialize());
            PlayerPrefs.Save();
        }

        /// <summary>Сумма звёзд по всем уровням — это и есть результат в таблице лидеров.</summary>
        public static int TotalStars
        {
            get
            {
                EnsureLoaded();
                int sum = 0;
                foreach (var kv in _stars) sum += kv.Value;
                return sum;
            }
        }

        public static int StarsOf(int level)
        {
            EnsureLoaded();
            return _stars.TryGetValue(level, out int v) ? v : 0;
        }

        /// <summary>Записывает результат уровня. Возвращает true, если звёзд стало больше.</summary>
        public static bool Submit(int level, int stars)
        {
            EnsureLoaded();
            stars = Mathf.Clamp(stars, 0, 3);
            bool better = !_stars.TryGetValue(level, out int cur) || stars > cur;
            if (better)
            {
                _stars[level] = stars;
                PlayerPrefs.SetString(KeyBest, Serialize());
                PlayerPrefs.Save();
            }
            return better;
        }

        /// <summary>
        /// Звёзды по остатку выстрелов: 3 — прошёл с запасом, 1 — впритык.
        ///
        /// Пороги смягчены (было 40% и 18%): при старых даже первый уровень с 30 ходами
        /// давал две звезды, хотя проходится он спокойно. Игра казуальная — три звезды
        /// должны быть нормой за аккуратную игру, а не наградой за идеальную.
        /// Значения согласованы с запасом ходов из Docs/balance.csv (~30% сверх нужного).
        /// </summary>
        public static int StarsForShots(int shotsLeft, int startingShots)
        {
            if (startingShots <= 0) return 1;
            float k = (float)shotsLeft / startingShots;
            if (k >= 0.28f) return 3;
            if (k >= 0.10f) return 2;
            return 1;
        }
    }
}
