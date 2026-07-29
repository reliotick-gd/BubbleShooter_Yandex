using UnityEngine;

namespace CozyAnimalTown
{
    public enum GameState { Aiming, Resolving, Win, Lose }

    /// <summary>
    /// Оркестратор сессии: старт уровня → прицел/выстрел → победа/проигрыш.
    /// Меты, валюты и жизней нет — прогресс это номер уровня (роумится облаком).
    /// Спецшары (бомба/радуга) — не в генерации, а заряды бонусов снизу.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        GameConfig     cfg;
        Camera         cam;
        HexGrid        grid;
        BoardManager   board;
        TrajectorySimulator sim;
        Shooter        shooter;
        Hud            hud;

        int shotsLeft;
        int currentLevel;
        int attemptsThisLevel;
        int lastStartedLevel = -1;
        float _levelStartTime;        // для duration_s в аналитике уровня
        LevelDef levelDef;

        // Свой кулдаун поверх платформенного лимита — модерация требует показов
        // только в логических паузах. 90с — стартовое значение, ещё не A/B-тюнили.
        const float InterstitialCooldown = 90f;
        float _lastInterstitialTime = -999f;

        // комбо-цепочка (чистый визуальный «сок», без очков)
        int   _combo;
        float _lastPopTime = -999f;
        const float ComboWindow = 1.8f;

        bool _loseByOverflow;
        bool _paused;
        bool _titleOpen;
        bool _lbOpen;      // открыт экран таблицы лидеров
        bool _onboarding;
        // rewarded-воскрешение: до 2 раз за попытку — Яндекс не режет частоту rewarded,
        // а между шансами всегда проходит целый проигрыш (> 30с лимита).
        const int MaxSecondChances = 2;
        int  _secondChanceCount;
        bool _adBusy;             // rewarded уже запрошен — глушим дабл-клик по кнопке
        float _adBusyDeadline;    // страховка: SDK не прислал ни одного колбэка
        string _adPlacement = "";  // плейсмент текущего rewarded (для аналитики/таймаута)
        bool _adShownSent;
        float _activePlaySec;     // честный playtime: без рекламы и фоновой вкладки
        int  _pendingCloudLevel;  // облако пришло на тронутом уровне → применим на след. старте

        public bool SecondChanceAvailable => _secondChanceCount < MaxSecondChances;

        /// <summary>Доступен после исчерпания «вторых шансов» — Яндекс разрешает пропуск уровня как rewarded-награду.</summary>
        public bool SkipLevelAvailable => _secondChanceCount >= MaxSecondChances;

        /// <summary>Онбординг проигрывает показательный жест — блокируем ввод пушки.</summary>
        public void SetOnboarding(bool v) => _onboarding = v;

        /// <summary>Спрайт текущего боевого шара (демо-шар онбординга ему соответствует).</summary>
        public Sprite CurrentBallSprite => shooter != null ? shooter.CurrentSprite : null;

        public const int BonusStart = 10;                // выдаётся при разблокировке
        public const int RainbowUnlockLevel = 3;
        public const int BombUnlockLevel    = 7;

        // Размеры наград за rewarded вынесены в константы: их дословно называют кнопки и
        // диалог подтверждения. п.4.5.1 Яндекса требует, чтобы игрок ЗАРАНЕЕ видел, что
        // именно и сколько он получит — а текст не должен разъезжаться с реальной выдачей.
        public const int RefillAmount      = 3;   // зарядов бомбы/радуги за ролик
        public const int SecondChanceShots = 5;   // выстрелов за «Второй шанс»
        public const int OverflowClearRows = 3;   // рядов снизу, если проиграл по переполнению
        const string KeyBomb    = "cat_bomb";
        const string KeyRainbow = "cat_rainbow";
        int bombCharges, rainbowCharges;

        public int BombCharges    => bombCharges;
        public int RainbowCharges => rainbowCharges;
        public bool RainbowUnlocked => currentLevel >= RainbowUnlockLevel;
        public bool BombUnlocked    => currentLevel >= BombUnlockLevel;

