using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Доска: хранит пузыри по ячейкам, спавнит уровень, обрабатывает
    /// установку выстрела -> матч 3+ -> падение отсоединённых гроздей.
    /// </summary>
    public class BoardManager : MonoBehaviour
    {
        GameConfig cfg;
        HexGrid grid;
        readonly Dictionary<Vector2Int, Bubble> cells = new();
        readonly List<int> tmpColors = new();

        // переиспользуемые коллекции для flood/disconnect — убираем GC на каждый выстрел
        readonly List<Vector2Int>      _floodRes  = new(64);
        readonly HashSet<Vector2Int>   _floodSeen = new(64);
        readonly Stack<Vector2Int>     _floodStack= new(64);
        readonly HashSet<Vector2Int>   _dropReach = new(128);
        readonly Stack<Vector2Int>     _dropStack = new(64);
        readonly List<Vector2Int>      _dropList  = new(32);
        // Локальная копия соседей для BestRainbowColor: FloodColor внутри затирает
        // общий буфер HexGrid.Neighbors(), поэтому держать его ссылку нельзя.
        readonly Vector2Int[]          _rainbowNb = new Vector2Int[HexGrid.NeighborCount];
        readonly HashSet<Vector2Int>   _dmgSet    = new(16);   // соседние разрушимые к матчу

        public int Count => cells.Count;

        public void Init(GameConfig cfg, HexGrid grid)
        {
            this.cfg = cfg;
            this.grid = grid;
        }

        bool _dropInRunning;

        public void BuildLevel(LevelDef def)
        {
            Clear();
            for (int r = 0; r < cfg.initialRows; r++)
            {
                int n = grid.RowCells(r);
                for (int q = 0; q < n; q++)
                    Spawn(new Vector2Int(q, r), Random.Range(0, cfg.colorCount));
            }
            if (def.obstacles != null)
                foreach (var o in def.obstacles)
                {
                    var cell = new Vector2Int(o.q, o.r);
                    if (grid.InRange(cell)) Spawn(cell, LevelDef.TypeToId(o.type));
                }
            if (!_dropInRunning) StartCoroutine(DropInAnim());
        }

        /// <summary>Шары-звери, которые ещё нужно убрать (цель уровня). Препятствия не в счёт.</summary>
        public int PoppableCount
        {
            get
            {
                int c = 0;
                foreach (var b in cells.Values)
                    if (b && GameConfig.IsAnimal(b.colorId)) c++;
                return c;
            }
        }

        IEnumerator DropInAnim()
        {
            _dropInRunning = true;
            const float dropOffset = 2.2f;
            const float duration   = 0.38f;

            var entries = new List<(Transform tr, Vector3 final)>(cells.Count);
            foreach (var b in cells.Values)
            {
                if (!b) continue;
                Vector3 final = b.transform.position;
                b.transform.position = new Vector3(final.x, final.y + dropOffset, final.z);
                entries.Add((b.transform, final));
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float ease = 1f - Mathf.Pow(1f - Mathf.Min(t, 1f), 3f);
                foreach (var (tr, final) in entries)
                    if (tr) tr.position = new Vector3(final.x, final.y + dropOffset * (1f - ease), final.z);
                yield return null;
            }
            foreach (var (tr, final) in entries)
                if (tr) tr.position = final;
            _dropInRunning = false;
        }

        public void Clear()
        {
            // Останавливаем PopAnim-корутины до возврата в пул — иначе поздний PopAnim
            // дважды вернёт тот же объект и BubblePool выдаст его двум пузырям сразу.
            StopAllCoroutines();
            // StopAllCoroutines бьёт и DropInAnim до её финальной строки — без сброса
            // флаг остался бы true навсегда, и въезд доски больше не проигрался бы.
            _dropInRunning = false;
            foreach (var b in cells.Values)
                if (b) ReturnToPool(b.gameObject);
            cells.Clear();
        }

        public bool IsOccupied(Vector2Int c) => cells.ContainsKey(c);

        /// <summary>Самый нижний занятый ряд (-1 если доска пуста) — для детекта проигрыша по линии смерти.</summary>
        public int LowestRow()
        {
            int max = -1;
            foreach (var kv in cells)
                if (kv.Key.y > max) max = kv.Key.y;
            return max;
        }

        /// <summary>Мировая позиция центра ячейки (для призрака приземления в Shooter).</summary>
        public Vector3 CellToWorld(Vector2Int c) => grid.CellToWorld(c);

        Bubble Spawn(Vector2Int cell, int color)
        {
            // Защита от перетирания: в крайнем случае (FindPlacement вернул занятую
            // Clamp-ячейку) ячейка может быть занята — вернём старый пузырь в пул,
            // иначе он осиротеет (утечка + висит на экране).
            if (cells.TryGetValue(cell, out var occupied) && occupied)
                ReturnToPool(occupied.gameObject);

            var go = BubblePool.Instance != null ? BubblePool.Instance.Get() : new GameObject();
#if UNITY_EDITOR
            go.name = $"Bubble_{cell.x}_{cell.y}"; // редактор-онли — в билде лишний стринг-аллок на каждый пузырь
#endif
            go.transform.SetParent(transform);
            go.transform.position = grid.CellToWorld(cell);
            go.transform.localScale = Vector3.one * cfg.Diameter;

            var sr = go.GetComponent<SpriteRenderer>() ?? go.AddComponent<SpriteRenderer>();
            sr.sprite = GameConfig.IsObstacle(color)
                ? ObstacleFactory.Get(color)
                : AnimalSpriteFactory.Get(color, cfg.palette[Mathf.Clamp(color, 0, cfg.palette.Length - 1)]);
            sr.color = Color.white;
            sr.sortingOrder = 1;

            var b = go.GetComponent<Bubble>() ?? go.AddComponent<Bubble>();
            b.colorId = color;
            b.hp = GameConfig.IsObstacle(color) ? GameConfig.ObstacleHp(color) : 0;
            b.sr = sr;

            cells[cell] = b;
            return b;
        }

        static void ReturnToPool(GameObject go)
        {
            if (BubblePool.Instance != null) BubblePool.Instance.Return(go);
            else Destroy(go);
        }

        public bool AnyBubbleNear(Vector3 pos, float dist)
        {
            float sq = dist * dist;
            // Проверяем только ячейку под точкой и 6 соседей (O(7)), а не всю доску:
            // вызывается на каждом из ~800 шагов симуляции траектории за кадр.
            Vector2Int c = grid.WorldToCell(pos);
            if (NearCell(c, pos, sq)) return true;
            var nb = grid.Neighbors(c);
            for (int i = 0; i < HexGrid.NeighborCount; i++)
                if (NearCell(nb[i], pos, sq)) return true;
            return false;
        }

        bool NearCell(Vector2Int c, Vector3 pos, float sq) =>
            cells.TryGetValue(c, out var b) && b &&
            (grid.CellToWorld(c) - pos).sqrMagnitude <= sq;

        /// <summary>Рядом есть «липкое» — шар/слайм (не рикошетящее). Снаряд останавливается.</summary>
        public bool AnyStickyNear(Vector3 pos, float dist)
        {
            float sq = dist * dist;
            Vector2Int c = grid.WorldToCell(pos);
            if (StickyNearCell(c, pos, sq)) return true;
            var nb = grid.Neighbors(c);
            for (int i = 0; i < HexGrid.NeighborCount; i++)
                if (StickyNearCell(nb[i], pos, sq)) return true;
            return false;
        }

        bool StickyNearCell(Vector2Int c, Vector3 pos, float sq) =>
            cells.TryGetValue(c, out var b) && b && !GameConfig.IsReflector(b.colorId) &&
            (grid.CellToWorld(c) - pos).sqrMagnitude <= sq;

        /// <summary>Если снаряд задел рикошетящее препятствие (камень/лёд) — вернуть нормаль
        /// отражения (от центра препятствия к снаряду) и его ячейку.</summary>
        public bool TryReflect(Vector3 pos, float dist, out Vector3 normal, out Vector2Int hitCell,
            List<Vector2Int> ignore = null)
        {
            normal = Vector3.zero; hitCell = default;
            float sq = dist * dist, best = float.MaxValue;
            bool found = false;
            Vector2Int c0 = grid.WorldToCell(pos);
            CheckReflector(c0, pos, sq, ref best, ref normal, ref hitCell, ref found, ignore);
            var nb = grid.Neighbors(c0);
            for (int i = 0; i < HexGrid.NeighborCount; i++)
                CheckReflector(nb[i], pos, sq, ref best, ref normal, ref hitCell, ref found, ignore);
            if (found && normal.sqrMagnitude > 1e-6f) normal.Normalize();
            return found;
        }

        void CheckReflector(Vector2Int c, Vector3 pos, float sq, ref float best,
            ref Vector3 normal, ref Vector2Int hitCell, ref bool found, List<Vector2Int> ignore)
        {
            if (!cells.TryGetValue(c, out var b) || b == null || !GameConfig.IsReflector(b.colorId)) return;
            // лёд, уже отразивший снаряд в этой симуляции, считается разбитым — проходим сквозь
            if (ignore != null && ignore.Contains(c)) return;
            Vector3 center = grid.CellToWorld(c);
            float d2 = (pos - center).sqrMagnitude;
            if (d2 <= sq && d2 < best) { best = d2; normal = pos - center; hitCell = c; found = true; }
        }

        public bool IsIce(Vector2Int c) =>
            cells.TryGetValue(c, out var b) && b && b.colorId == GameConfig.IceId;

        public void BreakObstacle(Vector2Int c)
        {
            if (!cells.TryGetValue(c, out var b) || !b) return;
            Vector3 center = grid.CellToWorld(c);
            RemoveAt(c);
            AudioService.PlayPop();
            PopParticles.Instance?.Burst(center, new Color(0.7f, 0.9f, 1f), 6);
        }

        public Vector2Int FindPlacement(Vector3 pos)
        {
            Vector2Int c = grid.WorldToCell(pos);
            if (grid.InRange(c) && !IsOccupied(c)) return c;

            Vector2Int best = c;
            float bestSq = float.MaxValue;
            bool any = false;
            var nb2 = grid.Neighbors(c);
            for (int _i = 0; _i < HexGrid.NeighborCount; _i++)
            {
                var n = nb2[_i];
                if (grid.InRange(n) && !IsOccupied(n))
                {
                    float d = (grid.CellToWorld(n) - pos).sqrMagnitude;
                    if (d < bestSq) { bestSq = d; best = n; any = true; }
                }
            }
            return any ? best : grid.Clamp(c);
        }

        /// <summary>Ставит пузырь, пробует матч и падение. Возвращает число убранных.</summary>
        public int Place(Vector2Int cell, int color)
        {
            if (IsOccupied(cell)) cell = FindPlacement(grid.CellToWorld(cell));

            if (color == GameConfig.BombId)
            {
                Spawn(cell, color); // спавним на кадр, чтобы был визуал вспышки
                int removed = 0;
                Vector3 center = grid.CellToWorld(cell);
                var blast = HexRadius(cell, 2);
                foreach (var c in blast) RemoveAt(c);
                removed += blast.Count;
                RemoveAt(cell); // сама бомба
                removed++;
                AudioService.PlayPop();
                PopParticles.Instance?.Burst(center, new Color(1f, 0.55f, 0.10f), removed);
                CameraFitter.Instance?.TriggerShake(0.20f, 0.30f);
                var drop = FindDisconnected();
                if (drop.Count > 0) AudioService.PlayDrop();
                foreach (var c in drop) RemoveAt(c);
                removed += drop.Count;
                return removed;
            }

            Spawn(cell, color);

            // Радуга матчит цвет соседа с самой большой floodable-группой, а не первый
            // попавшийся — иначе не лопалась бы, даже когда рядом есть выгодный цвет.
            int matchColor = color;
            if (color == GameConfig.RainbowId)
            {
                matchColor = BestRainbowColor(cell);
                if (matchColor < 0) matchColor = GameConfig.RainbowId; // нет обычных соседей — ничего
            }

            int removed2 = 0;
            var match = FloodColor(cell, matchColor);
            if (match.Count >= 3)
            {
                AudioService.PlayPop();
                Vector3 center = grid.CellToWorld(cell);
                foreach (var c in match) RemoveAt(c);
                removed2 += match.Count;
                PopFeedback.Instance?.Show(center, removed2);
                Color burstCol = (matchColor >= 0 && matchColor < cfg.palette.Length)
                    ? cfg.palette[matchColor]
                    : Color.white;
                PopParticles.Instance?.Burst(center, burstCol, match.Count);
                if (removed2 >= 6) CameraFitter.Instance?.TriggerShake(0.12f * (removed2 / 6f), 0.22f);

                // соседний поп ломает лёд/ящик (hp--). Разрушенные считаем в removed.
                removed2 += DamageAround(match);
            }

            var drop2 = FindDisconnected();
            if (drop2.Count > 0) AudioService.PlayDrop();
            foreach (var c in drop2) RemoveAt(c);
            removed2 += drop2.Count;
            return removed2;
        }

        // Все ячейки в гексагональном радиусе r от центра (кроме самого центра).
        List<Vector2Int> HexRadius(Vector2Int center, int r)
        {
            var result = new List<Vector2Int>();
            var visited = new HashSet<Vector2Int> { center };
            var queue   = new Queue<(Vector2Int cell, int dist)>();
            queue.Enqueue((center, 0));
            while (queue.Count > 0)
            {
                var (c, d) = queue.Dequeue();
                if (d > 0 && cells.ContainsKey(c)) result.Add(c);
                if (d < r)
                { var nb3 = grid.Neighbors(c); for (int _i=0;_i<HexGrid.NeighborCount;_i++) if (visited.Add(nb3[_i])) queue.Enqueue((nb3[_i],d+1)); }
            }
            return result;
        }

        // Выбирает цвет соседа с наибольшей floodable-группой. -1 = нет обычных соседей.
        // ВАЖНО: радуга уже должна быть заспавнена в cell — флуд считает её wildcard-ом.
        int BestRainbowColor(Vector2Int cell)
        {
            // Снимаем соседей в локальный буфер ДО первого FloodColor: флуд внутри
            // зовёт grid.Neighbors() и перетирает общий статический буфер _buf —
            // иначе со 2-й итерации nb[i] читался бы как мусор (соседи чужой клетки).
            var nb = grid.Neighbors(cell);
            for (int i = 0; i < HexGrid.NeighborCount; i++) _rainbowNb[i] = nb[i];

            int bestColor = -1, bestCount = 0, seenMask = 0;
            for (int i = 0; i < HexGrid.NeighborCount; i++)
            {
                if (!cells.TryGetValue(_rainbowNb[i], out var b) || b == null) continue;
                int c = b.colorId;
                if (c < 0 || c >= GameConfig.RainbowId) continue; // только обычные цвета
                int m = 1 << c;
                if ((seenMask & m) != 0) continue;                // этот цвет уже считали
                seenMask |= m;
                int cnt = FloodColor(cell, c).Count;              // _floodRes перезапишется ниже финальным флудом
                if (cnt > bestCount) { bestCount = cnt; bestColor = c; }
            }
            return bestColor;
        }

        /// <summary>Спасение при проигрыше «по краю»: убирает нижние <paramref name="rows"/> рядов, освобождая место у линии смерти.</summary>
        public int ClearBottomRows(int rows)
        {
            int low = LowestRow();
            if (low < 0) return 0;
            int threshold = low - rows + 1;

            var toRemove = new List<Vector2Int>();
            foreach (var kv in cells)
                if (kv.Key.y >= threshold) toRemove.Add(kv.Key);
            if (toRemove.Count == 0) return 0;

            Vector3 center = grid.CellToWorld(new Vector2Int(grid.RowCells(low) / 2, low));
            foreach (var c in toRemove) RemoveAt(c);

            // отсоединённые грозди после расчистки тоже падают
            var drop = FindDisconnected();
            foreach (var c in drop) RemoveAt(c);

            AudioService.PlayDrop();
            PopParticles.Instance?.Burst(center, new Color(0.5f, 0.8f, 1f), toRemove.Count);
            CameraFitter.Instance?.TriggerShake(0.18f, 0.3f);
            return toRemove.Count + drop.Count;
        }

        /// <summary>Есть ли на доске хоть один шар этого цвета (чтобы не заряжать «мёртвый»).</summary>
        public bool HasColor(int id)
        {
            foreach (var b in cells.Values)
                if (b && b.colorId == id) return true;
            return false;
        }

        public int RandomPresentColor(int colorCount)
        {
            // Bitmask вместо List.Contains: O(1) на каждый пузырь, без аллокаций.
            // colorCount ≤ 8, поэтому 32-битного int достаточно с запасом.
            int seen = 0;
            tmpColors.Clear();
            foreach (var b in cells.Values)
            {
                int id = b.colorId;
                if (id >= 0 && id < colorCount)
                {
                    int mask = 1 << id;
                    if ((seen & mask) == 0) { seen |= mask; tmpColors.Add(id); }
                }
            }
            if (tmpColors.Count == 0) return Random.Range(0, colorCount);
            return tmpColors[Random.Range(0, tmpColors.Count)];
        }

        void RemoveAt(Vector2Int c)
        {
            if (!cells.TryGetValue(c, out var b)) return;
            cells.Remove(c);
            if (b) StartCoroutine(PopAnim(b.gameObject, b.sr));
        }

        IEnumerator PopAnim(GameObject go, SpriteRenderer sr)
        {
            float t = 0f;
            Vector3 baseScale = go.transform.localScale;
            while (t < 1f)
            {
                t += Time.deltaTime * 10f;
                float s = Mathf.Sin(t * Mathf.PI);
                go.transform.localScale = baseScale * (1f + s * 0.55f);
                if (sr) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t);
                yield return null;
            }
            ReturnToPool(go);
        }

        List<Vector2Int> FloodColor(Vector2Int start, int color)
        {
            _floodRes.Clear(); _floodSeen.Clear(); _floodStack.Clear();
            _floodSeen.Add(start);
            _floodStack.Push(start);
            while (_floodStack.Count > 0)
            {
                var c = _floodStack.Pop();
                if (!cells.TryGetValue(c, out var b) || b == null) continue;
                bool match = b.colorId == color || b.colorId == GameConfig.RainbowId;
                if (!match) continue;
                _floodRes.Add(c);
                var nb4 = grid.Neighbors(c);
                for (int _i = 0; _i < HexGrid.NeighborCount; _i++)
                    if (IsOccupied(nb4[_i]) && _floodSeen.Add(nb4[_i])) _floodStack.Push(nb4[_i]);
            }
            return _floodRes;
        }

        // Наносит 1 урон каждому разрушимому соседу лопнувшей группы; возвращает число разрушенных.
        int DamageAround(List<Vector2Int> source)
        {
            _dmgSet.Clear();
            foreach (var c in source)
            {
                var nb = grid.Neighbors(c);
                for (int i = 0; i < HexGrid.NeighborCount; i++)
                    if (cells.TryGetValue(nb[i], out var b) && b && GameConfig.IsDestructible(b.colorId))
                        _dmgSet.Add(nb[i]);
            }
            int destroyed = 0;
            foreach (var p in _dmgSet)
            {
                if (!cells.TryGetValue(p, out var b) || !b) continue;
                if (--b.hp <= 0) { RemoveAt(p); destroyed++; }
            }
            return destroyed;
        }

        List<Vector2Int> FindDisconnected()
        {
            _dropReach.Clear(); _dropStack.Clear(); _dropList.Clear();
            // Якоря: верхний ряд И препятствия (фиксированные мид-филд блокеры не падают
            // и держат прилепленные к ним шары).
            foreach (var kv in cells)
                if ((kv.Key.y == 0 || GameConfig.IsObstacle(kv.Value.colorId)) && _dropReach.Add(kv.Key))
                    _dropStack.Push(kv.Key);

            while (_dropStack.Count > 0)
            {
                var c = _dropStack.Pop();
                var nb5 = grid.Neighbors(c);
                for (int _i = 0; _i < HexGrid.NeighborCount; _i++)
                    if (IsOccupied(nb5[_i]) && _dropReach.Add(nb5[_i])) _dropStack.Push(nb5[_i]);
            }

            // Препятствия сами никогда не «отсоединяются» (их не роняем).
            foreach (var kv in cells)
                if (!GameConfig.IsObstacle(kv.Value.colorId) && !_dropReach.Contains(kv.Key))
                    _dropList.Add(kv.Key);
            return _dropList;
        }
    }
}
