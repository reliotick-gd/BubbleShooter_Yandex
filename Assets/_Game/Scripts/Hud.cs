using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CozyAnimalTown
{
    public class Hud : MonoBehaviour
    {
        GameManager gm;
        Canvas canvas;
        RectTransform root;          // колонка доски — за ней и живёт игровой HUD в портрете
        RectTransform stage;         // весь экран — сюда уезжает HUD на десктопе
        RectTransform modalRoot;     // жёсткий 1080×1920 — итоги/настройки/диалог рекламы

        // Хром HUD, который на широком экране переезжает в поля; на портрете остаётся
        // над/под доской. Раскладка пересобирается при смене ориентации окна.
        RectTransform _rtBack, _rtGear, _rtLb, _rtPillL, _rtPillR, _rtStrip;
        RectTransform _rtRainbow, _rtBomb;
        readonly RectTransform[] _slotRt = new RectTransform[8];
        readonly Image[] _slotDisc = new Image[8];
        Image _stripBg;
        int _layoutMode = -1;        // 0 — портрет, 1 — широкий

        // Украшения только для десктопной раскладки — в портрете для них нет места.
        TMP_Text _capColors, _capRainbow, _capBomb;

        GameObject resultRoot, winGroup, loseGroup, pauseRoot;
        TMP_Text titleText, _loseCount;
        Image _soundIcon;
        GameObject bombAd, rainbowAd;
        Image _bombIcon, _rainbowIcon;
        GameObject _bombCountBadge, _rainbowCountBadge;
        GameObject _bombLock, _rainbowLock;

        RectTransform[] _confetti;
        Vector2[] _confPos, _confVel; float[] _confRotV;
        TMP_Text _winTitle, _winSub, _nextLabel;
        RectTransform _winMascotRt, _glowRt, _sunburstRt, _loseMascotRt;
        CanvasGroup _nextCg;
        RectTransform _nextRt;
        int _lastSubIdx = -1;
        string[] _winSubs;
        static string WinWord => Loc.T("Congratulations!", "Поздравляем!");
        static readonly Color[] Candy =
        {
            new Color(0.98f, 0.42f, 0.66f),
            new Color(0.98f, 0.62f, 0.22f),
            new Color(0.45f, 0.80f, 0.47f),
            new Color(0.32f, 0.70f, 0.98f),
            new Color(0.74f, 0.47f, 0.96f),
            new Color(0.97f, 0.44f, 0.50f),
        };

        TMP_Text _tLevel, _tShots, _tBomb, _tRainbow;

        readonly Image[] _slotImg     = new Image[8];
        readonly GameObject[] _slotLock = new GameObject[8];
        int _shownUnlocked = -1;
        // Уровень, на котором открывается зверь i (должен совпадать с кривой ColorsForLevel).
        static readonly int[] UnlockLevel = { 1, 1, 1, 6, 11, 17, 26, 41 };

        static readonly Color White     = Color.white;
        static readonly Color Brown      = new Color(0.42f, 0.34f, 0.27f, 1f);
        static readonly Color BrownSoft  = new Color(0.58f, 0.49f, 0.41f, 1f);
        static readonly Color Dim        = new Color(0.20f, 0.16f, 0.12f, 0.42f);
        static readonly Color Green      = new Color(0.42f, 0.76f, 0.52f, 1f);
        static readonly Color GreenAd    = new Color(0.30f, 0.78f, 0.44f, 1f);
        static readonly Color Peach      = new Color(0.96f, 0.86f, 0.74f, 1f);
        static readonly Color BadgeDark  = new Color(0.38f, 0.31f, 0.25f, 1f);
        static readonly Color LockTint   = new Color(0.30f, 0.27f, 0.24f, 0.85f);

        GameState _lastState = (GameState)(-1);
        int _cLevel = -1, _cShots = -1, _cBomb = -1, _cRainbow = -1;
        int _uBomb = -1, _uRainbow = -1;

        public void Init(GameManager gm)
        {
            this.gm = gm;
            _winSubs = new[]
            {
                Loc.T("Nicely done, keep going!",   "Отлично, так держать!"),
                Loc.T("You cleared it with style!", "Уровень пройден с блеском!"),
                Loc.T("Boom! Level complete!",      "Бум! Уровень пройден!"),
                Loc.T("Purr-fectly played!",        "Мур-р! Превосходно!"),
                Loc.T("Sweet victory, on you go!",  "Сладкая победа, вперёд!"),
            };
            canvas = UiKit.CreateCanvas("HudCanvas", 100);
            root      = UiKit.Column(canvas);
            stage     = UiKit.Stage(canvas);
            modalRoot = UiKit.ModalColumn(canvas);
            BuildTopBar();
            BuildUnlockStrip();
            BuildBonusBar();
            BuildResultPanel();
            BuildPausePanel();
            BuildSideDecor();
            resultRoot.SetActive(false);
            pauseRoot.SetActive(false);
            ApplyResponsiveLayout();
        }

        /// <summary>Подписи и маскот боковых панелей — существуют всегда, но видны
        /// только в широкой раскладке (в колонке 9:16 для них нет места).</summary>
        void BuildSideDecor()
        {
            _capColors = UiKit.Label(stage, Loc.T("COLOURS IN PLAY", "ЦВЕТА В ИГРЕ"), 26,
                TextAnchor.MiddleLeft, BrownSoft);
            _capColors.fontStyle = FontStyles.Bold;

            _capRainbow = UiKit.Label(stage, Loc.T("RAINBOW", "РАДУГА"), 24, TextAnchor.MiddleCenter, BrownSoft);
            _capRainbow.fontStyle = FontStyles.Bold;
            _capBomb = UiKit.Label(stage, Loc.T("BOMB", "БОМБА"), 24, TextAnchor.MiddleCenter, BrownSoft);
            _capBomb.fontStyle = FontStyles.Bold;
        }

        /// <summary>
        /// Раскладка HUD под форму экрана. Портрет (телефон) — как было: пилюли и легенда
        /// над доской, бонусы под ней. Широкий экран (десктоп) — доска занимает всю высоту
        /// колонки, а хром уезжает в поля по бокам: иначе легенда налезает на верх доски,
        /// а бонусы — на пушку (в портрете между ними 288 дизайн-единиц запаса, в ландшафте 13).
        /// Заодно поля перестают быть пустой заливкой — требование п.5.9 / п.5.1.1.2 Яндекса.
        /// </summary>
        void ApplyResponsiveLayout()
        {
            int mode = ScreenColumn.IsWide ? 1 : 0;
            if (mode == _layoutMode) return;
            _layoutMode = mode;

            if (mode == 1) WideLayout();
            else           PortraitLayout();
        }

        void Reparent(RectTransform rt, RectTransform parent)
        {
            if (rt != null && rt.parent != parent) rt.SetParent(parent, false);
        }

        void PortraitLayout()
        {
            Reparent(_rtBack, root); Reparent(_rtGear, root); Reparent(_rtLb, root);
            Reparent(_rtPillL, root); Reparent(_rtPillR, root); Reparent(_rtStrip, root);
            Reparent(_rtRainbow, root); Reparent(_rtBomb, root);

            UiKit.Anchor(_rtBack, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(30f, -30f), new Vector2(96f, 96f));
            UiKit.Anchor(_rtGear, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-30f, -30f), new Vector2(96f, 96f));
            UiKit.Anchor(_rtLb,   new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-146f, -30f), new Vector2(96f, 96f));

            UiKit.Anchor(_rtPillL, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-124f, -150f), new Vector2(228f, 100f));
            UiKit.Anchor(_rtPillR, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2( 124f, -150f), new Vector2(228f, 100f));

            _stripBg.color = White;
            UiKit.Anchor(_rtStrip, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -298f), new Vector2(1026f, 118f));
            UiKit.SetShadowVisible(_rtStrip, true);
            for (int i = 0; i < 8; i++)
            {
                _slotDisc[i].color = new Color(1f, 1f, 1f, 0f);   // полоска сама белая
                UiKit.Anchor(_slotRt[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((i - 3.5f) * 120f, 0f), new Vector2(78f, 78f));
                UiKit.Anchor((RectTransform)_slotImg[i].transform, new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78f, 78f));
            }

            SizeBonus(_rtRainbow, _rainbowIcon, rainbowAd, _rainbowCountBadge, _rainbowLock, 128f);
            SizeBonus(_rtBomb,    _bombIcon,    bombAd,    _bombCountBadge,    _bombLock,    128f);
            UiKit.Anchor(_rtRainbow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-100f, 100f), new Vector2(128f, 128f));
            UiKit.Anchor(_rtBomb,    new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2( 100f, 100f), new Vector2(128f, 128f));

            _capColors.gameObject.SetActive(false);
            _capRainbow.gameObject.SetActive(false);
            _capBomb.gameObject.SetActive(false);
        }

        /// <summary>Подгоняет иконку бонуса и его бейджи под диаметр кнопки.</summary>
        void SizeBonus(RectTransform btn, Image icon, GameObject ad, GameObject count, GameObject lockB, float d)
        {
            UiKit.Anchor((RectTransform)icon.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(d * 0.92f, d * 0.92f));

            // Бейдж «▶ Реклама +3» шире кнопки, поэтому в портрете он встаёт НАД кружком:
            // сбоку два бонуса стоят в 200 единицах друг от друга и бейджи бы пересеклись.
            // В широкой раскладке кнопки друг под другом — там бейдж ложится справа сверху.
            float k = d / 128f;
            if (ScreenColumn.IsWide)
                UiKit.Anchor((RectTransform)ad.transform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(8f * k, -10f * k), new Vector2(200f * k, 56f * k));
            else
                UiKit.Anchor((RectTransform)ad.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 16f * k), new Vector2(196f * k, 54f * k));
            UiKit.Anchor((RectTransform)count.transform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(-14f * k, 12f * k), new Vector2(52f * k, 52f * k));
            UiKit.Anchor((RectTransform)lockB.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -36f * k), new Vector2(86f * k, 44f * k));
        }

        void WideLayout()
        {
            Reparent(_rtBack, stage); Reparent(_rtGear, stage); Reparent(_rtLb, stage);
            Reparent(_rtPillL, stage); Reparent(_rtPillR, stage); Reparent(_rtStrip, stage);
            Reparent(_rtRainbow, stage); Reparent(_rtBomb, stage);

            // Центры боковых панелей — между краем колонки (540) и краем экрана.
            float half  = StageFitter.HalfWidth;
            float panel = (half + ColumnFitter.DesignW * 0.5f) * 0.5f;

            // Стрелка «назад» — в нижний угол, подальше от бонусов и настроек.
            UiKit.Anchor(_rtBack, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(120f, 100f), new Vector2(112f, 112f));
            UiKit.Anchor(_rtLb,   new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-244f, -76f), new Vector2(120f, 120f));
            UiKit.Anchor(_rtGear, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-104f, -76f), new Vector2(120f, 120f));

            // Левая панель: счётчики сверху, под ними — легенда зверей в две колонки.
            UiKit.Anchor(_rtPillL, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-panel - 124f, -170f), new Vector2(236f, 116f));
            UiKit.Anchor(_rtPillR, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-panel + 124f, -170f), new Vector2(236f, 116f));

            _capColors.gameObject.SetActive(true);
            UiKit.Anchor(_capColors.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f),
                new Vector2(-panel - 242f, -272f), new Vector2(420f, 40f));

            // Полоска-подложка не нужна: в две колонки зверей держат собственные белые диски.
            // Тень гасим отдельно — иначе от прозрачной панели остаётся бежевый призрак.
            _stripBg.color = new Color(1f, 1f, 1f, 0f);
            UiKit.Anchor(_rtStrip, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-panel, -450f), new Vector2(500f, 300f));
            UiKit.SetShadowVisible(_rtStrip, false);
            for (int i = 0; i < 8; i++)
            {
                _slotDisc[i].color = White;
                UiKit.Anchor(_slotRt[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(((i & 1) - 0.5f) * 190f, 92f - (i >> 1) * 122f), new Vector2(104f, 104f));
                UiKit.Anchor((RectTransform)_slotImg[i].transform, new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(86f, 86f));
            }

            // Правая панель: бонусы крупно, друг под другом — рядом встаёт бейдж «▶ +3».
            SizeBonus(_rtRainbow, _rainbowIcon, rainbowAd, _rainbowCountBadge, _rainbowLock, 168f);
            SizeBonus(_rtBomb,    _bombIcon,    bombAd,    _bombCountBadge,    _bombLock,    168f);
            UiKit.Anchor(_rtRainbow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(panel, 190f), new Vector2(168f, 168f));
            UiKit.Anchor(_rtBomb,    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(panel, -110f), new Vector2(168f, 168f));

            _capRainbow.gameObject.SetActive(true);
            _capBomb.gameObject.SetActive(true);
            UiKit.Anchor(_capRainbow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(panel, 190f - 122f), new Vector2(300f, 36f));
            UiKit.Anchor(_capBomb.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(panel, -110f - 122f), new Vector2(300f, 36f));
        }

        void BuildTopBar()
        {
            var back = UiKit.CircleButton(root, new Vector2(0f, 1f), new Vector2(30f, -30f),
                96f, White, out var backIcon, () => gm.OpenTitle());
            backIcon.sprite = IconFactory.Get("back", Brown);
            _rtBack = (RectTransform)back.transform;

            var gear = UiKit.CircleButton(root, new Vector2(1f, 1f), new Vector2(-30f, -30f),
                96f, White, out var gearIcon, () => gm.SetPaused(true));
            gearIcon.sprite = IconFactory.Get("gear", Brown);
            _rtGear = (RectTransform)gear.transform;

            var lb = UiKit.CircleButton(root, new Vector2(1f, 1f), new Vector2(-146f, -30f),
                96f, White, out var lbIcon, () => gm.OpenLeaderboard());
            lbIcon.sprite = IconFactory.Get("leaderboard", Brown);
            _rtLb = (RectTransform)lb.transform;

            _tLevel = BuildStatPill(new Vector2(-124f, -150f), Loc.T("LEVEL", "УРОВЕНЬ"), out _rtPillL);
            _tShots = BuildStatPill(new Vector2( 124f, -150f), Loc.T("SHOTS", "ВЫСТРЕЛЫ"), out _rtPillR);
        }

        TMP_Text BuildStatPill(Vector2 pos, string caption, out RectTransform rt)
        {
            var pill = UiKit.Pill(root, new Vector2(0.5f, 1f), pos, new Vector2(228f, 100f), White);
            rt = pill.rectTransform;

            var val = UiKit.Label(pill.transform, "0", 46, TextAnchor.MiddleCenter, Brown);
            val.fontStyle = FontStyles.Bold;
            val.rectTransform.anchorMin = new Vector2(0f, 0.28f);
            val.rectTransform.anchorMax = Vector2.one;
            val.rectTransform.offsetMin = val.rectTransform.offsetMax = Vector2.zero;

            var cap = UiKit.Label(pill.transform, caption, 18, TextAnchor.MiddleCenter, BrownSoft);
            cap.fontStyle = FontStyles.Bold;
            cap.rectTransform.anchorMin = Vector2.zero;
            cap.rectTransform.anchorMax = new Vector2(1f, 0.32f);
            cap.rectTransform.offsetMin = new Vector2(0f, 8f);
            cap.rectTransform.offsetMax = Vector2.zero;
            return val;
        }

        void BuildUnlockStrip()
        {
            var pal = new GameConfig().palette;

            var strip = UiKit.Pill(root, new Vector2(0.5f, 1f), new Vector2(0f, -298f),
                new Vector2(1026f, 118f), White);
            _stripBg = strip;
            _rtStrip = strip.rectTransform;

            for (int i = 0; i < 8; i++)
            {
                float x = (i - 3.5f) * 120f;
                var go = new GameObject("Slot" + i, typeof(RectTransform));
                go.transform.SetParent(strip.transform, false);

                // Слот = белый диск-подложка, зверь — ребёнком поверх. В портрете диск
                // прозрачный (полоска сама белая), на десктопе он и держит форму легенды.
                var disc = go.AddComponent<Image>();
                disc.sprite = SpriteFactory.Circle();
                disc.raycastTarget = false;
                disc.color = new Color(1f, 1f, 1f, 0f);
                _slotDisc[i] = disc;
                UiKit.Anchor(disc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, 0f), new Vector2(78f, 78f));

                var animalGo = new GameObject("Animal", typeof(RectTransform));
                animalGo.transform.SetParent(go.transform, false);
                var img = animalGo.AddComponent<Image>();
                img.sprite = AnimalSpriteFactory.Get(i, pal[i]);
                img.preserveAspect = true;
                img.raycastTarget = false;
                UiKit.Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(78f, 78f));
                _slotImg[i] = img;
                _slotRt[i]  = disc.rectTransform;

                if (i >= 3)
                {
                    var badge = UiKit.Panel(go.transform, BadgeDark, false);
                    UiKit.Anchor(badge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0f, -27f), new Vector2(58f, 28f));
                    var lvl = UiKit.Label(badge.transform, Loc.T("Lv", "Ур.") + UnlockLevel[i], 19,
                        TextAnchor.MiddleCenter, White);
                    lvl.fontStyle = FontStyles.Bold;
                    UiKit.Stretch(lvl.rectTransform);
                    _slotLock[i] = badge.gameObject;
                }
            }
        }

        void BuildBonusBar()
        {
            _tRainbow = BuildBonus(new Vector2(-100f, 100f), GameConfig.RainbowId, GameManager.RainbowUnlockLevel,
                out _rainbowIcon, out rainbowAd, out _rainbowCountBadge, out _rainbowLock, () => gm.UseRainbow(),
                out _rtRainbow);
            _tBomb    = BuildBonus(new Vector2(100f, 100f), GameConfig.BombId, GameManager.BombUnlockLevel,
                out _bombIcon, out bombAd, out _bombCountBadge, out _bombLock, () => gm.UseBomb(),
                out _rtBomb);
        }

        TMP_Text BuildBonus(Vector2 posFromBottom, int specialId, int unlockLevel, out Image icon,
            out GameObject adBadge, out GameObject countBadge, out GameObject lockBadge,
            UnityEngine.Events.UnityAction onClick, out RectTransform btnRt)
        {
            var btn = UiKit.CircleButton(root, new Vector2(0.5f, 0f), posFromBottom, 128f,
                White, out icon, onClick);
            icon.sprite = AnimalSpriteFactory.Get(specialId, Color.white);
            var holder = (RectTransform)btn.transform;
            btnRt = holder;

            var badge = UiKit.Panel(holder, BadgeDark, false);
            UiKit.Anchor(badge.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(-8f, 6f), new Vector2(52f, 52f));
            var count = UiKit.Label(badge.transform, "5", 30, TextAnchor.MiddleCenter, White);
            count.fontStyle = FontStyles.Bold;
            UiKit.Stretch(count.rectTransform);
            countBadge = badge.gameObject;

            // Иконку бонуса под рекламным бейджем не прячем — игрок должен видеть, какой
            // бонус пополняет; клик проходит сквозь бейдж на кнопку под ним.
            // п.4.5.1: бейдж называет количество награды («▶ +3»), а полный текст
            // («Посмотри рекламу и получи +3 радуги») — в диалоге подтверждения; ролик
            // стартует только оттуда. Раньше здесь был голый ▶ — за это и сняли черновик.
            var ad = UiKit.Panel(holder, GreenAd, false);
            UiKit.Anchor(ad.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(2f, 8f), new Vector2(118f, 54f));
            var adTxt = UiKit.Label(ad.transform, AdLoc.RefillBadge, 26, TextAnchor.MiddleCenter, White);
            adTxt.fontStyle = FontStyles.Bold;
            adTxt.enableAutoSizing = true;
            adTxt.fontSizeMin = 18; adTxt.fontSizeMax = 26;
            UiKit.Stretch(adTxt.rectTransform);
            adBadge = ad.gameObject;

            var lockB = UiKit.Panel(holder, BadgeDark, false);
            UiKit.Anchor(lockB.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -36f), new Vector2(86f, 44f));
            var lockTxt = UiKit.Label(lockB.transform, Loc.T("Lv", "Ур.") + unlockLevel, 27, TextAnchor.MiddleCenter, White);
            lockTxt.fontStyle = FontStyles.Bold;
            UiKit.Stretch(lockTxt.rectTransform);
            lockBadge = lockB.gameObject;
            return count;
        }

        static readonly Color CandyBlue = new Color(0.30f, 0.68f, 0.98f, 1f);
        static readonly Color CandyOrng = new Color(0.98f, 0.62f, 0.22f, 1f);
        static readonly Color Coral     = new Color(0.96f, 0.44f, 0.42f, 1f);
        static readonly Color LoseBlue      = new Color(0.44f, 0.66f, 0.87f, 1f);
        static readonly Color LoseLightBlue = new Color(0.84f, 0.91f, 0.97f, 1f);
        static readonly Color LoseBlueText  = new Color(0.32f, 0.53f, 0.78f, 1f);
        TMP_Text _loseContLabel;
        GameObject _scBtn, _scShadow;
        GameObject _skipBtn, _skipShadow;

        void BuildResultPanel()
        {
            resultRoot = new GameObject("Result", typeof(RectTransform));
            resultRoot.transform.SetParent(modalRoot, false);
            UiKit.Stretch((RectTransform)resultRoot.transform);
            var dim = UiKit.Panel(resultRoot.transform, new Color(0.07f, 0.06f, 0.09f, 0.88f), true);
            UiKit.FullScreenDim(dim.rectTransform);

            winGroup = NewGroup("Win", resultRoot.transform);
            _sunburstRt  = MakeGlow(winGroup.transform, new Color(1f, 0.84f, 0.32f, 0.42f), 1500f, new Vector2(0f, 60f), UiKit.SunburstSprite);
            _glowRt      = MakeGlow(winGroup.transform, new Color(1f, 0.9f, 0.42f, 0.5f),   1120f, new Vector2(0f, 60f), UiKit.GlowSprite);
            _winMascotRt = MakeMascot(winGroup.transform, "Mascots/cat_3x4", new Vector2(640f, 910f), new Vector2(0f, 40f), Color.white, false);
            BuildConfetti(winGroup.transform);
            BuildWinWord(winGroup.transform);

            _winSub = UiKit.Label(winGroup.transform, _winSubs[0], 42,
                TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f));
            _winSub.fontStyle = FontStyles.Bold;
            ApplyOutline(_winSub, new Color(0.30f, 0.18f, 0.06f), 0.22f);
            UiKit.Anchor(_winSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -520f), new Vector2(920f, 70f));

            var next = UiKit.CandyBtn(winGroup.transform, Loc.T("Level ", "Уровень ") + "2", 60, CandyOrng, White,
                () => gm.NextLevel());
            _nextLabel = next.GetComponentInChildren<TMP_Text>();
            ApplyOutline(_nextLabel, new Color(0.62f, 0.32f, 0.06f), 0.16f);
            _nextRt = (RectTransform)next.transform;
            UiKit.Anchor(_nextRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 180f), new Vector2(660f, 152f));
            UiKit.AddShadow(_nextRt, 12f, -7f);
            _nextCg = next.gameObject.AddComponent<CanvasGroup>();

            loseGroup = NewGroup("Lose", resultRoot.transform);
            _loseMascotRt = MakeMascot(loseGroup.transform, "Mascots/sad_kitty", new Vector2(690f, 880f), new Vector2(0f, 70f),
                Color.white, false);
            // Текст (причина поражения) задаётся позже, в UpdateModals.
            titleText = UiKit.Label(loseGroup.transform, "", 64, TextAnchor.MiddleCenter, Coral);
            titleText.fontStyle = FontStyles.Bold;
            titleText.enableAutoSizing = true; titleText.fontSizeMin = 40; titleText.fontSizeMax = 64;
            ApplyOutline(titleText, new Color(0.32f, 0.08f, 0.06f), 0.18f);
            UiKit.Anchor(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 656f), new Vector2(980f, 160f));

            // «Left: N» шаров красным — стимул досмотреть rewarded ради второго шанса.
            var ball = new GameObject("Ball", typeof(RectTransform));
            ball.transform.SetParent(loseGroup.transform, false);
            var bimg = ball.AddComponent<Image>();
            bimg.sprite = AnimalSpriteFactory.Get(0, new GameConfig().palette[0]);
            bimg.preserveAspect = true; bimg.raycastTarget = false;
            UiKit.Anchor(bimg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-98f, -432f), new Vector2(76f, 76f));
            _loseCount = UiKit.Label(loseGroup.transform, "", 44, TextAnchor.MiddleLeft, White);
            _loseCount.fontStyle = FontStyles.Bold;
            UiKit.Anchor(_loseCount.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(-42f, -432f), new Vector2(340f, 64f));

            // п.4.5.1: в подписи названо количество награды («+5 выстрелов»), не только действие.
            var cont = UiKit.CandyBtn(loseGroup.transform, AdLoc.SecondChanceShotsBtn, 44, LoseBlue, White,
                () => gm.WatchAdForSecondChance());
            _loseContLabel = cont.GetComponentInChildren<TMP_Text>();
            _loseContLabel.enableAutoSizing = true;
            _loseContLabel.fontSizeMin = 28; _loseContLabel.fontSizeMax = 46;
            _loseContLabel.lineSpacing = -12f;
            ApplyOutline(_loseContLabel, new Color(0.16f, 0.30f, 0.52f), 0.16f);
            var contRt = (RectTransform)cont.transform;
            UiKit.Anchor(contRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 316f), new Vector2(680f, 150f));
            _scShadow = UiKit.AddShadow(contRt, 12f, -7f).gameObject;
            _scBtn = cont.gameObject;
            AddAdBadge(cont.transform);

            // Зелёный, не синий как «Второй шанс» — это «продвижение», а не «спасение».
            var skip = UiKit.CandyBtn(loseGroup.transform, AdLoc.SkipLevelBtn, 42, Green, White,
                () => gm.WatchAdForSkipLevel());
            var skipLabel = skip.GetComponentInChildren<TMP_Text>();
            skipLabel.enableAutoSizing = true;
            skipLabel.fontSizeMin = 26; skipLabel.fontSizeMax = 42;
            skipLabel.lineSpacing = -12f;
            ApplyOutline(skipLabel, new Color(0.16f, 0.42f, 0.24f), 0.16f);
            var skipRt = (RectTransform)skip.transform;
            UiKit.Anchor(skipRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 316f), new Vector2(680f, 150f));
            _skipShadow = UiKit.AddShadow(skipRt, 12f, -7f).gameObject;
            _skipBtn = skip.gameObject;
            AddAdBadge(skip.transform);

            var retry = UiKit.CandyBtn(loseGroup.transform, Loc.T("Try again", "Ещё раз"), 52, LoseLightBlue, LoseBlueText,
                () => gm.Retry());
            ApplyOutline(retry.GetComponentInChildren<TMP_Text>(), new Color(0.62f, 0.74f, 0.90f), 0.14f);
            var retryRt = (RectTransform)retry.transform;
            UiKit.Anchor(retryRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 146f), new Vector2(680f, 150f));
            UiKit.AddShadow(retryRt, 12f, -7f);
        }

        RectTransform MakeMascot(Transform parent, string resPath, Vector2 size, Vector2 pos, Color tint, bool preserve)
        {
            var go = new GameObject("Mascot", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            var tex = Resources.Load<Texture2D>(resPath);
            if (tex != null)
                img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            img.preserveAspect = preserve; img.raycastTarget = false; img.color = tint;
            UiKit.Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            return img.rectTransform;
        }

        RectTransform MakeGlow(Transform parent, Color col, float size, Vector2 pos, Sprite sprite)
        {
            var go = new GameObject("Glow", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite; img.color = col; img.raycastTarget = false;
            UiKit.Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            return img.rectTransform;
        }

        // Обводка текста (индивидуальный материал, чтобы не задеть остальной текст).
        static void ApplyOutline(TMP_Text t, Color col, float width)
        {
            t.fontMaterial = new Material(t.fontSharedMaterial);
            t.fontMaterial.SetFloat(TMPro.ShaderUtilities.ID_OutlineWidth, width);
            t.fontMaterial.SetColor(TMPro.ShaderUtilities.ID_OutlineColor, col);
        }

        // RU-вариант раньше был пустой строкой — бейдж оставался голым треугольником, то есть
        // ровно та же претензия п.4.5.1, что и по кнопке бонуса. Теперь слово есть в обоих языках.
        void AddAdBadge(Transform btn)
        {
            var badge = UiKit.Panel(btn, GreenAd, false);
            UiKit.Anchor(badge.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-100f, 8f), new Vector2(184f, 50f));
            var t = UiKit.Label(badge.transform, UiSymbols.Play + " " + AdLoc.AdWord, 24, TextAnchor.MiddleCenter, White);
            t.fontStyle = FontStyles.Bold;
            UiKit.Stretch(t.rectTransform);
        }

        void BuildPausePanel()
        {
            pauseRoot = new GameObject("Pause", typeof(RectTransform));
            pauseRoot.transform.SetParent(modalRoot, false);
            UiKit.Stretch((RectTransform)pauseRoot.transform);
            var dim = UiKit.Panel(pauseRoot.transform, Dim, true);
            UiKit.FullScreenDim(dim.rectTransform);

            var box = Card(pauseRoot.transform, new Vector2(0f, 0f), new Vector2(680f, 460f));
            var title = UiKit.Label(box.transform, Loc.T("Settings", "Настройки"), 48, TextAnchor.MiddleCenter, Brown);
            title.fontStyle = FontStyles.Bold;
            UiKit.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(520f, 80f));

            var close = UiKit.CircleButton(box.transform, new Vector2(1f, 1f), new Vector2(-16f, -16f),
                80f, Peach, out var closeIcon, () => gm.SetPaused(false));
            closeIcon.sprite = IconFactory.Get("close", Brown);

            // У Яндекса нет платформенной кнопки mute — мьютим сами (PlayerPrefs +
            // YandexBridge.ApplyAudio), а не через AudioListener напрямую: иначе закрытие
            // рекламы сбросило бы состояние.
            var sound = UiKit.CircleButton(box.transform, new Vector2(0.5f, 0.5f), new Vector2(-90f, -30f),
                140f, Peach, out _soundIcon, () =>
                {
                    bool muted = !YandexBridge.UserMuted;
                    YandexBridge.SetUserMuted(muted);
                    Analytics.MuteToggle(muted);
                    RefreshSoundIcon();
                });
            RefreshSoundIcon();

            var restart = UiKit.CircleButton(box.transform, new Vector2(0.5f, 0.5f), new Vector2(90f, -30f),
                140f, Peach, out var restartIcon, () => { gm.SetPaused(false); gm.Retry(); });
            restartIcon.sprite = IconFactory.Get("restart", Brown);

#if UNITY_EDITOR
            var reset = UiKit.Btn(box.transform, "Reset progress (dev)", 28,
                new Color(0.86f, 0.55f, 0.50f, 1f), Color.white,
                () => { gm.SetPaused(false); gm.ResetProgress(); });
            UiKit.Anchor((RectTransform)reset.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(460f, 82f));
            UiKit.AddShadow((RectTransform)reset.transform, 10f, -6f);
#endif
        }

        void RefreshSoundIcon()
        {
            if (_soundIcon)
                _soundIcon.sprite = IconFactory.Get(YandexBridge.UserMuted ? "sound_off" : "sound_on", Brown);
        }

        Image Card(Transform parent, Vector2 pos, Vector2 size)
        {
            var img = UiKit.Panel(parent, White, true);
            UiKit.Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            UiKit.AddShadow(img.rectTransform, 16f, -10f);
            return img;
        }

        void SoftButton(Transform parent, string text, Color bg, Color fg,
            Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var btn = UiKit.Btn(parent, text, 38, bg, fg, onClick);
            var rt = (RectTransform)btn.transform;
            UiKit.Anchor(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pos, size);
            UiKit.AddShadow(rt, 10f, -6f);
        }

        static GameObject NewGroup(string name, Transform box)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(box, false);
            UiKit.Stretch((RectTransform)go.transform);
            return go;
        }

        void BuildWinWord(Transform parent)
        {
            // Единый TMP с цветными тегами (не отдельные объекты на букву) — сохраняет
            // кернинг; «лесенку» и дугу делаем анимацией меша посимвольно (CascadeTitle).
            string[] hex = { "FF5A7A", "FF8A3D", "FFC13D", "5FCB6A", "3DC7D6", "4D8CF5", "B36BF0" };
            var rich = new System.Text.StringBuilder();
            for (int i = 0; i < WinWord.Length; i++)
                rich.Append("<color=#").Append(hex[i % hex.Length]).Append('>').Append(WinWord[i]).Append("</color>");
            _winTitle = UiKit.Label(parent, rich.ToString(), 84, TextAnchor.MiddleCenter, Color.white);
            _winTitle.fontStyle = FontStyles.Bold;
            _winTitle.characterSpacing = 1f;
            _winTitle.enableAutoSizing = true; _winTitle.fontSizeMin = 44; _winTitle.fontSizeMax = 84;
            ApplyOutline(_winTitle, new Color(0.22f, 0.12f, 0.05f), 0.22f);
            UiKit.Anchor(_winTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 720f), new Vector2(1040f, 220f));
        }

        void BuildConfetti(Transform parent)
        {
            int n = 32;
            _confetti = new RectTransform[n];
            _confPos = new Vector2[n]; _confVel = new Vector2[n]; _confRotV = new float[n];
            for (int i = 0; i < n; i++)
            {
                var img = UiKit.Panel(parent, Candy[i % Candy.Length], false);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                img.rectTransform.sizeDelta = new Vector2(Random.Range(20f, 32f), Random.Range(12f, 20f));
                img.gameObject.SetActive(false);
                _confetti[i] = img.rectTransform;
            }
        }

        void InitConfetti()
        {
            for (int i = 0; i < _confetti.Length; i++)
            {
                _confPos[i] = new Vector2(Random.Range(-505f, 505f), Random.Range(-960f, 1240f));
                _confVel[i] = new Vector2(Random.Range(-70f, 70f), -Random.Range(240f, 470f));
                _confRotV[i] = Random.Range(-220f, 220f);
                _confetti[i].GetComponent<Image>().color = Candy[Random.Range(0, Candy.Length)];
                _confetti[i].anchoredPosition = _confPos[i];
                _confetti[i].localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                _confetti[i].gameObject.SetActive(true);
            }
        }

        void UpdateConfetti(float dt)
        {
            for (int i = 0; i < _confetti.Length; i++)
            {
                _confPos[i] += _confVel[i] * dt;
                _confPos[i].x += Mathf.Sin(Time.unscaledTime * 2f + i) * 26f * dt;
                _confPos[i].x = Mathf.Clamp(_confPos[i].x, -515f, 515f);   // не вылазить за 9:16
                if (_confPos[i].y < -1080f)
                {
                    _confPos[i] = new Vector2(Random.Range(-505f, 505f), Random.Range(1000f, 1360f));
                    _confVel[i] = new Vector2(Random.Range(-70f, 70f), -Random.Range(240f, 470f));
                    _confetti[i].GetComponent<Image>().color = Candy[Random.Range(0, Candy.Length)];
                }
                _confetti[i].anchoredPosition = _confPos[i];
                _confetti[i].Rotate(0, 0, _confRotV[i] * dt);
            }
        }

        IEnumerator PlayWinIntro()
        {
            _nextCg.alpha = 0f; _nextRt.localScale = new Vector3(0.6f, 0.6f, 1f);
            if (_winMascotRt) _winMascotRt.localScale = Vector3.zero;
            if (_glowRt) _glowRt.localScale = Vector3.zero;
            if (_sunburstRt) _sunburstRt.localScale = Vector3.zero;

            int idx = Random.Range(0, _winSubs.Length);
            if (idx == _lastSubIdx) idx = (idx + 1) % _winSubs.Length;
            _lastSubIdx = idx;
            _winSub.text = _winSubs[idx];
            _nextLabel.text = Loc.T("Level ", "Уровень ") + (gm.CurrentLevel + 1);

            InitConfetti();
            PopParticles.Instance?.WinBurst();
            // PlayWin не зовём здесь — джингл уже играет GameManager.OnResolved (иначе сыграет дважды).
            if (_sunburstRt) StartCoroutine(PopScale(_sunburstRt, 0f));
            if (_glowRt) StartCoroutine(PopScale(_glowRt, 0.02f));
            if (_winMascotRt) StartCoroutine(PopScale(_winMascotRt, 0.06f));
            yield return CascadeTitle();
            yield return new WaitForSecondsRealtime(0.12f);
            yield return FadePopNext();
        }

        IEnumerator PopScale(RectTransform rt, float delay)
        {
            rt.localScale = Vector3.zero;
            float e = 0f;
            while (e < delay) { e += Time.unscaledDeltaTime; yield return null; }
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * 4.5f;
                float s = EaseOutBack(Mathf.Clamp01(t));
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        IEnumerator CascadeTitle()
        {
            var tmp = _winTitle;
            tmp.ForceMeshUpdate();
            var info = tmp.textInfo;
            int n = info.characterCount;
            var orig = new Vector3[info.meshInfo.Length][];
            for (int m = 0; m < info.meshInfo.Length; m++)
                orig[m] = (Vector3[])info.meshInfo[m].vertices.Clone();

            float xmin = float.MaxValue, xmax = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                var ci = info.characterInfo[i];
                if (!ci.isVisible) continue;
                var o = orig[ci.materialReferenceIndex];
                float cx = (o[ci.vertexIndex].x + o[ci.vertexIndex + 2].x) * 0.5f;
                if (cx < xmin) xmin = cx; if (cx > xmax) xmax = cx;
            }
            float span = Mathf.Max(1f, xmax - xmin);
            const float arcAmp = 30f;

            const float perChar = 0.05f, popDur = 0.24f;
            float t = 0f; int soundIdx = 0; bool done = false;
            while (!done)
            {
                t += Time.unscaledDeltaTime;
                done = true;
                for (int i = 0; i < n; i++)
                {
                    var ci = info.characterInfo[i];
                    if (!ci.isVisible) continue;
                    float p = Mathf.Clamp01((t - i * perChar) / popDur);
                    if (p < 1f) done = false;
                    float s = EaseOutBack(p);
                    int mat = ci.materialReferenceIndex, vi = ci.vertexIndex;
                    var verts = info.meshInfo[mat].vertices;
                    var o = orig[mat];
                    Vector3 c = (o[vi] + o[vi + 2]) * 0.5f;
                    float u = (c.x - xmin) / span;
                    Vector3 tc = c + new Vector3(0f, arcAmp * Mathf.Sin(u * Mathf.PI), 0f);
                    for (int k = 0; k < 4; k++) verts[vi + k] = tc + (o[vi + k] - c) * s;
                }
                for (int m = 0; m < info.meshInfo.Length; m++)
                {
                    info.meshInfo[m].mesh.vertices = info.meshInfo[m].vertices;
                    tmp.UpdateGeometry(info.meshInfo[m].mesh, m);
                }
                while (soundIdx < n && t >= soundIdx * perChar)
                {
                    if (info.characterInfo[soundIdx].isVisible) AudioService.PlayStar();
                    soundIdx++;
                }
                yield return null;
            }
        }

        IEnumerator FadePopNext()
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * 4f;
                float ct = Mathf.Clamp01(t);
                _nextCg.alpha = ct;
                float s = Mathf.Lerp(0.6f, 1f, EaseOutBack(ct));
                _nextRt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            _nextCg.alpha = 1f; _nextRt.localScale = Vector3.one;
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.9f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        void Update()
        {
            if (gm == null) return;
            ApplyResponsiveLayout();   // окно браузера могли растянуть/сузить на лету
            UpdateStats();
            UpdateBonuses();
            UpdateUnlocks();
            UpdateModals();
            UpdateWinFx();
        }

        void UpdateWinFx()
        {
            if (resultRoot == null || !resultRoot.activeSelf || gm.State != GameState.Win) return;
            float dt = Time.unscaledDeltaTime;
            if (_sunburstRt) _sunburstRt.Rotate(0f, 0f, 14f * dt);
            UpdateConfetti(dt);
        }

        void UpdateStats()
        {
            int level = gm.CurrentLevel, shots = gm.ShotsLeft;
            if (level != _cLevel) { _cLevel = level; _tLevel.text = level.ToString(); }
            if (shots != _cShots) { _cShots = shots; _tShots.text = shots.ToString(); }
        }

        void UpdateBonuses()
        {
            int r = gm.RainbowCharges, ru = gm.RainbowUnlocked ? 1 : 0;
            if (r != _cRainbow || ru != _uRainbow)
            {
                _cRainbow = r; _uRainbow = ru; _tRainbow.text = r.ToString();
                ApplyBonusState(r, gm.RainbowUnlocked, rainbowAd, _rainbowIcon, _rainbowCountBadge, _rainbowLock);
            }
            int b = gm.BombCharges, bu = gm.BombUnlocked ? 1 : 0;
            if (b != _cBomb || bu != _uBomb)
            {
                _cBomb = b; _uBomb = bu; _tBomb.text = b.ToString();
                ApplyBonusState(b, gm.BombUnlocked, bombAd, _bombIcon, _bombCountBadge, _bombLock);
            }
        }

        void ApplyBonusState(int charges, bool unlocked, GameObject ad, Image icon,
            GameObject countBadge, GameObject lockBadge)
        {
            if (lockBadge) lockBadge.SetActive(!unlocked);
            if (!unlocked)
            {
                if (ad) ad.SetActive(false);
                if (countBadge) countBadge.SetActive(false);
                if (icon) icon.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                return;
            }
            bool empty = charges == 0;
            if (ad) ad.SetActive(empty);
            if (countBadge) countBadge.SetActive(!empty);
            if (icon) icon.color = empty ? new Color(0.82f, 0.82f, 0.82f, 1f) : Color.white;
        }

        void UpdateUnlocks()
        {
            int unlocked = Mathf.Clamp(gm.LevelDef.colorCount, 0, 8);
            if (unlocked == _shownUnlocked) return;
            bool announce = _shownUnlocked >= 0;   // не анимируем на самом первом кадре
            for (int i = 0; i < 8; i++)
            {
                bool open = i < unlocked;
                _slotImg[i].color = open ? Color.white : LockTint;
                if (_slotLock[i]) _slotLock[i].SetActive(!open);
                if (announce && open && i >= _shownUnlocked)
                    StartCoroutine(UnlockPop(_slotImg[i].rectTransform));
            }
            _shownUnlocked = unlocked;
        }

        IEnumerator UnlockPop(RectTransform rt)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * 3.5f;
                float s = 1f + Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.6f * Mathf.Exp(-t * 2f);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        void UpdateModals()
        {
            bool paused = gm.IsPaused;
            if (pauseRoot.activeSelf != paused) pauseRoot.SetActive(paused);

            var st = gm.State;
            bool showResult = !paused && (st == GameState.Win || st == GameState.Lose);
            if (resultRoot.activeSelf != showResult) resultRoot.SetActive(showResult);

            if (showResult && st != _lastState)
            {
                winGroup.SetActive(st == GameState.Win);
                loseGroup.SetActive(st == GameState.Lose);
                if (st == GameState.Win)
                {
                    StartCoroutine(PlayWinIntro());
                }
                else
                {
                    bool overflow = gm.LoseByOverflow;
                    titleText.text = overflow
                        ? Loc.T("The bubbles reached the edge!", "Шарики дошли до края!")
                        : Loc.T("Out of shots!", "Выстрелы кончились!");
                    _loseCount.text = Loc.T("Left:", "Осталось:") + "  <color=#FF4747>" + gm.BubbleCount + "</color>";
                    // Награда за rewarded зависит от причины проигрыша — подпись кнопки
                    // обязана называть именно то, что выдадут (п.4.5.1).
                    if (_loseContLabel)
                        _loseContLabel.text = overflow ? AdLoc.SecondChanceClearBtn : AdLoc.SecondChanceShotsBtn;
                    // Один слот кнопки: «Второй шанс» (до 2 раз), затем «Пропустить уровень» —
                    // видна ровно одна из двух, вместе с тенью.
                    bool sc   = gm.SecondChanceAvailable;
                    bool skip = !sc && gm.SkipLevelAvailable;
                    if (_scBtn)      _scBtn.SetActive(sc);
                    if (_scShadow)   _scShadow.SetActive(sc);
                    if (_skipBtn)    _skipBtn.SetActive(skip);
                    if (_skipShadow) _skipShadow.SetActive(skip);
                }
            }
            _lastState = st;
        }
    }
}