        public GameState State   { get; private set; }
        public int  ShotsLeft    => shotsLeft;
        public int  BubbleCount  => board != null ? board.PoppableCount : 0;
        public int  CurrentLevel => currentLevel;
        public LevelDef LevelDef => levelDef;
        public bool LoseByOverflow => _loseByOverflow;
        public bool IsPaused     => _paused;

        /// <summary>Модальный оверлей открыт (пауза/титул/лидерборд/итоги/реклама) — пушка не стреляет «сквозь».</summary>
        public bool InputBlocked => _paused || _titleOpen || _lbOpen || _onboarding
            || YandexBridge.AdShowing || State == GameState.Win || State == GameState.Lose;

        /// <summary>Стрелка «назад»: показать вступительный экран поверх игры.
        /// Выход в «меню» — естественная пауза, здесь тоже показываем interstitial.</summary>
        public void OpenTitle()
        {
            if (_titleOpen) return;
            _titleOpen = true;
            Analytics.BackToTitle(currentLevel);
            TryShowInterstitial();
            new GameObject("TitleScreen").AddComponent<TitleScreen>().Show(() => _titleOpen = false);
        }

        public void OpenLeaderboard()
        {
            if (_lbOpen || YandexBridge.AdShowing) return;
            _lbOpen = true;
            Analytics.LeaderboardOpen(currentLevel);
            new GameObject("LeaderboardScreen").AddComponent<LeaderboardScreen>()
                .Show(() => _lbOpen = false);
        }

        public void Init(GameConfig cfg, Camera cam)
        {
            this.cfg = cfg;
            this.cam = cam;

            Analytics.SessionStart(PlayerPrefs.GetInt("cat_level", 1), YandexBridge.UserMuted);

            // Музыку стартуем после тапа «Играть» (жест игрока разблокирует WebAudio).
            MusicService.Play();

            var poolGo = new GameObject("BubblePool");
            poolGo.transform.SetParent(transform);
            poolGo.AddComponent<BubblePool>();

            var pfGo = new GameObject("PopFeedback");
            pfGo.transform.SetParent(transform);
            pfGo.AddComponent<PopFeedback>();

            var ppGo = new GameObject("PopParticles");
            ppGo.transform.SetParent(transform);
            ppGo.AddComponent<PopParticles>();

            var cdGo = new GameObject("ComboDisplay");
            cdGo.transform.SetParent(transform);
            cdGo.AddComponent<ComboDisplay>();

            var ltGo = new GameObject("LevelTransition");
            ltGo.transform.SetParent(transform);
            ltGo.AddComponent<LevelTransition>();

            grid = new HexGrid(cfg);

            var bgo = new GameObject("Board");
            bgo.transform.SetParent(transform);
            board = bgo.AddComponent<BoardManager>();
            board.Init(cfg, grid);

            sim = new TrajectorySimulator(cfg, grid, board);

            var sgo = new GameObject("Shooter");
            sgo.transform.SetParent(transform);
            shooter = sgo.AddComponent<Shooter>();
            shooter.Init(cfg, cam, this, board, sim);

            BuildBoardPanel();

            var hgo = new GameObject("Hud");
            hgo.transform.SetParent(transform);
            hud = hgo.AddComponent<Hud>();
            hud.Init(this);

            YandexBridge.DataLoadedEvent        += OnCloudDataLoaded;
            YandexBridge.SDKReadyEvent          += OnSDKReady;
            YandexBridge.InterstitialCloseEvent += OnInterstitialClosed;

            currentLevel   = PlayerPrefs.GetInt("cat_level", 1);
            RefreshBonusUnlocks();

            if (YandexBridge.SDKReady) CloudSave.RequestLoad();

            StartLevel();

            if (currentLevel == 1 && PlayerPrefs.GetInt("cat_onboarded", 0) == 0)
                new GameObject("Onboarding").AddComponent<Onboarding>().Begin(this);
        }

