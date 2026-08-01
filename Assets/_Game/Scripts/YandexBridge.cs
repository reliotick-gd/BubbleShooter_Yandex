using System;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace CozyAnimalTown
{
    /// <summary>
    /// Мост между C# и Yandex Games SDK v2 (через jslib + SendMessage).
    /// В редакторе все вызовы эмулируются мгновенно.
    /// GameObject должен называться "YandexBridge" — это таргет для SendMessage из JS.
    /// </summary>
    public class YandexBridge : MonoBehaviour
    {
        public static YandexBridge Instance { get; private set; }

        public static bool SDKReady { get; private set; }

        // true, пока показан рекламный оверлей. GameManager не трогает GameplayAPI, пока
        // это true — паузой геймплея на время ad управляет сам мост (OnAdOpen/OnAdClose).
        public static bool AdShowing { get; private set; }

        // ISO 639-1 ("ru"/"en"/"tr"…). UI-тексты берут язык синхронно через Loc до
        // постройки UI — здесь он хранится для аналитики и поздних подписчиков.
        public static string Lang { get; private set; } = "en";
        public static event Action<string> LangEvent;

        // Имя лидерборда в консоли разработчика Яндекса; рекорд = сумма звёзд (Progress.TotalStars).
        // Таблица заведена заново: в старой bbShTablica остались значения из эпохи очков
        // (четырёхзначные), и они навсегда висели бы выше любого честного результата,
        // максимум которого теперь 90 = 30 уровней × 3 звезды.
        public const string LeaderboardName = "BubbleShooterLadder";

        public static event Action         SDKReadyEvent;
        public static event Action<string> RewardedEvent;
        public static event Action<string> RewardedErrorEvent;
        public static event Action<bool>   InterstitialCloseEvent;
        public static event Action<string> DataLoadedEvent;
        public static event Action<string> LeaderboardLoadedEvent;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void YG_Init();
        [DllImport("__Internal")] static extern void YG_ShowRewarded(string id);
        [DllImport("__Internal")] static extern void YG_ShowInterstitial();
        [DllImport("__Internal")] static extern void YG_SaveData(string json);
        [DllImport("__Internal")] static extern void YG_LoadData();
        [DllImport("__Internal")] static extern void YG_GameplayStart();
        [DllImport("__Internal")] static extern void YG_GameplayStop();
        [DllImport("__Internal")] static extern int  YG_GameReady();   // 1 = дошло до SDK
        [DllImport("__Internal")] static extern void YG_ShowBanner();
        [DllImport("__Internal")] static extern void YG_SetLeaderboard(string name, int score);
        [DllImport("__Internal")] static extern void YG_LoadLeaderboard(string name);
#endif

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            gameObject.name = "YandexBridge"; // обязательно — SendMessage из JS ищет по имени
            DontDestroyOnLoad(gameObject);
            _userMuted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            ApplyAudio();   // мьют из прошлой сессии применяем сразу, а не с первого события
        }

        void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            YG_Init();
#else
            // редактор: SDK "готов" сразу, данные пустые, язык — системный
            OnLang(Application.systemLanguage == SystemLanguage.Russian ? "ru" : "en");
            OnSDKReady("");
            OnDataLoaded("{}");
#endif
        }

        // колбэки, которые дёргает JS через SendMessage (см. ReSharper disable ниже)
        // ReSharper disable UnusedMember.Local
        void OnSDKReady(string _)
        {
            SDKReady = true;
            // Sticky-баннер включаем сразу и навсегда — платформа сама размещает его вне
            // игрового canvas, макет 9:16 не страдает.
#if UNITY_WEBGL && !UNITY_EDITOR
            YG_ShowBanner();
            // Титул мог появиться раньше, чем SDK: тогда GameReady() отработал вхолостую
            // и _readySent остался false. Теперь ysdk на месте — досылаем готовность.
            GameReady();
#endif
            SDKReadyEvent?.Invoke();
        }

        void OnLang(string lang)
        {
            if (!string.IsNullOrEmpty(lang)) Lang = lang;
            LangEvent?.Invoke(Lang);
        }

        // Требования Яндекса: звук+геймплей на паузе во время полноэкранной рекламы,
        // звук стоп при сворачивании вкладки; плюс свой мьют-тумблер (у Яндекса нет
        // платформенной кнопки mute). _adDepth защищает от несимметричных колбэков
        // (onOpen без onClose и наоборот).
        int _adDepth;
        bool _resumeGameplayAfterAd;

        const string MuteKey = "cat_muted";
        static bool _userMuted;
        static bool _pageHidden;       // вкладка свёрнута/не видна (visibilitychange из JS)

        public static bool UserMuted => _userMuted;

        /// <summary>GameManager не копит playtime-хартбиты, пока это true.</summary>
        public static bool PageHidden => _pageHidden;

        public static void SetUserMuted(bool muted)
        {
            _userMuted = muted;
            PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
            PlayerPrefs.Save();   // WebGL: без Save() запись может не доехать до IndexedDB
            Instance?.ApplyAudio();
        }

        void ApplyAudio()
        {
            bool muted = AdShowing || _pageHidden || _userMuted;
            AudioListener.pause  = muted;
            AudioListener.volume = muted ? 0f : 1f;
        }

        // Событие приходит из visibilitychange в index.html; заодно стопим GameplayAPI.
        bool _resumeGameplayAfterHide;

        void OnVisibilityChanged(string v)
        {
            bool hidden = v == "1";
            if (hidden == _pageHidden) return;
            _pageHidden = hidden;
            ApplyAudio();
            if (hidden)
            {
                _resumeGameplayAfterHide = _gameplayActive;
                GameplayStop();
            }
            else if (_resumeGameplayAfterHide && !AdShowing)
            {
                GameplayStart();
            }
        }

        void OnAdOpen(string _)
        {
            if (_adDepth++ == 0) OverlayOpened();
        }

        void OnAdClose(string _)
        {
            if (_adDepth > 0 && --_adDepth == 0) OverlayClosed();
        }

        // game_api_pause/resume: платформенная стартовая реклама и диалоги. Платформа
        // не обязана слать события строго парой — свой флаг парности защищает _adDepth
        // от порчи при несимметричных вызовах.
        bool _platformPaused;

        void OnPlatformPause(string _)
        {
            if (_platformPaused) return;
            _platformPaused = true;
            if (++_adDepth == 1) OverlayOpened();
        }

        void OnPlatformResume(string _)
        {
            if (!_platformPaused) return;
            _platformPaused = false;
            if (_adDepth > 0 && --_adDepth == 0) OverlayClosed();
        }

        float _tsBeforeOverlay = 1f;   // уважаем возможный timeScale=0 паузы игры

        // Сторож зависшего оверлея. OverlayOpened ставит timeScale = 0 и глушит звук,
        // а снимается это только из OverlayClosed. Если SDK прислал onOpen (или
        // game_api_pause) и не прислал парного события — например рекламный iframe
        // умер или диалог закрыли навигацией, — игра остаётся замороженной навсегда,
        // до перезагрузки страницы. Страховка в GameManager.Update тут не спасает:
        // она специально отодвигает дедлайн, пока AdShowing == true.
        // 180 секунд с запасом перекрывают самый длинный ролик.
        const float OverlayWatchdog = 180f;
        float _overlayOpenedAt = -1f;

        void Update()
        {
            if (!AdShowing || _overlayOpenedAt < 0f) return;
            if (Time.realtimeSinceStartup - _overlayOpenedAt < OverlayWatchdog) return;
            Debug.LogWarning("[YG] оверлей висит дольше " + OverlayWatchdog + " с — снимаем принудительно");
            _adDepth = 0;
            _platformPaused = false;
            OverlayClosed();
        }

        void OverlayOpened()
        {
            AdShowing = true;
            _overlayOpenedAt = Time.realtimeSinceStartup;
            ApplyAudio();
            _tsBeforeOverlay = Time.timeScale;
            Time.timeScale = 0f;
            _resumeGameplayAfterAd = _gameplayActive;
            GameplayStop();
        }

        void OverlayClosed()
        {
            AdShowing = false;
            _overlayOpenedAt = -1f;
            ApplyAudio();   // вернёт звук, только если мьют/фоновая вкладка не активны
            Time.timeScale = _tsBeforeOverlay;
            if (_resumeGameplayAfterAd) GameplayStart();
        }

        // Идемпотентно: дубли start/stop — no-op. Зовётся из GameManager на старте/
        // финише уровня и автоматически вокруг рекламы (см. OnAdOpen/Close).
        static bool _gameplayActive;

        public static void GameplayStart()
        {
            if (_gameplayActive) return;
            _gameplayActive = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            YG_GameplayStart();
#endif
        }

        public static void GameplayStop()
        {
            if (!_gameplayActive) return;
            _gameplayActive = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            YG_GameplayStop();
#endif
        }

        // Требование Яндекса: вызывается ровно один раз, когда игра полностью
        // загрузилась и готова к взаимодействию (у нас — когда показан титульный экран).
        static bool _readySent;

        public static void GameReady()
        {
            if (_readySent) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Защёлкиваем только по факту успеха: если ysdk ещё не подъехал, попытку
            // повторит OnSDKReady. Иначе сигнал готовности терялся молча.
            _readySent = YG_GameReady() == 1;
#else
            _readySent = true;
#endif
        }

        void OnRewarded(string id)       => RewardedEvent?.Invoke(id);
        void OnRewardedError(string id)  => RewardedErrorEvent?.Invoke(id);
        void OnInterstitialClose(string v) => InterstitialCloseEvent?.Invoke(v == "1");
        void OnDataLoaded(string json)   => DataLoadedEvent?.Invoke(json);
        void OnLeaderboardLoaded(string json) => LeaderboardLoadedEvent?.Invoke(json);
        // ReSharper restore UnusedMember.Local

        public static void ShowRewarded(string callbackId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            YG_ShowRewarded(callbackId);
#else
            Instance?.OnRewarded(callbackId); // мгновенная выдача в редакторе
#endif
        }

        public static void ShowInterstitial()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            YG_ShowInterstitial();
