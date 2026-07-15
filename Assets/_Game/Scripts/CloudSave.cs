using System;
using UnityEngine;

namespace CozyAnimalTown
{
    [Serializable]
    public class SaveData
    {
        public int level = 1;
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

        public static void Save(int level)
        {
            // Прогресс монотонно растёт: не перетираем большее значение меньшим
            // (например, если облако второго девайса легло только в prefs).
            level = Mathf.Max(level, PlayerPrefs.GetInt(KeyLevel, 1));
            PlayerPrefs.SetInt(KeyLevel, level);
            PlayerPrefs.Save();
            YandexBridge.SaveData(JsonUtility.ToJson(new SaveData { level = level }));
            YandexBridge.SetLeaderboardScore(level);
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
            PlayerPrefs.Save();
            return level;
        }
    }
}