        void OnDestroy()
        {
            YandexBridge.DataLoadedEvent        -= OnCloudDataLoaded;
            YandexBridge.SDKReadyEvent          -= OnSDKReady;
            YandexBridge.InterstitialCloseEvent -= OnInterstitialClosed;
            YandexBridge.RewardedEvent      -= OnRewarded;
            YandexBridge.RewardedErrorEvent -= OnRewardedError;
        }

        // GameplayAPI: геймплей «идёт» только в Aiming/Resolving без оверлея.
        void Update()
        {
            // Копим только «честное» playtime: видимая вкладка, без рекламы (realtime
            // завысил бы простоем). Клэмп гасит скачок dt после возврата из фона.
            if (!YandexBridge.AdShowing && !YandexBridge.PageHidden)
            {
                _activePlaySec += Mathf.Min(Time.unscaledDeltaTime, 0.5f);
                Analytics.Heartbeat(_activePlaySec);
            }

            // Страховка: rewarded запрошен, а SDK молчит (ни open, ни error) — не
            // оставляем кнопки мёртвыми до конца попытки. Пока оверлей на экране,
            // дедлайн отодвигается (игрок может смотреть видео долго).
            if (_adBusy)
            {
                // оверлей реально открылся → ad_shown (воронка request→shown→rewarded)
                if (YandexBridge.AdShowing && !_adShownSent)
                {
                    _adShownSent = true;
                    Analytics.AdShown(_adPlacement);
                }
                if (YandexBridge.AdShowing)
                    _adBusyDeadline = Time.realtimeSinceStartup + 60f;
                else if (Time.realtimeSinceStartup > _adBusyDeadline)
                {
                    // разлочиваем кнопки, но подписку НЕ трогаем: если реклама откроется
                    // позже (медленный филл), награда всё равно применится, а не потеряется.
                    _adBusy = false;
                    Analytics.AdError(_adPlacement, "timeout");
                }
            }

            if (YandexBridge.AdShowing) return;
            bool playing = (State == GameState.Aiming || State == GameState.Resolving) && !InputBlocked;
            if (playing) YandexBridge.GameplayStart();
            else         YandexBridge.GameplayStop();
        }

        void OnSDKReady() => CloudSave.RequestLoad();

        void OnCloudDataLoaded(string json)
        {
            // бамп SaveVer: локальный сброс уже сделан — облако не мержим,
            // а перезаписываем свежим состоянием (иначе вернётся стёртый прогресс)
            if (CloudSave.PendingCloudWipe)
            {
                CloudSave.ClearCloudWipe();
                SaveProgress();
                return;
            }
            // MergeWithLocal пишет max(local,cloud) в prefs — значение прогресса
            // сохранено при любом раскладе (Save тоже делает max, откат исключён).
            int level = CloudSave.MergeWithLocal(json);
            if (level > currentLevel)
            {
                if (State == GameState.Aiming && shotsLeft == cfg.startingShots)
                {
                    // уровень ещё не тронут → применяем облако сразу
                    currentLevel = level;
                    board.Clear();
                    StartLevel();
                }
                else
                {
                    // уровень уже играется — не выдёргиваем игрока; догоним прогресс
                    // на следующем старте (после победы/рестарта), а не только по перезагрузке
                    _pendingCloudLevel = level;
                }
            }
        }

        void StartLevel()
        {
            // догоняем облачный прогресс, если он пришёл, пока игрался прошлый уровень
            if (_pendingCloudLevel > currentLevel) currentLevel = _pendingCloudLevel;
            _pendingCloudLevel = 0;

            _loseByOverflow    = false;
            _secondChanceCount = 0;
            _adBusy            = false;
            levelDef           = LevelCatalog.Get(currentLevel);
            cfg.initialRows   = levelDef.rows;
            cfg.colorCount    = levelDef.colorCount;
            cfg.startingShots = levelDef.shots;

            board.BuildLevel(levelDef);
            RefreshBonusUnlocks();
            shotsLeft  = cfg.startingShots;
            _combo       = 0;
            _lastPopTime = -999f;
            shooter.ResetQueue();
            shooter.Reload();
            State = GameState.Aiming;
            YandexBridge.GameplayStart();

            if (currentLevel == lastStartedLevel) attemptsThisLevel++;
            else { attemptsThisLevel = 1; lastStartedLevel = currentLevel; }

            _levelStartTime = Time.realtimeSinceStartup;
            Analytics.LevelStart(currentLevel, attemptsThisLevel);
            MaybeShowObstacleHint();
        }

