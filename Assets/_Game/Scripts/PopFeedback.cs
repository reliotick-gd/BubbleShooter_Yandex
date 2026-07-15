using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CozyAnimalTown
{
    /// <summary>
    /// Плавающий "+N" при попе пузырей — пул TMP_Text на отдельном канвасе под HUD,
    /// позиция считается из world→screen каждый кадр (подъём + затухание).
    /// </summary>
    public class PopFeedback : MonoBehaviour
    {
        public static PopFeedback Instance { get; private set; }

        const int PoolSize = 12;

        // Pre-cached "+N" strings — no concat alloc on every pop
        static readonly string[] _countStr = new string[101];
        static PopFeedback()
        {
            for (int i = 0; i <= 100; i++) _countStr[i] = "+" + i;
        }

        struct Slot { public bool active; public float t; public Vector3 worldPos; }
        readonly Slot[] _slots = new Slot[PoolSize];
        TMP_Text[] _texts;
        int _next;

        Canvas _canvas;
        Camera _cam;

        void Awake()
        {
            Instance = this;
            _cam = Camera.main;
            _canvas = UiKit.CreateCanvas("PopFeedbackCanvas", 50);  // ниже HUD (100)
            _texts = new TMP_Text[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var t = UiKit.Label(_canvas.transform, "", 48, TextAnchor.MiddleCenter, Color.white);
                t.fontStyle = FontStyles.Bold;
                UiKit.Anchor(t.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f),
                             Vector2.zero, new Vector2(240, 80));
                t.gameObject.SetActive(false);
                _texts[i] = t;
            }
        }

        public void Show(Vector3 worldPos, int count)
        {
            if (count <= 0) return;
            int i = _next % PoolSize;
            _next++;
            _slots[i] = new Slot { active = true, t = 0f, worldPos = worldPos + Vector3.up * 0.3f };

            var col = count >= 9 ? new Color(1f, 0.85f, 0.1f)
                    : count >= 5 ? new Color(0.5f, 1f, 0.5f)
                                 : Color.white;
            _texts[i].text = (uint)count <= 100u ? _countStr[count] : "+" + count;
            _texts[i].color = col;
            _texts[i].gameObject.SetActive(true);
        }

        void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            float sf = _canvas.scaleFactor <= 0f ? 1f : _canvas.scaleFactor;
            float dt = Time.deltaTime * 2.2f;

            for (int i = 0; i < PoolSize; i++)
            {
                if (!_slots[i].active) continue;
                _slots[i].t += dt;
                float t = _slots[i].t;
                if (t >= 1f)
                {
                    _slots[i].active = false;
                    _texts[i].gameObject.SetActive(false);
                    continue;
                }

                Vector3 wp = _slots[i].worldPos + Vector3.up * (t * 0.8f);
                Vector3 sp = _cam.WorldToScreenPoint(wp);
                if (sp.z < 0f) { _texts[i].gameObject.SetActive(false); continue; }
                if (!_texts[i].gameObject.activeSelf) _texts[i].gameObject.SetActive(true);

                _texts[i].rectTransform.anchoredPosition = new Vector2(sp.x, sp.y + t * 80f) / sf;

                var c = _texts[i].color;
                c.a = 1f - t * t;
                _texts[i].color = c;
            }
        }
    }
}
