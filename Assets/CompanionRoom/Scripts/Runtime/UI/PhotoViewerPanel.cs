using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CompanionRoom.UI
{
    /// <summary>The album opened from the photo frame: one photo at a time, ◀ ▶ or arrow keys to turn.</summary>
    public class PhotoViewerPanel : RoomPanel
    {
        RawImage photo;
        AspectRatioFitter fitter;
        Text caption;
        Text hint;
        Button previous;
        Button next;
        PhotoAlbum album;
        int index;
        float fade = 1f;

        public event Action<int> IndexChanged;
        public int Index => index;

        public static PhotoViewerPanel Build(Transform parent, CompanionRoomApp app, RoomUISkin skin)
        {
            var root = UIFactory.Rect("PhotoViewer", parent).Stretch();
            var panel = root.gameObject.AddComponent<PhotoViewerPanel>();
            UIFactory.Backdrop(root, skin);

            var card = UIFactory.Panel("Card", root, skin);
            card.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1240f, 880f));

            var title = UIFactory.Panel("Title", card, skin, skin.titlePlate, withShadow: false);
            title.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(240f, 64f));
            UIFactory.Text("Label", title, skin, 32, skin.accentInk, TextAnchor.MiddleCenter, "相簿").rectTransform.Stretch(0f, 0f, 0f, 3f);

            // A paper mat with the photo fitted inside it.
            var mat = UIFactory.Image("Mat", card, skin.button, new Color(1f, 0.99f, 0.97f));
            mat.rectTransform.Stretch(110f, 70f, 110f, 150f);
            var photoArea = UIFactory.Rect("PhotoArea", mat.transform).Stretch(22f, 22f, 22f, 22f);
            var photoRect = UIFactory.Rect("Photo", photoArea);
            photoRect.anchorMin = Vector2.zero;
            photoRect.anchorMax = Vector2.one;
            photoRect.offsetMin = photoRect.offsetMax = Vector2.zero;
            panel.photo = photoRect.gameObject.AddComponent<RawImage>();
            panel.photo.raycastTarget = false;
            panel.fitter = photoRect.gameObject.AddComponent<AspectRatioFitter>();
            panel.fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            panel.caption = UIFactory.Text("Caption", card, skin, 30, skin.ink, TextAnchor.MiddleCenter);
            panel.caption.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(1000f, 50f));
            panel.hint = UIFactory.Text("Hint", card, skin, 22, skin.inkSoft, TextAnchor.MiddleCenter);
            panel.hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(1100f, 40f));

            panel.previous = UIFactory.RoundButton("Previous", card, skin, "◀", 76f, () => panel.Step(-1), 30);
            panel.previous.GetComponent<RectTransform>().Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(56f, 40f), new Vector2(76f, 76f));
            panel.next = UIFactory.RoundButton("Next", card, skin, "▶", 76f, () => panel.Step(1), 30);
            panel.next.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-56f, 40f), new Vector2(76f, 76f));

            var close = UIFactory.RoundButton("Close", card, skin, "×", 68f, panel.Hide, 40);
            close.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-14f, -14f), new Vector2(68f, 68f));

            panel.InitPanel(app, skin, card);
            return panel;
        }

        /// <summary>Opens the album at <paramref name="start"/> and waits until it is closed.</summary>
        public IEnumerator Browse(PhotoAlbum photos, int start)
        {
            album = photos;
            index = Mathf.Clamp(start, 0, Mathf.Max(0, album.Count - 1));
            hint.text = album.IsPlaceholder
                ? $"還沒有照片：把 PNG / JPG 放進「{PhotoAlbum.UserFolder}」"
                : "← → 翻頁　Esc 關閉";
            ShowCurrent();
            Show();
            yield return WaitUntilClosed();
        }

        public void Step(int delta)
        {
            if (album == null || album.Count <= 1)
                return;
            index = (index + delta + album.Count) % album.Count;
            App?.Audio?.Play(RoomSound.Flip, 0.6f);
            ShowCurrent();
        }

        void ShowCurrent()
        {
            var texture = album.Get(index);
            photo.texture = texture;
            photo.enabled = texture != null;
            fitter.aspectRatio = texture != null ? texture.width / (float)Mathf.Max(1, texture.height) : 1.5f;
            caption.text = album.Count > 0 ? $"{album.Entries[index].title}　{index + 1} / {album.Count}" : "";
            previous.interactable = next.interactable = album.Count > 1;
            fade = 0f;
            IndexChanged?.Invoke(index);
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen)
                return;
            fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / 0.25f);
            photo.color = new Color(1f, 1f, 1f, fade);
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                Step(-1);
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                Step(1);
        }
    }
}
