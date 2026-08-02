using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

namespace CozyAnimalTown
{
    /// <summary>Экран лидеров: раскладка и цвета подогнаны под нативную таблицу Яндекса.</summary>
    public class LeaderboardScreen : MonoBehaviour
    {
        // поля заполняет JsonUtility рефлексией — CS0649 ложный
#pragma warning disable 649
        [Serializable] class LbEntry { public int rank; public long score; public string name; public string avatar; public bool isUser; }
        // error различает «таблица пуста» и «загрузить не удалось» — раньше и то и другое
        // приезжало одинаковым пустым списком, и при обрыве сети игроку показывали
        // «Пока пусто — стань первым!», то есть заведомую неправду.
        [Serializable] class LbData  { public int userRank; public LbEntry[] entries; public bool error; }
#pragma warning restore 649

        const float RowH    = 116f;
        const float ZigzagH = 64f;

        static readonly Color BgDark    = new Color(0.086f, 0.086f, 0.105f, 0.985f);
        static readonly Color RowLine   = new Color(1f, 1f, 1f, 0.07f);
        static readonly Color TxtWhite  = new Color(0.95f, 0.95f, 0.97f, 1f);
        static readonly Color TxtGrey   = new Color(0.62f, 0.62f, 0.66f, 1f);
        static readonly Color Highlight = new Color(1f, 1f, 1f, 0.15f);
        static readonly Color BackCirc  = new Color(0.22f, 0.22f, 0.25f, 1f);
        static readonly Color[] AvatarPastel =
        {
            new Color(0.93f, 0.55f, 0.55f), new Color(0.95f, 0.75f, 0.40f),
            new Color(0.55f, 0.78f, 0.55f), new Color(0.50f, 0.68f, 0.90f),
            new Color(0.76f, 0.58f, 0.88f), new Color(0.92f, 0.62f, 0.78f),
        };

        Canvas canvas;
        RectTransform _content;
        RectTransform _viewport;

        // Сколько соседей игрока показываем по каждую сторону от его строки.
        // Блок под разделителем = 2 + сам игрок + 2 = ровно 5 строк.
        const int NearSide = 2;
        TMP_Text _status;
        Action _onClose;
        bool _closing;
        // загруженные аватарки (Texture2D + Sprite) — руками уничтожаем при закрытии,
        // иначе за сессию с многократными открытиями накапливаются в WebGL-памяти
        readonly System.Collections.Generic.List<UnityEngine.Object> _loadedAvatars = new();

        public void Show(Action onClose)
        {
            _onClose = onClose;

            canvas = UiKit.CreateCanvas("LeaderboardCanvas", 510);
            var root = UiKit.Column(canvas);

            var bg = UiKit.Panel(root, BgDark, true);
            UiKit.Stretch(bg.rectTransform);

            var back = UiKit.CircleButton(root, new Vector2(0f, 1f), new Vector2(30f, -30f),
                96f, BackCirc, out var backIcon, Close);
            backIcon.sprite = IconFactory.Get("back", TxtWhite);

            var title = UiKit.Label(root, Loc.T("Leaderboard", "Таблица лидеров"),
                60, TextAnchor.MiddleLeft, TxtWhite);
            title.fontStyle = FontStyles.Bold;
            UiKit.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(150f, -30f), new Vector2(880f, 96f));

            BuildScroll(root);

            _status = UiKit.Label(root, Loc.T("Loading…", "Загрузка…"),
                40, TextAnchor.MiddleCenter, TxtGrey);
            UiKit.Anchor(_status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(800f, 80f));

            YandexBridge.LeaderboardLoadedEvent += OnData;
            YandexBridge.RequestLeaderboard();
            _timeoutCo = StartCoroutine(LoadTimeout());
        }

        Coroutine _timeoutCo;

        /// <summary>
        /// Ответа может не прийти вообще — например промис SDK не резолвится. Без этого
        /// экран навсегда оставался на «Загрузка…»: подписка живёт до закрытия, а других
        /// способов узнать о провале нет.
        /// </summary>
        IEnumerator LoadTimeout()
        {
            yield return new WaitForSecondsRealtime(8f);
            if (_closing || _status == null) yield break;
            YandexBridge.LeaderboardLoadedEvent -= OnData;
            _status.gameObject.SetActive(true);
            _status.text = Loc.T("Couldn't load the table", "Не удалось загрузить таблицу");
        }

