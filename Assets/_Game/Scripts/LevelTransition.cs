using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CozyAnimalTown
{
    /// <summary>
    /// Белый flash-оверлей при переходе между уровнями.
    /// Использование: LevelTransition.Instance.Play(action) —
    /// fade-in (0.18с) → action() → fade-out (0.28с).
    /// </summary>
    public class LevelTransition : MonoBehaviour
    {
        public static LevelTransition Instance { get; private set; }

        Image      _overlay;
        CanvasGroup _cg;
        bool       _busy;

        void Awake()
        {
            Instance = this;
            var canvas = UiKit.CreateCanvas("TransitionCanvas", 900); // поверх всего

            var go = new GameObject("Overlay", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            _overlay = go.AddComponent<Image>();
            _overlay.color = new Color(1f, 0.97f, 0.90f, 0f);
            _overlay.raycastTarget = false;
            UiKit.Stretch((RectTransform)go.transform);

            _cg                 = go.AddComponent<CanvasGroup>();
            _cg.alpha           = 0f;
            _cg.blocksRaycasts  = false;
            _cg.interactable    = false;
        }

        /// <summary>Fade-in → action → fade-out. Игнорируется если уже играет.</summary>
        public void Play(Action action) => StartCoroutine(DoTransition(action));

        IEnumerator DoTransition(Action action)
        {
            if (_busy) { action?.Invoke(); yield break; }
            _busy              = true;
            _cg.blocksRaycasts = true;

            // finally гарантирует снятие блокировки ввода: если action бросит исключение,
            // без этого blocksRaycasts/_busy зависли бы навсегда → игра «зафризена».
            try
            {
                float t = 0f;
                while (t < 1f)
                {
                    t      += Time.deltaTime / 0.18f;
                    _cg.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                    yield return null;
                }
                _cg.alpha = 1f;

                // catch без yield внутри: C# итератор не позволяет yield return в try
                // с catch, поэтому исключение action ловим отдельно от fade-циклов
                try { action?.Invoke(); }
                catch (Exception e) { Debug.LogError($"[LevelTransition] action failed: {e}"); }
                yield return null; // кадр на применение изменений

                t = 0f;
                while (t < 1f)
                {
                    t      += Time.deltaTime / 0.28f;
                    _cg.alpha = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t));
                    yield return null;
                }
            }
            finally
            {
                _cg.alpha          = 0f;
                _cg.blocksRaycasts = false;
                _busy              = false;
            }
        }
    }
}
