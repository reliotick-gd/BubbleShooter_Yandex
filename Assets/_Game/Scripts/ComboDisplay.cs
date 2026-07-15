using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CozyAnimalTown
{
    /// <summary>
    /// Большой анимированный попап «COMBO x3!» при цепочке попов подряд.
    /// Создаётся GameManager-ом; вызывается через Instance.Show(combo).
    /// </summary>
    public class ComboDisplay : MonoBehaviour
    {
        public static ComboDisplay Instance { get; private set; }

        Canvas        _canvas;
        TMP_Text      _label;
        CanvasGroup   _cg;
        Coroutine     _anim;

        static readonly Color[] ComboColors =
        {
            new Color(1.0f, 0.82f, 0.10f), // x2  — золотой
            new Color(1.0f, 0.55f, 0.10f), // x3  — оранжевый
            new Color(0.95f, 0.25f, 0.25f), // x4  — красный
            new Color(0.75f, 0.30f, 0.95f), // x5+ — фиолетовый
        };

        // Кэш строк комбо, без аллока на каждый выстрел; лениво в Awake (не static ctor) — слово локализуется через Loc.
        static string[] ComboStr;

        void Awake()
        {
            Instance = this;
            if (ComboStr == null)
            {
                string word = Loc.T("COMBO", "КОМБО");
                ComboStr = new string[12];
                for (int i = 0; i < ComboStr.Length; i++)
                {
                    int n = i + 2;
                    ComboStr[i] = n >= 5 ? $"{word} x{n}!!!" : $"{word} x{n}!";
                }
            }
            _canvas  = UiKit.CreateCanvas("ComboCanvas", 150);  // sortingOrder 150 — над HUD(100)
            var col  = UiKit.Column(_canvas);
            var go = new GameObject("ComboLabel", typeof(RectTransform));
            go.transform.SetParent(col, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            DynamicFont.Apply(tmp);
            tmp.fontSize          = 112;
            tmp.fontStyle         = FontStyles.Bold;
            tmp.alignment         = TextAlignmentOptions.Center;
            tmp.text              = "";
            tmp.raycastTarget     = false;   // невидимый текст не должен перехватывать клики
            tmp.textWrappingMode  = TextWrappingModes.NoWrap;
            _label = tmp;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 200f);
            rt.anchoredPosition = new Vector2(0f, 160f);

            _cg                = go.AddComponent<CanvasGroup>();
            _cg.alpha          = 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable   = false;
        }

        public void Show(int combo)
        {
            if (combo < 2) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Animate(combo));
        }

        /// <summary>Мгновенно убрать попап (на экране итогов он неуместен).</summary>
        public void Hide()
        {
            if (_anim != null) { StopCoroutine(_anim); _anim = null; }
            if (_cg) _cg.alpha = 0f;
        }

        IEnumerator Animate(int combo)
        {
            int clampedIdx = Mathf.Clamp(combo - 2, 0, ComboColors.Length - 1);
            Color col = ComboColors[clampedIdx];
            _label.color = col;
            int strIdx   = Mathf.Clamp(combo - 2, 0, ComboStr.Length - 1);
            _label.text  = combo - 2 < ComboStr.Length ? ComboStr[strIdx] : $"COMBO x{combo}!!!";

            var rt = (RectTransform)_label.rectTransform;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 8f;
                float c1 = Mathf.Clamp01(t);
                float s  = 1f + Mathf.Sin(c1 * Mathf.PI) * 0.35f;
                rt.localScale = new Vector3(s, s, 1f);
                _cg.alpha     = Mathf.Clamp01(t * 4f);
                yield return null;
            }

            yield return new WaitForSeconds(0.55f);

            t = 0f;
            while (t < 1f)
            {
                t      += Time.deltaTime * 3.5f;
                _cg.alpha = 1f - Mathf.Clamp01(t);
                yield return null;
            }
            _cg.alpha = 0f;
            _anim = null;
        }
    }
}
