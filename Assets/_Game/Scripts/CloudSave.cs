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
        // Настройки. Чек-лист площадки требует, чтобы между сессиями роумился не только
        // прогресс, но и настройки: без этого игрок, выключивший звук на телефоне,
        // на компьютере под тем же аккаунтом снова получал музыку.
        // −1 = поля не было в сейве старого формата, локальное значение не трогаем.
        public int muted     = -1;
        public int onboarded = -1;   // онбординг заново на втором устройстве раздражает
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
        const string KeyMuted     = "cat_muted";      // см. YandexBridge.MuteKey
        const string KeyOnboarded = "cat_onboarded";

        /// <summary>
        /// Отправить текущее состояние в облако, не трогая номер уровня.
        ///
        /// Нужен потому, что Save() зовётся только на смене уровня и победе, а заряды
        /// покупаются рекламой в любой момент: посмотрел ролик, закрыл вкладку не
        /// доиграв — на другом устройстве купленного нет. Сюда же уходят ежедневный
        /// подарок и переключение звука.
        ///
        /// Отдельный метод, а не Save(): тот попутно шлёт результат в таблицу лидеров,
        /// а дёргать её на каждый тык по кнопке звука незачем.
        /// </summary>
        public static void Flush()
        {
            YandexBridge.SaveData(JsonUtility.ToJson(Snapshot(PlayerPrefs.GetInt(KeyLevel, 1))));
        }

        static SaveData Snapshot(int level) => new SaveData
        {
            level     = level,
            bomb      = PlayerPrefs.GetInt(KeyBomb, 0),
            rainbow   = PlayerPrefs.GetInt(KeyRainbow, 0),
            best      = Progress.Raw,
            muted     = PlayerPrefs.GetInt(KeyMuted, 0),
            onboarded = PlayerPrefs.GetInt(KeyOnboarded, 0),
        };

        public static void Save(int level)
        {
            // Прогресс монотонно растёт: не перетираем большее значение меньшим
            // (например, если облако второго девайса легло только в prefs).
            level = Mathf.Max(level, PlayerPrefs.GetInt(KeyLevel, 1));
            PlayerPrefs.SetInt(KeyLevel, level);
            PlayerPrefs.Save();

            YandexBridge.SaveData(JsonUtility.ToJson(Snapshot(level)));

            // В таблицу — сумма звёзд, а не номер уровня: по уровню все, кто добрался
            // до конца, оказывались с одинаковым результатом.
            YandexBridge.SetLeaderboardScore(Progress.TotalStars);
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

            // Настройки — не по максимуму, а «облако побеждает, если поле там есть»:
            // мьют это осознанный выбор игрока, и брать Max означало бы, что выключить
            // звук на втором устройстве уже невозможно.
            if (cloud.muted >= 0) PlayerPrefs.SetInt(KeyMuted, cloud.muted);
            // Онбординг наоборот по максимуму: если он пройден хоть где-то, показывать
            // его заново не надо.
            if (cloud.onboarded > 0) PlayerPrefs.SetInt(KeyOnboarded, 1);
            PlayerPrefs.Save();

            // Мьют уже прочитан в Awake — после мержа его надо перечитать, иначе
            // облачная настройка ляжет в prefs, но на звук подействует только со
            // следующего запуска.
            YandexBridge.ReloadMuteFromPrefs();

            Progress.MergeRaw(cloud.best);
            return level;
        }
    }
}
