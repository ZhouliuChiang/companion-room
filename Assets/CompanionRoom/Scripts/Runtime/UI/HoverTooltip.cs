using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CompanionRoom.UI
{
    /// <summary>A little name plate that follows the pointer while it rests on a room item.</summary>
    public class HoverTooltip : MonoBehaviour
    {
        RectTransform canvasRect;
        RectTransform plate;
        CanvasGroup group;
        Text label;
        string target;
        float shown;

        public static HoverTooltip Build(Transform parent, RoomUISkin skin)
        {
            var root = UIFactory.Rect("Tooltip", parent).Stretch();
            var tooltip = root.gameObject.AddComponent<HoverTooltip>();
            tooltip.canvasRect = (RectTransform)parent;
            tooltip.plate = UIFactory.Panel("Plate", root, skin, skin.namePlate, withShadow: false);
            tooltip.plate.anchorMin = tooltip.plate.anchorMax = new Vector2(0.5f, 0.5f);
            tooltip.plate.pivot = new Vector2(0f, 0f);
            tooltip.group = tooltip.plate.gameObject.AddComponent<CanvasGroup>();
            tooltip.group.blocksRaycasts = false;
            tooltip.group.interactable = false;
            tooltip.label = UIFactory.Text("Label", tooltip.plate, skin, 26, skin.accentInk, TextAnchor.MiddleCenter);
            tooltip.label.rectTransform.Stretch(18f, 0f, 18f, 3f);
            tooltip.label.horizontalOverflow = HorizontalWrapMode.Overflow;
            tooltip.group.alpha = 0f;
            return tooltip;
        }

        /// <summary>The name to show, or null to fade out.</summary>
        public void Set(string text)
        {
            target = RoomSettings.ShowItemNames && !string.IsNullOrEmpty(text) ? text : null;
            if (target != null)
            {
                label.text = target;
                plate.sizeDelta = new Vector2(label.preferredWidth + 40f, 48f);
            }
        }

        void LateUpdate()
        {
            shown = Mathf.MoveTowards(shown, target != null ? 1f : 0f, Time.unscaledDeltaTime / 0.12f);
            group.alpha = shown;
            var mouse = Mouse.current;
            if (shown <= 0f || mouse == null)
                return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouse.position.ReadValue(), null, out var local))
            {
                var size = canvasRect.rect.size;
                var position = local + new Vector2(22f, 18f);
                position.x = Mathf.Min(position.x, size.x * 0.5f - plate.sizeDelta.x - 8f);
                position.y = Mathf.Min(position.y, size.y * 0.5f - plate.sizeDelta.y - 8f);
                plate.anchoredPosition = position;
            }
        }
    }
}
