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

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes           = scenes,
                locationPathName = "Builds/WebGL",
                target           = BuildTarget.WebGL,
                options          = BuildOptions.None,
            });

            var s = report.summary;
            Debug.Log($"[CIBuild] WebGL {s.result}, {s.totalErrors} errors, {s.totalSize} bytes");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
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
