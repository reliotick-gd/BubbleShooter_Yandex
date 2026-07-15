# AGENT HANDOFF — Bubble Shooter: Cozy Animals (Яндекс Игры)

> Читать первым в новом чате. Здесь всё, чтобы продолжить без повторного исследования проекта.
> Отвечай пользователю ПО-РУССКИ. Код-комментарии — русские. UI-текст в игре — RU/EN через `Loc`.

## 1. Что это и статус
`E:\GameDev\Unity\Projects\Bubble_shooter_animal_YandexGames` — Unity 6.4 (6000.4.7f1), WebGL,
bubble shooter для **Яндекс Игр**. Форк CrazyGames-версии (`Bubble_shooter_animal_CrazyGames`,
сабмитнута туда 03.07.2026); геймплей/арт/уровни идентичны, платформенный слой переписан:
Yandex SDK v2, Яндекс.Метрика, локализация RU/EN, внутриигровой тумблер звука.

**Статус на 04.07.2026: порт кода ЗАВЕРШЁН + пройден независимый аудит, все находки
(вкл. желательные) исправлены, компилируется 0 ошибок (обе ветки: editor и
UNITY_WEBGL&&!UNITY_EDITOR). НЕ сделано: страница игры в консоли Яндекса не создана,
счётчик Метрики не создан (заглушка `window.YM_COUNTER_ID = 0` в index.html), WebGL-билд
не собирался, в живом Яндексе не тестировалось.**

### Фиксы по независимому аудиту (04.07.2026) — что уже починено, не искать повторно
1. Мьют из прошлой сессии применяется сразу (`ApplyAudio()` в `YandexBridge.Awake`).
2. Анти-откат прогресса: `CloudSave.Save` пишет `max(level, prefs)`; поздний приход облака
   больше не перетирается меньшим значением.
3. События платформы `game_api_pause/resume` обрабатываются (jslib `YG_Init` → hook →
   `OnPlatformPause/Resume` → общий `OverlayOpened/Closed` с рекламой; свой флаг парности).
4. Титул модальный: фон с `raycast:true` — клики не проваливаются в HUD под ним.
5. `Time.timeScale` после рекламы восстанавливается в значение ДО показа (`_tsBeforeOverlay`),
   а не безусловно в 1.
6. Лидерборды на актуальном API `ysdk.leaderboards.setScore/getEntries` (+фолбэк на
   устаревший `getLeaderboards()`).
7. Показы рекламы в jslib обёрнуты в try/catch; в GameManager — таймаут `_adBusy`
   (20с до открытия оверлея, продлевается пока оверлей виден): SDK замолчал — кнопки оживают.
8. Бамп SaveVer теперь чистит и облако (`cat_cloud_wipe` → первый ответ облака не мержится,
   а перезаписывается).
9. Playtime-хартбиты считают только честное активное время (видимая вкладка, без рекламы;
   `_activePlaySec` вместо realtime; титул до «Играть» не считается).
10. Аватарки лидерборда: Texture2D+Sprite уничтожаются при закрытии экрана.
11. Лоадер локализуется мгновенно по `navigator.language` (потом уточняется по i18n.lang);
    тексты ошибок тоже RU/EN.
12. `LoadingAPI.ready()` теперь зовёт TitleScreen в момент реальной интерактивности
    (после 0.6с защиты ввода), а не GameBootstrap.
13. Самоинжект SDK в jslib — относительный `/sdk.js` (абсолютные S3-URL запрещены требованиями).
14. Unity splash выключен (`m_ShowUnitySplashScreen: 0` — Unity 6 разрешает на любой лицензии).
15. Комментарии про кулдаун/частоту interstitial приведены в соответствие коду (90с).

НЕ делалось из аудита (осознанно): чистка TMP-атласа LiberationSans SDF (~1-2 МБ в билде) —
рискованно без живой проверки в редакторе (TMP Settings ссылается на него как default font
до подмены DynamicFont); TODO при оптимизации веса. Brotli без Decompression Fallback
оставлен — CDN Яндекса отдаёт .br корректно; проверить на первом черновике, при проблеме
включить fallback в Player Settings.

