using System;
using UnityEngine;

namespace CozyAnimalTown
{
    [Serializable]
    public class SaveData
    {
        public int level = 1;
        // Заряды бустеров тоже роумятся: игрок, купивший их рекламой и сменивший
        // устройство, иначе терял купленное.
        public int bomb    = -1;   // −1 = поля не было в старом сейве, не трогаем локальное
        public int rainbow = -1;
        // Рекорды по уровням в компактном виде (см. Progress) — от них считается лидерборд.
        public string best = "";
    }

    /// <summary>
    /// Сохранение прогресса: PlayerPrefs (всегда) + Yandex player.setData (WebGL;
    /// авторизованные — в аккаунт, гости — хранилище платформы).
    /// Меты/монет/жизней больше нет — роумится только номер уровня.
    /// </summary>
    public static class CloudSave
    {
        const string KeyLevel     = "cat_level";
        const string KeyCloudWipe = "cat_cloud_wipe";
        const string KeyBomb      = "cat_bomb";
        const string KeyRainbow   = "cat_rainbow";

        public static void Save(int level)
        {
            // Прогресс монотонно растёт: не перетираем большее значение меньшим
            // (например, если облако второго девайса легло только в prefs).
            level = Mathf.Max(level, PlayerPrefs.GetInt(KeyLevel, 1));
            PlayerPrefs.SetInt(KeyLevel, level);
            PlayerPrefs.Save();

            YandexBridge.SaveData(JsonUtility.ToJson(new SaveData
            {
                level   = level,
                bomb    = PlayerPrefs.GetInt(KeyBomb, 0),
                rainbow = PlayerPrefs.GetInt(KeyRainbow, 0),
                best    = Progress.Raw,
            }));

            // В таблицу — сумма лучших результатов, а не номер уровня: по уровню все,
            // кто добрался до конца, оказывались с одинаковым результатом.
            YandexBridge.SetLeaderboardScore(Progress.TotalScore);
        }

        // Бамп SaveVer стирает локальный прогресс (GameBootstrap), но облако при обычном
        // мерже тут же вернуло бы старый уровень. Флаг говорит: первый ответ облака
        // НЕ мержить, а перезаписать свежим (сброшенным) состоянием.
        public static bool PendingCloudWipe => PlayerPrefs.GetInt(KeyCloudWipe, 0) == 1;

        public static void MarkCloudWipe()
        {
            PlayerPrefs.SetInt(KeyCloudWipe, 1);
            PlayerPrefs.Save();
        }

        public static void ClearCloudWipe()
        {
            PlayerPrefs.DeleteKey(KeyCloudWipe);
            PlayerPrefs.Save();
        }

        public static void RequestLoad() => YandexBridge.LoadData();

        /// <summary>Применяет облачные данные к PlayerPrefs; облако побеждает только если его уровень выше локального.</summary>
        public static int MergeWithLocal(string json)
        {
            SaveData cloud;
            try { cloud = JsonUtility.FromJson<SaveData>(json) ?? new SaveData(); }
            catch { cloud = new SaveData(); }

            int localLevel = PlayerPrefs.GetInt(KeyLevel, 1);
            int level = Mathf.Max(localLevel, cloud.level);
            PlayerPrefs.SetInt(KeyLevel, level);

            // Заряды и рекорды сливаем по максимуму — так пересадка между устройствами
            // ничего не отнимает. Поля bomb/rainbow = −1 у сейвов старого формата.
            if (cloud.bomb    >= 0) PlayerPrefs.SetInt(KeyBomb,    Mathf.Max(PlayerPrefs.GetInt(KeyBomb, 0),    cloud.bomb));
            if (cloud.rainbow >= 0) PlayerPrefs.SetInt(KeyRainbow, Mathf.Max(PlayerPrefs.GetInt(KeyRainbow, 0), cloud.rainbow));
            PlayerPrefs.Save();

            Progress.MergeRaw(cloud.best);
            return level;
        }
    }
}
