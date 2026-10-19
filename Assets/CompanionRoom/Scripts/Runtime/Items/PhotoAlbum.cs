using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CompanionRoom
{
    /// <summary>
    /// The photos the frame can show: textures assigned in the inspector, then every PNG/JPG in the
    /// "Photos" folder next to the game (the project folder in the editor) and in
    /// StreamingAssets/Photos. Files are read when first shown. With none at all, three painted
    /// placeholders stand in.
    /// </summary>
    public sealed class PhotoAlbum
    {
        public sealed class Entry
        {
            public string title;
            public string path;
            public Texture2D texture;
            public bool owned;
        }

        static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };

        public readonly List<Entry> Entries = new();
        public bool IsPlaceholder { get; private set; }

        /// <summary>The folder players drop photos into (created on first use).</summary>
        public static string UserFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Photos"));

        public int Count => Entries.Count;

        public static PhotoAlbum Collect(IEnumerable<Texture2D> builtIn)
        {
            var album = new PhotoAlbum();
            if (builtIn != null)
                foreach (var texture in builtIn)
                    if (texture != null)
                        album.Entries.Add(new Entry { title = texture.name, texture = texture });

            try
            {
                Directory.CreateDirectory(UserFolder);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CompanionRoom] Could not create photo folder: {e.Message}");
            }
            album.AddFolder(UserFolder);
            album.AddFolder(Path.Combine(Application.streamingAssetsPath, "Photos"));

            if (album.Entries.Count == 0)
            {
                album.IsPlaceholder = true;
                string[] titles = { "春天的山坡", "海邊的夕陽", "星星很多的夜晚" };
                for (int i = 0; i < titles.Length; i++)
                    album.Entries.Add(new Entry { title = titles[i], texture = UI.UISprites.PlaceholderPhoto(i), owned = true });
            }
            return album;
        }

        void AddFolder(string folder)
        {
            if (!Directory.Exists(folder))
                return;
            var files = new List<string>();
            foreach (var file in Directory.GetFiles(folder))
                if (Array.IndexOf(Extensions, Path.GetExtension(file).ToLowerInvariant()) >= 0)
                    files.Add(file);
            files.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
                Entries.Add(new Entry { title = Path.GetFileNameWithoutExtension(file), path = file });
        }

        public Texture2D Get(int index)
        {
            if (index < 0 || index >= Entries.Count)
                return null;
            var entry = Entries[index];
            if (entry.texture == null && !string.IsNullOrEmpty(entry.path))
            {
                try
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = entry.title, wrapMode = TextureWrapMode.Clamp };
                    if (texture.LoadImage(File.ReadAllBytes(entry.path)))
                    {
                        texture.anisoLevel = 4;
                        entry.texture = texture;
                        entry.owned = true;
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(texture);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CompanionRoom] Could not read photo {entry.path}: {e.Message}");
                }
            }
            return entry.texture;
        }

        /// <summary>Frees textures this album loaded or generated.</summary>
        public void Release(Texture keep = null)
        {
            foreach (var entry in Entries)
                if (entry.owned && entry.texture != null && entry.texture != keep)
                {
                    UnityEngine.Object.Destroy(entry.texture);
                    entry.texture = null;
                }
        }
    }
}