### Фиксы по ВТОРОМУ независимому аудиту (04.07.2026) — тоже готово, не искать повторно
- **[В-1] Гонка initSDK:** тег `/sdk.js` больше не в `<head>` с `onload=`; функции
  initSDK/bootUnity/loadUnity объявлены ПЕРВЫМИ, скрипт SDK инжектируется в конце (гонки нет).
- **[В-2] Таймаут init:** `YaGames.init()` в `Promise` c 10-сек таймаутом + финальный
  `setTimeout(bootUnity, 15000)` — вечного экрана загрузки не будет; `bootUnity` идемпотентен.
- **[В-3] Поздняя награда rewarded:** таймаут `_adBusy` теперь только разлочивает кнопки,
  подписку на OnRewarded НЕ снимает — если реклама откроется на 25-й секунде, награда применится.
- **[В-4] SaveVer:** комментарий-предохранитель «ПОСЛЕ РЕЛИЗА НЕ БАМПАТЬ» в GameBootstrap.
- **[Ж-1] Воронка рекламы:** `ad_shown` теперь шлётся и для rewarded (по факту открытия оверлея),
  `ad_error` получил `reason` (no_fill/closed_early/timeout/not_shown), placement в таймауте не теряется.
- **[Ж-2] Мёртвый вес:** удалены неиспользуемые маскоты `Mascots/{bear_3x4,bunny_3x4}.png` (−5 МБ исходников).
- **[Ж-3] Мобильный прицел:** hover-прицел активен только при `Touchscreen.current == null` —
  на телефоне эмулированная мышь больше не «залипляет» пунктир. Проверить на реальном телефоне.
- **[Ж-5] Кросс-девайс:** облако, пришедшее на тронутом уровне, применяется на следующем
  StartLevel (`_pendingCloudLevel`), а не только по перезагрузке.
- **[Ж-6] Аналитика:** `second_chance_offered` шлётся только когда кнопка реально показана;
  добавлены `skip_level_offered/accepted/declined`.
- **Придирка:** фейды TitleScreen на `unscaledDeltaTime` (не замирают при timeScale=0).

НЕ делалось из аудита-2 (осознанно): `runInBackground` (`ProjectSettings:85`, =0) — проверить
поведение фокуса на живой платформе; BoardManager.Clear-orphan (практически недостижимо через UI);
`_malloc` в YG_GetLang (одноразовая утечка байтов на старте — пренебрежимо); `leaderboard.png`
без `.meta` (Unity сгенерит при импорте — проверить, что грузится).

Аудитория — casual/женская, стиль «милые зверюшки», cozy. UI: плоский кремовый фон, белые
скруглённые пиллы, коричневый Nunito (кириллица в TTF есть — проверено по cmap), пастель.

## 2. Как работать (важно)
- **Unity-редактор недоступен, игру не запускаю.** Пользователь сам жмёт Play/билдит и шлёт скрины.
- **После КАЖДОГО изменения — проверяй компиляцию** командой ниже. Не отдавать код без «0 ошибок».
- Правки — точечные (Edit), стиль/комментарии как в окружающем коде (русские).
- Пользователь бывает резок/матерится — норм, просто делай задачу. Если он ЗАДАЁТ ВОПРОС — сначала ответь.

