using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace CozyAnimalTown
{
    /// <summary>
    /// Аналитика → Яндекс.Метрика (ym reachGoal через Analytics.jslib).
    /// ID счётчика задаётся в index.html (window.YM_COUNTER_ID); пока он 0,
    /// события уходят в console.log страницы. В редакторе — Debug.Log.
    ///
    /// Схема событий (воронка):
    ///   session_start {level, lang, muted} → level_start {level, attempt}
    ///     → level_complete {level, attempt, shots_left, duration_s}
    ///     | level_fail {level, attempt, reason, duration_s}
    ///       → second_chance_offered / accepted / declined
    ///       → skip_level_offered / accepted / declined (после 2 вторых шансов)
    ///       → level_retry {level, attempt}
    ///   milestone_level_N (5/10/15/20/25/30/40/50), endless_reached
    ///   playtime_1m/3m/5m/10m/20m/30m (хартбиты для прокси-ретеншена)
    ///   ad_request/ad_shown/ad_rewarded {placement}, ad_error {placement, reason}
    ///   placement: interstitial | second_chance | skip_level | refill_bomb | refill_rainbow
    ///   bonus_unlocked/bonus_used/bonus_refill_offer {type}
    ///   onboarding_shown/onboarding_done {skipped}, hint_shown {type}
    ///   back_to_title {level}, mute_toggle {on}
    ///   + страница сама шлёт: game_loaded {load_seconds}, game_load_error,
    ///     session_end {level} (pagehide — трек отвала «закрыл вкладку»).
    /// В Метрике на каждое имя создаётся цель типа «JavaScript-событие»,
    /// параметры видны в отчёте «Параметры визитов».
    /// </summary>
    public static class Analytics
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void YM_Event(string name, string json);
#endif

        public static void SessionStart(int level, bool muted) =>
            Send("session_start", $"{{\"level\":{level},\"lang\":\"{Esc(Loc.LangCode)}\",\"muted\":{Bool(muted)}}}");

        // Хартбит чистого игрового времени (вызов каждый кадр из GameManager) — прокси
        // playtime-ретеншена без отдельных таймеров; каждый порог шлётся раз за сессию.
        static readonly int[]    HbSec  = { 60, 180, 300, 600, 1200, 1800 };
        static readonly string[] HbName = { "playtime_1m", "playtime_3m", "playtime_5m", "playtime_10m", "playtime_20m", "playtime_30m" };
        static int _hbIdx;

        public static void Heartbeat(float realtimeSec)
        {
            while (_hbIdx < HbSec.Length && realtimeSec >= HbSec[_hbIdx])
            {
                Send(HbName[_hbIdx], "{}");
                _hbIdx++;
            }
        }

        public static void LevelStart(int level, int attempt) =>
            Send("level_start", $"{{\"level\":{level},\"attempt\":{attempt}}}");

        public static void LevelComplete(int level, int attempt, int shotsLeft, int durationS)
        {
            Send("level_complete", $"{{\"level\":{level},\"attempt\":{attempt},\"shots_left\":{shotsLeft},\"duration_s\":{durationS}}}");
            // отдельные цели-майлстоуны — на них удобно строить воронку в Метрике
            if (level == 5 || level == 10 || level == 15 || level == 20 ||
                level == 25 || level == 30 || level == 40 || level == 50)
                Send("milestone_level_" + level, "{}");
        }

        /// <summary>reason: "overflow" (шары дошли до края) | "out_of_shots".</summary>
        public static void LevelFail(int level, int attempt, string reason, int durationS) =>
            Send("level_fail", $"{{\"level\":{level},\"attempt\":{attempt},\"reason\":\"{Esc(reason)}\",\"duration_s\":{durationS}}}");

        public static void LevelRetry(int level, int attempt) =>
            Send("level_retry", $"{{\"level\":{level},\"attempt\":{attempt}}}");

        public static void BackToTitle(int level) =>
            Send("back_to_title", $"{{\"level\":{level}}}");

        public static void EndlessReached() =>
            Send("endless_reached", "{}");

        public static void SecondChanceOffered(int level, bool byOverflow) =>
            Send("second_chance_offered", $"{{\"level\":{level},\"by_overflow\":{Bool(byOverflow)}}}");

        public static void SecondChanceAccepted(int level) =>
            Send("second_chance_accepted", $"{{\"level\":{level}}}");

        public static void SecondChanceDeclined(int level) =>
            Send("second_chance_declined", $"{{\"level\":{level}}}");

        public static void SkipLevelOffered(int level) =>
            Send("skip_level_offered", $"{{\"level\":{level}}}");

        public static void SkipLevelAccepted(int level) =>
            Send("skip_level_accepted", $"{{\"level\":{level}}}");

        public static void SkipLevelDeclined(int level) =>
            Send("skip_level_declined", $"{{\"level\":{level}}}");

        public static void AdRequest(string placement) =>
            Send("ad_request", $"{{\"placement\":\"{Esc(placement)}\"}}");

        public static void AdShown(string placement) =>
            Send("ad_shown", $"{{\"placement\":\"{Esc(placement)}\"}}");

        public static void AdRewarded(string placement) =>
            Send("ad_rewarded", $"{{\"placement\":\"{Esc(placement)}\"}}");

        /// <summary>reason: no_fill | closed_early | timeout | not_shown | "".</summary>
        public static void AdError(string placement, string reason = "") =>
            Send("ad_error", $"{{\"placement\":\"{Esc(placement)}\",\"reason\":\"{Esc(reason)}\"}}");

        public static void BonusUnlocked(string type, int level) =>
            Send("bonus_unlocked", $"{{\"type\":\"{Esc(type)}\",\"level\":{level}}}");

        public static void BonusUsed(string type, int level) =>
            Send("bonus_used", $"{{\"type\":\"{Esc(type)}\",\"level\":{level}}}");

        /// <summary>Тап по пустому бонусу — игроку предложено пополнение за рекламу.</summary>
        public static void BonusRefillOffer(string type, int level) =>
            Send("bonus_refill_offer", $"{{\"type\":\"{Esc(type)}\",\"level\":{level}}}");

        public static void OnboardingShown() =>
            Send("onboarding_shown", "{}");

        public static void OnboardingDone(bool skipped) =>
            Send("onboarding_done", $"{{\"skipped\":{Bool(skipped)}}}");

        /// <summary>type: ice | slime | rock.</summary>
        public static void HintShown(string type) =>
            Send("hint_shown", $"{{\"type\":\"{Esc(type)}\"}}");

        public static void MuteToggle(bool on) =>
            Send("mute_toggle", $"{{\"on\":{Bool(on)}}}");

        public static void LeaderboardOpen(int level) =>
            Send("leaderboard_open", $"{{\"level\":{level}}}");

        static string Bool(bool v) => v ? "true" : "false";

        /// <summary>Минимальное экранирование строки для встраивания в JSON-литерал.</summary>
        static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        static void Send(string name, string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            YM_Event(name, json);
#else
            Debug.Log($"[Analytics] {name} {json}");
#endif
        }
    }
}
