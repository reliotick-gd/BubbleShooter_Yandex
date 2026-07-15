using System;

namespace CozyAnimalTown
{
    [Serializable]
    public struct ObstacleCell
    {
        public int q;      // столбец
        public int r;      // ряд
        public int type;   // 0 = лёд, 1 = слайм, 2 = камень (по возрастанию сложности)
    }

    /// <summary>Описание уровня. Шары красятся процедурно, препятствия — из данных.</summary>
    [Serializable]
    public struct LevelDef
    {
        public int index;
        public int rows;
        public int colorCount;
        public int shots;
        public ObstacleCell[] obstacles;

        public static int TypeToId(int type) => type switch
        {
            1 => GameConfig.SlimeId,
            2 => GameConfig.RockId,
            _ => GameConfig.IceId,
        };
    }
}