### Compile-check (Git Bash, dotnet v10)
```bash
cd "E:/GameDev/Unity/Projects/Bubble_shooter_animal_YandexGames"
grep -v "<Compile Include" Assembly-CSharp.csproj | grep -v "</Project>" > _verify.csproj
printf '  <ItemGroup>\n    <Compile Include="Assets/**/*.cs" />\n  </ItemGroup>\n</Project>\n' >> _verify.csproj
"/c/Program Files/dotnet/dotnet" build _verify.csproj -nologo -v:m -p:IntermediateOutputPath=Temp/verify_obj/ -p:OutputPath=Temp/verify_bin/ 2>&1 | grep -Ei "error|успешно|Ошибок|warning CS"
rm -rf _verify.csproj Temp/verify_obj Temp/verify_bin
```
Для WEBGL-ветки — вместо printf добавить в _verify.csproj перед ItemGroup:
`<PropertyGroup><DefineConstants>$(DefineConstants.Replace('UNITY_EDITOR','DISABLED_EDITOR'));UNITY_WEBGL</DefineConstants></PropertyGroup>`
YAML-ассеты (.asset/.meta/ProjectSettings) писать UTF-8 БЕЗ BOM (PowerShell 5.1 Set-Content подсовывает BOM — использовать `[IO.File]::WriteAllText(...,UTF8Encoding($false))`).

## 3. Архитектура (не изменилась против CG-версии)
- **Нет сцен/префабов** — вся игра собирается кодом в рантайме. Точка входа `GameBootstrap.Boot()`
  (`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`): миграция сейва → шрифт/иконки → камеры →
  YandexBridge → прогрев фабрик → `TitleScreen` + `YandexBridge.GameReady()` → по тапу `GameManager`.
- **Единый макет 9:16 ВЕЗДЕ** (на широком экране — колонка по центру, поля крем): `ScreenColumn`,
  `CameraFitter` (+SkyCamera), `UiKit.Column` → дизайн-рут **1080×1920**.
- 35+ скриптов в `Assets/_Game/Scripts/`, namespace `CozyAnimalTown`. Детали геймплея/уровней/экранов —
  в AGENT_HANDOFF CG-проекта (`Bubble_shooter_animal_CrazyGames\AGENT_HANDOFF.md`, §4-6, §9) — всё актуально.

## 4. Платформенный слой Яндекса (НОВОЕ, отличия от CG)
### YandexBridge.cs (бывш. CrazyBridge, фасад событий тот же)
- `YG_Init` (jslib): index.html кладёт SDK в `window.ysdk` ДО загрузки Unity → OnLang + OnSDKReady.
  Фолбэки: YaGames без init / самоинжект `sdk.games.s3.yandex.net/sdk.js`.
- **Звук — три источника мьюта**, всё сведено в `ApplyAudio()`: реклама (`AdShowing`) ∨ фоновая
  вкладка (`_pageHidden`, из `visibilitychange` в index.html — ТРЕБОВАНИЕ Яндекса) ∨ пользовательский
  тумблер (`UserMuted`, PlayerPrefs `cat_muted`, кнопка в настройках). НЕ трогать AudioListener напрямую
  мимо моста.
- **Реклама**: rewarded — награда только `onRewarded`+`onClose` (в jslib флаг `got`); interstitial —
  `onClose(wasShown)`. `_adDepth`, `Time.timeScale=0`, GameplayStop/Start вокруг показа — как раньше.
- **GameplayAPI** `features.GameplayAPI.start/stop` — идемпотентно в C#; также стоп при уходе вкладки в фон.
- **LoadingAPI.ready()** — `GameReady()` один раз из GameBootstrap (титул на экране).
- **Сейв**: `player.setData({level},flush=true)` / `getData` через `getPlayer({scopes:false})`
  (кэш `window.__ysdkPlayer`); гость — хранилище платформы, авторизованный — аккаунт. Лимит 100/5мин — ок.
- **Interstitial-кулдаун НАШ**: `GameManager.InterstitialCooldown = 90f` сек + не раньше уровня 2
  (Яндекс, в отличие от CG, частоту сам не режет). 90с — стартовое, крутить по метрикам.
- **Sticky-баннер**: `YG_ShowBanner` (showBannerAdv + getBannerAdvStatus-guard), зовётся из
  `OnSDKReady`, висит всегда. Показом по умолчанию рулит платформа; галку «Использовать API
  для показа sticky-баннера» в консоли НЕ ставить (она для скрытия баннера на старте).