        // Первое появление препятствия данного типа → мини-подсказка (один тип за уровень).
        void MaybeShowObstacleHint()
        {
            if (levelDef.obstacles == null || levelDef.obstacles.Length == 0) return;
            int id = 0; string txt = null; string kind = null;
            if      (HasObstacle(GameConfig.IceId)   && TrySee("cat_seen_ice"))   { id = GameConfig.IceId;   kind = "ice";   txt = Loc.T("Ice bounces the shot!", "Лёд отражает выстрел!"); }
            else if (HasObstacle(GameConfig.SlimeId) && TrySee("cat_seen_slime")) { id = GameConfig.SlimeId; kind = "slime"; txt = Loc.T("Bubbles stick to slime", "Шарики липнут к слизи"); }
            else if (HasObstacle(GameConfig.RockId)  && TrySee("cat_seen_rock"))  { id = GameConfig.RockId;  kind = "rock";  txt = Loc.T("Rock bounces the shot", "Камень отражает выстрел"); }
            else return;
            Analytics.HintShown(kind);

            // позиция первого препятствия этого типа → стрелка/подсветка от подсказки к нему
            Vector2 pos = Vector2.zero;
            foreach (var o in levelDef.obstacles)
                if (LevelDef.TypeToId(o.type) == id)
                {
                    pos = CameraFitter.WorldToDesign(grid.CellToWorld(new Vector2Int(o.q, o.r)), cfg);
                    break;
                }
            new GameObject("ObstacleHint").AddComponent<ObstacleHint>().Show(id, txt, pos, this);
        }

        bool HasObstacle(int id)
        {
            foreach (var o in levelDef.obstacles) if (LevelDef.TypeToId(o.type) == id) return true;
            return false;
        }

        static bool TrySee(string key)
        {
            if (PlayerPrefs.GetInt(key, 0) != 0) return false;
            PlayerPrefs.SetInt(key, 1); PlayerPrefs.Save();
            return true;
        }

        public void NextLevel()
        {
            TryShowInterstitial();
            LevelTransition.Instance?.Play(() =>
            {
                currentLevel = Mathf.Min(currentLevel + 1, LevelCatalog.Count);
                // выход за 30 ручных уровней = бесконечный режим (один раз за игрока)
                if (currentLevel == 31 && PlayerPrefs.GetInt("cat_endless_sent", 0) == 0)
                {
                    PlayerPrefs.SetInt("cat_endless_sent", 1); PlayerPrefs.Save();
                    Analytics.EndlessReached();
                }
                SaveProgress();
                board.Clear();
                StartLevel();
            });
        }

        /// <summary>Рестарт уровня — естественная пауза, показываем interstitial здесь
        /// (rewarded живёт на отдельной кнопке «Второй шанс»).</summary>
        public void Retry()
        {
            // отказ от предложенной rewarded-кнопки = нажал «Ещё раз», пока она была живой
            if (State == GameState.Lose)
            {
                if (SecondChanceAvailable)   Analytics.SecondChanceDeclined(currentLevel);
                else if (SkipLevelAvailable) Analytics.SkipLevelDeclined(currentLevel);
            }
            Analytics.LevelRetry(currentLevel, attemptsThisLevel);
            TryShowInterstitial();
            LevelTransition.Instance?.Play(() =>
            {
                board.Clear();
                StartLevel();
            });
        }

