mergeInto(LibraryManager.library, {

  // Инициализация Yandex Games SDK v2. Наш index.html (WebGLTemplates/YandexGames)
  // грузит /sdk.js, делает YaGames.init() ДО загрузки Unity и кладёт SDK в window.ysdk —
  // поэтому к моменту вызова YG_Init SDK обычно уже готов. Ветки ниже — страховка
  // для локального запуска/нестандартного хостинга.
  YG_Init: function () {
    var finished = false;
    var finish = function () {
      if (finished) return;   // страховка от двойного вызова (onerror + таймаут)
      finished = true;
      var lang = 'en';
      try {
        if (window.ysdk && window.ysdk.environment && window.ysdk.environment.i18n) {
          lang = window.ysdk.environment.i18n.lang || 'en';
        }
      } catch (e) { }
      // Пауза платформы (game_api_pause/resume): стартовая реклама самой платформы,
      // её диалоги. Требование Яндекса — глушить звук/останавливать геймплей.
      try {
        var sdk = window.ysdk;
        if (sdk && sdk.on) {
          var evP = (sdk.EVENTS && sdk.EVENTS.GAME_API_PAUSE)  || 'game_api_pause';
          var evR = (sdk.EVENTS && sdk.EVENTS.GAME_API_RESUME) || 'game_api_resume';
          sdk.on(evP, function () { SendMessage('YandexBridge', 'OnPlatformPause', ''); });
          sdk.on(evR, function () { SendMessage('YandexBridge', 'OnPlatformResume', ''); });
          console.log('[YG] game_api_pause/resume hooked');
        }
      } catch (e) { console.warn('[YG] event hook failed', e); }
      console.log('[YG] init finish. sdk =', !!window.ysdk, 'lang =', lang);
      SendMessage('YandexBridge', 'OnLang', lang);
      SendMessage('YandexBridge', 'OnSDKReady', '');
    };

    // 1) index.html уже всё сделал
    if (window.ysdk) { finish(); return; }

    var doInit = function () {
      YaGames.init().then(function (sdk) {
        window.ysdk = sdk;
        finish();
      }, function (e) {
        console.warn('[YG] YaGames.init() упал — игра без рекламы/облака:', e);
        finish();
      });
    };

    // 2) скрипт SDK на странице, но init не делался
    if (typeof YaGames !== 'undefined') { doInit(); return; }

    // 3) SDK нет вообще (шаблон подменили/нестандартный хостинг) — пробуем
    //    относительный /sdk.js (абсолютные URL на S3 Яндекса запрещены требованиями).
    var tries = 0;
    var iv = setInterval(function () {
      if (typeof YaGames !== 'undefined') {
        clearInterval(iv);
        doInit();
      } else if (++tries > 150) { // ~15 c
        clearInterval(iv);
        console.error('[YG] SDK не загрузился за отведённое время');
        finish();
      }
    }, 100);
    if (!document.querySelector('script[src*="sdk.js"]')) {
      var s = document.createElement('script');
      s.src = '/sdk.js';
      s.onerror = function () {   // локальный запуск: /sdk.js нет — игра без SDK
        console.error('[YG] не удалось загрузить /sdk.js');
        clearInterval(iv);
        finish();
      };
      document.head.appendChild(s);
      console.log('[YG] injected /sdk.js');
    }
  },

  // Синхронное чтение языка для Loc до постройки UI — index.html гарантирует
  // ysdk до старта Unity, поэтому язык уже доступен.
  YG_GetLang: function () {
    var lang = 'en';
    try {
      if (window.ysdk && window.ysdk.environment && window.ysdk.environment.i18n) {
        lang = window.ysdk.environment.i18n.lang || 'en';
      }
    } catch (e) { }
    var size = lengthBytesUTF8(lang) + 1;
    var buf = _malloc(size);
    stringToUTF8(lang, buf, size);
    return buf;
  },

  // Rewarded-видео. Награду выдаём ТОЛЬКО если пришёл onRewarded (просмотр засчитан),
  // и только после onClose — чтобы состояние игры менялось при закрытом оверлее.
  YG_ShowRewarded: function (callbackIdPtr) {
    var callbackId = UTF8ToString(callbackIdPtr);
    console.log('[YG] showRewardedVideo id =', callbackId, '| sdk =', !!window.ysdk);
    if (!(window.ysdk && window.ysdk.adv)) {
      SendMessage('YandexBridge', 'OnRewardedError', callbackId);
      return;
    }
    var got = false;
    try {
      window.ysdk.adv.showRewardedVideo({
        callbacks: {
          onOpen: function () {
            console.log('[YG] rewarded onOpen');
            SendMessage('YandexBridge', 'OnAdOpen', '');
          },
          onRewarded: function () {
            console.log('[YG] rewarded onRewarded', callbackId);
            got = true;
          },
          onClose: function () {
            console.log('[YG] rewarded onClose, got =', got);
            SendMessage('YandexBridge', 'OnAdClose', '');
            SendMessage('YandexBridge', got ? 'OnRewarded' : 'OnRewardedError', callbackId);
          },
          onError: function (e) {
            console.warn('[YG] rewarded onError', e);
            SendMessage('YandexBridge', 'OnAdClose', '');
            SendMessage('YandexBridge', 'OnRewardedError', callbackId);
          }
        }
      });
    } catch (e) {   // синхронное исключение SDK — оверлей не открывался, только ошибка
      console.warn('[YG] showRewardedVideo threw', e);
      SendMessage('YandexBridge', 'OnRewardedError', callbackId);
    }
  },

  // Наш кулдаун поверх платформенного лимита — в GameManager.TryShowInterstitial (C#).
  YG_ShowInterstitial: function () {
    console.log('[YG] showFullscreenAdv | sdk =', !!window.ysdk);
    if (!(window.ysdk && window.ysdk.adv)) {
      SendMessage('YandexBridge', 'OnInterstitialClose', '0');
      return;
    }
    try {
      window.ysdk.adv.showFullscreenAdv({
        callbacks: {
          onOpen: function () {
            console.log('[YG] fullscreen onOpen');
            SendMessage('YandexBridge', 'OnAdOpen', '');
          },
          onClose: function (wasShown) {
            console.log('[YG] fullscreen onClose, wasShown =', wasShown);
            SendMessage('YandexBridge', 'OnAdClose', '');
            SendMessage('YandexBridge', 'OnInterstitialClose', wasShown ? '1' : '0');
          },
          onError: function (e) {
            console.warn('[YG] fullscreen onError', e);
            SendMessage('YandexBridge', 'OnAdClose', '');
            SendMessage('YandexBridge', 'OnInterstitialClose', '0');
          }
        }
      });
    } catch (e) {   // синхронное исключение SDK — оверлей не открывался
      console.warn('[YG] showFullscreenAdv threw', e);
      SendMessage('YandexBridge', 'OnInterstitialClose', '0');
    }
  },

  // Требование Яндекса: start при запуске/возобновлении, stop на паузе/итогах/рекламе/фоне.
  YG_GameplayStart: function () {
    try {
      if (window.ysdk && window.ysdk.features && window.ysdk.features.GameplayAPI) {
        window.ysdk.features.GameplayAPI.start();
      }
    } catch (e) { }
  },

  YG_GameplayStop: function () {
    try {
      if (window.ysdk && window.ysdk.features && window.ysdk.features.GameplayAPI) {
        window.ysdk.features.GameplayAPI.stop();
      }
    } catch (e) { }
  },

  // LoadingAPI.ready(): игра загрузилась и готова к взаимодействию (титул на экране).
  YG_GameReady: function () {
    try {
      if (window.ysdk && window.ysdk.features && window.ysdk.features.LoadingAPI) {
        window.ysdk.features.LoadingAPI.ready();
        console.log('[YG] LoadingAPI.ready()');
      }
    } catch (e) { }
  },

  // Показом по умолчанию управляет ПЛАТФОРМА (галка «использовать API» в консоли
  // НЕ ставится — прячет баннер на старте). Этот вызов — страховка от дубля.
  YG_ShowBanner: function () {
    if (!(window.ysdk && window.ysdk.adv)) return;
    try {
      if (window.ysdk.adv.getBannerAdvStatus) {
        window.ysdk.adv.getBannerAdvStatus().then(function (st) {
          if (st && st.stickyAdvIsShowing) { console.log('[YG] sticky banner уже показан'); return; }
          window.ysdk.adv.showBannerAdv().then(
            function () { console.log('[YG] sticky banner shown'); },
            function (e) { console.warn('[YG] sticky banner error', e); });
        }, function () {
          window.ysdk.adv.showBannerAdv();
        });
      } else {
        window.ysdk.adv.showBannerAdv();
      }
    } catch (e) { console.warn('[YG] showBannerAdv failed', e); }
  },

  // Лидерборд: техническое имя задаёт C# (YandexBridge.LeaderboardName).
  // Актуальный API — leaderboards.setScore; getLeaderboards() — фолбэк для старых
  // клиентов. Гостям сабмит недоступен — проверяем isAvailableMethod заранее.
  YG_SetLeaderboard: function (namePtr, score) {
    var name = UTF8ToString(namePtr);
    if (!window.ysdk) return;
    var ok   = function ()  { console.log('[YG] leaderboard "' + name + '" score =', score); };
    var fail = function (e) { console.warn('[YG] leaderboard submit error', e); };
    var submit = function () {
      try {
        if (window.ysdk.leaderboards && window.ysdk.leaderboards.setScore) {
          window.ysdk.leaderboards.setScore(name, score).then(ok, fail);
        } else {
          window.ysdk.getLeaderboards().then(function (lb) {
            return lb.setLeaderboardScore(name, score);
          }).then(ok, fail);
        }
      } catch (e) { fail(e); }
    };
    try {
      if (window.ysdk.isAvailableMethod) {
        // имя метода в проверке зависит от поколения API
        var probe = (window.ysdk.leaderboards && window.ysdk.leaderboards.setScore)
          ? 'leaderboards.setScore' : 'leaderboards.setLeaderboardScore';
        window.ysdk.isAvailableMethod(probe).then(function (avail) {
          if (avail) submit();
          else console.log('[YG] leaderboard submit недоступен (гость)');
        }, submit);
      } else submit();
    } catch (e) { fail(e); }
  },

  // Топ-20 + окрестность игрока. Ответ → YandexBridge.OnLeaderboardLoaded как JSON
  // {userRank, entries:[{rank,score,name,avatar,isUser}]}; ошибка → пустой список.
  YG_LoadLeaderboard: function (namePtr) {
    var name = UTF8ToString(namePtr);
    var reply = function (json) { SendMessage('YandexBridge', 'OnLeaderboardLoaded', json); };
    var empty = '{"userRank":0,"entries":[]}';
    if (!window.ysdk) { reply(empty); return; }

    // сперва uniqueID игрока — надёжнее для подсветки «это я», чем сравнение рангов
    var myId = '';
    var load = function () {
      var opts = { quantityTop: 20, includeUser: true, quantityAround: 3 };
      // актуальный API — ysdk.leaderboards.getEntries; старый — фолбэк
      var promise = (window.ysdk.leaderboards && window.ysdk.leaderboards.getEntries)
        ? window.ysdk.leaderboards.getEntries(name, opts)
        : window.ysdk.getLeaderboards().then(function (lb) {
            return lb.getLeaderboardEntries(name, opts);
          });
      promise.then(function (res) {
        var out = { userRank: (res && res.userRank) || 0, entries: [] };
        (res && res.entries || []).forEach(function (e) {
          var nm = (e.player && e.player.publicName) ? e.player.publicName : '';
          var av = '';
          try { if (e.player && e.player.getAvatarSrc) av = e.player.getAvatarSrc('small') || ''; } catch (ex) { }
          var isUser = myId ? !!(e.player && e.player.uniqueID === myId)
                            : (out.userRank > 0 && e.rank === out.userRank);
          out.entries.push({ rank: e.rank, score: e.score, name: nm, avatar: av, isUser: isUser });
        });
        reply(JSON.stringify(out));
      }).catch(function (e) {
        console.warn('[YG] getLeaderboardEntries error', e);
        reply(empty);
      });
    };
    if (window.__ysdkPlayer && window.__ysdkPlayer.getUniqueID) {
      try { myId = window.__ysdkPlayer.getUniqueID() || ''; } catch (e) { }
      load();
    } else if (window.ysdk.getPlayer) {
      window.ysdk.getPlayer({ scopes: false }).then(function (p) {
        window.__ysdkPlayer = p;
        try { myId = p.getUniqueID() || ''; } catch (e) { }
        load();
      }, load);
    } else load();
  },

  // Кросс-девайс сейв через player.setData (авторизованные — аккаунт Яндекса, гости —
  // локальное хранилище платформы). flush=true: снапшот маленький, лимит 100/5мин не грозит.
  YG_SaveData: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    var send = function (p) {
      if (!p) return;
      var obj = {};
      try { obj = JSON.parse(json) || {}; } catch (e) { }
      try { p.setData(obj, true); } catch (e) { console.warn('[YG] setData failed', e); }
    };
    if (window.__ysdkPlayer) { send(window.__ysdkPlayer); return; }
    if (!(window.ysdk && window.ysdk.getPlayer)) return;
    window.ysdk.getPlayer({ scopes: false }).then(function (p) {
      window.__ysdkPlayer = p;
      send(p);
    }, function (e) { console.warn('[YG] getPlayer failed', e); });
  },

  YG_LoadData: function () {
    var reply = function (json) { SendMessage('YandexBridge', 'OnDataLoaded', json || '{}'); };
    var read = function (p) {
      if (!p) { reply('{}'); return; }
      try {
        p.getData().then(function (d) {
          var json = '{}';
          try { json = JSON.stringify(d || {}); } catch (e) { }
          reply(json);
        }, function () { reply('{}'); });
      } catch (e) { reply('{}'); }
    };
    if (window.__ysdkPlayer) { read(window.__ysdkPlayer); return; }
    if (!(window.ysdk && window.ysdk.getPlayer)) { reply('{}'); return; }
    window.ysdk.getPlayer({ scopes: false }).then(function (p) {
      window.__ysdkPlayer = p;
      read(p);
    }, function () { reply('{}'); });
  }

});
