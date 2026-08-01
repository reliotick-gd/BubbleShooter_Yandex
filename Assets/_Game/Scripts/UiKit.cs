using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace CozyAnimalTown
{
    /// <summary>
    /// Хелперы для построения uGUI из кода (проект собирается без сцен/префабов).
    /// Текст — TextMeshPro с дефолтным шрифт-ассетом (LiberationSans SDF): кириллица
    /// растеризуется в SDF-атлас и одинаково рендерится в WebGL (в отличие от
    /// builtin-шрифта, у которого в вебе нет кириллицы). Символов ★☆♥✕✓▶≡→ в шрифте
    /// нет — они идут процедурными TMP-спрайтами через инлайн-теги (см. [UiSymbols]).
    /// EventSystem поднимается с InputSystemUIInputModule (проект на новой Input System).
    /// </summary>
    public static class UiKit
    {
        static TextAlignmentOptions ToTMPAlign(TextAnchor a) => a switch
        {
            TextAnchor.UpperLeft    => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter  => TextAlignmentOptions.Top,
            TextAnchor.UpperRight   => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft   => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight  => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft    => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter  => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight   => TextAlignmentOptions.BottomRight,
            _                       => TextAlignmentOptions.Center,
        };

        static Sprite _rounded;
        public static Sprite RoundedSprite =>
            _rounded != null ? _rounded : (_rounded = BuildRounded(64, 22f));

        static Sprite BuildRounded(int size, float r)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float c = size * 0.5f, b = size * 0.5f - 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - c), dy = Mathf.Abs(y + 0.5f - c);
                    float qx = dx - (b - r), qy = dy - (b - r);
                    float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                    float sdf = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                    float a = Mathf.Clamp01(0.5f - sdf);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        static Sprite _board;
        /// <summary>В отличие от остальных спрайтов этого файла — используется через
        /// SpriteRenderer в режиме Sliced, а не Image.</summary>
        public static Sprite RoundBoardSprite =>
            _board != null ? _board : (_board = BuildRounded(128, 40f));

        static Sprite _candy;
        /// <summary>Градиент и глянец запечены в текстуру — не нужен отдельный
        /// блик-оверлей поверх кнопки.</summary>
        public static Sprite CandySprite =>
            _candy != null ? _candy : (_candy = BuildCandy(64, 24f));

        static Sprite BuildCandy(int size, float r)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float c = size * 0.5f, b = size * 0.5f - 0.5f;
            for (int y = 0; y < size; y++)
            {
                float vy = y / (float)(size - 1);                       // 0 низ, 1 верх
                float grad  = Mathf.Lerp(0.80f, 1.0f, vy);             // темнее снизу
                float gloss = Mathf.Exp(-Mathf.Pow((vy - 0.80f) / 0.14f, 2f)) * 0.28f; // блик вверху
                byte val = (byte)(Mathf.Clamp01(grad + gloss) * 255f);
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - c), dy = Mathf.Abs(y + 0.5f - c);
                    float qx = dx - (b - r), qy = dy - (b - r);
                    float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                    float sdf = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                    float a = Mathf.Clamp01(0.5f - sdf);
                    px[y * size + x] = new Color32(val, val, val, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        static Sprite _glow;
        public static Sprite GlowSprite =>
            _glow != null ? _glow : (_glow = BuildGlow(128));

        static Sprite BuildGlow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;                                   // мягкий спад
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite _sunburst;
        public static Sprite SunburstSprite =>
            _sunburst != null ? _sunburst : (_sunburst = BuildSunburst(256, 14));

        static Sprite BuildSunburst(int size, int rays)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float c = size * 0.5f, seg = Mathf.PI * 2f / rays;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    float ang = Mathf.Atan2(dy, dx); if (ang < 0f) ang += Mathf.PI * 2f;
                    float within = ang / seg; int idx = (int)within;
                    float a = 0f;
                    if ((idx & 1) == 0)
                    {
                        float edge = Mathf.Sin((within - idx) * Mathf.PI);       // мягкие края луча
                        float radial = Mathf.Clamp01((dist - 0.04f) / 0.18f) * Mathf.Clamp01(1f - (dist - 0.45f) / 0.55f);
                        a = edge * radial;
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite _shadow;
        /// <summary>Граница 9-slice = r+blur, иначе слайсинг обрежет размытие по краям.</summary>
        public static Sprite ShadowSprite =>
            _shadow != null ? _shadow : (_shadow = BuildShadow(96, 26f, 18f));

        static Sprite BuildShadow(int size, float r, float blur)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float c = size * 0.5f, b = size * 0.5f - 0.5f - blur;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - c), dy = Mathf.Abs(y + 0.5f - c);
                    float qx = dx - (b - r), qy = dy - (b - r);
                    float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                    float sdf = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                    float a = 1f - Mathf.SmoothStep(0f, blur, sdf);   // 1 внутри → 0 через blur
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            float border = r + blur;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        static readonly Color ShadowCol = new Color(0.72f, 0.60f, 0.32f, 0.20f); // лёгкая тёплая жёлтая тень

        static Sprite _circleShadow;
        public static Sprite CircleShadowSprite =>
            _circleShadow != null ? _circleShadow : (_circleShadow = BuildCircleShadow(96, 40f, 14f));

        static Sprite BuildCircleShadow(int size, float r, float blur)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    float a = 1f - Mathf.SmoothStep(r - blur, r, d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Тень — отдельный объект-сосед, а не дочерний: SetActive(false) на
        /// элементе её не спрячет, для этого используй возвращаемое значение.</summary>
        public static Image AddShadow(RectTransform target, float grow = 10f, float dy = -6f, bool circle = false)
        {
            var go = new GameObject("Shadow", typeof(RectTransform));
            go.transform.SetParent(target.parent, false);
            go.transform.SetSiblingIndex(target.GetSiblingIndex());
            var img = go.AddComponent<Image>();
            img.sprite = circle ? CircleShadowSprite : ShadowSprite;
            img.type = circle ? Image.Type.Simple : Image.Type.Sliced;
            img.color = ShadowCol;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = target.anchorMin; rt.anchorMax = target.anchorMax;
            rt.pivot = target.pivot;
            rt.sizeDelta = target.sizeDelta + new Vector2(grow, grow);
            rt.anchoredPosition = target.anchoredPosition + new Vector2(0f, dy);

            // Тень — сосед, а не ребёнок (ребёнок рисовался бы поверх). Значит при переносе
            // элемента в другой рут (HUD уезжает в боковые панели на десктопе) она бы осталась
            // висеть на старом месте. Пусть следит за хозяином сама.
            var f = go.AddComponent<ShadowFollow>();
            f.target = target; f.grow = grow; f.dy = dy;
            return img;
        }

        /// <summary>Показать/скрыть тень элемента (например, когда сама панель стала прозрачной).</summary>
        public static void SetShadowVisible(RectTransform target, bool visible)
        {
            if (target == null) return;
            // Ищем от корня канваса, а не от родителя: сразу после переноса элемента тень
            // ещё висит на СТАРОМ руте — она переедет только в ближайшем LateUpdate.
            foreach (var f in target.root.GetComponentsInChildren<ShadowFollow>(true))
                if (f.target == target) f.GetComponent<Image>().enabled = visible;
        }

        /// <summary>Держит тень у хозяина: тот же родитель, якоря, размер и порядок отрисовки.</summary>
        public class ShadowFollow : MonoBehaviour
        {
            public RectTransform target;
            public float grow, dy;

            // Последнее применённое состояние. Геометрия тени меняется только при смене
            // раскладки и при переносе элемента, а LateUpdate до этого безусловно писал
            // пять свойств RectTransform каждый кадр у каждой из ~15 живых теней —
            // сотни interop-вызовов в кадр на IL2CPP ради неизменившихся значений.
            Transform _lastParent;
            Vector2 _lastMin, _lastMax, _lastPivot, _lastSize, _lastPos;
            bool _applied;

            void LateUpdate()
            {
                if (target == null) { Destroy(gameObject); return; }
                // Хозяин скрыт — тень всё равно не видна (у неё гасится Image), значит и
                // возить её незачем. Особенно это про mid-offer: тень ему сосед, а не
                // ребёнок, поэтому она продолжала работать даже при выключенной кнопке.
                if (!target.gameObject.activeInHierarchy) return;

                var rt = (RectTransform)transform;
                if (rt.parent != target.parent) { rt.SetParent(target.parent, false); _applied = false; }
                // Только если тень уехала ВПЕРЁД хозяина — иначе индексы будут мигать каждый кадр.
                if (rt.GetSiblingIndex() > target.GetSiblingIndex())
                    rt.SetSiblingIndex(target.GetSiblingIndex());

                Vector2 size = target.sizeDelta + new Vector2(grow, grow);
                Vector2 pos  = target.anchoredPosition + new Vector2(0f, dy);
                if (_applied && _lastParent == target.parent
                    && _lastMin == target.anchorMin && _lastMax == target.anchorMax
                    && _lastPivot == target.pivot && _lastSize == size && _lastPos == pos)
                    return;

                rt.anchorMin = target.anchorMin; rt.anchorMax = target.anchorMax;
                rt.pivot = target.pivot;
                rt.sizeDelta = size;
                rt.anchoredPosition = pos;

                _lastParent = target.parent;
                _lastMin = target.anchorMin; _lastMax = target.anchorMax;
                _lastPivot = target.pivot; _lastSize = size; _lastPos = pos;
                _applied = true;
            }
        }

        public static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            // ConstantPixelSize: координаты канваса = пиксели экрана. Фиксированный
            // 9:16-макет 1080×1920 держит UiKit.Column (через ColumnFitter), поля добиваются кремом.
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        /// <summary>Колонка 9:16, совпадающая с вьюпортом доски (на десктопе — поля-небо
        /// по бокам).</summary>
        public static RectTransform Column(Canvas canvas)
        {
            var go = new GameObject("Column", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            go.AddComponent<ColumnFitter>();
            return (RectTransform)go.transform;
        }

        /// <summary>Рут с жёстким макетом 1080×1920 — для модалок (итоги, настройки,
        /// диалог рекламы). Их вёрстка сделана под 9:16 и не должна ехать за раскладкой доски.</summary>
        public static RectTransform ModalColumn(Canvas canvas)
        {
            var go = new GameObject("ModalColumn", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            go.AddComponent<ColumnFitter>().fixedPortrait = true;
            return (RectTransform)go.transform;
        }

        /// <summary>Полноэкранный рут в тех же единицах, что и Column — для HUD в полях
        /// вокруг доски на десктопе (см. StageFitter).</summary>
        public static RectTransform Stage(Canvas canvas)
        {
            var go = new GameObject("Stage", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            go.AddComponent<StageFitter>();
            return (RectTransform)go.transform;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            // модуль создан в рантайме (не через инспектор) — дефолтные UI-экшены не назначены сами
            module.AssignDefaultActions();
        }

        public static TMP_Text Label(Transform parent, string text, int fontSize,
                                     TextAnchor anchor, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            DynamicFont.Apply(t);          // шрифт из TTF (латиница+цифры+кириллица)
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = ToTMPAlign(anchor);
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public static Image Panel(Transform parent, Color color, bool raycast)
        {
            var go = new GameObject("Panel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Button Btn(Transform parent, string text, int fontSize,
                                 Color bg, Color fg, UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = bg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors; cb.fadeDuration = 0.08f;
            cb.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            cb.highlightedColor = Color.white;
            btn.colors = cb;
            btn.onClick.AddListener(AudioService.PlayClick);
            if (onClick != null) btn.onClick.AddListener(onClick);

            var label = Label(go.transform, text, fontSize, TextAnchor.MiddleCenter, fg);
            label.fontStyle = FontStyles.Bold;
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            return btn;
        }

        public static Button CandyBtn(Transform parent, string text, int fontSize,
                                      Color bg, Color fg, UnityAction onClick)
        {
            var go = new GameObject("CandyBtn", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = CandySprite;
            img.type = Image.Type.Sliced;
            img.color = bg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors; cb.fadeDuration = 0.08f;
            cb.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            cb.highlightedColor = Color.white; btn.colors = cb;
            btn.onClick.AddListener(AudioService.PlayClick);
            if (onClick != null) btn.onClick.AddListener(onClick);

            var label = Label(go.transform, text, fontSize, TextAnchor.MiddleCenter, fg);
            label.fontStyle = FontStyles.Bold;
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            return btn;
        }

        /// <summary>iconSlot возвращается пустым — спрайт нужно назначить после вызова.</summary>
        public static Button CircleButton(Transform parent, Vector2 anchor, Vector2 pos,
            float diameter, Color bg, out Image iconSlot, UnityAction onClick)
        {
            var go = new GameObject("CircleBtn", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = SpriteFactory.Circle();   // настоящий круг
            img.type = Image.Type.Simple;
            img.color = bg;
            var rt = img.rectTransform;
            Anchor(rt, anchor, anchor, pos, new Vector2(diameter, diameter));

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors; cb.fadeDuration = 0.08f;
            cb.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f); btn.colors = cb;
            btn.onClick.AddListener(AudioService.PlayClick);
            if (onClick != null) btn.onClick.AddListener(onClick);
            AddShadow(rt, 10f, -5f, circle: true);

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            iconSlot = iconGo.AddComponent<Image>();
            iconSlot.raycastTarget = false;
            iconSlot.preserveAspect = true;
            var irt = iconSlot.rectTransform;
            irt.anchorMin = new Vector2(0.5f, 0.5f); irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(diameter * 0.56f, diameter * 0.56f);
            return btn;
        }

        public static Image Pill(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color bg)
        {
            var img = Panel(parent, bg, false);
            Anchor(img.rectTransform, anchor, anchor, pos, size);
            AddShadow(img.rectTransform, 8f, -5f);
            return img;
        }

        public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot,
                                  Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
        }

        /// <summary>Затемнение модалки на ВЕСЬ экран. Stretch растянул бы его только по
        /// руту (колонка 9:16), и на десктопе тёмная вуаль была бы узкой полосой посередине.
        /// Заведомо огромный прямоугольник перекрывает экран любой формы.</summary>
        public static void FullScreenDim(RectTransform rt)
        {
            Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8000f, 8000f));
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

    }
}