        /// <summary>Полный сброс прогресса (для теста): уровень→1, онбординг/подсказки покажутся заново.</summary>
        public void ResetProgress()
        {
            foreach (var k in new[] { "cat_level", "cat_bomb", "cat_rainbow", "cat_onboarded",
                "cat_seen_ice", "cat_seen_slime", "cat_seen_rock", "cat_endless_sent" })
                PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();

            currentLevel = 1;
            lastStartedLevel = -1;
            RefreshBonusUnlocks();
            _paused = false; Time.timeScale = 1f;
            board.Clear();
            StartLevel();
            new GameObject("Onboarding").AddComponent<Onboarding>().Begin(this);
        }

        public void WatchAdForSecondChance()
        {
            if (_adBusy || !SecondChanceAvailable) return;   // дабл-клик / шансы исчерпаны
            BeginRewarded("second_chance");
            Analytics.SecondChanceAccepted(currentLevel);
        }

        public void WatchAdForSkipLevel()
        {
            if (_adBusy || !SkipLevelAvailable) return;
            BeginRewarded("skip_level");
            Analytics.SkipLevelAccepted(currentLevel);
        }

        void BeginRewarded(string placement)
        {
            _adBusy = true;
            _adPlacement = placement;
            _adShownSent = false;
            _adBusyDeadline = Time.realtimeSinceStartup + 20f;   // SDK должен успеть открыть оверлей
            SubscribeRewarded();
            Analytics.AdRequest(placement);
            YandexBridge.ShowRewarded(placement);
        }

        /// <summary>Выдаёт стартовые заряды в момент разблокировки бонуса по уровню (радуга — Lv3, бомба — Lv7).</summary>
        void RefreshBonusUnlocks()
        {
            rainbowCharges = GrantOnUnlock(KeyRainbow, RainbowUnlocked, "rainbow");
            bombCharges    = GrantOnUnlock(KeyBomb,    BombUnlocked,    "bomb");
#if UNITY_EDITOR
            if (RainbowUnlocked) rainbowCharges = 999;   // dev: удобно тестить (в билд не идёт)
            if (BombUnlocked)    bombCharges    = 999;
#endif
        }

        int GrantOnUnlock(string key, bool unlocked, string type)
        {
            if (!unlocked) return 0;
            if (!PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.SetInt(key, BonusStart); PlayerPrefs.Save();
                Analytics.BonusUnlocked(type, currentLevel);
            }
            return PlayerPrefs.GetInt(key, BonusStart);
        }

        /// <summary>Тап по бонусу «бомба»: заряжает следующий выстрел; если зарядов
        /// нет — предлагает пополнить за rewarded.</summary>
        public void UseBomb()
        {
            if (State != GameState.Aiming || InputBlocked) return;
            if (!BombUnlocked) return;
            if (bombCharges > 0)
            {
                // заряд тратится только если удалось поставить в выстрел/очередь
                if (shooter.ArmSpecial(GameConfig.BombId))
                {
                    bombCharges--;
                    PlayerPrefs.SetInt(KeyBomb, bombCharges);
                    PlayerPrefs.Save();   // WebGL: без Save() запись может не доехать до IndexedDB
                    Analytics.BonusUsed("bomb", currentLevel);
                }
            }
            else RefillBonus(GameConfig.BombId);
        }

        public void UseRainbow()
        {
            if (State != GameState.Aiming || InputBlocked) return;
            if (!RainbowUnlocked) return;
            if (rainbowCharges > 0)
            {
                if (shooter.ArmSpecial(GameConfig.RainbowId))
                {
                    rainbowCharges--;
                    PlayerPrefs.SetInt(KeyRainbow, rainbowCharges);
                    PlayerPrefs.Save();
                    Analytics.BonusUsed("rainbow", currentLevel);
                }
            }
            else RefillBonus(GameConfig.RainbowId);
        }