- **Лидерборд** — техимя `YandexBridge.LeaderboardName = "bbShTablica"` (создать в консоли,
  тип «лучший результат», счёт = максимальный уровень). Сабмит: `CloudSave.Save` →
  `SetLeaderboardScore(level)`; jslib проверяет `isAvailableMethod` (гости молча пропускаются).
  Просмотр: кнопка в HUD слева от шестерёнки (PNG `Icons/leaderboard.png`, Material Icons;
  процедурный подиум-фолбэк) → `GameManager.OpenLeaderboard()` (`_lbOpen` в InputBlocked) →
  `LeaderboardScreen.cs`: тёмный экран в стиле нативной таблицы Яндекса — топ-20 +
  зигзаг-разрыв + окрестность игрока (`getLeaderboardEntries {quantityTop:20, includeUser,
  quantityAround:3}`), свой ряд подсвечен, аватарки грузятся UnityWebRequestTexture
  (фолбэк — пастельный круг с буквой), ScrollRect. В редакторе — фейковые данные
  (заглушка в YandexBridge.RequestLeaderboard).

### Локализация — Loc.cs
`Loc.T("en", "ру")` в месте постройки текста. Язык: WebGL — синхронный `YG_GetLang()` (jslib читает
`ysdk.environment.i18n.lang`; работает до постройки UI, т.к. index.html ждёт init SDK до старта Unity);
редактор — язык системы. ru/be/kk/uk → RU, остальное → EN. Локализовано ВСЁ: титул (лого
«Бабл Шутер:\nМилые Зверята»), HUD (УРОВЕНЬ/ВЫСТРЕЛЫ, «Ур.N»), победа («Поздравляем!», подзаголовки,
«Уровень N»), поражение («Второй шанс»/«Ещё раз»/причины/«Осталось:»), настройки, онбординг, подсказки
препятствий, КОМБО, лоадер в index.html. AD-бейдж: EN «▶ AD», RU только «▶».

### Аналитика — Analytics.cs + Analytics.jslib + index.html
`ym(YM_COUNTER_ID,'reachGoal',name,params)`. **ID счётчика — ЗАГЛУШКА** (`window.YM_COUNTER_ID = 0`
в `Assets/WebGLTemplates/YandexGames/index.html`); пока 0 — события в console.log. В редакторе — Debug.Log.
События: session_start{level,lang,muted}, level_start/complete/fail/retry (+attempt/reason/duration_s),
milestone_level_5/10/15/20/25/30/40/50, endless_reached, second_chance_offered/accepted/declined,
ad_request/shown/rewarded/error{placement: interstitial|second_chance|refill_bomb|refill_rainbow},
bonus_unlocked/used/refill_offer{type}, onboarding_shown/done{skipped}, hint_shown{ice|slime|rock},
back_to_title, mute_toggle, playtime_1m/3m/5m/10m/20m/30m (хартбиты из GameManager.Update);
страница шлёт game_loaded{load_seconds}, game_load_error, session_end{level} (pagehide, уровень
из window.__lastLevel — его пишет Analytics.jslib). **В Метрике завести JS-цели на каждое имя.**

### Шаблон Assets/WebGLTemplates/YandexGames/index.html
`/sdk.js` async → `YaGames.init()` → `window.ysdk` → только потом грузится Unity. Кремовый лоадер
(локализуется по lang), сниппет Метрики (guard по YM_COUNTER_ID), visibilitychange → мост,
pagehide → session_end. ProjectSettings: `webGLTemplate: PROJECT:YandexGames`.

## 5. Монетизация (решение пользователя: деньги — главное)
- **Interstitial** — переходы: NextLevel (победа), Retry («Ещё раз»/Restart), OpenTitle (назад).
  Гейт: уровень ≥ 2 + кулдаун 90с (A/B потом). Требование Яндекса: «реклама только в логических паузах».
