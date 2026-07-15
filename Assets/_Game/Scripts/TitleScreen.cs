using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CozyAnimalTown
{
    /// <summary>
    /// Титульный экран: логотип, плавающие зверьки, кнопка старта.
    /// Показывается поверх всего (sortOrder 500).
    /// Вызови Show(onStart), экран скроется сам после нажатия.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        Canvas     _canvas;
        GameObject _root;
        TMP_Text       _tapHint;
        Action     _onStart;
        bool       _ready;          // можно нажимать (короткая задержка после появления)
        bool       _closing;        // уже закрываемся

        // плавающие зверьки
        struct FloatAnim
        {
            public RectTransform rt;
            public float baseY, freq, amp, phase;
        }
        FloatAnim[] _floaters;

        static readonly Color BgTop    = new Color(0.960f, 0.940f, 0.902f, 1f);
        static readonly Color TxtMain  = new Color(0.42f, 0.34f, 0.27f, 1f);
        static readonly Color TxtHint  = new Color(0.58f, 0.49f, 0.41f, 1f);
        static readonly Color ColBtn   = new Color(0.42f, 0.76f, 0.52f, 1f);     // мягкий зелёный
        static readonly Color[] Palette =
        {
            new Color(0.93f,0.40f,0.42f), new Color(0.98f,0.76f,0.30f),
            new Color(0.52f,0.78f,0.52f), new Color(0.40f,0.66f,0.86f),
            new Color(0.78f,0.56f,0.86f),
        };

        public void Show(Action onStart)
        {
            _onStart = onStart;
            BuildUi();
            _root.SetActive(true);
            // Титул на экране = загрузка завершена → LoadingAPI.ready() зовём РОВНО здесь,
            // как требует Яндекс (п. 1.19), не привязывая к задержке ввода ниже.
            // Идемпотентно — повторные показы по кнопке «назад» = no-op.
            YandexBridge.GameReady();
            StartCoroutine(AllowInputAfter(0.6f));
            StartCoroutine(FadeIn());
        }

        void BuildUi()
        {
            _canvas = UiKit.CreateCanvas("TitleCanvas", 500);
            var col = UiKit.Column(_canvas);   // фиксированный 9:16-макет, поля — крем

            _root = new GameObject("TitleRoot", typeof(RectTransform));
            _root.transform.SetParent(col, false);
            var rootRt = (RectTransform)_root.transform;
            UiKit.Stretch(rootRt);

            // raycast: true — титул модальный, клики не должны проваливаться в HUD под
            // ним (иначе тап по месту шестерёнки закрывал титул И открывал паузу)
            var bg = UiKit.Panel(_root.transform, BgTop, true);
            UiKit.Stretch(bg.rectTransform);

            // RU-название выбрано по факт-чеку каталога («Шарики» — топ-запрос, ключевым
            // словом первым). ВАЖНО: должно совпадать с названием на странице каталога.
            string logo = Loc.T("Bubble Shooter:\nCozy Animals", "Шарики:\nМилые Зверята");
            var shadow = UiKit.Label(_root.transform, logo,
                                     88, TextAnchor.MiddleCenter,
                                     new Color(0.18f, 0.10f, 0.04f, 0.18f));
            shadow.fontStyle = FontStyles.Bold;
            FitTitle(shadow);
            UiKit.Anchor(shadow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                         new Vector2(7, -207), new Vector2(920, 220));

            var title = UiKit.Label(_root.transform, logo,
                                    88, TextAnchor.MiddleCenter, TxtMain);
            title.fontStyle = FontStyles.Bold;
            FitTitle(title);
            UiKit.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                         new Vector2(0, -200), new Vector2(920, 220));

            var sub = UiKit.Label(_root.transform,
                                  Loc.T("— match & pop the animals —", "— лопай шарики со зверятами —"),
                                  34, TextAnchor.MiddleCenter, TxtHint);
            UiKit.Anchor(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                         new Vector2(0, -400), new Vector2(860, 60));

            BuildFloaters();

            var btn = UiKit.Btn(_root.transform, Loc.T("  Play!  ", "  Играть!  "), 52, ColBtn, Color.white,
                                () => OnStart());
            btn.transition = Selectable.Transition.None;
            var btnRt = (RectTransform)btn.transform;
            UiKit.Anchor(btnRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                         new Vector2(0, 180), new Vector2(480, 130));
            UiKit.AddShadow(btnRt, 12f, -7f);

            _tapHint = UiKit.Label(_root.transform, Loc.T("tap anywhere", "нажми в любом месте"),
                                   32, TextAnchor.MiddleCenter, TxtHint);
            UiKit.Anchor(_tapHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                         new Vector2(0, 110), new Vector2(700, 50));

            // версия (мелко) — берём из PlayerSettings, чтобы не расходилась с bundleVersion
            var ver = UiKit.Label(_root.transform, $"v{Application.version}  ·  reliotick  ·  2026",
                                  24, TextAnchor.MiddleCenter, new Color(0.6f, 0.5f, 0.4f));
            UiKit.Anchor(ver.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                         new Vector2(0, 26), new Vector2(500, 40));

            _root.SetActive(false);
        }

        // Лого длинное — даём ему переноситься на 2 строки и ужиматься под рамку,
        // чтобы на узких экранах (вертикальный моб) не вылезало за края.
        static void FitTitle(TMP_Text t)
        {
            t.textWrappingMode = TextWrappingModes.Normal;
            t.enableAutoSizing = true;
            t.fontSizeMin = 44;
            t.fontSizeMax = 88;
        }

        void BuildFloaters()
        {
            int[] animals = { 0, 1, 2, 3, 4 };
            float[] xPos  = { 0.12f, 0.30f, 0.50f, 0.70f, 0.88f };
            float[] yBase = { 0.44f, 0.50f, 0.46f, 0.52f, 0.45f };
            float[] freqs = { 1.1f,  0.85f, 1.3f,  0.95f, 1.05f };
            float[] amps  = { 28f,   22f,   34f,   26f,   30f   };

            _floaters = new FloatAnim[animals.Length];
            for (int i = 0; i < animals.Length; i++)
            {
                int cid = animals[i];
                var go = new GameObject($"Animal{i}", typeof(RectTransform));
                go.transform.SetParent(_root.transform, false);
                var img = go.AddComponent<Image>();
                img.sprite         = AnimalSpriteFactory.Get(cid, Palette[cid]);
                img.preserveAspect = true;
                img.raycastTarget  = false;

                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(xPos[i], yBase[i]);
                rt.anchorMax = new Vector2(xPos[i], yBase[i]);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(140f, 140f);
                rt.anchoredPosition = Vector2.zero;

                _floaters[i] = new FloatAnim
                {
                    rt    = rt,
                    baseY = 0f,
                    freq  = freqs[i],
                    amp   = amps[i],
                    phase = i * 1.2f,
                };
            }
        }

        void OnStart()
        {
            if (!_ready || _closing) return;
            _closing = true;
            StartCoroutine(FadeOutAndLaunch());
        }

        IEnumerator AllowInputAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            _ready = true;   // защита от случайного тапа во время fade-in
        }

        IEnumerator FadeIn()
        {
            var cg = _root.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * 2.5f;   // не замирает при timeScale=0 (реклама)
                cg.alpha = Mathf.Clamp01(t);
                yield return null;
            }
            cg.alpha = 1f;
        }

        IEnumerator FadeOutAndLaunch()
        {
            var cg = _root.GetComponent<CanvasGroup>() ?? _root.AddComponent<CanvasGroup>();
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * 3.5f;
                cg.alpha = 1f - Mathf.Clamp01(t);
                yield return null;
            }
            _onStart?.Invoke();
            Destroy(_canvas.gameObject);
            Destroy(gameObject);
        }

        void Update()
        {
            if (_root == null || !_root.activeSelf) return;

            if (_floaters != null)
            {
                float time = Time.time;
                foreach (ref var f in _floaters.AsSpan())
                    f.rt.anchoredPosition = new Vector2(0f, Mathf.Sin(time * f.freq + f.phase) * f.amp);
            }

            if (_tapHint != null)
            {
                float a = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f);
                var c = _tapHint.color;
                c.a = 0.5f + 0.5f * a;
                _tapHint.color = c;
            }

            if (_ready && !_closing && InputService.PressedThisFrame)
                OnStart();
        }
    }
}