        void OnDestroy()
        {
            YandexBridge.LeaderboardLoadedEvent -= OnData;
            foreach (var o in _loadedAvatars)
                if (o) Destroy(o);
            _loadedAvatars.Clear();
        }

        void Close()
        {
            if (_closing) return;
            _closing = true;
            AudioService.PlayClick();
            _onClose?.Invoke();
            if (canvas) Destroy(canvas.gameObject);
            Destroy(gameObject);
        }

        void BuildScroll(RectTransform root)
        {
            var vpGo = new GameObject("Viewport", typeof(RectTransform));
            vpGo.transform.SetParent(root, false);
            var vp = (RectTransform)vpGo.transform;
            vp.anchorMin = new Vector2(0f, 0f);
            vp.anchorMax = new Vector2(1f, 1f);
            vp.offsetMin = new Vector2(20f, 24f);
            vp.offsetMax = new Vector2(-20f, -156f);
            _viewport = vp;
            vpGo.AddComponent<RectMask2D>();
            var vpImg = vpGo.AddComponent<Image>();      // невидимый, но ловит драг скролла
            vpImg.color = Color.clear;

            var cGo = new GameObject("Content", typeof(RectTransform));
            cGo.transform.SetParent(vp, false);
            _content = (RectTransform)cGo.transform;
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot     = new Vector2(0.5f, 1f);
            _content.offsetMin = new Vector2(0f, 0f);
            _content.offsetMax = new Vector2(0f, 0f);

            var sr = vpGo.AddComponent<ScrollRect>();
            sr.content            = _content;
            sr.viewport           = vp;
            sr.horizontal         = false;
            sr.vertical           = true;
            sr.movementType       = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity  = 40f;
        }

        void OnData(string json)
        {
            YandexBridge.LeaderboardLoadedEvent -= OnData;
            if (_closing || _content == null) return;

            LbData data;
            try { data = JsonUtility.FromJson<LbData>(json) ?? new LbData(); }
            catch { data = new LbData(); }
            var entries = data.entries ?? new LbEntry[0];
            if (_timeoutCo != null) { StopCoroutine(_timeoutCo); _timeoutCo = null; }

            if (entries.Length == 0)
            {
                _status.text = data.error
                    ? Loc.T("Couldn't load the table", "Не удалось загрузить таблицу")
                    : Loc.T("No records yet — be the first!", "Пока пусто — стань первым!");
                // Подсказку про вход показываем и здесь: раньше ранний return делал её
                // недостижимой ровно в том случае, когда она нужнее всего — гостю,
                // которому таблица пришла пустой.
                if (!data.error && data.userRank <= 0) BuildSignInHint(-120f);
                return;
            }
            _status.gameObject.SetActive(false);

            // Разрыв рангов делит ответ на две части: топ таблицы и окрестность игрока.
            int gap = -1;
            for (int i = 1; i < entries.Length; i++)
                if (entries[i].rank > entries[i - 1].rank + 1) { gap = i; break; }

            float y = 0f;
            if (gap < 0)
            {
                // Игрок сам в топе — разрыва нет, рисуем подряд.
                for (int i = 0; i < entries.Length; i++) { BuildRow(entries[i], y); y -= RowH; }
            }
            else
            {
                // Экран должен открываться так, чтобы строка игрока была видна СРАЗУ,
                // без прокрутки. Поэтому число строк топа не фиксировано, а считается
                // от реальной высоты вьюпорта: она заметно разная в портрете и в
                // горизонтальной раскладке (модалка одна, а видимая часть — нет).
                float vpH = _viewport != null ? _viewport.rect.height : 1400f;
                int fitRows = Mathf.FloorToInt((vpH - ZigzagH) / RowH);
                int nearVisible = NearSide * 2 + 1;                     // соседи + сам игрок
                int topCount = Mathf.Clamp(fitRows - nearVisible, 3, gap);

                for (int i = 0; i < topCount; i++) { BuildRow(entries[i], y); y -= RowH; }

                BuildZigzag(y);
                y -= ZigzagH;

                // Ниже разделителя — от «игрок минус два» и ДО КОНЦА выдачи. Видно из
                // них ровно пять, остальные достаются прокруткой: посмотреть, кто идёт
                // следом за тобой, — единственное, ради чего этот экран вообще листают.
                int from = gap;
                for (int i = gap; i < entries.Length; i++)
                    if (entries[i].rank >= data.userRank - NearSide) { from = i; break; }

                for (int i = from; i < entries.Length; i++) { BuildRow(entries[i], y); y -= RowH; }
            }

            // гостю — подсказка, почему его нет в таблице
            if (data.userRank <= 0)
            {
                BuildSignInHint(y - 40f);
                y -= 90f;
            }

            _content.sizeDelta = new Vector2(0f, -y + 20f);
            // Открываемся строго сверху. Без явного сброса ScrollRect оставлял список
            // прокрученным на середину — в горизонтальной раскладке экран открывался
            // где-то на 13-м месте.
            _content.anchoredPosition = Vector2.zero;
        }

