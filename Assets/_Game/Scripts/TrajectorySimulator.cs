using System.Collections.Generic;
using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Симуляция полёта шара с отскоком от стен.
    /// _pts — переиспользуемый список, не аллоцирует каждый кадр.
    /// </summary>
    public class TrajectorySimulator
    {
        readonly GameConfig   cfg;
        readonly HexGrid      grid;
        readonly BoardManager board;

        readonly List<Vector3> _pts = new(64);

        // ячейки льда, задетые на последней симуляции (реальный выстрел их разбивает)
        readonly List<Vector2Int> _iceHits = new(4);
        public IReadOnlyList<Vector2Int> IceHits => _iceHits;

        public TrajectorySimulator(GameConfig cfg, HexGrid grid, BoardManager board)
        {
            this.cfg   = cfg;
            this.grid  = grid;
            this.board = board;
        }

        public List<Vector3> Simulate(Vector3 origin, Vector3 dir, out Vector2Int cell)
        {
            _pts.Clear();
            _pts.Add(origin);
            _iceHits.Clear();

            Vector3 pos  = origin;
            Vector3 d    = dir.normalized;
            float   step = cfg.Radius * 0.5f;
            if (step < 0.04f) step = 0.04f;

            float reflX   = cfg.ReflectX;
            float ceilY   = cfg.CeilingY;
            float nearDist = cfg.CellSize * 0.9f;
            cell = default;

            float reflectDist = cfg.Diameter * 0.84f;
            float bottomY = cfg.shooterY - cfg.Radius;

            for (int i = 0; i < 800; i++)
            {
                pos.x += d.x * step;
                pos.y += d.y * step;

                if (pos.x < -reflX)      { pos.x = -reflX; d.x = -d.x; _pts.Add(pos); }
                else if (pos.x > reflX)  { pos.x =  reflX; d.x = -d.x; _pts.Add(pos); }

                // нижняя стенка: после рикошета вниз шар не улетает в бесконечность (фикс фриза)
                if (pos.y < bottomY) { pos.y = bottomY; d.y = -d.y; _pts.Add(pos); }

                // рикошет от камня/льда: отражаем направление от нормали и выталкиваем.
                // _iceHits — список уже разбитого льда: от него шар больше НЕ отражается
                // (иначе снаряд отскакивал бы от ледышки, чья текстура уже исчезла).
                if (board.TryReflect(pos, reflectDist, out var n, out var rc, _iceHits))
                {
                    d = Vector3.Reflect(d, n);
                    if (d.sqrMagnitude > 1e-6f) d.Normalize();
                    if (d.y > -0.05f && d.y < 0.05f) d.y = d.y >= 0f ? 0.05f : -0.05f; // не «залипать» горизонтально
                    pos = board.CellToWorld(rc) + n * (reflectDist + step * 0.4f);
                    _pts.Add(pos);
                    if (board.IsIce(rc)) _iceHits.Add(rc);
                    continue;
                }

                if (pos.y >= ceilY)
                {
                    cell = board.FindPlacement(pos);
                    _pts.Add(pos);
                    return _pts;
                }

                // остановка у «липкого» (шар/слайм) — не у рикошетящего
                if (board.AnyStickyNear(pos, nearDist))
                {
                    cell = board.FindPlacement(pos);
                    _pts.Add(pos);
                    return _pts;
                }
            }

            pos.y = ceilY;
            cell  = board.FindPlacement(pos);
            _pts.Add(pos);
            return _pts;
        }
    }
}
