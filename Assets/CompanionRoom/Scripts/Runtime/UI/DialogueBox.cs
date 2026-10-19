using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CompanionRoom.UI
{
    /// <summary>
    /// 凡安伴生-style dialogue: her portrait stands at the right behind a paper box with a name plate,
    /// each line is typed out (with small pauses at punctuation), a ▼ blinks once it is complete.
    /// Questions are asked RPG-style: the answers stack at the box's lower right on dashed cards, with
    /// a ▶ cursor on the selected one. A click anywhere, Space or Enter finishes the line or moves on;
    /// while choosing, ↑↓ move the cursor, Space/Enter confirm, number keys pick directly.
    /// Interactions drive it with <c>yield return Say(...)</c> / <c>Ask(...)</c>.
    /// </summary>
    public class DialogueBox : MonoBehaviour
    {
        const float BoxWidth = 1320f;
        const float BoxHeight = 236f;
        const float BoxBottom = 34f;
        const float ChoiceWidth = 380f;
        const float ChoiceHeight = 58f;
        const float ChoiceMargin = 44f;   // from the box's right and bottom edges
        const float PortraitFade = 60f;   // soft bottom edge of the still portrait
        const float PortraitSideFade = 30f;   // soft right edge where the still is cut off

        sealed class Choice
        {
            public Button button;
            public Image highlight;
            public Text cursor;
            public Text label;
        }

        CompanionRoomApp app;
        RoomUISkin skin;
        CanvasGroup group;
        Image catcher;
        RectTransform box;
        RectTransform portraitRect;
        PortraitView portrait;
        RectTransform namePlate;
        Text nameText;
        Text lineText;
        Text nextIndicator;
        RectTransform choiceRoot;
        Sprite choiceCard;
        Sprite choiceGlow;
        readonly List<Choice> choiceButtons = new();
        int selected;

        string line = "";
        float typed;
        float pause;
        float shown;
        bool open;
        bool waiting;
        bool advanced;
        int choice = -1;
        int choiceCount;
        Vector2 portraitHome;
        Vector2 boxHome;

        public event Action<string> LineStarted;
        public event Action Opened;
        public event Action Closed;

        public bool IsOpen => open;
        public bool IsTyping => typed < line.Length;
        public bool IsChoosing => choiceCount > 0;
        public bool IsWaiting => waiting;
        public string Line => line;
        public int ChoiceCount => choiceCount;
        public bool PortraitEnabled { get; set; } = true;
        /// <summary>The name on the plate; an empty name hides the plate.</summary>
        public string SpeakerName
        {
            get => nameText.text;
            set
            {
                nameText.text = value ?? "";
                namePlate.gameObject.SetActive(!string.IsNullOrEmpty(value));
            }
        }

        public static DialogueBox Build(Transform parent, CompanionRoomApp app, RoomUISkin skin, string speaker)
        {
            var root = UIFactory.Rect("Dialogue", parent).Stretch();
            var dialogue = root.gameObject.AddComponent<DialogueBox>();
            dialogue.app = app;
            dialogue.skin = skin;
            dialogue.group = root.gameObject.AddComponent<CanvasGroup>();

            dialogue.catcher = UIFactory.Image("ClickCatcher", root, null, new Color(0f, 0f, 0f, 0f), true);
            dialogue.catcher.rectTransform.Stretch();
            dialogue.catcher.gameObject.AddComponent<ClickRelay>().Clicked = dialogue.Advance;

            // Portrait first so the box overlaps her lower half.
            // The still's mask is wider/taller by the fades so its soft left and top edges fall on empty space,
            // and it stops at portraitWidthShown so her skirt's right side is cut off (softly) on the right.
            var portraitSize = skin.portraitSize;
            var portraitPosition = new Vector2(-150f, BoxBottom + BoxHeight - 20f);
            if (skin.portrait != null)
            {
                float shown = skin.portraitSize.x * skin.portraitWidthShown;
                portraitSize = new Vector2(PortraitSideFade + shown, skin.portraitSize.y + PortraitFade);
                portraitPosition.x -= skin.portraitSize.x - shown;
            }
            dialogue.portraitRect = UIFactory.Rect("Portrait", root);
            dialogue.portraitRect.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), portraitPosition, portraitSize);
            var mask = dialogue.portraitRect.gameObject.AddComponent<RectMask2D>();
            var liveImage = dialogue.portraitRect.gameObject.AddComponent<RawImage>();
            liveImage.raycastTarget = false;
            var stillImage = UIFactory.Image("Still", dialogue.portraitRect, null, Color.white);
            stillImage.rectTransform.Stretch();
            if (skin.portrait != null)
            {
                // Keep the original art intact; frame its upper body and fade it out at the waist, so the
                // cut never shows as a hard line where she sticks out past the box (slide-in, wide screens).
                mask.softness = new Vector2Int(Mathf.RoundToInt(PortraitSideFade), Mathf.RoundToInt(PortraitFade));
                float fullHeight = skin.portraitSize.x * skin.portrait.rect.height / skin.portrait.rect.width;
                stillImage.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(PortraitSideFade, -PortraitFade),
                    new Vector2(skin.portraitSize.x, fullHeight));
            }
            dialogue.portrait = dialogue.portraitRect.gameObject.AddComponent<PortraitView>();
            dialogue.portrait.Init(liveImage, stillImage, skin.portrait);
            dialogue.portraitHome = dialogue.portraitRect.anchoredPosition;

            dialogue.box = UIFactory.Panel("Box", root, skin, skin.dialogueBox);
            // The panel body receives raycasts before the full-screen catcher behind it.
            // Bubble clicks from its paper/name plate to the same advance action.
            dialogue.box.gameObject.AddComponent<ClickRelay>().Clicked = dialogue.Advance;
            dialogue.box.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, BoxBottom), new Vector2(BoxWidth, BoxHeight));
            dialogue.boxHome = dialogue.box.anchoredPosition;

            dialogue.namePlate = UIFactory.Panel("NamePlate", dialogue.box, skin, skin.namePlate, withShadow: false);
            dialogue.namePlate.Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(54f, 2f), new Vector2(190f, 58f));
            dialogue.nameText = UIFactory.Text("Name", dialogue.namePlate, skin, 32, skin.accentInk, TextAnchor.MiddleCenter);
            dialogue.nameText.rectTransform.Stretch(0f, 0f, 0f, 3f);
            dialogue.SpeakerName = speaker;

            dialogue.lineText = UIFactory.Text("Line", dialogue.box, skin, 36, skin.ink);
            dialogue.lineText.rectTransform.Stretch(70f, 54f, 90f, 30f);
            dialogue.lineText.lineSpacing = 1.25f;
            dialogue.lineText.supportRichText = false;
            dialogue.lineText.horizontalOverflow = HorizontalWrapMode.Overflow;

            dialogue.nextIndicator = UIFactory.Text("Next", dialogue.box, skin, 30, skin.accent, TextAnchor.MiddleCenter, "▼");
            dialogue.nextIndicator.rectTransform.Place(new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-56f, 40f), new Vector2(40f, 40f));

            // Answers live in the box's lower right, so they slide in and out with it.
            dialogue.choiceRoot = UIFactory.Rect("Choices", dialogue.box);
            dialogue.choiceRoot.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-ChoiceMargin, ChoiceMargin - 14f), new Vector2(ChoiceWidth, 10f));
            dialogue.choiceCard = UISprites.DashedPanel(new Color(skin.paper.r, skin.paper.g, skin.paper.b, 0.92f), Color.Lerp(skin.frame, skin.accent, 0.35f));
            dialogue.choiceGlow = UISprites.Panel(48, 10f, 0f, Color.white, Color.white, 0f, Color.clear, 0f);
            var layout = dialogue.choiceRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.LowerRight;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            dialogue.choiceRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            dialogue.HideNow();
            return dialogue;
        }

        /// <summary>An answer: a dashed card with the text, a soft fill and a ▶ cursor while selected.</summary>
        Choice ChoiceButton(int index)
        {
            while (choiceButtons.Count <= index)
            {
                int i = choiceButtons.Count;
                var card = UIFactory.Image("Choice" + i, choiceRoot, choiceCard, Color.white, true);
                card.type = Image.Type.Tiled;
                card.rectTransform.sizeDelta = new Vector2(ChoiceWidth, ChoiceHeight);
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = card;
                button.transition = Selectable.Transition.None;
                var nav = button.navigation;
                nav.mode = Navigation.Mode.None;
                button.navigation = nav;
                button.onClick.AddListener(() => Pick(i));
                card.gameObject.AddComponent<HoverRelay>().Entered = () => selected = i;

                var highlight = UIFactory.Image("Highlight", card.transform, choiceGlow, new Color(skin.accent.r, skin.accent.g, skin.accent.b, 0.2f));
                highlight.rectTransform.Stretch(6f, 6f, 6f, 6f);
                var cursor = UIFactory.Text("Cursor", card.transform, skin, 24, skin.accent, TextAnchor.MiddleCenter, "▶");
                cursor.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 1f), new Vector2(30f, 40f));
                var label = UIFactory.Text("Label", card.transform, skin, 30, skin.ink, TextAnchor.MiddleLeft);
                label.rectTransform.Stretch(54f, 2f, 18f, 5f);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                choiceButtons.Add(new Choice { button = button, highlight = highlight, cursor = cursor, label = label });
            }
            return choiceButtons[index];
        }

        /// <summary>Questions leave room on the right of the line for the answers.</summary>
        void FitLineForChoices(bool choices)
        {
            lineText.rectTransform.Stretch(70f, 54f, choices ? ChoiceMargin + ChoiceWidth + 30f : 90f, 30f);
        }

        void Open()
        {
            if (open)
                return;
            open = true;
            gameObject.SetActive(true);
            group.blocksRaycasts = true;
            if (PortraitEnabled) portrait.Show(app != null ? app.Companion : null);
            else portrait.Hide();
            Opened?.Invoke();
        }

        /// <summary>Hides the box (the portrait slides away with it).</summary>
        public void Close()
        {
            if (!open)
                return;
            open = false;
            waiting = false;
            line = "";
            typed = 0f;
            SetChoices(null);
            group.blocksRaycasts = false;
            Closed?.Invoke();
        }

        public void HideNow()
        {
            bool was = open;
            open = false;
            waiting = false;
            line = "";
            typed = 0f;
            shown = 0f;
            SetChoices(null);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            portrait.Hide();
            lineText.text = "";
            if (was)
                Closed?.Invoke();
        }

        /// <summary>Starts typing a line without waiting for the player (e.g. above an input field).</summary>
        public void Show(string text) => ShowLine(text, forQuestion: false);

        void ShowLine(string text, bool forQuestion)
        {
            Open();
            SetChoices(null);
            FitLineForChoices(forQuestion);
            // Wrapped up front so characters never jump to the next line while typing.
            line = TextWrap.Wrap(text ?? "", lineText.font, lineText.fontSize, lineText.fontStyle, lineText.rectTransform.rect.width - 8f);
            typed = 0f;
            pause = 0.05f;
            lineText.text = "";
            advanced = false;
            LineStarted?.Invoke(line);
        }

        /// <summary>Types a line and waits until the player moves on.</summary>
        public IEnumerator Say(string text)
        {
            Show(text);
            waiting = true;
            while (!advanced && open)
                yield return null;
            waiting = false;
        }

        public IEnumerator Say(IList<string> lines)
        {
            if (lines == null)
                yield break;
            foreach (var text in lines)
                yield return Say(text);
        }

        /// <summary>Types a question, then shows the choices; <paramref name="picked"/> gets the index.</summary>
        public IEnumerator Ask(string text, IList<string> options, Action<int> picked)
        {
            ShowLine(text, forQuestion: true);
            while (IsTyping && open)
                yield return null;
            choice = -1;
            SetChoices(options);
            while (choice < 0 && open)
                yield return null;
            int result = choice;
            SetChoices(null);
            if (result >= 0)
                picked?.Invoke(result);
        }

        /// <summary>Says every line but the last, then asks with the last one.</summary>
        public IEnumerator Ask(IList<string> lines, IList<string> options, Action<int> picked)
        {
            int count = lines?.Count ?? 0;
            for (int i = 0; i < count - 1; i++)
                yield return Say(lines[i]);
            yield return Ask(count > 0 ? lines[count - 1] : "", options, picked);
        }

        void SetChoices(IList<string> options)
        {
            choiceCount = options?.Count ?? 0;
            selected = 0;
            for (int i = 0; i < Mathf.Max(choiceButtons.Count, choiceCount); i++)
            {
                bool used = i < choiceCount;
                if (!used && i >= choiceButtons.Count)
                    continue;
                var choice = ChoiceButton(i);
                choice.button.gameObject.SetActive(used);
                if (used)
                    choice.label.text = options[i];
            }
            choiceRoot.gameObject.SetActive(choiceCount > 0);
            ShowSelection();
        }

        void ShowSelection()
        {
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                bool on = i == selected && i < choiceCount;
                var choice = choiceButtons[i];
                choice.highlight.enabled = on;
                choice.cursor.enabled = on;
                // The cursor nudges forward and back, like an RPG pointer.
                choice.cursor.rectTransform.anchoredPosition = new Vector2(28f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 5f, 1f);
            }
        }

        /// <summary>A click or key: finishes the typing, or (once complete) lets the waiting line go.</summary>
        public void Advance()
        {
            if (!open || IsChoosing)
                return;
            if (IsTyping)
            {
                typed = line.Length;
                return;
            }
            if (waiting)
            {
                advanced = true;
                app?.Audio?.Play(RoomSound.Click, 0.5f);
            }
        }

        void MoveSelection(int step)
        {
            if (choiceCount == 0)
                return;
            selected = (selected + step + choiceCount) % choiceCount;
            app?.Audio?.Play(RoomSound.Click, 0.35f);
        }

        public void Pick(int index)
        {
            if (index < 0 || index >= choiceCount)
                return;
            app?.Audio?.Play(RoomSound.Click, 0.8f);
            choice = index;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            shown = Mathf.MoveTowards(shown, open ? 1f : 0f, dt / (open ? 0.25f : 0.18f));
            float eased = 1f - (1f - shown) * (1f - shown);
            group.alpha = eased;
            box.anchoredPosition = boxHome + new Vector2(0f, (1f - eased) * -24f);
            if (!open && shown <= 0f)
            {
                portrait.Hide();
                return;
            }

            var keyboard = Keyboard.current;
            if (open && keyboard != null)
            {
                if (IsChoosing)
                {
                    for (int i = 0; i < choiceCount && i < 9; i++)
                        if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                            Pick(i);
                    if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                        MoveSelection(-1);
                    if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                        MoveSelection(1);
                    if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                        Pick(selected);
                }
                else if (!IsTypingIntoField() && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                {
                    Advance();
                }
            }

            if (IsTyping)
            {
                if (pause > 0f)
                {
                    pause -= dt;
                }
                else
                {
                    int before = Mathf.FloorToInt(typed);
                    typed = Mathf.Min(line.Length, typed + dt * RoomSettings.TextSpeed);
                    int after = Mathf.FloorToInt(typed);
                    if (after > before && after < line.Length)
                        pause = PauseAfter(line[after - 1]);
                }
            }
            int count = Mathf.FloorToInt(typed);
            if (lineText.text.Length != count)
                lineText.text = line.Substring(0, count);

            if (IsChoosing)
                ShowSelection();
            catcher.raycastTarget = open && !IsChoosing && (waiting || IsTyping);
            bool ready = open && !IsTyping && waiting && !IsChoosing;
            nextIndicator.enabled = ready && Mathf.Repeat(Time.unscaledTime, 1.1f) < 0.75f;
            nextIndicator.rectTransform.anchoredPosition = new Vector2(-56f, 40f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f)) * 5f);

            // She slides in from the right and bobs a little while speaking.
            float bob = IsTyping ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 10f)) * 4f : 0f;
            portraitRect.anchoredPosition = portraitHome + new Vector2((1f - eased) * 180f, bob);
        }

        static float PauseAfter(char c) => c switch
        {
            '。' or '！' or '？' or '!' or '?' => 0.28f,
            '…' => 0.12f,
            '，' or '、' or ',' or '；' or '：' => 0.12f,
            '—' => 0.08f,
            _ => 0f,
        };

        static bool IsTypingIntoField()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.GetComponent<InputField>() != null;
        }
    }

    /// <summary>Forwards the pointer entering to a callback.</summary>
    public class HoverRelay : MonoBehaviour, IPointerEnterHandler
    {
        public Action Entered;

        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();
    }

    /// <summary>Forwards a pointer click to a callback.</summary>
    public class ClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Clicked?.Invoke();
        }
    }
}
