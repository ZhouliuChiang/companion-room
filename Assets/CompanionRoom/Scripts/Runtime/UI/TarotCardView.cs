using System;
using CompanionRoom.Tarot;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CompanionRoom.UI
{
    /// <summary>
    /// One tarot card in the UI: a back, and a face with the card's art (or a drawn placeholder with
    /// its numeral and name). Rounded solid surfaces rotate around Y; reversed art is upside down.
    /// Clickable while <see cref="Clickable"/> is set, with a soft pulse to invite the click.
    /// </summary>
    public class TarotCardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        RectTransform rect;
        Image back;
        RectTransform face;
        Image faceArt;
        Text numeral;
        Text cardName;
        Text english;
        Image glow;
        TarotSolidGraphic solidFront, solidBack, solidEdge;
        bool reversedArt;
        Sprite blankFace;
        float flip = -1f;   // -1 back, +1 face
        float flipTarget = -1f;
        float hover;
        bool pointerInside;

        public Action<TarotCardView> Clicked;
        public bool Clickable { get; set; }
        public bool FaceUp => flipTarget > 0f;
        public bool IsFlipping => !Mathf.Approximately(flip, flipTarget);
        public RectTransform Rect => rect;
        public float Thickness => rect.rect.width * TarotSolidGraphic.ThicknessRatio;

        public static TarotCardView Create(string name, Transform parent, RoomUISkin skin, Vector2 size)
        {
            var root = UIFactory.Rect(name, parent);
            root.sizeDelta = size;
            var view = root.gameObject.AddComponent<TarotCardView>();
            view.rect = root;

            view.glow = UIFactory.Image("Glow", root, skin.shadow, new Color(skin.highlight.r, skin.highlight.g, skin.highlight.b, 0f));
            view.glow.rectTransform.Stretch(-34f, -34f, -34f, -34f);

            var shadow = UIFactory.Image("Shadow", root, skin.shadow, new Color(0.15f, 0.08f, 0.12f, 0.45f));
            shadow.rectTransform.Stretch(-16f, -8f, -16f, -22f);

            view.back = UIFactory.Image("Back", root, skin.cardBack, Color.white, true);
            view.back.rectTransform.Stretch();
            view.back.type = Image.Type.Simple;

            view.face = UIFactory.Rect("Face", root).Stretch();
            var faceBase = UIFactory.Image("Base", view.face, skin.cardFace, Color.white, true);
            faceBase.rectTransform.Stretch();
            view.faceArt = UIFactory.Image("Art", view.face, null, Color.white);
            view.faceArt.rectTransform.Stretch();
            view.faceArt.preserveAspect = true;
            view.numeral = UIFactory.Text("Numeral", view.face, skin, Mathf.RoundToInt(size.y * 0.075f), new Color(0.62f, 0.48f, 0.3f), TextAnchor.MiddleCenter);
            view.numeral.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -size.y * 0.06f), new Vector2(size.x, size.y * 0.12f));
            view.cardName = UIFactory.Text("Name", view.face, skin, Mathf.RoundToInt(size.y * 0.11f), skin.ink, TextAnchor.MiddleCenter);
            view.cardName.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size.y * 0.02f), new Vector2(size.x * 0.9f, size.y * 0.2f));
            view.cardName.horizontalOverflow = HorizontalWrapMode.Overflow;
            view.english = UIFactory.Text("English", view.face, skin, Mathf.RoundToInt(size.y * 0.05f), skin.inkSoft, TextAnchor.MiddleCenter);
            view.english.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, size.y * 0.07f), new Vector2(size.x * 0.9f, size.y * 0.1f));
            view.english.horizontalOverflow = HorizontalWrapMode.Overflow;

            view.blankFace = skin.cardFace;
            view.solidEdge = MakeSurface("SolidCreamEdge", TarotSolidGraphic.Surface.Edge, null, new Color(.96f, .86f, .73f));
            view.solidBack = MakeSurface("MoonRabbitBack", TarotSolidGraphic.Surface.Back, skin.cardBack, Color.white);
            view.solidFront = MakeSurface("IllustratedFront", TarotSolidGraphic.Surface.Front, skin.cardFace, Color.white);
            view.back.gameObject.SetActive(false);
            view.face.gameObject.SetActive(false);
            view.SetFaceUp(false, instant: true);
            return view;

            TarotSolidGraphic MakeSurface(string label, TarotSolidGraphic.Surface surface, Sprite sprite, Color tint)
            {
                var child = UIFactory.Rect(label, root).Stretch();
                var graphic = child.gameObject.AddComponent<TarotSolidGraphic>();
                graphic.color = tint;
                graphic.Configure(surface, sprite);
                return graphic;
            }
        }

        /// <summary>Shows a card's face content (art or placeholder) and orientation.</summary>
        public void SetCard(TarotCard card, bool reversed, Sprite art)
        {
            reversedArt = reversed;
            solidFront.Configure(TarotSolidGraphic.Surface.Front, art != null ? art : blankFace);
            faceArt.sprite = art;
            faceArt.enabled = art != null;
            bool placeholder = art == null;
            numeral.enabled = cardName.enabled = english.enabled = placeholder;
            numeral.text = card?.numeral ?? "";
            cardName.text = card?.name ?? "";
            english.text = card?.english ?? "";
            face.localRotation = Quaternion.Euler(0f, 0f, reversed ? 180f : 0f);
            ApplyFlip();
        }

        public void SetFaceUp(bool up, bool instant = false)
        {
            flipTarget = up ? 1f : -1f;
            if (instant)
                flip = flipTarget;
            ApplyFlip();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Clickable && eventData.button == PointerEventData.InputButton.Left)
                Clicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData) => pointerInside = true;
        public void OnPointerExit(PointerEventData eventData) => pointerInside = false;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (IsFlipping)
            {
                flip = Mathf.MoveTowards(flip, flipTarget, dt / 0.28f);
                ApplyFlip();
            }
            hover = Mathf.MoveTowards(hover, Clickable && pointerInside ? 1f : 0f, dt / 0.12f);
            float pulse = Clickable ? 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f) + hover * 0.4f : 0f;
            var c = glow.color;
            c.a = Mathf.MoveTowards(c.a, pulse, dt * 3f);
            glow.color = c;
            float lift = hover * 12f;
            back.rectTransform.anchoredPosition = face.anchoredPosition = new Vector2(0f, lift);
            ApplyFlip();
        }

        void ApplyFlip()
        {
            float progress = (flip + 1f) * .5f;
            float angle = 180f * (1f - Mathf.SmoothStep(0, 1, progress));
            float lift = hover * 12f + Mathf.Sin(progress * Mathf.PI) * 14f;
            solidEdge.SetPose(angle, lift, false);
            solidBack.SetPose(angle, lift, false);
            solidFront.SetPose(angle, lift, reversedArt);
        }
    }
}
