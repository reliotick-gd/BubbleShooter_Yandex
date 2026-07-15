using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CozyAnimalTown
{
    /// <summary>
    /// Пушка: прицел (точки по траектории), выстрел, полёт снаряда и передача
    /// результата на доску. Цвета берём из присутствующих на доске (анти-софтлок).
    /// </summary>
    public class Shooter : MonoBehaviour
    {
        const float Speed = 22f;

        GameConfig cfg;
        Camera cam;
        GameManager gm;
        BoardManager board;
        TrajectorySimulator sim;

        int current, next;         // цвета обычных шаров
        int curSpecial = -1;       // спецшар текущего выстрела (-1 = обычный)
        int nextSpecial = -1;      // спецшар в очереди (следующий выстрел)
        bool hasNext;
        bool firing;
        bool _gestureOverUI;   // текущее касание началось над кнопкой → не стреляем
        bool _gestureActive;   // нажатие началось в игровом поле (а не пока висел оверлей)

        SpriteRenderer proj;
        SpriteRenderer nextIndicator;
        List<SpriteRenderer> dots;
        readonly List<Vector2Int> _iceToBreak = new(4);

        // Кэш последней симуляции: пока направление прицела и доска не менялись,
        // 800-шаговый прогон не повторяем (на десктопе прицел виден при простом
        // наведении мыши — без кэша симуляция гонялась каждый кадр).
        Vector3 _simDir; bool _simValid;
        List<Vector3> _simPath; Vector2Int _simCell;

        Vector3 Origin => new Vector3(0f, cfg.shooterY, 0f);

        /// <summary>Спрайт текущего снаряда (для онбординга — демо-шар как боевой).</summary>
        public Sprite CurrentSprite => proj != null ? proj.sprite : null;

        public void Init(GameConfig cfg, Camera cam, GameManager gm, BoardManager board, TrajectorySimulator sim)
        {
            this.cfg = cfg; this.cam = cam; this.gm = gm; this.board = board; this.sim = sim;

            var go = new GameObject("Projectile");
            go.transform.SetParent(transform);
            go.transform.localScale = Vector3.one * cfg.Diameter;
            proj = go.AddComponent<SpriteRenderer>();
            proj.sprite = SpriteFactory.Circle();
            proj.sortingOrder = 3;

            // холдер «next» — лёгкий кремовый кружок-слот (виден на белом поле)
            var holder = new GameObject("NextHolder");
            holder.transform.SetParent(transform);
            holder.transform.localScale = Vector3.one * cfg.Diameter * 0.92f;
            holder.transform.position = new Vector3(cfg.HalfWidth * 0.55f, cfg.shooterY, 0f);
            var hsr = holder.AddComponent<SpriteRenderer>();
            hsr.sprite = SpriteFactory.Circle();
            hsr.color = new Color(0.91f, 0.88f, 0.81f, 1f);
            hsr.sortingOrder = 1;

            var ng = new GameObject("NextIndicator");
            ng.transform.SetParent(transform);
            ng.transform.localScale = Vector3.one * cfg.Diameter * 0.6f;
            ng.transform.position = new Vector3(cfg.HalfWidth * 0.55f, cfg.shooterY, 0f);
            nextIndicator = ng.AddComponent<SpriteRenderer>();
            nextIndicator.sprite = SpriteFactory.Circle();
            nextIndicator.sortingOrder = 2;
            nextIndicator.color = Color.white;

            EnsureDots();
        }

        public void Reload()
        {
            current = hasNext ? next : board.RandomPresentColor(cfg.colorCount);
            next    = board.RandomPresentColor(cfg.colorCount);
            hasNext = true;
            // цвет исчез с доски → не заряжаем «мёртвый», берём присутствующий
            if (!board.HasColor(current)) current = board.RandomPresentColor(cfg.colorCount);
            if (!board.HasColor(next))    next    = board.RandomPresentColor(cfg.colorCount);
            // спец-очередь сдвигается: заряженный «в очередь» становится текущим
            curSpecial  = nextSpecial;
            nextSpecial = -1;
            _simValid = false;   // доска изменилась — кэш траектории недействителен
            RefreshProj();
            RefreshNext();
        }

        /// <summary>Сброс очереди при старте уровня — "next" мог быть цветом из прошлого
        /// уровня с другим colorCount. Спец-очередь тоже сбрасываем.</summary>
        public void ResetQueue() { hasNext = false; curSpecial = -1; nextSpecial = -1; }

        /// <summary>Заряжает спецшар: сначала ТЕКУЩИЙ выстрел; если он уже занят —
        /// ставит в ОЧЕРЕДЬ (следующий выстрел). Возвращает false, если оба заняты
        /// (тогда бонус НЕ тратится — не «сгорает»).</summary>
        public bool ArmSpecial(int specialId)
        {
            if (curSpecial < 0)  { curSpecial  = specialId; RefreshProj(); return true; }
            if (nextSpecial < 0) { nextSpecial = specialId; RefreshNext(); return true; }
            return false;
        }

        void RefreshProj()
        {
            int id = curSpecial >= 0 ? curSpecial : current;
            Color col = (curSpecial < 0 && current < cfg.palette.Length) ? cfg.palette[current] : Color.white;
            proj.sprite = AnimalSpriteFactory.Get(id, col);
            proj.color = Color.white; proj.enabled = true;
            proj.transform.position = Origin;
        }

        void RefreshNext()
        {
            if (!nextIndicator) return;
            int id = nextSpecial >= 0 ? nextSpecial : next;
            Color col = (nextSpecial < 0 && next < cfg.palette.Length) ? cfg.palette[next] : Color.white;
            nextIndicator.sprite = AnimalSpriteFactory.Get(id, col);
            nextIndicator.color = Color.white; nextIndicator.enabled = true;
        }

        public void HideProjectile()
        {
            if (proj) proj.enabled = false;
            if (nextIndicator) nextIndicator.enabled = false;
            HideAim();
        }

        void Update()
        {
            if (gm == null || cfg == null) return;
            // Сброс _gestureActive здесь критичен: нажатие по кнопке оверлея проходит, пока
            // ввод заблокирован, и НЕ регистрируется как игровой жест — иначе отпускание
            // того же касания после закрытия оверлея стало бы «фантомным» выстрелом.
            if (gm.State != GameState.Aiming || firing || gm.InputBlocked)
            { HideAim(); _gestureOverUI = false; _gestureActive = false; return; }
            if (!InputService.HasPointer) { HideAim(); return; }

            if (InputService.PressedThisFrame)
            {
                _gestureActive = true;
                _gestureOverUI = PointerOverUI();
            }

            Vector3 o = Origin;
            Vector3 dir = InputService.WorldPos(cam) - o;
            if (dir.y < 0.15f) dir.y = 0.15f; // не целимся вниз
            dir.Normalize();

            // Прицел виден при удержании (тач) или наведении мыши (десктоп); жест над UI
            // игнорируем. Hover считаем только без тачскрина — на мобильных браузерах
            // эмулированная мышь иначе «залипала» бы прицелом без касания.
            bool isMouse = UnityEngine.InputSystem.Mouse.current != null &&
                           UnityEngine.InputSystem.Pointer.current == UnityEngine.InputSystem.Mouse.current &&
                           UnityEngine.InputSystem.Touchscreen.current == null;
            bool aiming = (InputService.IsPressed || isMouse) && !_gestureOverUI;
            bool released = InputService.ReleasedThisFrame;

            // Симулируем только когда результат реально нужен и направление сменилось
            // (кэш см. объявление _simDir/_simValid выше). Конечную ячейку игроку не
            // показываем — в траектории есть лёгкая случайность, это было бы подсказкой.
            List<Vector3> path = null;
            Vector2Int cell = default;
            if (aiming || released)
            {
                if (!_simValid || dir != _simDir)
                {
                    _simPath = sim.Simulate(o, dir, out _simCell);
                    _simDir = dir; _simValid = true;
                }
                path = _simPath; cell = _simCell;
            }

            if (aiming) ShowAim(path); else HideAim();

            // Выстрел — на отпускании, а не на нажатии; валиден только если жест начался
            // в игре и не над UI (см. комментарий про _gestureActive выше).
            if (released)
            {
                bool validShot = _gestureActive && !_gestureOverUI;
                _gestureActive = false;
                if (validShot)
                {
                    HideAim();
                    AudioService.PlayShoot();
                    _iceToBreak.Clear();
                    _iceToBreak.AddRange(sim.IceHits);   // лёд, задетый рикошетом — разбить в полёте
                    StartCoroutine(Fire(path, cell));
                }
            }
        }

        // Не стреляем, если палец над uGUI-элементом (кнопка «Карта», модалка Win/Lose).
        // RaycastAll — единственный надёжный способ: IsPointerOverGameObject(deviceId)
        // не работает с InputSystemUIInputModule (deviceId ≠ pointerId = -1 / finger-index).
        static readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        static bool PointerOverUI()
        {
            if (EventSystem.current == null) return false;
            var ptr = UnityEngine.InputSystem.Pointer.current;
            if (ptr == null) return false;
            var ped = new PointerEventData(EventSystem.current)
                { position = ptr.position.ReadValue() };
            _uiHits.Clear();
            EventSystem.current.RaycastAll(ped, _uiHits);
            return _uiHits.Count > 0;
        }

        IEnumerator Fire(List<Vector3> path, Vector2Int cell)
        {
            firing = true;
            gm.SetResolving();
            int shotId = curSpecial >= 0 ? curSpecial : current;
            float iceBreakSq = cfg.Diameter * 0.95f * (cfg.Diameter * 0.95f);

            // try/finally: сброс firing даже при исключении в Place/OnResolved —
            // иначе пушка осталась бы навсегда заблокированной (фриз ввода).
            try
            {
                Vector3 pos = path[0];
                proj.transform.position = pos;

                for (int i = 1; i < path.Count; i++)
                {
                    Vector3 target = path[i];
                    while ((target - pos).sqrMagnitude > 0.0001f)
                    {
                        pos = Vector3.MoveTowards(pos, target, Speed * Time.deltaTime);
                        proj.transform.position = pos;
                        // лёд разбивается ПОЧТИ СРАЗУ при касании в полёте (не в конце)
                        for (int k = _iceToBreak.Count - 1; k >= 0; k--)
                            if ((board.CellToWorld(_iceToBreak[k]) - pos).sqrMagnitude < iceBreakSq)
                            { board.BreakObstacle(_iceToBreak[k]); _iceToBreak.RemoveAt(k); }
                        if ((target - pos).sqrMagnitude <= 0.0001f) break;
                        yield return null;
                    }
                }

                for (int k = 0; k < _iceToBreak.Count; k++) board.BreakObstacle(_iceToBreak[k]);
                _iceToBreak.Clear();

                int removed = board.Place(cell, shotId);
                AudioService.PlayLand();
                gm.OnResolved(removed);
            }
            finally
            {
                firing = false;
            }
        }

        void EnsureDots()
        {
            if (dots != null) return;
            dots = new List<SpriteRenderer>();
            for (int i = 0; i < 22; i++)
            {
                var g = new GameObject("Dot");
                g.transform.SetParent(transform);
                g.transform.localScale = Vector3.one * cfg.Diameter * 0.28f;
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Circle();
                sr.color = new Color(0.25f, 0.22f, 0.20f, 0.55f);
                sr.sortingOrder = 0;
                sr.enabled = false;
                dots.Add(sr);
            }
        }

        void ShowAim(List<Vector3> path)
        {
            EnsureDots();
            float spacing = cfg.CellSize * 0.55f;
            int di = 0;
            float carry = spacing * 0.5f;

            for (int i = 0; i < path.Count - 1 && di < dots.Count; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                float seg = (b - a).magnitude;
                if (seg < 1e-4f) continue;
                Vector3 dir = (b - a) / seg;
                float t = carry;
                while (t <= seg && di < dots.Count)
                {
                    dots[di].transform.position = a + dir * t;
                    dots[di].enabled = true;
                    di++;
                    t += spacing;
                }
                carry = t - seg;
            }

            for (int i = di; i < dots.Count; i++) dots[i].enabled = false;
        }

        void HideAim()
        {
            if (dots == null) return;
            foreach (var d in dots) if (d) d.enabled = false;
        }
    }
}
