using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>Все настройки прототипа в одном месте.</summary>
    public class GameConfig
    {
        public int columns = 11;          // ячеек в чётном (широком) ряду
        // initialRows / colorCount / startingShots перезаписываются из LevelCatalog
        // в GameManager.StartLevel(); значения ниже — дефолт уровня 1.
        public int initialRows = 4;
        public int colorCount = 3;
        public int startingShots = 35;
        public int maxRows      = 11;     // линия смерти; +1 ряд запаса

        public float boardWidth = 9f;
        public float topRowY = 4.0f;
        public float shooterY = -4.4f;

        public Color background = new Color(0.96f, 0.93f, 0.87f);

        // Заливает и колонку, и поля вокруг неё на десктопе (см. CameraFitter/SkyCamera).
        public Color bgColor = new Color(0.960f, 0.940f, 0.902f);

        // ID спецпузырей — вне диапазона 0..colorCount-1
        public const int RainbowId = 8;
        public const int BombId    = 9;

        // ID препятствий (не матчатся, не считаются в цель уровня)
        public const int RockId  = 10;  // РИКОШЕТ, неразрушимый (только бомба сносит)
        public const int IceId   = 11;  // РИКОШЕТ, разбивается от первого касания
        public const int SlimeId = 12;  // ЛИПКИЙ (шар прилипает), рушится соседним попом (hp 2)

        public static bool IsAnimal(int id)   => id >= 0 && id < RainbowId;
        public static bool IsObstacle(int id) => id >= RockId;

        /// <summary>Камень/лёд — отражают снаряд (рикошет), к ним не прилипают шары.</summary>
        public static bool IsReflector(int id) => id == RockId || id == IceId;
        /// <summary>Слайм — липкий: снаряд останавливается рядом и прилипает.</summary>
        public static bool IsSticky(int id) => id == SlimeId;
        /// <summary>Рушится от соседнего попа (slime hp 2). Лёд ломается касанием отдельно.</summary>
        public static bool IsDestructible(int id) => id == SlimeId;

        /// <summary>Стартовое hp препятствия (−1 = неразрушимо соседним попом).</summary>
        public static int ObstacleHp(int id) => id == SlimeId ? 2 : -1;

        public Color[] palette =
        {
            new Color(0.97f, 0.34f, 0.40f), // 0 сочный коралл-красный
            new Color(1.00f, 0.74f, 0.16f), // 1 золотой янтарь
            new Color(0.38f, 0.82f, 0.42f), // 2 сочный зелёный
            new Color(0.24f, 0.64f, 0.97f), // 3 яркий голубой
            new Color(0.72f, 0.42f, 0.95f), // 4 насыщенный фиолет
            new Color(1.00f, 0.50f, 0.80f), // 5 карамельный розовый
            new Color(0.94f, 0.94f, 0.97f), // 6 светлый (панда)
            new Color(0.92f, 0.66f, 0.38f), // 7 тёплый карамельный (собачка)
        };

        // производные величины, кэшируются при первом обращении
        float _cellSize, _radius, _diameter, _rowHeight, _halfWidth, _reflectX;
        bool  _derived;

        void ComputeDerived()
        {
            if (_derived) return;
            _cellSize  = boardWidth / columns;
            _radius    = _cellSize * 0.48f;
            _diameter  = _radius   * 2f;
            _rowHeight = _cellSize * 0.866f;
            _halfWidth = boardWidth * 0.5f;
            _reflectX  = _halfWidth - _radius;
            _derived   = true;
        }

        public float CellSize  { get { ComputeDerived(); return _cellSize;  } }
        public float Radius    { get { ComputeDerived(); return _radius;    } }
        public float Diameter  { get { ComputeDerived(); return _diameter;  } }
        public float RowHeight { get { ComputeDerived(); return _rowHeight; } }
        public float HalfWidth { get { ComputeDerived(); return _halfWidth; } }
        public float ReflectX  { get { ComputeDerived(); return _reflectX;  } }
        public float CeilingY  => topRowY; // не кэшируем — topRowY может меняться
    }
}
