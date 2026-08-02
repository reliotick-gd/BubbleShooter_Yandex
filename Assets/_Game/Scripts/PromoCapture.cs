#if DEVELOPMENT_BUILD || UNITY_EDITOR || PROMO_CAPTURE
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Съёмка промоматериалов из командной строки. Компилируется ТОЛЬКО в
    /// development-сборке — в релизном WebGL-билде этого кода нет.
    ///
    ///   BubbleShooter.exe -screen-width 1080 -screen-height 1920 -screen-fullscreen 0 \
    ///       -lang ru -promo D:\out -promomode shots
    ///
    /// Режимы:
    ///   shots — три статичных кадра геймплея (PNG);
    ///   video — последовательность JPG-кадров с фиксированным шагом времени, которую
    ///           потом склеивает PromoEncoder (UnityEditor.Media.MediaEncoder).
    ///
    /// ПОЧЕМУ НЕ ЖИВОЙ ВВОД. Выстрелы делает Shooter.DebugFire, а не синтезированные
    /// события Input System: подделывать нажатия в плеере ненадёжно, а трогать боевой
    /// путь ввода ради съёмки нельзя. Направление выстрела выбирается перебором — берём
    /// то, что реально соберёт группу, иначе на видео игрок бы мазал.
    /// </summary>
    public static class PromoCapture
    {
        public const int Fps = 30;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            string outDir = Arg("-promo");
            if (string.IsNullOrEmpty(outDir)) return;

            // Одинаковая раскладка доски от запуска к запуску: RU- и EN-версии одного
            // материала должны отличаться только языком, иначе их не сравнить.
            Random.InitState(20260801);

            int level = int.TryParse(Arg("-plevel"), out int lv) ? lv : 6;
            PlayerPrefs.SetInt("cat_save_ver", 5);
            PlayerPrefs.SetInt("cat_level", level);
            // -onboard: снимаем кадр обучения (оно идёт только на 1 уровне и только
            // если раньше его не проходили).
            PlayerPrefs.SetInt("cat_onboarded", Arg("-onboard") != null ? 0 : 1);
            PlayerPrefs.SetInt("cat_seen_ice", 1);
            PlayerPrefs.SetInt("cat_seen_slime", 1);
            PlayerPrefs.SetInt("cat_seen_rock", 1);
            // Бустеры заряжены и разблокированы: на промо они должны быть видны живыми,
            // а не под замком — это часть механики, которую мы продаём.
            PlayerPrefs.SetInt("cat_rainbow", 7);
            PlayerPrefs.SetInt("cat_bomb", 4);
            PlayerPrefs.SetInt(GameManager.KeyRainbowGranted, 1);
            PlayerPrefs.SetInt(GameManager.KeyBombGranted, 1);
            PlayerPrefs.SetInt("cat_grant_backfill", 1);
            // Подарок «уже забран»: модалка перекрыла бы кадр геймплея.
            PlayerPrefs.SetString("cat_daily", System.DateTime.UtcNow.ToString("yyyyMMdd"));
            PlayerPrefs.Save();

            var go = new GameObject("PromoCapture");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>().Begin(outDir, Arg("-promomode") ?? "shots");
        }

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == name) return a[i + 1];
            return null;
        }

        class Runner : MonoBehaviour
        {
            string _dir, _mode;
            int _frame;
            bool _recording;

            public void Begin(string dir, string mode)
            {
                _dir = dir; _mode = mode;
                System.IO.Directory.CreateDirectory(dir);
                StartCoroutine(Run());
            }

            IEnumerator Run()
            {
                bool video = _mode == "video";
                if (video)
                {
                    // Фиксированный шаг времени: игра считает кадры «как будто» по 1/30 с,
                    // сколько бы реально ни занял рендер и запись файла. Без этого запись
                    // на диск растягивала бы игровое время и анимации дёргались.
                    Time.captureDeltaTime = 1f / Fps;
                    StartCoroutine(RecordLoop());
                }

                var title = Object.FindAnyObjectByType<TitleScreen>();
                float t0 = Time.realtimeSinceStartup;
                while (title == null && Time.realtimeSinceStartup - t0 < 20f)
                { title = Object.FindAnyObjectByType<TitleScreen>(); yield return null; }
                if (title == null) { Debug.LogError("[Promo] нет TitleScreen"); Application.Quit(1); yield break; }

                // Титул держим в кадре только на видео — это те самые «до 30 %»
                // не-геймплея, которые Яндекс разрешает под оформление.
                _recording = video;
                yield return Wait(video ? 2.2f : 1.2f);

                var m = typeof(TitleScreen).GetMethod("FadeOutAndLaunch",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                title.StartCoroutine((IEnumerator)m.Invoke(title, null));

                GameManager gm = null;
                t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 20f)
                {
                    gm = Object.FindAnyObjectByType<GameManager>();
                    if (gm != null && gm.BubbleCount > 0) break;
                    yield return null;
                }
                if (gm == null) { Debug.LogError("[Promo] нет GameManager"); Application.Quit(1); yield break; }

                var shooter = Object.FindAnyObjectByType<Shooter>();
                var board   = Object.FindAnyObjectByType<BoardManager>();
                yield return Wait(1.6f);   // доска «падает» сверху — ждём конца анимации

                if (video) yield return VideoScript(gm, shooter, board);
                else       yield return ShotScript(gm, shooter, board);
                yield return Wait(0.1f);

                _recording = false;
                yield return null;
                Debug.Log($"[Promo] готово, кадров: {_frame}");
                Application.Quit(0);
            }

            // ---------- сценарий статичных кадров ----------

            /// <summary>
            /// Один кадр за запуск: каждый снимается на своём уровне, чтобы три
            /// скриншота показывали разный контент, а не одну и ту же доску трижды.
            /// Номер приходит в -promomode (shot1/shot2/shot3), уровень — в -plevel.
            /// </summary>
            IEnumerator ShotScript(GameManager gm, Shooter sh, BoardManager board)
            {
                Vector3 aim;
                switch (_mode)
                {
                    // Кадр 1: полная доска и натянутый прицел. Механика читается за
                    // секунду просмотра — куда целиться и чем стрелять.
                    case "shot1":
                        // Узкий верхний сектор: при пологих углах пунктир уходит через
                        // весь экран и в кадре наезжает на пушку и слот «следующий»,
                        // читаясь как каша. Ровная диагональ вверх понятна с одного взгляда.
                        aim = BestDirection(sh, board, out _, 55, 125);
                        sh.DebugAim(aim);
                        yield return Wait(0.35f);
                        yield return Capture("01_aim", png: true);
                        break;

                    // Экран таблицы лидеров на фейковых данных из YandexBridge-стаба —
                    // там намеренно есть латинские, кириллические и смешанные имена.
                    // Единственный способ УВИДЕТЬ баг отрисовки имён до заливки на площадку.
                    case "lb":
                        gm.OpenLeaderboard();
                        yield return Wait(2.5f);
                        yield return Capture("lb", png: true);
                        break;

                    // Экран поражения — для проверки вёрстки кнопок «второго шанса».
                    // Состояние выставляем напрямую: доигрывать уровень до нуля выстрелов
                    // ради одного кадра долго и незачем.
                    // Кадр обучения: ждём, пока рука доедет до середины жеста.
                    case "onboard":
                        yield return Wait(3.0f);
                        yield return Capture("onboard", png: true);
                        break;

                    case "lose":
                        SetPrivate(gm, "shotsLeft", 0);
                        SetPrivate(gm, "<State>k__BackingField", GameState.Lose);
                        yield return Wait(2.4f);
                        yield return Capture("lose", png: true);
                        break;

                    // Кадр 2: ПЕРВОЕ попадание уровня. Именно первое: на втором и дальше
                    // включается надпись «КОМБО xN», а она оранжевая поверх пёстрых шаров
                    // и в кадре читается как мусор. Здесь остаются частицы и кольцо удара.
                    case "shot2":
                        aim = BestDirection(sh, board, out _);
                        sh.DebugFire(aim);
                        yield return WaitResolved(gm, 0.16f);
                        yield return Capture("02_pop", png: true);
                        break;

                    // Кадр 3: доска разобрана до середины, бустеры заряжены и видны.
                    // Показывает и прогресс, и то, что игроку есть чем себе помочь.
                    default:
                        for (int i = 0; i < 7; i++)
                        {
                            aim = BestDirection(sh, board, out _);
                            if (!sh.DebugFire(aim)) break;
                            yield return WaitResolved(gm, 0f);
                            if (gm.State != GameState.Aiming) break;
                        }
                        aim = BestDirection(sh, board, out _);
                        sh.DebugAim(aim);
                        yield return Wait(0.45f);
                        yield return Capture("03_progress", png: true);
                        break;
                }
            }

            // ---------- сценарий видео ----------

            IEnumerator VideoScript(GameManager gm, Shooter sh, BoardManager board)
            {
                // ~14 секунд непрерывного геймплея при общей длине ~18 с. Требование
                // Яндекса — не менее 70 % хронометража занимает реальный геймплей.
                float until = Time.time + 13.5f;
                bool usedRainbow = false;

                while (Time.time < until && gm.State == GameState.Aiming)
                {
                    // Ближе к концу показываем бустер: это отдельная механика, и её надо
                    // успеть продать до конца ролика.
                    if (!usedRainbow && Time.time > until - 5f && gm.RainbowCharges > 0)
                    {
                        usedRainbow = true;
                        gm.UseRainbow();
                        yield return Wait(0.7f);
                    }

                    Vector3 dir = BestDirection(sh, board, out _);
                    // Живой прицел перед выстрелом: без паузы ролик выглядит как автомат,
                    // а не как игра, в которую играет человек.
                    yield return AimFor(sh, dir, 0.55f);
                    if (!sh.DebugFire(dir)) break;
                    yield return WaitResolved(gm, 0.35f);
                }

                // Хвост: экран победы со звёздами — награда, ради которой всё затевалось.
                if (gm.State != GameState.Win)
                {
                    SetPrivate(gm, "_lastStars", 3);
                    SetPrivate(gm, "<State>k__BackingField", GameState.Win);
                }
                yield return Wait(3.2f);
            }

            IEnumerator AimFor(Shooter sh, Vector3 dir, float seconds)
            {
                float t = 0f;
                while (t < seconds) { sh.DebugAim(dir); t += Time.deltaTime; yield return null; }
            }

            // ---------- выбор выстрела ----------

            /// <summary>
            /// Перебирает направления и берёт то, что соберёт самую большую группу
            /// своего цвета. gain — сколько одноцветных соседей будет у места посадки
            /// (2 и больше означает, что шары точно лопнут).
            /// </summary>
            Vector3 BestDirection(Shooter sh, BoardManager board, out int gain,
                                  int fromDeg = 20, int toDeg = 160)
            {
                gain = -1;
                Vector3 best = new Vector3(0.3f, 1f, 0f).normalized;
                if (sh == null || board == null) return best;

                int myColor = sh.DebugCurrentColor;
                int bestScore = int.MinValue;
                for (int deg = fromDeg; deg <= toDeg; deg += 2)
                {
                    float r = deg * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(r), Mathf.Sin(r), 0f);
                    if (!sh.DebugPredict(dir, out var cell)) continue;

                    int same = board.CountSameNeighbors(cell, myColor);
                    // Чуть выше по доске — интереснее в кадре: шар летит дальше и дольше.
                    int score = same * 10 + cell.y;
                    if (score > bestScore) { bestScore = score; gain = same; best = dir; }
                }
                if (gain < 0) gain = 0;
                return best;
            }

            IEnumerator WaitResolved(GameManager gm, float grabDelay)
            {
                // Даём выстрелу долететь и раствориться анимациям. grabDelay > 0 —
                // это момент «сразу после попадания», когда в кадре ещё живут частицы.
                float t0 = Time.time;
                while (gm.State == GameState.Resolving && Time.time - t0 < 4f) yield return null;
                if (grabDelay > 0f) yield return Wait(grabDelay);
                else                yield return Wait(0.12f);
            }

            IEnumerator Wait(float seconds)
            {
                // Именно игровое время: при Time.captureDeltaTime оно идёт ровными
                // шагами 1/30, и длительность в ролике получается ровно такой, как здесь.
                float t = 0f;
                while (t < seconds) { t += Time.deltaTime; yield return null; }
            }

            // ---------- запись ----------

            IEnumerator RecordLoop()
            {
                var wait = new WaitForEndOfFrame();
                while (true)
                {
                    yield return wait;
                    if (_recording) WriteFrame();
                }
            }

            void WriteFrame()
            {
                var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
                tex.Apply();
                // JPG, а не PNG: 540 кадров 1080×1920 в PNG — это гигабайты на диске,
                // а кодек всё равно пережимает. Качество 92 артефактов не даёт.
                System.IO.File.WriteAllBytes(
                    System.IO.Path.Combine(_dir, $"f{_frame:D5}.jpg"), tex.EncodeToJPG(92));
                _frame++;
                Object.Destroy(tex);
            }

            IEnumerator Capture(string name, bool png)
            {
                yield return new WaitForEndOfFrame();
                var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
                tex.Apply();
                string path = System.IO.Path.Combine(_dir, name + (png ? ".png" : ".jpg"));
                System.IO.File.WriteAllBytes(path, png ? tex.EncodeToPNG() : tex.EncodeToJPG(95));
                Debug.Log($"[Promo] снято: {path} ({Screen.width}x{Screen.height})");
                Object.Destroy(tex);
            }

            static void SetPrivate(object target, string field, object value)
            {
                var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null) f.SetValue(target, value);
                else Debug.LogWarning($"[Promo] нет поля {field}");
            }
        }
    }
}
#endif
