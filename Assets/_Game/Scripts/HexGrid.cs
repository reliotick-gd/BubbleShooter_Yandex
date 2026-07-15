using System.Runtime.CompilerServices;
using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Логика гекс-сетки со смещением (odd-r): нечётные ряды сдвинуты вправо на пол-ячейки.
    /// Координаты ячейки = (col, row), row=0 — верхний ряд.
    ///
    /// Neighbours() возвращает СТАТИЧЕСКИЙ буфер из 6 элементов — не аллоцирует.
    /// Никогда не вызывай Neighbours() вложенно или храни возвращаемый массив.
    /// </summary>
    public class HexGrid
    {
        readonly GameConfig cfg;

        // Кэшированные производные от cfg — не пересчитываем на каждом из 800 шагов симуляции
        public readonly float CellSize;
        public readonly float RowHeight;
        public readonly float TopRowY;
        public readonly float ReflectX;
        readonly int _columns;

        static readonly Vector2Int[] EvenN =
            { new(-1,0), new(1,0), new(-1,-1), new(0,-1), new(-1,1), new(0,1) };
        static readonly Vector2Int[] OddN  =
            { new(-1,0), new(1,0), new(0,-1), new(1,-1), new(0,1), new(1,1) };

        // Статический буфер — WebGL однопоточный, безопасно
        static readonly Vector2Int[] _buf = new Vector2Int[6];
        public const int NeighborCount = 6;

        public HexGrid(GameConfig cfg)
        {
            this.cfg  = cfg;
            _columns  = cfg.columns;
            CellSize  = cfg.CellSize;
            RowHeight = cfg.RowHeight;
            TopRowY   = cfg.topRowY;
            ReflectX  = cfg.ReflectX;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int RowCells(int row) => (row & 1) != 0 ? _columns - 1 : _columns;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float RowOriginX(int row) => -((RowCells(row) - 1) * 0.5f) * CellSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 CellToWorld(Vector2Int c) =>
            new Vector3(RowOriginX(c.y) + c.x * CellSize, TopRowY - c.y * RowHeight, 0f);

        public Vector2Int WorldToCell(Vector3 p)
        {
            int approx = Mathf.RoundToInt((TopRowY - p.y) / RowHeight);
            if (approx < 0) approx = 0;

            Vector2Int best  = new(0, approx);
            float      bestSq = float.MaxValue;
            int rowEnd = approx + 1;
            for (int row = Mathf.Max(0, approx - 1); row <= rowEnd; row++)
            {
                int n = RowCells(row);
                float ox = -((n - 1) * 0.5f) * CellSize;
                int col = Mathf.Clamp(Mathf.RoundToInt((p.x - ox) / CellSize), 0, n - 1);
                var cell = new Vector2Int(col, row);
                float sq = (CellToWorld(cell) - p).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = cell; }
            }
            return best;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InRange(Vector2Int c)
        {
            if (c.y < 0) return false;
            int n = RowCells(c.y);
            return (uint)c.x < (uint)n; // unsigned comparison = один branch вместо двух
        }

        public Vector2Int Clamp(Vector2Int c)
        {
            int row = Mathf.Max(0, c.y);
            int n   = RowCells(row);
            return new Vector2Int(Mathf.Clamp(c.x, 0, n - 1), row);
        }

        /// <summary>
        /// Возвращает указатель на статический буфер из 6 соседей.
        /// Нельзя держать ссылку между кадрами или вызывать вложенно!
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2Int[] Neighbors(Vector2Int c)
        {
            var src = (c.y & 1) != 0 ? OddN : EvenN;
            int cx = c.x, cy = c.y;
            _buf[0] = new Vector2Int(cx + src[0].x, cy + src[0].y);
            _buf[1] = new Vector2Int(cx + src[1].x, cy + src[1].y);
            _buf[2] = new Vector2Int(cx + src[2].x, cy + src[2].y);
            _buf[3] = new Vector2Int(cx + src[3].x, cy + src[3].y);
            _buf[4] = new Vector2Int(cx + src[4].x, cy + src[4].y);
            _buf[5] = new Vector2Int(cx + src[5].x, cy + src[5].y);
            return _buf;
        }
    }
}
