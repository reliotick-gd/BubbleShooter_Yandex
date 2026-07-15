using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Постоянная фоновая музыка. По умолчанию — мягкий процедурный луп
    /// (до-мажорная пентатоника, синусы), без файлов и без рисков по копирайту
    /// при модерации площадки. Если положить трек в Resources/Music/bg_music
    /// (ogg/wav/mp3) — он подхватится сам и заменит процедурный.
    ///
    /// Громкость низкая (фон, не перебивает SFX). На время рекламы глушится вместе
    /// со всем звуком (AudioListener.pause в YandexBridge.OnAdOpen). Запускать ТОЛЬКО
    /// после жеста игрока (клик «Играть») — иначе браузер не даст автоплей и WebAudio
    /// будет suspended. Поэтому Play() зовётся из GameManager.Init (после тапа на тайтле).
    /// </summary>
    public static class MusicService
    {
        const int   Rate   = 44100;
        const float Volume = 0.22f;   // фон — тихо

        static AudioSource _src;
        static bool _started;

        public static void Play()
        {
            if (_started) return;
            _started = true;

            var go = new GameObject("MusicService") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            _src = go.AddComponent<AudioSource>();
            _src.loop         = true;
            _src.spatialBlend = 0f;
            _src.volume       = Volume;
            _src.priority     = 0;     // не вытесняется при нехватке голосов

            // готовый трек из Resources/Music/bg_music — если положили; иначе процедурный луп
            var file = Resources.Load<AudioClip>("Music/bg_music");
            _src.clip = file != null ? file : BuildLoop();
            _src.Play();
        }

        // Пэд фазо-выровнен, мелодия затухает до границы — поэтому петля бесшовна
        // (без щелчка на стыке).
        static AudioClip BuildLoop()
        {
            const float loopSec = 16f;
            int n = (int)(Rate * loopSec);
            var buf = new float[n];

            float padRoot  = AlignFreq(130.81f, loopSec); // C3
            float padFifth = AlignFreq(196.00f, loopSec); // G3
            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / Rate;
                // «дыхание» громкости: 2 полных цикла за луп → на стыке совпадает
                float lfo = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (2f / loopSec) * t - Mathf.PI * 0.5f);
                float pad = Mathf.Sin(2f * Mathf.PI * padRoot  * t) * 0.6f
                          + Mathf.Sin(2f * Mathf.PI * padFifth * t) * 0.4f;
                buf[i] = pad * 0.16f * (0.55f + 0.45f * lfo);
            }

            float[] scale = { 261.63f, 293.66f, 329.63f, 392.00f, 440.00f, 523.25f }; // C-D-E-G-A-C5
            int[]   seq   = { 0, 2, 4, 3, 4, 2, 1, 3, 4, 5, 3, 2, 1, 2, 0, -1 }; // -1 = пауза на стыке
            float noteDur = loopSec / seq.Length;
            for (int s = 0; s < seq.Length; s++)
            {
                if (seq[s] < 0) continue;
                float freq  = scale[seq[s]];
                int   start = (int)(s * noteDur * Rate);
                int   len   = (int)(noteDur * 0.95f * Rate); // короче ноты — успевает затихнуть
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float lt   = (float)i / Rate;
                    float env  = Mathf.Exp(-lt * 3.2f) * Mathf.Min(1f, lt * 60f); // мягкая атака + колокольный спад
                    float tone = Mathf.Sin(2f * Mathf.PI * freq      * lt)
                               + 0.25f * Mathf.Sin(2f * Mathf.PI * freq * 2f * lt);
                    buf[start + i] += tone * env * 0.12f;
                }
            }

            var clip = AudioClip.Create("bgm", n, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        // Округляет частоту так, чтобы в луп уложилось целое число периодов →
        // на стыке петли фаза синуса совпадает (нет щелчка). Сдвиг тона неслышим.
        static float AlignFreq(float freq, float loopSec) => Mathf.Round(freq * loopSec) / loopSec;
    }
}
