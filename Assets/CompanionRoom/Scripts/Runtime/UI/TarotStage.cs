using System;
using System.Collections;
using System.Collections.Generic;
using CompanionRoom.Tarot;
using UnityEngine;
using UnityEngine.UI;

namespace CompanionRoom.UI
{
    /// <summary>
    /// The reading table above the dialogue box: the face-down deck waiting while 凡安 asks whether
    /// you have your question in mind (nothing is typed), the deck being shuffled, cards dealt into
    /// their positions, and each card turned over when clicked.
    /// </summary>
    public class TarotStage : MonoBehaviour
    {
        static readonly Vector2 CardSize = new(214f, 350f);
        const float StageY = 150f;
        // Left of centre, so 凡安's portrait on the right never covers the last card.
        const float StageX = -130f;
        const int DeckCards = 6;

        CompanionRoomApp app;
        RoomUISkin skin;
        CanvasGroup group;
        Image backdrop;
        RectTransform table;
        readonly List<TarotCardView> deck = new();
        readonly List<TarotCardView> slots = new();
        float shown;
        bool visible;
        TarotCardView clicked;

        public bool IsOpen => visible;
        /// <summary>The deck lies waiting on the table (before shuffling).</summary>
        public bool IsShowingDeck => visible && deck.Count > 0 && slots.Count == 0;
        public IReadOnlyList<TarotCardView> Slots => slots;

        public static TarotStage Build(Transform parent, CompanionRoomApp app, RoomUISkin skin)
        {
            var root = UIFactory.Rect("TarotStage", parent).Stretch();
            var stage = root.gameObject.AddComponent<TarotStage>();
            stage.app = app;
            stage.skin = skin;
            stage.group = root.gameObject.AddComponent<CanvasGroup>();
            stage.backdrop = UIFactory.Backdrop(root, skin);
            stage.backdrop.color = new Color(skin.backdrop.r, skin.backdrop.g, skin.backdrop.b, skin.backdrop.a * 0.8f);

            stage.table = UIFactory.Rect("Table", root);
            stage.table.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(StageX, StageY), new Vector2(1000f, 520f));

            stage.HideNow();
            return stage;
        }

        void Show()
        {
            visible = true;
            gameObject.SetActive(true);
            group.blocksRaycasts = true;
        }

        public void Hide()
        {
            visible = false;
            group.blocksRaycasts = false;
        }

        public void HideNow()
        {
            visible = false;
            shown = 0f;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            Clear();
            gameObject.SetActive(false);
        }

        void Clear()
        {
            foreach (var card in deck)
                Destroy(card.gameObject);
            foreach (var card in slots)
                Destroy(card.gameObject);
            deck.Clear();
            slots.Clear();
        }

        /// <summary>Opens the table with the face-down deck in the middle, waiting to be shuffled.</summary>
        public void ShowDeck()
        {
            Show();
            Clear();
            for (int i = 0; i < DeckCards; i++)
            {
                var card = TarotCardView.Create("Deck" + i, table, skin, CardSize);
                card.Rect.anchoredPosition = new Vector2(0f, i * 1.5f);
                deck.Add(card);
            }
        }

        /// <summary>Shuffles the stack in the middle of the table, then deals face down.</summary>
        public IEnumerator ShuffleAndDeal(TarotSpread spread)
        {
            if (!IsShowingDeck)
                ShowDeck();
            app?.Audio?.Play(RoomSound.Shuffle);
            // Riffle: cards swing out left and right and fall back onto the stack.
            const float riffle = 1.3f;
            for (float t = 0f; t < riffle; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < deck.Count; i++)
                {
                    float phase = t * 7f + i * 1.3f;
                    float side = (i % 2 == 0 ? -1f : 1f) * Mathf.Abs(Mathf.Sin(phase)) * 120f * (1f - t / riffle);
                    deck[i].Rect.anchoredPosition = new Vector2(side, i * 1.5f + Mathf.Sin(phase * 0.5f) * 8f);
                    deck[i].Rect.localRotation = Quaternion.Euler(0f, 0f, side * 0.06f);
                }
                yield return null;
            }
            for (int i = 0; i < deck.Count; i++)
            {
                deck[i].Rect.anchoredPosition = new Vector2(0f, i * 1.5f);
                deck[i].Rect.localRotation = Quaternion.identity;
            }
            yield return Wait(0.25f);

            // Deal into the positions, left to right.
            int count = spread.positions.Length;
            float spacing = CardSize.x + 70f;
            for (int i = 0; i < count; i++)
            {
                var target = new Vector2((i - (count - 1) * 0.5f) * spacing, 0f);
                var card = TarotCardView.Create("Card" + i, table, skin, CardSize);
                card.Rect.anchoredPosition = new Vector2(0f, DeckCards * 1.5f);
                card.Clicked = c => clicked = c;
                slots.Add(card);
                app?.Audio?.Play(RoomSound.Flip, 0.7f);
                var from = card.Rect.anchoredPosition;
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.32f)
                {
                    float e = 1f - (1f - t) * (1f - t);
                    card.Rect.anchoredPosition = Vector2.Lerp(from, target, e) + new Vector2(0f, Mathf.Sin(e * Mathf.PI) * 40f);
                    card.Rect.localRotation = Quaternion.Euler(0f, 0f, (1f - e) * 12f * (i - 1));
                    yield return null;
                }
                card.Rect.anchoredPosition = target;
                card.Rect.localRotation = Quaternion.identity;
                yield return Wait(0.08f);
            }

            // The rest of the stack slides away.
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
            {
                foreach (var card in deck)
                {
                    if (!card.TryGetComponent<CanvasGroup>(out var c))
                        c = card.gameObject.AddComponent<CanvasGroup>();
                    c.alpha = 1f - t;
                    card.Rect.anchoredPosition += new Vector2(0f, -Time.unscaledDeltaTime * 200f);
                }
                yield return null;
            }
            foreach (var card in deck)
                Destroy(card.gameObject);
            deck.Clear();
        }

        /// <summary>Waits for the player to click the card at <paramref name="index"/>, then turns it over.</summary>
        public IEnumerator WaitAndReveal(int index, TarotCard card, bool reversed, Sprite art)
        {
            if (index < 0 || index >= slots.Count)
                yield break;
            var view = slots[index];
            view.SetCard(card, reversed, art);
            clicked = null;
            view.Clickable = true;
            while (clicked != view && visible)
                yield return null;
            view.Clickable = false;
            yield return Reveal(index, card, reversed);
        }

        /// <summary>Turns a card over right away (also used by the self test).</summary>
        public IEnumerator Reveal(int index, TarotCard card, bool reversed)
        {
            var view = slots[index];
            view.Clickable = false;
            app?.Audio?.Play(RoomSound.Flip);
            view.SetFaceUp(true);
            while (view.IsFlipping)
                yield return null;
            app?.Audio?.Play(RoomSound.Reveal, 0.6f);
        }

        /// <summary>Lets a waiting <see cref="WaitAndReveal"/> continue as if its card were clicked.</summary>
        public void ClickCard(int index)
        {
            if (index >= 0 && index < slots.Count && slots[index].Clickable)
                clicked = slots[index];
        }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
                yield return null;
        }

        void Update()
        {
            shown = Mathf.MoveTowards(shown, visible ? 1f : 0f, Time.unscaledDeltaTime / 0.25f);
            group.alpha = shown;
            if (!visible && shown <= 0f)
            {
                Clear();
                gameObject.SetActive(false);
            }
        }
    }
}
