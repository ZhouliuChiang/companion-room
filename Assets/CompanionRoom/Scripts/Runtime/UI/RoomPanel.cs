using System.Collections;
using UnityEngine;

namespace CompanionRoom.UI
{
    /// <summary>
    /// A modal window (photos, settings, records): fades and pops in over a dim backdrop, and an
    /// interaction can <c>yield return</c> <see cref="WaitUntilClosed"/> to continue once it is closed.
    /// </summary>
    public abstract class RoomPanel : MonoBehaviour
    {
        protected RoomUISkin Skin { get; private set; }
        protected CompanionRoomApp App { get; private set; }

        CanvasGroup group;
        RectTransform card;
        float shown;
        bool visible;

        public bool IsOpen => visible;

        protected void InitPanel(CompanionRoomApp app, RoomUISkin skin, RectTransform animatedCard)
        {
            App = app;
            Skin = skin;
            card = animatedCard;
            group = gameObject.GetComponent<CanvasGroup>();
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            gameObject.SetActive(false);
        }

        public void Show()
        {
            if (visible)
                return;
            visible = true;
            gameObject.SetActive(true);
            group.blocksRaycasts = true;
            group.interactable = true;
            App?.Audio?.Play(RoomSound.Open, 0.8f);
            OnShown();
        }

        public void Hide()
        {
            if (!visible)
                return;
            visible = false;
            group.blocksRaycasts = false;
            group.interactable = false;
            App?.Audio?.Play(RoomSound.Close, 0.6f);
            OnHidden();
        }

        /// <summary>Closes at once without the fade (used when an interaction is interrupted).</summary>
        public void HideNow()
        {
            bool wasVisible = visible;
            visible = false;
            shown = 0f;
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            gameObject.SetActive(false);
            if (wasVisible)
                OnHidden();
        }

        public IEnumerator WaitUntilClosed()
        {
            while (visible)
                yield return null;
        }

        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }

        protected virtual void Update()
        {
            float target = visible ? 1f : 0f;
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime / (visible ? 0.22f : 0.16f));
            float eased = 1f - (1f - shown) * (1f - shown);
            group.alpha = eased;
            if (card != null)
            {
                float scale = Mathf.Lerp(0.94f, 1f, eased);
                card.localScale = new Vector3(scale, scale, 1f);
            }
            if (!visible && shown <= 0f)
                gameObject.SetActive(false);
        }
    }
}
