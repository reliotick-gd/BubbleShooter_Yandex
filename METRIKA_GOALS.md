# Цели Яндекс.Метрики — полный список (42 шт.)

Все цели — тип **«JavaScript-событие»**, идентификатор = имя из таблицы (точь-в-точь).
Создавать в счётчике, номер которого вписан в `Assets/WebGLTemplates/YandexGames/index.html`
(`window.YM_COUNTER_ID`). Параметры видны в отчёте «Параметры визитов».
Совет: цели с 💰 пометить в Метрике как «конверсионные» — по ним строится доходная воронка.

## Сессия и загрузка (страница шлёт сама)
| Цель | Параметры | Смысл |
|---|---|---|
| game_loaded | load_seconds | движок загрузился (+время) |
| game_load_error | — | билд не загрузился (алерт!) |
| session_end | level | закрыл вкладку (последний уровень — точка отвала) |

## Сессия (из игры)
| Цель | Параметры |
|---|---|
| session_start | level, lang, muted |
| playtime_1m / playtime_3m / playtime_5m / playtime_10m / playtime_20m / playtime_30m | — (6 целей, честное активное время) |

## Воронка уровней
| Цель | Параметры |
|---|---|
| level_start | level, attempt |
| level_complete | level, attempt, shots_left, duration_s |
| level_fail | level, attempt, reason (overflow / out_of_shots), duration_s |
| level_retry | level, attempt |
| milestone_level_5 / _10 / _15 / _20 / _25 / _30 / _40 / _50 | — (8 целей, для воронки прогресса) |
| endless_reached | — (дошёл до бесконечного режима) |
| back_to_title | level |

## Реклама 💰
| Цель | Параметры |
|---|---|
| ad_request 💰 | placement |
| ad_shown 💰 | placement |
| ad_rewarded 💰 | placement |
| ad_error | placement, reason (no_fill / closed_early / timeout / not_shown) |

placement: `interstitial` · `second_chance` · `skip_level` · `refill_bomb` · `refill_rainbow`

## Rewarded-офферы лоуз-экрана 💰
| Цель | Параметры |
|---|---|
| second_chance_offered / _accepted / _declined | level (+by_overflow у offered) |
| skip_level_offered / _accepted / _declined | level |

## Бонусы
| Цель | Параметры |
|---|---|
| bonus_unlocked | type (bomb/rainbow), level |
| bonus_used | type, level |
| bonus_refill_offer 💰 | type, level |

## Онбординг и прочее
| Цель | Параметры |
|---|---|
| onboarding_shown | — |
| onboarding_done | skipped |
| hint_shown | type (ice/slime/rock) |
| mute_toggle | on |
| leaderboard_open | level |

## Минимальный набор, если лень заводить все 42
level_start, level_complete, level_fail, ad_shown, ad_rewarded, playtime_5m, session_end,
second_chance_accepted, skip_level_accepted — по ним уже видно ретеншен, отвалы и доход.