#else
            Instance?.OnInterstitialClose("1");
#endif
        }

        public static void SaveData(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            YG_SaveData(json);
#endif
        }

        public static void LoadData()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            YG_LoadData();
#else
            Instance?.OnDataLoaded("{}");
#endif
        }

        /// <summary>Гостям сабмит недоступен — jslib молча пропускает через isAvailableMethod.</summary>
        public static void SetLeaderboardScore(int score)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            YG_SetLeaderboard(LeaderboardName, score);
#endif
        }

        public static void RequestLeaderboard()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            YG_LoadLeaderboard(LeaderboardName);
#else
            // редактор: фейковые данные (топ + разрыв + окрестность игрока) — верстать экран без билда
            Instance?.OnLeaderboardLoaded(
                "{\"userRank\":4729,\"entries\":[" +
                "{\"rank\":1,\"score\":12309162,\"name\":\"Виктор Кем\",\"avatar\":\"\",\"isUser\":false}," +
                "{\"rank\":2,\"score\":9990652,\"name\":\"Довлет Атаев\",\"avatar\":\"\",\"isUser\":false}," +
                "{\"rank\":3,\"score\":9918986,\"name\":\"Петрович\",\"avatar\":\"\",\"isUser\":false}," +
                "{\"rank\":4,\"score\":9828492,\"name\":\"Николай И.\",\"avatar\":\"\",\"isUser\":false}," +
                "{\"rank\":5,\"score\":9588890,\"name\":\"Iwuff Iwuffich\",\"avatar\":\"\",\"isUser\":false}," +
                "{\"rank\":4726,\"score\":300,\"name\":\"Дмитрий Тимохин\",\"avatar\":\"\",\"isUser\":false}," +
                "{\"rank\":4727,\"score\":294,\"name\":\"Min Sora\",\"avatar\":\"\",\"isUser\":false}," +
                "{\"rank\":4728,\"score\":294,\"name\":\"александр м.\",\"avatar\":\"\",\"isUser\":false}," +
                "{\"rank\":4729,\"score\":294,\"name\":\"Renat Protchenko\",\"avatar\":\"\",\"isUser\":true}" +
                "]}");
#endif
        }
    }
}
