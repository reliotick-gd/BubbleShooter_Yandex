using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// SFX. Drop-in: файл Resources/Audio/{name}.wav заменит синтез. Нет файла →
    /// синтез: транзиент + тон + хвост, фильтрованный шум, лёгкий рандом высоты.
    /// </summary>
    public static class AudioService
    {
        const int Rate = 44100;

        static AudioSource _src;
        static AudioClip _shoot, _pop, _drop, _win, _lose, _star, _click, _land;

        static AudioSource Src
        {
            get
            {
                if (_src != null) return _src;
                var go = new GameObject("AudioService") { hideFlags = HideFlags.HideInHierarchy };
                Object.DontDestroyOnLoad(go);
                _src = go.AddComponent<AudioSource>();
                _src.spatialBlend = 0f;
                _src.volume = 0.7f;
                return _src;
            }
        }

        public static void Prewarm()
        {
            _star  ??= Load("star",  BuildStar);
            _shoot ??= Load("shoot", BuildShoot);
            _pop   ??= Load("pop",   BuildPop);
            _drop  ??= Load("drop",  BuildDrop);
            _win   ??= Load("win",   BuildWin);
            _lose  ??= Load("lose",  BuildLose);
            _click ??= Load("click", BuildClick);
            _land  ??= Load("land",  BuildLand);
        }

        static AudioClip Load(string name, System.Func<AudioClip> build)
        {
            var asset = Resources.Load<AudioClip>("Audio/" + name);
            return asset != null ? asset : build();
        }

        // Рандом высоты делает повторяющиеся звуки «живыми» (поп/выстрел).
        static void Play(AudioClip clip, float vol, float pitchJitter = 0f)
        {
            Src.pitch = 1f + (pitchJitter > 0f ? Random.Range(-pitchJitter, pitchJitter) : 0f);
            Src.PlayOneShot(clip, vol);
        }

        public static void PlayStar()  { _star  ??= Load("star",  BuildStar);  Play(_star,  0.5f); }
        public static void PlayShoot() { _shoot ??= Load("shoot", BuildShoot); Play(_shoot, 0.26f, 0.05f); }
        public static void PlayLand()  { _land  ??= Load("land",  BuildLand);  Play(_land,  0.28f, 0.04f); }
        public static void PlayPop()   { _pop   ??= Load("pop",   BuildPop);   Play(_pop,   0.72f, 0.08f); }
        public static void PlayDrop()  { _drop  ??= Load("drop",  BuildDrop);  Play(_drop,  0.5f); }
        public static void PlayWin()   { _win   ??= Load("win",   BuildWin);   Play(_win,   0.6f); }
        public static void PlayLose()  { _lose  ??= Load("lose",  BuildLose);  Play(_lose,  0.5f); }
        public static void PlayClick() { _click ??= Load("click", BuildClick); Play(_click, 0.4f, 0.04f); }

        // Поп = тело-пузырь (чирп вниз) + крэкл-транзиент + высокие искры — «между пузырём и фейерверком».
        static readonly float[] SparkT = { 0.05f, 0.10f, 0.15f };
        static readonly float[] SparkF = { 1500f, 1850f, 2200f };
        static AudioClip BuildPop()
        {
            int n = (int)(Rate * 0.22f);
            var s = new float[n];
            float prevNoise = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n, tt = (float)i / Rate;
                float freq = Mathf.Lerp(560f, 190f, Mathf.Sqrt(t));
                float body = Mathf.Sin(2f * Mathf.PI * freq * tt) * Mathf.Exp(-t * 7f) * 0.5f;

                float raw = Random.value * 2f - 1f;
                prevNoise = Mathf.Lerp(prevNoise, raw, 0.4f);
                float crackle = Mathf.Exp(-t * 26f) * prevNoise * 0.42f;

                float spark = 0f;
                for (int k = 0; k < SparkT.Length; k++)
                {
                    float dt = tt - SparkT[k];
                    if (dt >= 0f) spark += Mathf.Sin(2f * Mathf.PI * SparkF[k] * tt) * Mathf.Exp(-dt * 34f) * 0.16f;
                }
                s[i] = body + crackle + spark;
            }
            return MakeClip("pop", s);
        }

        // Прилёт шара в конечную точку: очень мягкий короткий «ток».
        static AudioClip BuildLand()
        {
            int n = (int)(Rate * 0.07f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n, tt = (float)i / Rate;
                float freq = Mathf.Lerp(300f, 170f, t);
                s[i] = Mathf.Sin(2f * Mathf.PI * freq * tt) * Mathf.Exp(-t * 22f) * 0.35f;
            }
            return MakeClip("land", s);
        }

        // Выстрел: мягкий деревянный pluck (тон + октава, плавная атака, быстрый спад).
        static AudioClip BuildShoot()
        {
            int n = (int)(Rate * 0.11f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n, tt = (float)i / Rate;
                float freq = Mathf.Lerp(460f, 340f, t);
                float attack = Mathf.Min(1f, t * 24f);
                float env = Mathf.Exp(-t * 9f) * attack;
                float tone = Mathf.Sin(2f * Mathf.PI * freq * tt)
                           + 0.2f * Mathf.Sin(2f * Mathf.PI * freq * 2f * tt);
                s[i] = tone * env * 0.32f;
            }
            return MakeClip("shoot", s);
        }

        // Падение грозди: глухой мягкий «бум».
        static AudioClip BuildDrop()
        {
            int n = (int)(Rate * 0.20f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n, tt = (float)i / Rate;
                float freq = Mathf.Lerp(120f, 55f, t);
                float env = Mathf.Exp(-t * 10f);
                s[i] = Mathf.Sin(2f * Mathf.PI * freq * tt) * env * 0.5f;
            }
            return MakeClip("drop", s);
        }

        // Победа: тёплый арпеджио C-E-G-C с октавным призвуком.
        static AudioClip BuildWin()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            float dur = 0.66f, step = dur / notes.Length;
            int n = (int)(Rate * dur);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                int ni = Mathf.Clamp((int)(t / step), 0, notes.Length - 1);
                float lt = (t - ni * step) / step;
                float env = Mathf.Sin(lt * Mathf.PI) * (1f - ni * 0.08f);
                float f = notes[ni];
                float tone = Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2f * t);
                s[i] = tone * env * 0.32f;
            }
            return MakeClip("win", s);
        }

        // Проигрыш: мягкий нисходящий глиссандо (без резкости).
        static AudioClip BuildLose()
        {
            float dur = 0.5f;
            int n = (int)(Rate * dur);
            var s = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float freq = Mathf.Lerp(440f, 160f, t * t);
                phase += freq / Rate;
                float env = Mathf.Sin(t * Mathf.PI) * 0.9f;
                s[i] = Mathf.Sin(phase * 2f * Mathf.PI) * env * 0.4f;
            }
            return MakeClip("lose", s);
        }

        // Звезда/искра: короткий яркий динь.
        static AudioClip BuildStar()
        {
            int n = (int)(Rate * 0.09f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n, tt = (float)i / Rate;
                float freq = Mathf.Lerp(1050f, 1500f, t);
                float env = Mathf.Exp(-t * 7f);
                s[i] = Mathf.Sin(2f * Mathf.PI * freq * tt) * env * 0.4f;
            }
            return MakeClip("star", s);
        }

        // UI-клик: тихий короткий блип.
        static AudioClip BuildClick()
        {
            int n = (int)(Rate * 0.045f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n, tt = (float)i / Rate;
                float env = Mathf.Exp(-t * 26f);
                s[i] = Mathf.Sin(2f * Mathf.PI * 680f * tt) * env * 0.3f;
            }
            return MakeClip("click", s);
        }

        static AudioClip MakeClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
