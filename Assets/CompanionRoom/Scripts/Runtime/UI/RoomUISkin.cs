using UnityEngine;

namespace CompanionRoom.UI
{
    /// <summary>
    /// Look of every window in the room (dialogue, photos, settings, tarot, records). Any sprite left
    /// empty is drawn procedurally from the colors below, so official art can be dropped in one slot
    /// at a time. Nine-slice sprites need their borders set in the Sprite Editor.
    /// </summary>
    [CreateAssetMenu(menuName = "兔兔房間/UI Skin", fileName = "RoomUISkin")]
    public class RoomUISkin : ScriptableObject
    {
        [Header("字型（空白 = Resources/CompanionRoom/Fonts/RoomHandwriting，再退回系統字型）")]
        public Font font;

        [Header("配色")]
        public Color paper = Hex("#FBF3EC");
        public Color frame = Hex("#8A5F5A");
        public Color line = Hex("#E2C3B5");
        public Color ink = Hex("#5A4542");
        public Color inkSoft = Hex("#9A7F7A");
        public Color accent = Hex("#D9798A");
        public Color accentInk = Hex("#FFF8F4");
        public Color backdrop = new(0.16f, 0.1f, 0.12f, 0.55f);
        public Color highlight = Hex("#FFD9A8");

        [Header("立繪（空白 = 巫然立繪，再退回即時渲染 3D 角色）")]
        public Sprite portrait;
        [Tooltip("立繪在畫面上的大小（1080p 參考解析度）。")]
        public Vector2 portraitSize = new(600f, 720f);
        [Tooltip("立繪從左邊算起露出的寬度比例，右邊多出來的裁掉（只用於立繪圖片）。")]
        [Range(0.5f, 1f)] public float portraitWidthShown = 0.84f;

        [Header("九宮格底圖（空白 = 程式產生）")]
        public Sprite dialogueBox;
        public Sprite namePlate;
        public Sprite panel;
        public Sprite titlePlate;
        public Sprite button;
        public Sprite accentButton;
        public Sprite roundButton;
        public Sprite shadow;
        public Sprite sliderTrack;
        public Sprite sliderFill;
        public Sprite sliderKnob;
        public Sprite checkBox;
        public Sprite checkMark;

        [Header("塔羅牌（空白 = 程式產生；牌面圖放 Resources/CompanionRoom/Tarot/Cards/<id>.png）")]
        public Sprite cardBack;
        public Sprite cardFace;

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        /// <summary>
        /// A runtime copy with every empty slot filled by a procedural sprite. The asset itself is
        /// never changed, so nothing generated can end up saved into it.
        /// </summary>
        public RoomUISkin Resolve()
        {
            var s = Instantiate(this);
            s.name = name + " (runtime)";
            s.hideFlags = HideFlags.DontSave;
            if (s.portrait == null)
                s.portrait = Resources.Load<Sprite>("CompanionRoom/Portraits/Wuran");
            if (s.font == null)
                s.font = Resources.Load<Font>("CompanionRoom/Fonts/RoomHandwriting");
            if (s.font == null)
                s.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei UI", "Microsoft JhengHei", "Noto Sans TC", "PingFang TC", "Arial" }, 32);

            var plateLine = Color.Lerp(accent, Color.white, 0.45f);
            s.dialogueBox = Or(s.dialogueBox, () => UISprites.Panel(128, 26f, 5f, paper, frame, 9f, line, 2f));
            s.namePlate = Or(s.namePlate, () => UISprites.Panel(96, 22f, 4f, accent, frame, 6f, plateLine, 1.5f));
            s.panel = Or(s.panel, () => UISprites.Panel(128, 28f, 5f, paper, frame, 10f, line, 2f));
            s.titlePlate = Or(s.titlePlate, () => UISprites.Panel(96, 22f, 4f, accent, frame, 6f, plateLine, 1.5f));
            s.button = Or(s.button, () => UISprites.Panel(96, 30f, 3.5f, paper, frame, 0f, Color.clear, 0f));
            s.accentButton = Or(s.accentButton, () => UISprites.Panel(96, 30f, 3.5f, accent, frame, 0f, Color.clear, 0f));
            s.roundButton = Or(s.roundButton, () => UISprites.Panel(96, 47f, 3.5f, paper, frame, 0f, Color.clear, 0f));
            s.shadow = Or(s.shadow, () => UISprites.Shadow(128, 34f, 18f));
            s.sliderTrack = Or(s.sliderTrack, () => UISprites.Panel(48, 11f, 2f, Color.Lerp(line, paper, 0.4f), Color.Lerp(frame, line, 0.5f), 0f, Color.clear, 0f));
            s.sliderFill = Or(s.sliderFill, () => UISprites.Panel(48, 11f, 0f, accent, accent, 0f, Color.clear, 0f));
            s.sliderKnob = Or(s.sliderKnob, () => UISprites.Panel(64, 31f, 3f, paper, frame, 0f, Color.clear, 0f));
            s.checkBox = Or(s.checkBox, () => UISprites.Panel(64, 14f, 3f, paper, frame, 0f, Color.clear, 0f));
            s.checkMark = Or(s.checkMark, () => UISprites.Panel(64, 10f, 0f, accent, accent, 0f, Color.clear, 0f));
            var backTexture = Resources.Load<Texture2D>("CompanionRoom/Tarot/Art/card_back");
            s.cardBack = backTexture != null
                ? Sprite.Create(backTexture, new Rect(0, 0, backTexture.width, backTexture.height), new Vector2(0.5f, 0.5f), 100f)
                : Or(s.cardBack, () => UISprites.CardBack(256, 420, Hex("#E7A3A2"), Hex("#FFF2DB"), Hex("#D68F91")));
            s.cardFace = Or(s.cardFace, () => UISprites.Panel(128, 18f, 4f, Hex("#FFF9F2"), Hex("#C9A46A"), 9f, Hex("#E9D5B0"), 2f));
            return s;
        }

        static Sprite Or(Sprite assigned, System.Func<Sprite> make) => assigned != null ? assigned : make();

        /// <summary>A resolved default skin for scenes that do not assign one.</summary>
        public static RoomUISkin CreateDefault()
        {
            var skin = CreateInstance<RoomUISkin>();
            skin.name = "RoomUISkin";
            var resolved = skin.Resolve();
            Destroy(skin);
            return resolved;
        }
    }
}