- **Rewarded** (все через `BeginRewarded(placement)`; `_adBusy`+таймаут глушат дабл-клики):
  - `second_chance` — **до 2 раз на попытку** (`MaxSecondChances=2`): +5 выстрелов / расчистка 3 рядов.
  - `skip_level` — та же кнопка после 2 израсходованных шансов (`SkipLevelAvailable`): засчитывает
    уровень пройденным, уходит на следующий (Яндекс явно разрешает пропуск уровня за rewarded).
  - `refill_bomb`/`refill_rainbow` — тап по пустому бонусу → **+3 заряда** (было +1).
  Лоуз-кнопки «Второй шанс» (синяя) ⇄ «Пропустить уровень» (зелёная) в одном слоте, видна одна;
  переключение — `Hud.UpdateModals` по `SecondChanceAvailable`/`SkipLevelAvailable`.
- НЕ сделано (аудит-2, на будущее): мид-левел оффер «+5 выстрелов» при `shotsLeft<=2`; мягкая
  валюта + «x2 монеты за победу» + покупки через SDK — требуют геймдизайна, отдельной задачей.
- **Sticky-баннер ВКЛЮЧЁН** всегда (показом управляет платформа по умолчанию — включается
  ещё до загрузки Unity). В консоли: настроить позицию (портрет «Внизу», десктоп вкл).
  Галку «Использовать API для показа sticky-баннера» НЕ ставить — она нужна лишь чтобы
  ПРЯТАТЬ баннер на старте и включать через API; нам баннер нужен всегда. Наш вызов
  `YG_ShowBanner` в OnSDKReady — безвредная страховка (проверяет статус, молчит если уже показан).

## 6. PlayerPrefs-ключи
`cat_level` (прогресс), `cat_bomb`/`cat_rainbow` (заряды), `cat_onboarded`,
`cat_seen_{ice,slime,rock}` (подсказки), `cat_endless_sent` (аналитика 31-го уровня),
`cat_muted` (тумблер звука), `cat_save_ver` (=5, миграция). Все записи — сразу с `PlayerPrefs.Save()`.

## 7. Что дальше (чек-лист перед релизом)
1. **Пользователь создаёт игру в консоли Яндекса** (games.yandex — кабинет разработчика):
   **название RU «Шарики: Милые Зверята» / EN «Bubble Shooter: Cozy Animals»** (совпадает с
   лого в игре — менять синхронно!), описание RU/EN с ключами «бабл шутер», «bubble shooter»,
   «шарики стрелялка», иконка 512×512, обложка, скриншоты (≥70% геймплея), категория,
   возраст, «управление мышью и тачем», ориентация: обе (макет 9:16 везде).
   Плюс: **лидерборд `bbShTablica`** (тип «лучший результат», по убыванию, без десятичных) и
   **галка управления sticky-баннером через API**.
2. **Создать счётчик Яндекс.Метрики** → вписать номер в `index.html` (`YM_COUNTER_ID`).
   Завести цели «JavaScript-событие» по списку из §4 (минимум: level_start, level_complete,
   level_fail, ad_shown, playtime_5m, session_end).
3. WebGL-билд (Brotli; см. настройки — не менялись), zip с index.html в корне, залить черновиком,
   прогнать в режиме отладки Яндекса: реклама (тестовая), сейв, язык, звук при сворачивании,
   LoadingAPI/GameplayAPI (консоль браузера — логи [YG]).
4. Проверить RU-тексты по скринам (влезание в пиллы/кнопки: УРОВЕНЬ/ВЫСТРЕЛЫ size 18, «Ур.41» в бейджах).
5. Модерация: рейтинг >30 обязателен после публикации; жалобы модерации чаще всего — реклама вне пауз
   и звук в фоне (оба закрыты кодом).

## 8. Память
Авто-память: `C:\Users\Renat\.claude\projects\E--GameDev-Unity-Projects\memory\` (общая база,
feedback-language: отвечать по-русски; bubble-shooter-crazygames-port — история CG-версии).
Этот handoff — самый свежий источник; при расхождении верь ему + текущему коду.
CG-проект НЕ трогать: он на модерации CrazyGames, живёт своей жизнью.
