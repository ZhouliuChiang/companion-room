using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CompanionRoom.UI
{
    /// <summary>Small builders for the uGUI pieces every room window is made of.</summary>
    public static class UIFactory
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchors at one point (0..1 each) and places by pivot and position.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            if (sprite != null && sprite.border.sqrMagnitude > 0f)
                image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        /// <summary>
        /// A framed panel with a soft shadow under it. Size and place the returned rect; anything
        /// parented to it afterwards draws on top of the frame.
        /// </summary>
        public static RectTransform Panel(string name, Transform parent, RoomUISkin skin, Sprite sprite = null, bool withShadow = true)
        {
            var holder = Rect(name, parent);
            if (withShadow)
            {
                var shadow = Image("Shadow", holder, skin.shadow, new Color(0.25f, 0.12f, 0.12f, 0.55f));
                shadow.rectTransform.Stretch(-26f, -18f, -26f, -34f);
            }
            var body = Image("Body", holder, sprite != null ? sprite : skin.panel, Color.white, true);
            body.rectTransform.Stretch();
            return holder;
        }

        public static Text Text(string name, Transform parent, RoomUISkin skin, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, string value = "")
        {
            var rt = Rect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = skin.font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.text = value;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.15f;
            text.supportRichText = true;
            return text;
        }

        static void Feedback(Selectable selectable)
        {
            var colors = selectable.colors;
            colors.normalColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            colors.selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.55f);
            colors.fadeDuration = 0.08f;
            selectable.colors = colors;
            var nav = selectable.navigation;
            nav.mode = Navigation.Mode.None;
            selectable.navigation = nav;
            selectable.gameObject.AddComponent<PressScale>();
        }

        public static Button Button(string name, Transform parent, RoomUISkin skin, string label, Vector2 size, UnityAction onClick, bool accent = false, int fontSize = 30)
        {
            var image = Image(name, parent, accent ? skin.accentButton : skin.button, Color.white, true);
            image.rectTransform.sizeDelta = size;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Feedback(button);
            if (onClick != null)
                button.onClick.AddListener(onClick);
            var text = Text("Label", image.transform, skin, fontSize, accent ? skin.accentInk : skin.ink, TextAnchor.MiddleCenter, label);
            text.rectTransform.Stretch(10f, 2f, 10f, 4f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return button;
        }

        public static Button RoundButton(string name, Transform parent, RoomUISkin skin, string glyph, float size, UnityAction onClick, int fontSize = 34)
        {
            var image = Image(name, parent, skin.roundButton, Color.white, true);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Feedback(button);
            if (onClick != null)
                button.onClick.AddListener(onClick);
            var text = Text("Glyph", image.transform, skin, fontSize, skin.ink, TextAnchor.MiddleCenter, glyph);
            text.rectTransform.Stretch(0f, 0f, 0f, 3f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return button;
        }

        public static Slider Slider(string name, Transform parent, RoomUISkin skin, float min, float max, float value, Action<float> changed)
        {
            var root = Rect(name, parent);
            root.sizeDelta = new Vector2(420f, 40f);
            var track = Image("Track", root, skin.sliderTrack, Color.white);
            track.rectTransform.Stretch(0f, 12f, 0f, 12f);
            var fillArea = Rect("FillArea", root).Stretch(0f, 12f, 0f, 12f);
            var fill = Image("Fill", fillArea, skin.sliderFill, Color.white);
            fill.rectTransform.Stretch();
            var handleArea = Rect("HandleArea", root).Stretch(16f, 0f, 16f, 0f);
            var knob = Image("Knob", handleArea, skin.sliderKnob, Color.white, true);
            knob.rectTransform.sizeDelta = new Vector2(34f, 0f);
            knob.rectTransform.anchorMin = new Vector2(0f, 0f);
            knob.rectTransform.anchorMax = new Vector2(0f, 1f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = knob.rectTransform;
            slider.targetGraphic = knob;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            Feedback(slider);
            if (changed != null)
                slider.onValueChanged.AddListener(v => changed(v));
            return slider;
        }

        public static Toggle Toggle(string name, Transform parent, RoomUISkin skin, bool value, Action<bool> changed)
        {
            var box = Image(name, parent, skin.checkBox, Color.white, true);
            box.rectTransform.sizeDelta = new Vector2(42f, 42f);
            var mark = Image("Mark", box.transform, skin.checkMark, Color.white);
            mark.rectTransform.Stretch(9f, 9f, 9f, 9f);
            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = mark;
            toggle.SetIsOnWithoutNotify(value);
            Feedback(toggle);
            if (changed != null)
                toggle.onValueChanged.AddListener(v => changed(v));
            return toggle;
        }

        /// <summary>A vertical scroll list; children go into the returned content transform.</summary>
        public static RectTransform ScrollList(string name, Transform parent, RoomUISkin skin, float spacing, out ScrollRect scroll)
        {
            var root = Rect(name, parent);
            scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", root).Stretch(0f, 0f, 14f, 0f);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.004f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(2, 2, 2, 2);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bar = Image("Scrollbar", root, skin.sliderTrack, new Color(1f, 1f, 1f, 0.8f), true);
            bar.rectTransform.anchorMin = new Vector2(1f, 0f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(1f, 0.5f);
            bar.rectTransform.sizeDelta = new Vector2(8f, 0f);
            var handle = Image("Handle", bar.transform, skin.sliderFill, Color.white, true);
            handle.rectTransform.Stretch();
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            return content;
        }

        public static InputField Input(string name, Transform parent, RoomUISkin skin, string placeholder, int fontSize = 30)
        {
            var image = Image(name, parent, skin.button, Color.white, true);
            var field = image.gameObject.AddComponent<InputField>();
            var text = Text("Text", image.transform, skin, fontSize, skin.ink, TextAnchor.MiddleLeft);
            text.rectTransform.Stretch(26f, 6f, 26f, 8f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.supportRichText = false;
            var hint = Text("Placeholder", image.transform, skin, fontSize, skin.inkSoft, TextAnchor.MiddleLeft, placeholder);
            hint.rectTransform.Stretch(26f, 6f, 26f, 8f);
            field.textComponent = text;
            field.placeholder = hint;
            field.targetGraphic = image;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 60;
            field.caretColor = skin.ink;
            field.selectionColor = new Color(skin.accent.r, skin.accent.g, skin.accent.b, 0.35f);
            Feedback(field);
            return field;
        }

        /// <summary>A full-screen dim layer that swallows clicks.</summary>
        public static Image Backdrop(Transform parent, RoomUISkin skin)
        {
            var image = Image("Backdrop", parent, null, skin.backdrop, true);
            image.rectTransform.Stretch();
            return image;
        }
    }

    /// <summary>Buttons shrink a little while pressed and grow back.</summary>
    public class PressScale : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler,
        UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        float target = 1f;
        float scale = 1f;
        bool inside;

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData) => target = 0.95f;
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData) => target = inside ? 1.03f : 1f;
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) { inside = true; target = 1.03f; }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) { inside = false; target = 1f; }

        void OnDisable()
        {
            inside = false;
            target = scale = 1f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            if (Mathf.Approximately(scale, target))
                return;
            scale = Mathf.MoveTowards(scale, target, Time.unscaledDeltaTime * 1.2f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