        /// <summary>Подсказка гостю. Без упоминания чужого товарного знака: модерация
        /// сняла черновик по п.3.5 именно за строку «Войди в Яндекс…» на этом экране.</summary>
        void BuildSignInHint(float y)
        {
            var hint = UiKit.Label(_content, Loc.T(
                    "Sign in to join the leaderboard",
                    "Войди в аккаунт, чтобы попасть в таблицу"),
                30, TextAnchor.MiddleCenter, TxtGrey);
            UiKit.Anchor(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, y), new Vector2(940f, 60f));
        }

        /// <summary>
        /// Имя игрока, гарантированно видимое на экране.
        ///
        /// ЗАЧЕМ. В таблице лидеров имена приходят какие угодно: латиница, кириллица,
        /// китайский, армянский, эмодзи. Шрифт игры — Nunito, в нём есть только латиница
        /// и кириллица, и запасного шрифта у нас больше нет (LiberationSans-фолбэк убран
        /// ради веса билда). Символ без глифа TMP рисует НИЧЕМ — строка визуально пустая,
        /// хотя формально непустая, поэтому старая проверка IsNullOrWhiteSpace её
        /// пропускала. Именно так в таблице появлялась строка с аватаром, рангом и очками,
        /// но без имени.
        ///
        /// Выкидываем символы, которых в шрифте нет; если после этого ничего не осталось —
        /// показываем «Игрок». Пустого имени на экране быть не может ни при каком ответе SDK.
        /// </summary>
        string SafeName(string raw)
        {
            string fallback = Loc.T("Player", "Игрок");
            if (string.IsNullOrWhiteSpace(raw)) return fallback;

            raw = raw.Trim();
            var font = DynamicFont.Asset;
            if (font == null) return raw;

            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                // tryAddCharacter: true — ОБЯЗАТЕЛЬНО. Динамический шрифт растеризует
                // глифы лениво, при генерации меша в конце кадра, и без этого флага
                // HasCharacter смотрит только в таблицу уже нарисованного: буквы, которых
                // ещё не было на экране (К, П, х...), считались отсутствующими и
                // выгрызались из имён — «Виктор Кем» превращался в «Виктор ем».
                // С флагом глиф добавляется в атлас прямо здесь, и false остаётся только
                // у действительно отсутствующих в TTF символов (эмодзи, иероглифы).
                // Пробел глифа не имеет, но ширину даёт — его оставляем.
                if (c == ' ' || font.HasCharacter(c, false, true)) sb.Append(c);
            }
            string clean = sb.ToString().Trim();
            if (clean.Length == 0)
            {
                Debug.LogWarning($"[LB] имя «{raw}» целиком без глифов в шрифте — показываем «{fallback}»");
                return fallback;
            }
            return clean;
        }

        void BuildRow(LbEntry e, float y)
        {
            var row = new GameObject("Row" + e.rank, typeof(RectTransform));
            row.transform.SetParent(_content, false);
            var rt = (RectTransform)row.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, y - RowH);
            rt.offsetMax = new Vector2(0f, y);

            if (e.isUser)
            {
                var hl = UiKit.Panel(row.transform, Highlight, false);
                UiKit.Stretch(hl.rectTransform);
                hl.rectTransform.offsetMin = new Vector2(6f, 6f);
                hl.rectTransform.offsetMax = new Vector2(-6f, -6f);
            }

            var holder = new GameObject("Avatar", typeof(RectTransform));
            holder.transform.SetParent(row.transform, false);
            var circle = holder.AddComponent<Image>();
            circle.sprite = SpriteFactory.Circle();
            circle.color  = AvatarPastel[Mathf.Abs(e.rank) % AvatarPastel.Length];
            circle.raycastTarget = false;
            UiKit.Anchor(circle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(78f, 0f), new Vector2(84f, 84f));
            holder.AddComponent<Mask>().showMaskGraphic = true;   // круглая маска для фото

            string nm = SafeName(e.name);
            var letter = UiKit.Label(holder.transform, nm.Substring(0, 1).ToUpperInvariant(),
                38, TextAnchor.MiddleCenter, Color.white);
            letter.fontStyle = FontStyles.Bold;
            UiKit.Stretch(letter.rectTransform);

            if (!string.IsNullOrEmpty(e.avatar))
                StartCoroutine(LoadAvatar(e.avatar, holder.transform));

            var name = UiKit.Label(row.transform, nm, 34, TextAnchor.MiddleLeft, TxtWhite);
            name.fontStyle        = FontStyles.Bold;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            // Truncate обрезает длинные имена по ширине. ВЫСОТА РЕКТА ОБЯЗАНА ВМЕЩАТЬ
            // СТРОКУ: Truncate и Ellipsis режут построчно, и строка, не влезшая по
            // ВЕРТИКАЛИ, выбрасывается целиком. Ровно из-за этого имена не рисовались
            // вообще: высота строки Nunito 1.364 em — при кегле 34 это 46.4 px, а рект
            // был 46 px. Не хватало 0.4 px, и лейбл всегда был пуст — при любых данных.
            // Ранг (26pt в 36px, запас 0.5 px) и счёт (Overflow) при этом рисовались,
            // что и уводило поиски в сторону данных и глифов.
            name.overflowMode     = TextOverflowModes.Truncate;
            UiKit.Anchor(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(150f, 18f), new Vector2(560f, 60f));

            var rank = UiKit.Label(row.transform, "#" + e.rank, 26, TextAnchor.MiddleLeft, TxtGrey);
            UiKit.Anchor(rank.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(150f, -24f), new Vector2(560f, 36f));

            var score = UiKit.Label(row.transform, e.score.ToString(), 38, TextAnchor.MiddleRight, TxtWhite);
            score.fontStyle = FontStyles.Bold;
            UiKit.Anchor(score.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-36f, 0f), new Vector2(330f, 50f));

            var line = UiKit.Panel(row.transform, RowLine, false);
            UiKit.Anchor(line.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 0f), new Vector2(1000f, 2f));
        }

        void BuildZigzag(float y)
        {
            var zz = new GameObject("Zigzag", typeof(RectTransform));
            zz.transform.SetParent(_content, false);
            var rt = (RectTransform)zz.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, y - ZigzagH);
            rt.offsetMax = new Vector2(0f, y);

            for (int i = 0; i < 25; i++)
            {
                var d = new GameObject("d", typeof(RectTransform));
                d.transform.SetParent(zz.transform, false);
                var img = d.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.28f);
                img.raycastTarget = false;
                var drt = (RectTransform)d.transform;
                drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
                drt.sizeDelta = new Vector2(16f, 16f);
                drt.anchoredPosition = new Vector2((i - 12) * 40f, 0f);
                drt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
        }

        // Любая ошибка загрузки (CORS/404/сеть) — молча остаёмся на букве-фолбэке.
        IEnumerator LoadAvatar(string url, Transform holder)
        {
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) yield break;
                if (_closing || holder == null) yield break;

                var tex = DownloadHandlerTexture.GetContent(req);
                if (tex == null) yield break;

                var go = new GameObject("Photo", typeof(RectTransform));
                go.transform.SetParent(holder, false);
                var img = go.AddComponent<Image>();
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), tex.width);
                img.sprite = sprite;
                img.raycastTarget = false;
                UiKit.Stretch((RectTransform)go.transform);
                _loadedAvatars.Add(tex);
                _loadedAvatars.Add(sprite);
            }
        }
    }
}
