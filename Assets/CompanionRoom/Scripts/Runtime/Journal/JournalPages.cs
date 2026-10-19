using System.Collections.Generic;
using CompanionRoom.Tarot;
using CompanionRoom.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CompanionRoom.Journal
{
    /// <summary>Geometry shared by the page artwork and the 3D book, in book units (1 = page height).</summary>
    static class JournalLayout
    {
        public const float PageWidth = 0.72f;
        public const float PageHeight = 1f;
        /// <summary>Height of the pop-up cards' fold line on the left page (page UV).</summary>
        public const float HingeV = 0.13f;
        const float CardAspect = 0.61f;

        /// <summary>
        /// Where card <paramref name="index"/> of <paramref name="count"/> lies on a record's left page,
        /// in page UV (0..1 from the bottom-left corner). The card stands up from its bottom edge.
        /// </summary>
        public static Rect CardFootprint(int index, int count)
        {
            float height, spacing;
            if (count <= 1) { height = 0.48f; spacing = 0f; }
            else if (count == 2) { height = 0.38f; spacing = 0.37f; }
            else if (count == 3) { height = 0.33f; spacing = 0.30f; }
            else
            {
                spacing = 0.9f / count;
                height = spacing * 0.88f * PageWidth / (PageHeight * CardAspect);
            }
            float width = height * PageHeight * CardAspect / PageWidth;
            float center = 0.5f + (index - (count - 1) * 0.5f) * spacing;
            return new Rect(center - width * 0.5f, HingeV, width, height);
        }
    }

    /// <summary>
    /// The journal's artwork, laid out in uGUI and baked into textures by <see cref="JournalPageRenderer"/>:
    /// covers, the title page inside the front cover, the contents page, and a two-page spread per
    /// reading (the date, question and card slots on the left, the reading on the right). Coordinates
    /// are pixels from the page's top-left corner.
    /// </summary>
    static class JournalPages
    {
        public const int Width = 960;
        public const int Height = 1334;
        public const int CardWidth = 256;
        public const int CardHeight = 420;

        public static readonly Color Paper = new(1f, 0.984f, 0.958f, 1f);
        static readonly Color Mint = new(0.62f, 0.84f, 0.78f, 0.85f);
        static readonly Color Lavender = new(0.78f, 0.72f, 0.92f, 0.85f);
        static readonly Color Butter = new(0.98f, 0.86f, 0.55f, 0.85f);

        public static Color CoverColor(RoomUISkin skin) => Color.Lerp(skin.accent, Color.white, 0.22f);
        static Color BandColor(RoomUISkin skin) => Color.Lerp(skin.accent, skin.frame, 0.45f);
        static Color PinkTape(RoomUISkin skin) => new(skin.accent.r, skin.accent.g, skin.accent.b, 0.7f);

        // ------------------------------------------------------------------ pages

        /// <summary>Inside the front cover: the journal's name, a bunny and a summary.</summary>
        public static void Title(RectTransform page, RoomUISkin skin, IReadOnlyList<TarotRecord> records)
        {
            Background(page, skin, leftPage: true);
            Tape(page, Width * 0.5f, 96f, 320f, 60f, -4f, Mint);
            Label(page, skin, "占卜手帳", 104, skin.ink, TextAnchor.MiddleCenter, 0f, 180f, Width, 140f);
            Label(page, skin, "DIVINATION  DIARY", 30, skin.inkSoft, TextAnchor.MiddleCenter, 0f, 320f, Width, 44f);
            Bunny(page, skin, Width * 0.5f, 590f, 1.35f, skin.frame, Color.white);
            Label(page, skin, "這裡收著塔羅牌的占卜紀錄。", 34, skin.ink, TextAnchor.MiddleCenter, 0f, 800f, Width, 50f);
            if (records.Count > 0)
            {
                Label(page, skin, $"共 {records.Count} 則紀錄", 38, skin.accent, TextAnchor.MiddleCenter, 0f, 900f, Width, 56f);
                Label(page, skin, $"最近一次　{records[0].CastAtLocal:yyyy/MM/dd}", 30, skin.inkSoft, TextAnchor.MiddleCenter, 0f, 966f, Width, 44f);
            }
            else
            {
                Label(page, skin, "還沒有紀錄", 38, skin.accent, TextAnchor.MiddleCenter, 0f, 900f, Width, 56f);
            }
        }

        /// <summary>The contents: newest readings first, with the page each one starts on.</summary>
        public static void Index(RectTransform page, RoomUISkin skin, IReadOnlyList<TarotRecord> records)
        {
            Background(page, skin, leftPage: false);
            Tape(page, 150f, 92f, 200f, 50f, -8f, PinkTape(skin));
            Label(page, skin, "目錄", 64, skin.ink, TextAnchor.MiddleLeft, 100f, 64f, 300f, 90f);
            Label(page, skin, "CONTENTS", 26, skin.inkSoft, TextAnchor.MiddleLeft, 250f, 92f, 300f, 50f);
            Rule(page, skin, 172f);
            PageNumber(page, skin, 1, leftPage: false);

            if (records.Count == 0)
            {
                Bunny(page, skin, Width * 0.5f, 560f, 0.9f, skin.frame, Color.white);
                Label(page, skin, "還沒有占卜紀錄。\n使用塔羅牌占卜，\n就會收在這本手帳裡。", 36, skin.inkSoft, TextAnchor.UpperCenter, 0f, 720f, Width, 200f);
                return;
            }

            const int rows = 12;
            float y = 200f;
            for (int i = 0; i < records.Count && i < rows; i++)
            {
                var record = records[i];
                var time = record.CastAtLocal;
                Label(page, skin, time.ToString("MM/dd"), 30, skin.accent, TextAnchor.MiddleLeft, 100f, y, 120f, 40f);
                Label(page, skin, record.SpreadName, 28, skin.ink, TextAnchor.MiddleLeft, 220f, y, 480f, 40f);
                string when = $"（{Weekday(time.DayOfWeek)}）{time:HH:mm}";
                var (timeName, _) = TimeOfDay(record.timeOfDay);
                if (timeName != null)
                    when += "・" + timeName;
                Label(page, skin, when, 24, skin.inkSoft, TextAnchor.MiddleLeft, 220f, y + 36f, 560f, 34f);
                Label(page, skin, $"p.{RecordPage(i)}", 28, skin.inkSoft, TextAnchor.MiddleRight, Width - 260f, y + 10f, 160f, 40f);
                var line = Shape(page, JournalArt.Solid, new Color(skin.line.r, skin.line.g, skin.line.b, 0.55f));
                At(line.rectTransform, 100f, y + 78f, Width - 200f, 2f);
                y += 88f;
            }
            if (records.Count > rows)
                Label(page, skin, $"……以及更早的 {records.Count - rows} 則", 26, skin.inkSoft, TextAnchor.MiddleLeft, 100f, y + 6f, Width - 200f, 40f);
        }

        /// <summary>The page a reading starts on (its left page).</summary>
        public static int RecordPage(int recordIndex) => 2 + recordIndex * 2;

        /// <summary>
        /// A reading's left page: date, time and spread at the top (the question is not written down),
        /// and a slot per card below. With <paramref name="printCards"/> the cards lie printed in their
        /// slots (used while the page is turning); otherwise the slots stay empty for the pop-up cards.
        /// </summary>
        public static void RecordLeft(RectTransform page, RoomUISkin skin, TarotRecord record, TarotDeck deck, int pageNumber, bool printCards)
        {
            Background(page, skin, leftPage: true);
            Tape(page, 150f, 60f, 240f, 54f, -12f, PinkTape(skin));
            PageNumber(page, skin, pageNumber, leftPage: true);

            var time = record.CastAtLocal;
            var date = Label(page, skin, time.ToString("MM/dd"), 92, skin.ink, TextAnchor.LowerLeft, 90f, 70f, 320f, 120f);
            float after = 90f + date.preferredWidth + 18f;
            Label(page, skin, $"（{Weekday(time.DayOfWeek)}）{time:HH:mm}", 32, skin.inkSoft, TextAnchor.LowerLeft, after, 140f, 300f, 44f);

            var (timeName, timeColor) = TimeOfDay(record.timeOfDay);
            if (timeName != null)
            {
                var pill = Shape(page, JournalArt.Round, timeColor);
                At(pill.rectTransform, Width - 90f - 150f, 104f, 150f, 60f);
                Label(pill.rectTransform, skin, timeName, 30, skin.accentInk, TextAnchor.MiddleCenter, 0f, 0f, 150f, 58f);
            }

            var spread = Label(page, skin, record.SpreadName, 42, skin.accent, TextAnchor.MiddleLeft, 90f, 214f, Width - 180f, 60f);
            var tape = Tape(page, 90f + spread.preferredWidth * 0.5f, 262f, spread.preferredWidth + 36f, 22f, -1f, Mint);
            tape.transform.SetSiblingIndex(spread.transform.GetSiblingIndex());

            // Card slots, with each position and card written under its fold line.
            int count = record.draws.Count;
            for (int i = 0; i < count; i++)
            {
                var draw = record.draws[i];
                var card = deck?.Find(draw.cardId);
                var slot = ToPixels(JournalLayout.CardFootprint(i, count));
                if (printCards)
                {
                    var view = TarotCardView.Create("Card" + i, page, skin, slot.size);
                    PlainCard(view);
                    view.SetCard(card, draw.reversed, deck?.ArtFor(card));
                    view.SetFaceUp(true, instant: true);
                    At(view.Rect, slot.x, slot.y, slot.width, slot.height);
                }
                else
                {
                    var shadow = Shape(page, skin.shadow, new Color(0.32f, 0.18f, 0.2f, 0.32f));
                    At(shadow.rectTransform, slot.x - 6f, slot.y - 22f, slot.width + 12f, slot.height + 4f);
                    var outline = Shape(page, JournalArt.Outline, new Color(skin.line.r, skin.line.g, skin.line.b, 1f));
                    At(outline.rectTransform, slot.x, slot.y, slot.width, slot.height);
                }
                float labelY = slot.y + slot.height + 12f;
                Label(page, skin, draw.position, 32, skin.accent, TextAnchor.UpperCenter, slot.center.x - 150f, labelY, 300f, 42f);
                string name = card != null ? $"{card.name}・{(draw.reversed ? "逆位" : "正位")}" : "（找不到這張牌）";
                Label(page, skin, name, 26, skin.inkSoft, TextAnchor.UpperCenter, slot.center.x - 150f, labelY + 42f, 300f, 38f);
            }
        }

        /// <summary>
        /// A reading's right page: short card notes (position, card, keywords) and a memo area. The
        /// reading itself is spoken in the dialogue box, not written here.
        /// </summary>
        public static void RecordRight(RectTransform page, RoomUISkin skin, TarotRecord record, TarotDeck deck, int pageNumber)
        {
            Background(page, skin, leftPage: false);
            Tape(page, Width - 170f, 66f, 230f, 52f, 9f, Lavender);
            var title = Label(page, skin, "牌面筆記", 60, skin.ink, TextAnchor.MiddleLeft, 100f, 64f, 400f, 90f);
            Label(page, skin, record.CastAtLocal.ToString("MM/dd HH:mm"), 28, skin.inkSoft, TextAnchor.MiddleLeft, 100f + title.preferredWidth + 24f, 92f, 300f, 50f);
            Rule(page, skin, 172f);
            PageNumber(page, skin, pageNumber, leftPage: false);

            float y = 214f;
            float step = record.draws.Count <= 1 ? 170f : 148f;
            foreach (var draw in record.draws)
            {
                var card = deck?.Find(draw.cardId);
                var tag = Shape(page, JournalArt.Round, skin.accent);
                At(tag.rectTransform, 100f, y + 4f, 132f, 52f);
                Label(tag.rectTransform, skin, draw.position, 28, skin.accentInk, TextAnchor.MiddleCenter, 0f, 0f, 132f, 50f);
                string name = card != null ? $"{card.name}・{(draw.reversed ? "逆位" : "正位")}" : "（找不到這張牌）";
                Label(page, skin, name, 40, skin.ink, TextAnchor.MiddleLeft, 256f, y, Width - 356f, 60f);
                if (card != null)
                    Label(page, skin, card.Meaning(draw.reversed).keywords, 28, skin.inkSoft, TextAnchor.MiddleLeft, 256f, y + 62f, Width - 356f, 44f);
                y += step;
            }

            // A little memo area, like the ruled box at the bottom of a planner page.
            float memo = Mathf.Max(y + 30f, 760f);
            Label(page, skin, "MEMO", 26, skin.accent, TextAnchor.MiddleLeft, 100f, memo, 200f, 40f);
            for (int i = 1; i <= 4; i++)
            {
                var line = Shape(page, JournalArt.Solid, new Color(skin.line.r, skin.line.g, skin.line.b, 0.7f));
                At(line.rectTransform, 100f, memo + 20f + i * 64f, Width - 200f, 2f);
            }
            Bunny(page, skin, Width - 190f, Height - 190f, 0.5f, skin.frame, Color.white);
            Label(page, skin, "牌想說的話，都收在這裡了。", 26, skin.inkSoft, TextAnchor.MiddleLeft, 100f, Height - 200f, 600f, 40f);
        }

        // ------------------------------------------------------------------ covers and cards

        /// <summary>The outside of a cover: stitched pink leather with an elastic band; the front has the label and a bunny.</summary>
        public static void Cover(RectTransform page, RoomUISkin skin, bool front)
        {
            var stitch = Shape(page, JournalArt.Outline, new Color(1f, 1f, 1f, 0.5f));
            At(stitch.rectTransform, 34f, 34f, Width - 68f, Height - 68f);
            var band = Shape(page, JournalArt.Solid, BandColor(skin));
            At(band.rectTransform, front ? Width - 170f : 122f, 0f, 48f, Height);

            if (!front)
            {
                var tail = Shape(page, JournalArt.Round, Color.white);
                At(tail.rectTransform, Width * 0.5f + 20f, Height * 0.5f - 50f, 100f, 100f);
                return;
            }
            const float cx = 415f;
            var plate = Shape(page, JournalArt.Round, Paper);
            At(plate.rectTransform, cx - 280f, 300f, 560f, 230f);
            var inner = Shape(page, JournalArt.Outline, new Color(skin.accent.r, skin.accent.g, skin.accent.b, 0.55f));
            At(inner.rectTransform, cx - 268f, 312f, 536f, 206f);
            Label(page, skin, "占卜手帳", 92, skin.ink, TextAnchor.MiddleCenter, cx - 280f, 318f, 560f, 120f);
            Label(page, skin, "TAROT  DIARY", 28, skin.inkSoft, TextAnchor.MiddleCenter, cx - 280f, 440f, 560f, 50f);
            Bunny(page, skin, cx, 860f, 1.7f, skin.frame, Color.white);
            foreach (var (x, y, size) in new[] { (150f, 640f, 18f), (690f, 610f, 14f), (640f, 1120f, 20f), (190f, 1080f, 12f), (520f, 700f, 10f) })
            {
                var star = Shape(page, JournalArt.Round, new Color(1f, 1f, 1f, 0.8f));
                At(star.rectTransform, x - size * 0.5f, y - size * 0.5f, size, size);
            }
        }

        /// <summary>A card face filling the whole texture (for the pop-up cards).</summary>
        public static void CardFace(RectTransform page, RoomUISkin skin, TarotCard card, bool reversed, Sprite art)
        {
            var view = TarotCardView.Create("Card", page, skin, new Vector2(CardWidth, CardHeight));
            PlainCard(view);
            view.SetCard(card, reversed, art);
            view.SetFaceUp(true, instant: true);
            view.Rect.anchorMin = view.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            view.Rect.anchoredPosition = Vector2.zero;
        }

        public static void CardBack(RectTransform page, RoomUISkin skin)
        {
            var back = UIFactory.Image("Back", page, skin.cardBack, Color.white);
            back.type = Image.Type.Simple;
            back.rectTransform.Stretch();
        }

        /// <summary>Drops the hover glow and drop shadow a card view brings along; only the card itself is wanted.</summary>
        static void PlainCard(TarotCardView view)
        {
            foreach (var part in new[] { "Glow", "Shadow" })
            {
                var child = view.transform.Find(part);
                if (child != null)
                    child.gameObject.SetActive(false);
            }
            view.enabled = false;
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>Dot grid, and the shade where the page curves into the spine.</summary>
        static void Background(RectTransform page, RoomUISkin skin, bool leftPage)
        {
            var dots = UIFactory.Rect("Dots", page);
            At(dots, 54f, 54f, Width - 108f, Height - 108f);
            var dotImage = dots.gameObject.AddComponent<RawImage>();
            dotImage.texture = JournalArt.Dots;
            dotImage.color = new Color(skin.line.r, skin.line.g, skin.line.b, 0.95f);
            dotImage.uvRect = new Rect(0f, 0f, (Width - 108f) / 36f, (Height - 108f) / 36f);
            dotImage.raycastTarget = false;

            var gutter = UIFactory.Rect("Gutter", page);
            At(gutter, leftPage ? Width - 110f : 0f, 0f, 110f, Height);
            var gutterImage = gutter.gameObject.AddComponent<RawImage>();
            gutterImage.texture = JournalArt.Gutter;
            gutterImage.color = new Color(0.36f, 0.2f, 0.18f, 0.26f);
            gutterImage.uvRect = leftPage ? new Rect(0f, 0f, 1f, 1f) : new Rect(1f, 0f, -1f, 1f);
            gutterImage.raycastTarget = false;
        }

        static RawImage Tape(RectTransform page, float centerX, float centerY, float width, float height, float angle, Color color)
        {
            var rt = UIFactory.Rect("Tape", page);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(centerX, -centerY);
            rt.sizeDelta = new Vector2(width, height);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            var image = rt.gameObject.AddComponent<RawImage>();
            image.texture = JournalArt.Stripes;
            image.color = color;
            image.uvRect = new Rect(0f, 0f, width / 32f, height / 32f);
            image.raycastTarget = false;
            return image;
        }

        static void Rule(RectTransform page, RoomUISkin skin, float y)
        {
            var line = Shape(page, JournalArt.Solid, skin.line);
            At(line.rectTransform, 100f, y, Width - 200f, 3f);
        }

        static void PageNumber(RectTransform page, RoomUISkin skin, int number, bool leftPage)
        {
            Label(page, skin, number.ToString(), 26, skin.inkSoft, leftPage ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight,
                leftPage ? 60f : Width - 160f, Height - 70f, 100f, 40f);
        }

        /// <summary>A little round bunny face drawn from rounded shapes, outlined in <paramref name="line"/>.</summary>
        static void Bunny(RectTransform page, RoomUISkin skin, float cx, float cy, float s, Color line, Color fill)
        {
            void Blob(float x, float y, float w, float h, float angle, Color color)
            {
                var image = Shape(page, JournalArt.Round, color);
                var rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(cx + x * s, -(cy + y * s));
                rt.sizeDelta = new Vector2(w * s, h * s);
                rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            const float o = 7f;
            Blob(-30f, -78f, 40f + o, 104f + o, 10f, line);
            Blob(30f, -78f, 40f + o, 104f + o, -10f, line);
            Blob(-30f, -78f, 40f, 104f, 10f, fill);
            Blob(30f, -78f, 40f, 104f, -10f, fill);
            Blob(-30f, -80f, 16f, 66f, 10f, new Color(skin.accent.r, skin.accent.g, skin.accent.b, 0.75f));
            Blob(30f, -80f, 16f, 66f, -10f, new Color(skin.accent.r, skin.accent.g, skin.accent.b, 0.75f));
            Blob(0f, 0f, 160f + o, 132f + o, 0f, line);
            Blob(0f, 0f, 160f, 132f, 0f, fill);
            Blob(-32f, -6f, 13f, 16f, 0f, skin.ink);
            Blob(32f, -6f, 13f, 16f, 0f, skin.ink);
            Blob(-54f, 18f, 28f, 14f, 0f, new Color(skin.accent.r, skin.accent.g, skin.accent.b, 0.45f));
            Blob(54f, 18f, 28f, 14f, 0f, new Color(skin.accent.r, skin.accent.g, skin.accent.b, 0.45f));
            Blob(0f, 10f, 12f, 8f, 0f, skin.accent);
        }

        static Image Shape(Transform parent, Sprite sprite, Color color)
        {
            var image = UIFactory.Image("Shape", parent, sprite, color);
            if (sprite != null && sprite.border.sqrMagnitude > 0f)
                image.type = Image.Type.Sliced;
            return image;
        }

        static Text Label(Transform parent, RoomUISkin skin, string value, int size, Color color, TextAnchor anchor, float x, float y, float width, float height)
        {
            var text = UIFactory.Text("Text", parent, skin, size, color, anchor, value);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            At(text.rectTransform, x, y, width, height);
            return text;
        }

        static RectTransform At(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        /// <summary>Page UV rect (origin bottom-left) → pixel rect from the top-left corner.</summary>
        static Rect ToPixels(Rect uv) => new(uv.x * Width, (1f - uv.yMax) * Height, uv.width * Width, uv.height * Height);

        static string Weekday(System.DayOfWeek day) => "日一二三四五六".Substring((int)day, 1);

        static (string, Color) TimeOfDay(string key) => key switch
        {
            "morning" => ("早晨", new Color(0.95f, 0.72f, 0.38f)),
            "sunset" => ("黃昏", new Color(0.9f, 0.55f, 0.5f)),
            "night" => ("夜晚", new Color(0.55f, 0.52f, 0.78f)),
            _ => (null, Color.clear),
        };
    }
}