        /// <summary>
        /// Тап по пустому бонусу — сразу rewarded. Что это ролик и что дадут RefillAmount
        /// зарядов, игрок видит на самом бейдже кнопки («▶ Реклама +3», AdLoc.RefillBadge) —
        /// этого требует п.4.5.1, и лишний диалог подтверждения только резал бы конверсию.
        /// </summary>
        void RefillBonus(int specialId)
        {
            if (_adBusy) return;   // rewarded уже в полёте — дабл-тап не шлёт второй запрос
            Analytics.BonusRefillOffer(specialId == GameConfig.BombId ? "bomb" : "rainbow", currentLevel);
            BeginRewarded(specialId == GameConfig.BombId ? "refill_bomb" : "refill_rainbow");
        }

        public void SetPaused(bool value)
        {
            _paused = value;
            Time.timeScale = value ? 0f : 1f;
        }

        public void SetResolving() => State = GameState.Resolving;

        public void OnResolved(int removed)
        {
            shotsLeft--;

            if (removed > 0)
            {
                if (Time.time - _lastPopTime <= ComboWindow) _combo++;
                else                                          _combo = 1;
                _lastPopTime = Time.time;
                if (_combo >= 2)
                {
                    ComboDisplay.Instance?.Show(_combo);
                    if (_combo >= 3)
                        CameraFitter.Instance?.TriggerShake(0.06f * _combo, 0.18f);
                }
            }
            else _combo = 0;

            if (CheckWin())
            {
                ComboDisplay.Instance?.Hide();   // на экране итогов попап неуместен
                AudioService.PlayWin();
                State = GameState.Win;
                YandexBridge.GameplayStop();
                shooter.HideProjectile();
                SaveProgress();
                Analytics.LevelComplete(currentLevel, attemptsThisLevel, shotsLeft,
                    (int)(Time.realtimeSinceStartup - _levelStartTime));
            }
            else if (board.LowestRow() >= cfg.maxRows)
            {
                Lose(byOverflow: true);
            }
            else if (shotsLeft <= 0)
            {
                Lose(byOverflow: false);
            }
            else
            {
                shooter.Reload();
                State = GameState.Aiming;
            }
        }

        void Lose(bool byOverflow)
        {
            _loseByOverflow = byOverflow;
            ComboDisplay.Instance?.Hide();
            AudioService.PlayLose();
            State = GameState.Lose;
            YandexBridge.GameplayStop();
            shooter.HideProjectile();
            Analytics.LevelFail(currentLevel, attemptsThisLevel,
                byOverflow ? "overflow" : "out_of_shots",
                (int)(Time.realtimeSinceStartup - _levelStartTime));
            // шлём offered только для реально показанной кнопки (иначе конверсия врёт)
            if (SecondChanceAvailable)   Analytics.SecondChanceOffered(currentLevel, byOverflow);
            else if (SkipLevelAvailable) Analytics.SkipLevelOffered(currentLevel);
        }

        bool CheckWin() => board.PoppableCount == 0;

        void SubscribeRewarded()
        {
            YandexBridge.RewardedEvent      -= OnRewarded;
            YandexBridge.RewardedErrorEvent -= OnRewardedError;
            YandexBridge.RewardedEvent      += OnRewarded;
            YandexBridge.RewardedErrorEvent += OnRewardedError;
        }

