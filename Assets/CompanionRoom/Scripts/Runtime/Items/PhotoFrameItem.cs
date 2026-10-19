using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CompanionRoom
{
    /// <summary>
    /// 相片: 凡安 says a word, then the album opens. The frame in the room shows the last photo looked
    /// at, cropped to fill its surface.
    /// </summary>
    public class PhotoFrameItem : RoomItem
    {
        [Header("相片")]
        [Tooltip("Photos bundled with the game. Players can also drop PNG/JPG files into the Photos folder next to the game.")]
        [SerializeField] List<Texture2D> photos = new();
        [Tooltip("The picture surface inside the frame. Empty = a child named \"Photo\".")]
        [SerializeField] Renderer photoSurface;
        [Tooltip("Width / height of that surface, for cropping.")]
        [SerializeField] float surfaceAspect = 0.8f;

        Material surfaceMaterial;
        Texture2D shown;
        Texture2D owned;     // a loaded/generated texture the frame must free
        PhotoAlbum album;

        void Start()
        {
            photoSurface = FindPart(photoSurface, "Photo");
            album = PhotoAlbum.Collect(photos);
            ShowOnFrame(Mathf.Clamp(RoomSettings.GetInt("PhotoIndex", 0), 0, Mathf.Max(0, album.Count - 1)));
            KeepShownAndRelease();
        }

        void OnDestroy()
        {
            album?.Release();
            if (owned != null)
                Destroy(owned);
            if (surfaceMaterial != null)
                Destroy(surfaceMaterial);
        }

        public override IEnumerator Interact(CompanionRoomApp app)
        {
            album = PhotoAlbum.Collect(photos);
            yield return app.Dialogue.Say(app.Lines.Pick(album.IsPlaceholder ? "photo_empty" : "photo"));
            app.Dialogue.Close();

            int start = Mathf.Clamp(RoomSettings.GetInt("PhotoIndex", 0), 0, Mathf.Max(0, album.Count - 1));
            app.UI.Photos.IndexChanged += OnIndexChanged;
            yield return app.UI.Photos.Browse(album, start);
            app.UI.Photos.IndexChanged -= OnIndexChanged;

            yield return app.Dialogue.Say(app.Lines.Pick("photo_after"));
        }

        public override void OnInteractionEnded(CompanionRoomApp app)
        {
            app.UI.Photos.IndexChanged -= OnIndexChanged;
            KeepShownAndRelease();
        }

        void OnIndexChanged(int index)
        {
            RoomSettings.SetInt("PhotoIndex", index);
            ShowOnFrame(index);
        }

        /// <summary>The frame takes over the photo it shows; the rest of the album is freed.</summary>
        void KeepShownAndRelease()
        {
            if (album == null)
                return;
            var entry = album.Entries.Find(e => e.texture != null && e.texture == shown);
            if (entry != null)
            {
                if (owned != null && owned != shown)
                    Destroy(owned);
                owned = entry.owned ? shown : null;
                entry.owned = false;
            }
            album.Release();
            album = null;
        }

        void ShowOnFrame(int index)
        {
            var texture = album?.Get(index);
            if (texture == null || photoSurface == null)
                return;
            shown = texture;
            if (surfaceMaterial == null)
                surfaceMaterial = photoSurface.material;
            surfaceMaterial.mainTexture = texture;
            float aspect = texture.width / (float)Mathf.Max(1, texture.height);
            var scale = Vector2.one;
            if (aspect > surfaceAspect)
                scale.x = surfaceAspect / aspect;
            else
                scale.y = aspect / surfaceAspect;
            surfaceMaterial.mainTextureScale = scale;
            surfaceMaterial.mainTextureOffset = (Vector2.one - scale) * 0.5f;
        }
    }
}
