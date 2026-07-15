using System;
using System.Collections.Generic;
using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Каталог уровней. Первые N — ручные раскладки из Resources/levels.json
    /// (правятся без перекомпиляции). Дальше — процедурная генерация по кривой
    /// сложности с процедурными препятствиями.
    /// </summary>
    public static class LevelCatalog
    {
#pragma warning disable 0649 // поля заполняет JsonUtility через рефлексию
        [Serializable] class LevelJson { public int rows = 4, colors = 3, shots = 30; public ObstacleCell[] obstacles; }
        [Serializable] class LevelSet  { public LevelJson[] levels; }
#pragma warning restore 0649

        static readonly LevelDef[] _authored = LoadAuthored();

        public static int Count => 999;

        public static LevelDef Get(int level1Based)
        {
            int n = Mathf.Max(1, level1Based);
            if (n - 1 < _authored.Length) return _authored[n - 1];
            return Generate(n);
        }

        static LevelDef[] LoadAuthored()
        {
            var ta = Resources.Load<TextAsset>("levels");
            if (ta == null) return Array.Empty<LevelDef>();
            LevelSet set;
            try { set = JsonUtility.FromJson<LevelSet>(ta.text); }
            catch { return Array.Empty<LevelDef>(); }
            if (set?.levels == null) return Array.Empty<LevelDef>();

            var res = new LevelDef[set.levels.Length];
            for (int i = 0; i < set.levels.Length; i++)
            {
                var l = set.levels[i];
                res[i] = new LevelDef
                {
                    index      = i + 1,
                    rows       = Mathf.Clamp(l.rows, 2, 10),
                    colorCount = Mathf.Clamp(l.colors, 3, 8),
                    shots      = Mathf.Max(8, l.shots),
                    obstacles  = l.obstacles ?? Array.Empty<ObstacleCell>(),
                };
            }
            return res;
        }

        // «Бесконечный» режим: после последнего авторского уровня показываем случайную
        // из ручных раскладок 1..N (шары красятся процедурно, поэтому каждый заход
        // свежий). Один уровень не повторяется чаще раза в 4; детерминировано по n,
        // поэтому Get(n) стабилен.
        static LevelDef Generate(int n)
        {
            int pool = _authored.Length;
            if (pool == 0) return Fallback(n);

            int idx = PickEndlessIndex(n, pool);      // 0-based в _authored
            var b = _authored[idx];
            // Глубоко в бесконечном режиме открываем 8-го зверя (бейдж «Lv41»):
            // мягкая эскалация для упорного игрока, полоса разблокировки честна.
            int colors = (n >= 41) ? Mathf.Max(b.colorCount, 8) : b.colorCount;
            return new LevelDef
            {
                index      = n,                       // показываем реальный номер
                rows       = b.rows,
                colorCount = colors,
                shots      = b.shots,
                obstacles  = b.obstacles,
            };
        }

        // Псевдослучайный индекс раскладки, исключающий 3 предыдущих выбора
        // (⇒ повтор не чаще раза в 4 уровня). Итеративно от первого «бесконечного»
        // уровня до n — дёшево и без рекурсии.
        static int PickEndlessIndex(int n, int pool)
        {
            int start = pool + 1;                     // первый уровень за ручными
            int h0 = -1, h1 = -1, h2 = -1;            // 3 последних выбора (h0 — самый свежий)
            int pick = 0;
            for (int lvl = start; lvl <= n; lvl++)
            {
                uint h = (uint)lvl * 2654435761u + 0x9E3779B9u;
                int cand = (int)(h % (uint)pool);
                int guard = 0;
                while ((cand == h0 || cand == h1 || cand == h2) && guard < pool)
                { cand = (cand + 1) % pool; guard++; }
                pick = cand;
                h2 = h1; h1 = h0; h0 = cand;
            }
            return pick;
        }

        // Страховка на случай отсутствия/пустого levels.json.
        static LevelDef Fallback(int n) => new LevelDef
        {
            index = n, rows = 6, colorCount = 6, shots = 26,
            obstacles = Array.Empty<ObstacleCell>(),
        };
    }
}
