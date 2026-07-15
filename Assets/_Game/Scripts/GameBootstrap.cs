using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Точка входа. Запускается автоматически после загрузки сцены — поэтому
    /// игру не нужно собирать в редакторе руками: открыл проект, нажал Play.
    /// Работает в любой сцене (в т.ч. в дефолтной SampleScene).
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<GameManager>() != null) return;

            // ⚠️ ПОСЛЕ РЕЛИЗА НЕ БАМПАТЬ SaveVer: бамп стирает прогресс у ВСЕХ игроков
            // (локально и в облаке через cat_cloud_wipe) без отката. Для изменений формата
            // сейва — мигрировать данные, а не удалять ключи.
            const int SaveVer = 5;
            if (PlayerPrefs.GetInt("cat_save_ver", 0) < SaveVer)
            {
                foreach (var k in new[] { "cat_level", "cat_bomb", "cat_rainbow",
                    "cat_onboarded", "cat_seen_ice", "cat_seen_slime", "cat_seen_rock",
                    "cat_endless_sent" })
                    PlayerPrefs.DeleteKey(k);
                PlayerPrefs.SetInt("cat_save_ver", SaveVer);
                PlayerPrefs.Save();
                // облако тоже надо перезаписать, иначе мерж вернёт стёртый прогресс
                CloudSave.MarkCloudWipe();
            }

            // должно быть готово до первого текста
            DynamicFont.Ensure();
            UiSymbols.Init();

            var cfg = new GameConfig();

            var cam = Camera.main;
            if (cam == null)
            {
                var cg = new GameObject("Main Camera");
                cam = cg.AddComponent<Camera>();
                cg.tag = "MainCamera";
            }

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cfg.background;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;

            if (cam.GetComponent<CameraFitter>() == null)
                cam.gameObject.AddComponent<CameraFitter>().Init(cfg);

            // YandexBridge создаём раньше GameManager — cloud save нужен при Init
            if (Object.FindAnyObjectByType<YandexBridge>() == null)
                new GameObject("YandexBridge").AddComponent<YandexBridge>();

            // прогрев фабрик до тайтл-скрина, чтобы первый уровень не давал заморозку
            Prewarm(cfg);

            var titleGo = new GameObject("TitleScreen");
            titleGo.AddComponent<TitleScreen>().Show(() =>
            {
                var root = new GameObject("GameRoot");
                root.AddComponent<GameManager>().Init(cfg, cam);
            });
            // LoadingAPI.ready() зовёт сам TitleScreen ровно в момент появления
            // титула на экране (п. 1.19 Яндекса) — не отсюда.
        }
        static void Prewarm(GameConfig cfg)
        {
            // бомба/радуга теперь бонусы, но их спрайты всё равно нужны
            for (int i = 0; i < cfg.palette.Length; i++)
                AnimalSpriteFactory.Get(i, cfg.palette[i]);
            AnimalSpriteFactory.Get(GameConfig.RainbowId, Color.white);
            AnimalSpriteFactory.Get(GameConfig.BombId,    Color.white);

            SpriteFactory.Circle();
            AudioService.Prewarm();
        }
    }
}
