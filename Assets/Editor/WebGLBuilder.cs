#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CozyAnimalTown.EditorTools
{
    /// <summary>
    /// Headless-сборка WebGL для Яндекс Игр. Запуск из CLI:
    ///   Unity.exe -quit -batchmode -nographics -buildTarget WebGL
    ///     -projectPath &lt;proj&gt;
    ///     -executeMethod CozyAnimalTown.EditorTools.WebGLBuilder.Build
    ///     -logFile &lt;log&gt;
    ///
    /// Ничего в настройках НЕ меняет — наследует всё из ProjectSettings проекта
    /// (Brotli, IL2CPP OptimizeSize, Managed Stripping High, шаблон YandexGames,
    /// версия 2.0.1.0). Сцены берутся из Build Settings (enabled).
    /// Результат: папка Builds/YandexGames с index.html в корне.
    /// </summary>
    public static class WebGLBuilder
    {
        public static void Build()
        {
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "YandexGames");
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[WebGLBuilder] Нет активных сцен в Build Settings.");
                EditorApplication.Exit(2);
                return;
            }

            var opts = new BuildPlayerOptions
            {
                scenes           = scenes,
                locationPathName = outDir,
                target           = BuildTarget.WebGL,
                targetGroup      = BuildTargetGroup.WebGL,
                options          = BuildOptions.None,
            };

            Debug.Log($"[WebGLBuilder] Старт сборки WebGL → {outDir}\n" +
                      $"  версия: {Application.version}\n" +
                      $"  сцены:  {string.Join(", ", scenes)}");

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary s = report.summary;

            Debug.Log($"[WebGLBuilder] Итог: {s.result}, размер {s.totalSize} байт, " +
                      $"ошибок {s.totalErrors}, время {s.totalTime}");

            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
#endif
