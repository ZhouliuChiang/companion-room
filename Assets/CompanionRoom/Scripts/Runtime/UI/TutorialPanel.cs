using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CompanionRoom.UI
{
    /// <summary>
    /// The first-run guide, one piece of furniture at a time: the item a step is about lights up
    /// (outline and a pulsing frame) and is the only thing in the room that can be clicked; once the
    /// player has tried it and its interaction is over, the next step follows. Steps without an item
    /// just explain and wait for 「下一步」. While an item is in use the card steps aside.
    /// Runs once on the first launch (remembered in <see cref="RoomSettings"/>) and can be replayed
    /// from the settings. Right / Enter go on, Left goes back, Esc skips the whole guide.
    /// </summary>
    public class TutorialPanel : RoomPanel
    {
        const string DoneKey = "TutorialDone";
        const float CardMargin = 48f;

        /// <summary>The guide has been finished or skipped before.</summary>
        public static bool Done => RoomSettings.GetInt(DoneKey, 0) == 1;

        struct Step
        {
            public string title;
            public string body;
            public Type target;
        }

        readonly List<Step> steps = new();
        int index;
        RoomItem highlighted;
        bool wasIdle;

        CanvasGroup content;
        RectTransform cardRect;
        Text titleText;
        Text bodyText;
        Text counterText;
        Text nextLabel;
        Button back;
        RectTransform ring;
        RectTransform canvasRect;
        Camera roomCamera;

        Type Target => IsOpen && index < steps.Count ? steps[index].target : null;

        public static TutorialPanel Build(Transform parent, CompanionRoomApp app, RoomUISkin skin)
        {
            var root = UIFactory.Rect("Tutorial", parent).Stretch();
            var panel = root.gameObject.AddComponent<TutorialPanel>();
            panel.canvasRect = (RectTransform)parent;
            var contentRect = UIFactory.Rect("Content", root).Stretch();
            panel.content = contentRect.gameObject.AddComponent<CanvasGroup>();

            // A soft frame drawn around the item the current step is about.
            var ringSprite = UISprites.Panel(96, 30f, 5f, new Color(1f, 1f, 1f, 0f), Color.white, 0f, Color.clear, 0f);
            var ringImage = UIFactory.Image("Ring", contentRect, ringSprite, skin.accent);
            panel.ring = ringImage.rectTransform;
            panel.ring.Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));

            var card = UIFactory.Panel("Card", contentRect, skin);
            card.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, CardMargin), new Vector2(960f, 300f));
            panel.cardRect = card;

            var title = UIFactory.Panel("Title", card, skin, skin.titlePlate, withShadow: false);
            title.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 60f));
            panel.titleText = UIFactory.Text("Label", title, skin, 30, skin.accentInk, TextAnchor.MiddleCenter);
            panel.titleText.rectTransform.Stretch(12f, 0f, 12f, 3f);
            panel.titleText.horizontalOverflow = HorizontalWrapMode.Overflow;

            panel.counterText = UIFactory.Text("Counter", card, skin, 24, skin.inkSoft, TextAnchor.MiddleRight);
            panel.counterText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -16f), new Vector2(160f, 36f));

            panel.bodyText = UIFactory.Text("Body", card, skin, 28, skin.ink, TextAnchor.UpperLeft);
            panel.bodyText.rectTransform.Stretch(48f, 56f, 48f, 104f);

            var skip = UIFactory.Button("Skip", card, skin, "跳過教學", new Vector2(220f, 64f), panel.Hide, fontSize: 26);
            ((RectTransform)skip.transform).Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 28f), new Vector2(220f, 64f));
            panel.back = UIFactory.Button("Back", card, skin, "上一步", new Vector2(200f, 64f), panel.Back, fontSize: 26);
            ((RectTransform)panel.back.transform).Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-256f, 28f), new Vector2(200f, 64f));
            var next = UIFactory.Button("Next", card, skin, "下一步", new Vector2(200f, 64f), panel.Next, accent: true, fontSize: 26);
            ((RectTransform)next.transform).Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 28f), new Vector2(200f, 64f));
            panel.nextLabel = next.GetComponentInChildren<Text>();

            panel.InitPanel(app, skin, card);
            if (app != null)
                app.InteractionEnded += panel.OnInteractionEnded;
            return panel;
        }

        /// <summary>Starts the guide from the first step (again, if it is already running).</summary>
        public void Begin()
        {
            if (IsOpen)
                OnShown();
            else
                Show();
        }

        /// <summary>While the guide runs, only the item of the current step reacts to clicks.</summary>
        public bool Allows(RoomItem item)
        {
            if (!IsOpen)
                return true;
            var target = Target;
            return item != null && target != null && target.IsInstanceOfType(item);
        }

        protected override void OnShown()
        {
            BuildSteps();
            roomCamera = Camera.main;
            index = 0;
            wasIdle = false;
            content.alpha = 1f;
            ShowStep();
        }

        protected override void OnHidden()
        {
            Highlight(null);
            RoomSettings.SetInt(DoneKey, 1);
        }

        void OnDestroy()
        {
            if (App != null)
                App.InteractionEnded -= OnInteractionEnded;
        }

        // The player has tried the highlighted item: on to the next step.
        void OnInteractionEnded(RoomItem item)
        {
            if (IsOpen && item != null && Allows(item))
                Next();
        }

        // Steps about furniture only appear when that piece is in the room.
        void BuildSteps()
        {
            string name = App != null && App.Lines != null && !string.IsNullOrEmpty(App.Lines.Speaker) ? App.Lines.Speaker : "她";
            steps.Clear();
            void Add(string title, string body, Type target = null)
            {
                if (target == null || Find(target) != null)
                    steps.Add(new Step { title = title, body = body, target = target });
            }

            Add("歡迎來到小房間", $"這裡是{name}的房間，她會一直待在你的桌面上陪著你。\n接下來會一樣一樣點亮房間裡的東西，你親手試一次就會了。隨時都可以按「跳過教學」。");
            Add("燈光", "先從燈光開始吧！\n點一下發亮的壁燈，把燈打開或關上試試看。", typeof(WallLampItem));
            Add("窗戶", "接著點一下窗戶，房間的時間會在早晨 → 夕陽 → 夜晚之間切換。", typeof(WindowItem));
            Add("電腦與設定", "點一下電腦打開設定，可以調整音量、文字速度、是否顯示在最上層，\n想再看一次這份教學也在這裡。看完按「完成」關掉就好。", typeof(ComputerItem));
            Add("靈擺", $"點一下靈擺，在心裡想著你的問題，跟著{name}的引導試一次。", typeof(PendulumItem));
            Add("塔羅占卜", "點一下桌上的塔羅牌，抽一次牌看看。", typeof(TarotDeckItem));
            Add("占卜紀錄", "剛剛的占卜都會記在手帳裡。\n點一下書櫃，把占卜手帳拿出來翻翻看。", typeof(RecordCabinetItem));
            Add("相框", "點一下相框，可以翻看相簿裡的照片。", typeof(PhotoFrameItem));
            Add("和她聊天", $"最後點一下{name}本人，她會轉過來和你說說話。\n對話出現時，點一下對話框就能看下一句。", typeof(CompanionTalkItem));
            if (FindAnyObjectByType<WitchLocomotion>() != null)
                Add("在房間裡走走", $"點一下房間裡空著的地板，{name}就會走到那個位置。");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            Add("擺放房間", "拖曳房間的牆緣，可以把整個房間移到桌面的任何位置；\n拖曳房間外側的角，可以放大或縮小房間。位置和大小都會被記住。");
#endif
            Add("準備好了", $"互動進行中隨時可以按 Esc 立刻結束。\n就這樣！好好享受和{name}在一起的時光吧。");
        }

        static RoomItem Find(Type type)
        {
            foreach (var item in RoomItem.All)
                if (item != null && type.IsInstanceOfType(item))
                    return item;
            return null;
        }

        void ShowStep()
        {
            var step = steps[index];
            titleText.text = step.title;
            bodyText.text = step.body;
            counterText.text = $"{index + 1} / {steps.Count}";
            back.interactable = index > 0;
            nextLabel.text = index == steps.Count - 1 ? "開始吧" : step.target != null ? "略過這步" : "下一步";
            Highlight(step.target != null ? Find(step.target) : null);
            PlaceRing();
        }

        /// <summary>Goes on to the next step (or finishes on the last one).</summary>
        public void Next()
        {
            App?.Audio?.Play(RoomSound.Click, 0.6f);
            if (index >= steps.Count - 1)
            {
                Hide();
                return;
            }
            index++;
            ShowStep();
        }

        void Back()
        {
            if (index <= 0)
                return;
            App?.Audio?.Play(RoomSound.Click, 0.6f);
            index--;
            ShowStep();
        }

        void Highlight(RoomItem item)
        {
            if (highlighted == item)
                return;
            if (highlighted != null)
                highlighted.SetHovered(false);
            highlighted = item;
            if (highlighted != null)
                highlighted.SetHovered(true);
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen)
                return;

            // While the player tries an item, the card and the frame make way for its windows.
            bool idle = App == null || !App.IsBusy;
            content.alpha = Mathf.MoveTowards(content.alpha, idle ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);
            content.blocksRaycasts = content.interactable = idle;
            if (idle)
            {
                var target = Target;
                Highlight(target != null ? Find(target) : null);
                // Hovering in and out of the item would clear the outline; keep it lit.
                if (highlighted != null)
                    highlighted.SetHovered(true);
            }
            else
                Highlight(null);

            // Keys only count once the room has been idle for a frame, so the Esc that ends an
            // item's interaction doesn't skip the whole guide too.
            var keyboard = Keyboard.current;
            if (idle && wasIdle && keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                    Hide();
                else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    Next();
                else if (keyboard.leftArrowKey.wasPressedThisFrame)
                    Back();
            }
            wasIdle = idle;

            // Runs before the camera rig, while the camera still sits where the room was last drawn.
            PlaceRing();
        }

        // Frames the highlighted item's model on screen, pulsing gently, and moves the card to the
        // top of the screen when the item sits in the lower half so it is never covered.
        void PlaceRing()
        {
            if (highlighted == null || roomCamera == null || !TryScreenRect(highlighted.ModelBounds, out var localMin, out var localMax))
            {
                ring.gameObject.SetActive(false);
                PlaceCard(top: false);
                return;
            }
            ring.gameObject.SetActive(true);
            float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4f);
            ring.sizeDelta = Vector2.Max(localMax - localMin + new Vector2(36f, 36f), new Vector2(72f, 72f)) * pulse;
            // The ring is anchored at the canvas's lower left corner.
            var centre = (localMin + localMax) * 0.5f;
            ring.anchoredPosition = centre - canvasRect.rect.min;
            PlaceCard(top: centre.y < 0f);
        }

        void PlaceCard(bool top)
        {
            var anchor = new Vector2(0.5f, top ? 1f : 0f);
            if (cardRect.anchorMin == anchor)
                return;
            cardRect.Place(anchor, anchor, new Vector2(0f, top ? -CardMargin - 30f : CardMargin), cardRect.sizeDelta);
        }

        // The model's bounds on screen, in the canvas's local space.
        bool TryScreenRect(Bounds bounds, out Vector2 localMin, out Vector2 localMax)
        {
            localMin = localMax = Vector2.zero;
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                var screen = roomCamera.WorldToScreenPoint(corner);
                if (screen.z <= 0f)
                    continue;
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            return min.x <= max.x
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, min, null, out localMin)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, max, null, out localMax);
        }
    }
}
