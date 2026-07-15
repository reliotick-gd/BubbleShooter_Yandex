using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CozyAnimalTown
{
    /// <summary>
    /// Туториал 1-го уровня (первый показ): рука ведёт прицел слева направо, затем
    /// показательный выстрел. Skippable по тапу; блокирует ввод пушки; флаг в PlayerPrefs.
    /// </summary>
    public class Onboarding : MonoBehaviour
    {
        const string Key = "cat_onboarded";

        // геометрия в дизайн-координатах (снизу): пушка, высота руки, первая линия шаров.
        // TopY — где обрывается траектория и пропадает демо-шар (аккурат у нижнего ряда
        // шаров). XTo подобран так, чтобы прицел на TopY не выходил за игровую зону.
        const float CannonY = 388f, HandY = 720f, TopY = 1050f, XFrom = -210f, XTo = 210f;

        static readonly Color Skin    = new Color(0.97f, 0.80f, 0.63f);
        static readonly Color Outline = new Color(0.34f, 0.26f, 0.20f);
        static readonly Color DotCol  = new Color(0.36f, 0.32f, 0.30f, 0.72f);
        static readonly Color Brown   = new Color(0.42f, 0.34f, 0.27f);

        GameManager gm;
        Canvas canvas;
        CanvasGroup cg;
        RectTransform handBack, handFront, ball;
        readonly RectTransform[] dots = new RectTransform[16];
        bool _skip, _done;
        int _cycles;  // сколько полных демо-циклов игрок посмотрел (0 = пропустил сразу)
        float _age;   // не пропускаем по «залётному» тапу с титульного экрана

        public void Begin(GameManager gm)
        {
            this.gm = gm;
            gm.SetOnboarding(true);
            Analytics.OnboardingShown();

            canvas = UiKit.CreateCanvas("OnboardCanvas", 96);
            var root = UiKit.Column(canvas);
            cg = root.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;

            // серая плашка ниже — чуть заходит на верхние шары, выделяется на белом поле
            var tip = UiKit.Pill(root, new Vector2(0.5f, 1f), new Vector2(0f, -668f),
                new Vector2(860f, 118f), new Color(0.53f, 0.54f, 0.58f, 1f));
            var tt = UiKit.Label(tip.transform,
                Loc.T("Drag to aim — release to shoot!", "Тяни — целься, отпусти — стреляй!"),
                40, TextAnchor.MiddleCenter, Color.white);
            tt.fontStyle = FontStyles.Bold;
            UiKit.Stretch(tt.rectTransform);

            for (int i = 0; i < dots.Length; i++)
            {
                var d = MakeImg(root, SpriteFactory.Circle(), DotCol, 20f);
                d.gameObject.SetActive(false);
                dots[i] = d;
            }

            // показательный шар = реальный боевой снаряд (скрыт до выстрела)
            var demoSprite = gm.CurrentBallSprite;
            if (demoSprite == null) demoSprite = AnimalSpriteFactory.Get(0, new GameConfig().palette[0]);
            ball = MakeImg(root, demoSprite, Color.white, 84f);
            ball.gameObject.SetActive(false);

            // рука рисуется двумя слоями (тёмный контур + скин сверху) — IconFactory даёт только один цвет за раз
            var hand = IconFactory.Get("hand", Skin);
            handBack  = MakeImg(root, IconFactory.Get("hand", Outline), Outline, 160f);
            handFront = MakeImg(root, hand, Skin, 150f);

            StartCoroutine(Run());
        }

        RectTransform MakeImg(Transform parent, Sprite sprite, Color col, float size)
        {
            var go = new GameObject("O", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite; img.color = col; img.preserveAspect = true; img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            if (!_done && _age > 0.6f && InputService.PressedThisFrame) _skip = true;
        }

        IEnumerator Run()
        {
            yield return Fade(0f, 1f, 0.35f);
            // Демо крутится в цикле до первого тапа игрока (первые 0.6с тап защищён).
            while (!_skip)
            {
                ball.gameObject.SetActive(false);
                SetHand(XFrom, HandY);
                yield return Phase(2.4f, t => { float x = Mathf.Lerp(XFrom, XTo, Mathf.SmoothStep(0f, 1f, t)); SetHand(x, HandY); DrawAim(x, HandY); });
                if (_skip) break;
                yield return Phase(0.3f, t => SetHand(XTo, HandY + 70f * t));   // «отпускание»
                if (_skip) break;
                yield return Phase(0.6f, t => Shoot(t));                        // показательный выстрел
                if (_skip) break;
                yield return Phase(0.4f, _ => { });                            // пауза перед повтором
                _cycles++;
            }
            yield return Fade(cg.alpha, 0f, 0.3f);
            Finish();
        }

        void SetHand(float x, float y)
        {
            var p = new Vector2(x, y - 46f);   // кончик пальца ~ (x,y)
            handBack.anchoredPosition = p; handFront.anchoredPosition = p;
        }

        void DrawAim(float hx, float hy)
        {
            Vector2 cannon = new(0f, CannonY);
            Vector2 dir = new Vector2(hx, hy) - cannon;
            if (dir.sqrMagnitude < 1f) dir = Vector2.up;
            dir.Normalize();
            float d = 64f;
            for (int i = 0; i < dots.Length; i++)
            {
                Vector2 p = cannon + dir * d;
                bool on = p.y < TopY;
                dots[i].gameObject.SetActive(on);
                if (on) dots[i].anchoredPosition = p;
                d += 54f;
            }
        }

        void Shoot(float t)
        {
            foreach (var d in dots) d.gameObject.SetActive(false);
            Vector2 cannon = new(0f, CannonY);
            Vector2 dir = new Vector2(XTo, HandY) - cannon;
            dir.Normalize();
            float total = (TopY - CannonY) / Mathf.Max(0.2f, dir.y);
            ball.gameObject.SetActive(t < 1f);
            ball.anchoredPosition = cannon + dir * (t * total);
        }

        IEnumerator Phase(float dur, System.Action<float> step)
        {
            float t = 0f;
            while (t < 1f && !_skip) { t += Time.unscaledDeltaTime / dur; step(Mathf.Clamp01(t)); yield return null; }
            if (!_skip) step(1f);
        }

        IEnumerator Fade(float a, float b, float dur)
        {
            float t = 0f;
            while (t < 1f) { t += Time.unscaledDeltaTime / dur; cg.alpha = Mathf.Lerp(a, b, Mathf.Clamp01(t)); yield return null; }
            cg.alpha = b;
        }

        void Finish()
        {
            _done = true;
            gm.SetOnboarding(false);
            Analytics.OnboardingDone(skipped: _cycles == 0);
            PlayerPrefs.SetInt(Key, 1); PlayerPrefs.Save();
            if (canvas) Destroy(canvas.gameObject);
            Destroy(gameObject);
        }
    }
}