        void OnRewarded(string id)
        {
            YandexBridge.RewardedEvent      -= OnRewarded;
            YandexBridge.RewardedErrorEvent -= OnRewardedError;
            _adBusy = false;
            Analytics.AdRewarded(id);

            switch (id)
            {
                case "second_chance":
                    _secondChanceCount++;       // до 2 воскрешений на попытку
                    if (_loseByOverflow)
                    {
                        board.ClearBottomRows(OverflowClearRows);
                        _loseByOverflow = false;
                        if (shotsLeft <= 0) shotsLeft = SecondChanceShots;
                    }
                    else shotsLeft += SecondChanceShots;
                    State = GameState.Aiming;
                    YandexBridge.GameplayStart();
                    shooter.Reload();
                    break;

                // Пропуск уровня за rewarded (после двух вторых шансов) — уходим на
                // следующий уровень (без interstitial: за награду показываем только
                // rewarded — правило Яндекса). Тот же путь, что и NextLevel после победы.
                case "skip_level":
                    LevelTransition.Instance?.Play(() =>
                    {
                        currentLevel = Mathf.Min(currentLevel + 1, LevelCatalog.Count);
                        SaveProgress();
                        board.Clear();
                        StartLevel();
                    });
                    break;

                // Пополнение за rewarded — по RefillAmount зарядов (стартовые 10 при
                // разблокировке; +3 за 30-сек ролик — осмысленная сделка, +1 конвертил плохо).
                case "refill_bomb":
                    bombCharges += RefillAmount;
                    PlayerPrefs.SetInt(KeyBomb, bombCharges);
                    PlayerPrefs.Save();
                    break;

                case "refill_rainbow":
                    rainbowCharges += RefillAmount;
                    PlayerPrefs.SetInt(KeyRainbow, rainbowCharges);
                    PlayerPrefs.Save();
                    break;
            }
        }

        void OnRewardedError(string id)
        {
            YandexBridge.RewardedEvent      -= OnRewarded;
            YandexBridge.RewardedErrorEvent -= OnRewardedError;
            _adBusy = false;   // без награды, но кнопки снова кликабельны
            // _adShownSent различает «не открылось» (no-fill) и «закрыл до награды»
            Analytics.AdError(id, _adShownSent ? "closed_early" : "no_fill");
        }

        void BuildBoardPanel()
        {
            float halfW  = cfg.HalfWidth + 0.35f;
            float top    = cfg.topRowY + cfg.CellSize * 1.3f;
            float bottom = cfg.shooterY - cfg.Diameter * 0.7f;

            // Мягкая тень под картой — без неё карта не «лежит» на фоне, а сливается с ним
            // (заметно с тех пор, как поля перестали быть плоской заливкой).
            var shadowGo = new GameObject("BoardPanelShadow");
            shadowGo.transform.SetParent(transform);
            shadowGo.transform.position = new Vector3(0f, (top + bottom) * 0.5f - 0.10f, 1f);
            var shadow = shadowGo.AddComponent<SpriteRenderer>();
            shadow.sprite       = UiKit.RoundBoardSprite;
            shadow.drawMode     = SpriteDrawMode.Sliced;
            shadow.size         = new Vector2(halfW * 2f + 0.18f, top - bottom + 0.18f);
            shadow.color        = new Color(0.42f, 0.34f, 0.27f, 0.13f);
            shadow.sortingOrder = -11;

            var go = new GameObject("BoardPanel");
            go.transform.SetParent(transform);
            go.transform.position = new Vector3(0f, (top + bottom) * 0.5f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = UiKit.RoundBoardSprite;
            sr.drawMode     = SpriteDrawMode.Sliced;
            sr.size         = new Vector2(halfW * 2f, top - bottom);
            sr.color        = new Color(0.992f, 0.988f, 0.976f);   // тёплый белый, как в макете
            sr.sortingOrder = -10;   // за пузырями (1), пушкой (3), но над фоном
        }

        void SaveProgress() => CloudSave.Save(currentLevel);

        void TryShowInterstitial()
        {
            if (currentLevel < 2) return; // не дёргаем рекламой новичков (онбординг)
            // Отсчёт от запроса, а не от показа: при ошибке тоже не долбим SDK.
            if (Time.realtimeSinceStartup - _lastInterstitialTime < InterstitialCooldown) return;
            _lastInterstitialTime = Time.realtimeSinceStartup;
            Analytics.AdRequest("interstitial");
            YandexBridge.ShowInterstitial();
        }

        void OnInterstitialClosed(bool wasShown)
        {
            // wasShown=false — чаще всего просто «нет рекламы для показа» (no-fill),
            // а не ошибка; помечаем отдельным reason, чтобы не путать в отчётах.
            if (wasShown) Analytics.AdShown("interstitial");
            else          Analytics.AdError("interstitial", "not_shown");
        }
    }
}
