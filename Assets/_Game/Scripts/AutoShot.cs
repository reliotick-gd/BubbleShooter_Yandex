#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Автопилот для съёмки скриншота геймплея из командной строки:
    ///   BubbleShooter.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -shot out.png
    /// Ставит нужное состояние прогресса, проматывает титул и онбординг, ждёт сборки доски,
    /// снимает кадр и выходит. Компилируется ТОЛЬКО в development-сборке и в редакторе —
    /// в релизном WebGL-билде этого кода нет.
    /// </summary>
    public static class AutoShot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            string path = Arg("-shot");
            if (string.IsNullOrEmpty(path)) return;

            // Состояние на кадре: 8-й уровень (бомба открывается на 7-м), онбординг пройден,
            // радуга заряжена, бомба пустая — чтобы попал в кадр бейдж «▶ +3» (п.4.5.1).
            PlayerPrefs.SetInt("cat_save_ver", 5);
            PlayerPrefs.SetInt("cat_level", 8);
            PlayerPrefs.SetInt("cat_onboarded", 1);
            PlayerPrefs.SetInt("cat_seen_ice", 1);
            PlayerPrefs.SetInt("cat_seen_slime", 1);
            PlayerPrefs.SetInt("cat_seen_rock", 1);
            PlayerPrefs.SetInt("cat_rainbow", 5);
            PlayerPrefs.SetInt("cat_bomb", 0);
            // Подарок за «сегодня» уже забран — иначе он перекрыл бы кадр геймплея.
            // Для съёмки самой модалки есть флаг -daily.
            PlayerPrefs.SetString("cat_daily",
                Arg("-daily") != null ? "" : System.DateTime.UtcNow.ToString("yyyyMMdd"));
            PlayerPrefs.Save();

            var go = new GameObject("AutoShot");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>().Begin(path);
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
            string _path;
            public void Begin(string path) { _path = path; StartCoroutine(Run()); }

            IEnumerator Run()
            {
                // 1. Дожидаемся титула и жмём «Играть» за игрока. Колбэк приватный —
                //    достаём рефлексией, чтобы не тащить тестовый API в игровой класс.
                TitleScreen title = null;
                float t0 = Time.realtimeSinceStartup;
                while (title == null && Time.realtimeSinceStartup - t0 < 20f)
                {
                    title = Object.FindAnyObjectByType<TitleScreen>();
                    yield return null;
                }
                if (title == null) { Debug.LogError("[AutoShot] нет TitleScreen"); Application.Quit(1); yield break; }

                yield return new WaitForSecondsRealtime(1.5f);   // фейд-ин титула

                // Именно FadeOutAndLaunch, а не Destroy+_onStart: канвас титула —
                // отдельный объект с sortingOrder 500, и убирает его только сам титул.
                var m = typeof(TitleScreen).GetMethod("FadeOutAndLaunch",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (m == null) { Debug.LogError("[AutoShot] нет FadeOutAndLaunch"); Application.Quit(1); yield break; }
                title.StartCoroutine((IEnumerator)m.Invoke(title, null));

                // 2. Ждём, пока доска реально соберётся.
                GameManager gm = null;
                t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 20f)
                {
                    gm = Object.FindAnyObjectByType<GameManager>();
                    if (gm != null && gm.BubbleCount > 0) break;
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(2.5f);   // анимации входа/подсказки

                // Наводим прицел по диагонали вверх — иначе пунктир смотрит туда, где
                // случайно оказался курсор, и на кадре выглядит мусором.
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null)
                {
                    mouse.WarpCursorPosition(new Vector2(Screen.width * 0.60f, Screen.height * 0.86f));
                    yield return null;
                    yield return new WaitForSecondsRealtime(0.6f);
                }

                yield return Capture(_path);

                // Второй кадр — модалка настроек: проверяем, что модальный рут не поехал
                // в широкой раскладке и затемнение кроет весь экран, а не колонку.
                string second = Arg("-shot2");
                if (!string.IsNullOrEmpty(second) && gm != null)
                {
                    gm.SetPaused(true);
                    yield return new WaitForSecondsRealtime(1.0f);
                    yield return Capture(second);
                }

                // Оффер «+5 выстрелов» показывается только при остатке ≤ 2. Дожать его
                // честной стрельбой нельзя, поэтому правим приватное поле рефлексией —
                // файл всё равно только для development-сборки.
                string midShot = Arg("-shotmid");
                if (!string.IsNullOrEmpty(midShot) && gm != null)
                {
                    SetPrivate(gm, "shotsLeft", 2);
                    yield return new WaitForSecondsRealtime(0.6f);
                    yield return Capture(midShot);
                }

                // Экран победы: выставляем состояние напрямую, чтобы снять вёрстку звёзд
                // и счёта, не проходя уровень целиком.
                string winShot = Arg("-shotwin");
                if (!string.IsNullOrEmpty(winShot) && gm != null)
                {
                    SetPrivate(gm, "_levelScore", 4830);
                    SetPrivate(gm, "_lastStars", 3);
                    SetPrivate(gm, "<State>k__BackingField", GameState.Win);
                    yield return new WaitForSecondsRealtime(2.2f);
                    yield return Capture(winShot);
                }

                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit(0);
            }

            static void SetPrivate(object target, string field, object value)
            {
                var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null) f.SetValue(target, value);
                else Debug.LogWarning($"[AutoShot] нет поля {field}");
            }

            // Модуль screencapture из manifest.json выпилен ради веса билда, поэтому
            // читаем бэкбуфер сами — imageconversion в проекте есть.
            static IEnumerator Capture(string path)
            {
                yield return new WaitForEndOfFrame();
                var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
                tex.Apply();
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[AutoShot] снято: {path} ({Screen.width}x{Screen.height})");
                Object.Destroy(tex);
            }
        }
    }
}
#endif
