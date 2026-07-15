using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CozyAnimalTown
{
    /// <summary>
    /// Мини-онбординг препятствия: серая плашка с коротким правилом + пульсирующая
    /// подсветка самого препятствия на поле. Висит до ПЕРВОГО выстрела (или тапа).
    /// Не блокирует игру.
    /// </summary>
    public class ObstacleHint : MonoBehaviour
    {
        Canvas canvas;
        CanvasGroup cg;
        RectTransform glow;
        GameManager gm;
        int _initShots;
        float _age;
        bool _closing;

        public void Show(int obstacleId, string text, Vector2 obstacleDesign, GameManager gm)
        {
            this.gm = gm;
            _initShots = gm != null ? gm.ShotsLeft : 0;

            canvas = UiKit.CreateCanvas("HintCanvas", 97);
            var root = UiKit.Column(canvas);
            cg = root.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;

            var pill = UiKit.Pill(root, new Vector2(0.5f, 1f), new Vector2(0f, -662f),
                new Vector2(720f, 116f), new Color(0.53f, 0.54f, 0.58f, 1f));
            var lbl = UiKit.Label(pill.transform, text, 40, TextAnchor.MiddleCenter, Color.white);
            lbl.fontStyle = FontStyles.Bold;
            UiKit.Stretch(lbl.rectTransform);

            var go = new GameObject("Glow", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var img = go.AddComponent<Image>();
            img.sprite = SpriteFactory.Circle();
            img.color = new Color(1f, 0.95f, 0.45f, 0.5f);
            img.raycastTarget = false;
            glow = img.rectTransform;
            UiKit.Anchor(glow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), obstacleDesign, new Vector2(126f, 126f));

            StartCoroutine(Run());
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            if (glow) { float s = 1f + 0.14f * Mathf.Sin(_age * 4.5f); glow.localScale = new Vector3(s, s, 1f); }
            // 0.5с защита от тапа, которым игрок как раз открыл подсказку
            if (!_closing && (( gm != null && gm.ShotsLeft < _initShots) || (_age > 0.5f && InputService.PressedThisFrame)))
            {
                _closing = true;
                StartCoroutine(Close());
            }
        }

        IEnumerator Run() { yield return Fade(0f, 1f, 0.3f); }

        IEnumerator Close()
        {
            yield return Fade(cg.alpha, 0f, 0.35f);
            if (canvas) Destroy(canvas.gameObject);
            Destroy(gameObject);
        }

        IEnumerator Fade(float a, float b, float dur)
        {
            float t = 0f;
            while (t < 1f) { t += Time.unscaledDeltaTime / dur; cg.alpha = Mathf.Lerp(a, b, Mathf.Clamp01(t)); yield return null; }
            cg.alpha = b;
        }
    }
}
