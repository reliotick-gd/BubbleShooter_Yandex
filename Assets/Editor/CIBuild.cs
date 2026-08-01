using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CozyAnimalTown.EditorTools
{
    /// <summary>
    /// Сборка из командной строки — нужна, чтобы снимать реальные скриншоты геймплея
    /// без ручного захода в редактор:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod CozyAnimalTown.EditorTools.CIBuild.BuildWindows
    /// Development-сборка: только в ней компилируется AutoShot (автопилот для скриншота).
    /// </summary>
    public static class CIBuild
    {
        /// <summary>
        /// Релизный WebGL для Яндекс Игр:
        ///   Unity.exe -batchmode -quit -projectPath . -executeMethod CozyAnimalTown.EditorTools.CIBuild.BuildWebGL
        /// БЕЗ BuildOptions.Development — иначе в архив уедут AutoShot и dev-консоль.
        /// Шаблон берётся из ProjectSettings (PROJECT:YandexGames): именно он тянет /sdk.js
        /// и инициализирует ysdk ДО загрузки Unity, поэтому Loc успевает прочитать язык.
        /// </summary>
        public static void BuildWebGL()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/SampleScene.unity" };

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            // Имя папки вывода = префикс файлов билда (YandexGames.data.br и т.д.).
            // Оно НЕ произвольное: именно с этим префиксом черновик грузился на площадке
            // раньше. Сборка в Builds/WebGL давала WebGL.*.br и падала с 404 на CDN.
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes           = scenes,
                locationPathName = "Builds/YandexGames",
                target           = BuildTarget.WebGL,
                options          = BuildOptions.None,
            });

            var s = report.summary;
            Debug.Log($"[CIBuild] WebGL {s.result}, {s.totalErrors} errors, {s.totalSize} bytes");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>
        /// Сборка для съёмки промоматериалов:
        ///   Unity.exe -batchmode -quit -nographics -projectPath . \
        ///     -executeMethod CozyAnimalTown.EditorTools.CIBuild.BuildPromo
        ///
        /// Это РЕЛИЗНАЯ сборка (без BuildOptions.Development) с отдельным символом
        /// PROMO_CAPTURE. Так надо потому, что development-плеер рисует в углу плашку
        /// «Development Build», а Яндекс требует промоматериалы без посторонних
        /// элементов интерфейса — кадр с плашкой снимут на модерации.
        ///
        /// Символ ставится ТОЛЬКО группе Standalone, поэтому в WebGL-билд ни PromoCapture,
        /// ни отладочные методы Shooter не попадают ни при каких обстоятельствах.
        /// </summary>
        public static void BuildPromo()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/SampleScene.unity" };

            // extraScriptingDefines, а НЕ PlayerSettings.SetScriptingDefineSymbols:
            // символ действует только на эту сборку и никуда не записывается. Через
            // PlayerSettings он оставался в ProjectSettings.asset — BuildPlayer сохраняет
            // настройки по ходу дела, а восстановить их обратно в batchmode нечем:
            // EditorApplication.Exit убивает процесс раньше любого отложенного сохранения.
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes           = scenes,
                locationPathName = "Builds/Promo/BubbleShooter.exe",
                target           = BuildTarget.StandaloneWindows64,
                options          = BuildOptions.None,
                extraScriptingDefines = new[] { "PROMO_CAPTURE" },
            });

            var result = report.summary.result;
            Debug.Log($"[CIBuild] Promo {result}, {report.summary.totalSize} bytes");
            EditorApplication.Exit(result == BuildResult.Succeeded ? 0 : 1);
        }

        public static void BuildWindows()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/SampleScene.unity" };

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes           = scenes,
                locationPathName = "Builds/Win/BubbleShooter.exe",
                target           = BuildTarget.StandaloneWindows64,
                options          = BuildOptions.Development,
            });

            var s = report.summary;
            Debug.Log($"[CIBuild] {s.result}, {s.totalErrors} errors, {s.totalSize} bytes");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
