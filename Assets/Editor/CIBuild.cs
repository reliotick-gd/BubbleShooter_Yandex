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
