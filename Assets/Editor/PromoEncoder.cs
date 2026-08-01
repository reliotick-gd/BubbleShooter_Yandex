using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;

namespace CozyAnimalTown.EditorTools
{
    /// <summary>
    /// Склеивает кадры, снятые PromoCapture, в MP4 (H.264).
    ///
    /// Кодек берём из самого редактора — UnityEditor.Media.MediaEncoder входит в
    /// поставку Unity, поэтому для промо-видео не нужен ни ffmpeg, ни Unity Recorder,
    /// ни какие-либо скачивания.
    ///
    /// Запуск:
    ///   Unity.exe -batchmode -quit -projectPath . \
    ///     -executeMethod CozyAnimalTown.EditorTools.PromoEncoder.Encode \
    ///     -frames D:\out\frames -video D:\out\promo.mp4 -fps 30
    ///
    /// ВАЖНО: без -nographics. Кодировщику нужен графический контекст, в headless-режиме
    /// он падает на создании сессии.
    /// </summary>
    public static class PromoEncoder
    {
        public static void Encode()
        {
            string dir = Arg("-frames");
            string outPath = Arg("-video");
            int fps = int.TryParse(Arg("-fps"), out int f) ? f : 30;

            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(outPath))
            { Fail("нужны -frames и -video"); return; }

            var files = Directory.GetFiles(dir, "f*.jpg").OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (files.Length == 0) { Fail($"в {dir} нет кадров f*.jpg"); return; }

            // Размер берём из первого кадра: съёмка идёт и в 1080x1920, и в 1920x1080,
            // а H.264 требует чётных сторон — округляем вниз, если вдруг нечётные.
            var probe = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            probe.LoadImage(File.ReadAllBytes(files[0]));
            int w = probe.width  & ~1;
            int h = probe.height & ~1;
            UnityEngine.Object.DestroyImmediate(probe);

            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            if (File.Exists(outPath)) File.Delete(outPath);

            var video = new VideoTrackAttributes
            {
                frameRate = new MediaRational(fps),
                width     = (uint)w,
                height    = (uint)h,
                includeAlpha = false
            };

            Debug.Log($"[PromoEncoder] {files.Length} кадров {w}x{h} @ {fps} fps -> {outPath}");

            // Два буфера на весь проход. Почему именно два: LoadImage подгоняет формат
            // текстуры под содержимое файла, и для JPEG она становится RGB24, а
            // MediaEncoder.AddFrame принимает строго RGBA32 («texture format 3 expected
            // to be 4»). Поэтому декодируем в один, а в кодек отдаём второй.
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var frame   = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                using (var enc = new MediaEncoder(outPath, video))
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        if (!decoded.LoadImage(File.ReadAllBytes(files[i])))
                        { Debug.LogWarning($"[PromoEncoder] пропущен кадр {files[i]}"); continue; }

                        if (decoded.width != w || decoded.height != h)
                        { Fail($"кадр {Path.GetFileName(files[i])} имеет размер {decoded.width}x{decoded.height}, ожидался {w}x{h}"); return; }

                        frame.SetPixels32(decoded.GetPixels32());
                        frame.Apply(false);
                        enc.AddFrame(frame);
                        if (i % 120 == 0) Debug.Log($"[PromoEncoder] {i}/{files.Length}");
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(frame);
                UnityEngine.Object.DestroyImmediate(decoded);
            }

            var info = new FileInfo(outPath);
            if (!info.Exists || info.Length == 0) { Fail("файл не создан"); return; }
            Debug.Log($"[PromoEncoder] готово: {outPath} ({info.Length / 1024} КБ, " +
                      $"{files.Length / (float)fps:F1} с)");
            EditorApplication.Exit(0);
        }

        static void Fail(string why)
        {
            Debug.LogError("[PromoEncoder] " + why);
            EditorApplication.Exit(1);
        }

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == name) return a[i + 1];
            return null;
        }
    }
}
