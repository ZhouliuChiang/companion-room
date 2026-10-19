using System;
using UnityEngine;

namespace CompanionRoom.Journal
{
    /// <summary>
    /// Small procedural pieces the journal pages are drawn with: a dot-grid cell, washi-tape stripes,
    /// the gutter shade by the spine, the striped page edges, and white rounded shapes to tint.
    /// Everything is white (or black for shade) so one texture serves any color.
    /// </summary>
    static class JournalArt
    {
        static Texture2D dots;
        static Texture2D stripes;
        static Texture2D gutter;
        static Texture2D pageEdges;
        static Sprite round;
        static Sprite outline;
        static Sprite solid;

        /// <summary>One soft dot in a transparent 36 px cell; tile it with RawImage.uvRect.</summary>
        public static Texture2D Dots => dots != null ? dots : dots = Make(36, 36, "JournalDots", TextureWrapMode.Repeat, (x, y) =>
        {
            float d = new Vector2(x + 0.5f - 18f, y + 0.5f - 18f).magnitude;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(2.6f - d));
        });

        /// <summary>Diagonal stripes for washi tape (tile 32 px).</summary>
        public static Texture2D Stripes => stripes != null ? stripes : stripes = Make(32, 32, "JournalStripes", TextureWrapMode.Repeat, (x, y) =>
        {
            float s = Mathf.Repeat((x + y) / 16f, 1f);
            return new Color(1f, 1f, 1f, s < 0.5f ? 1f : 0.62f);
        });

        /// <summary>Black that deepens towards the right edge: the shade where a page curves into the spine.</summary>
        public static Texture2D Gutter => gutter != null ? gutter : gutter = Make(64, 4, "JournalGutter", TextureWrapMode.Clamp, (x, y) =>
        {
            float t = x / 63f;
            return new Color(0f, 0f, 0f, t * t * t);
        });

        /// <summary>Cream with fine lines: the side of a stack of pages.</summary>
        public static Texture2D PageEdges => pageEdges != null ? pageEdges : pageEdges = Make(4, 32, "JournalPageEdges", TextureWrapMode.Repeat, (x, y) =>
        {
            float c = y % 4 == 0 ? 0.84f : 0.97f;
            return new Color(c, c * 0.975f, c * 0.94f, 1f);
        });

        /// <summary>A white rounded shape (pill, circle, label) to tint through Image.color.</summary>
        public static Sprite Round => round != null ? round : round = RoundedSprite("JournalRound", 64, 31f, 0f);

        /// <summary>A white rounded outline (frames, card slots, stitching).</summary>
        public static Sprite Outline => outline != null ? outline : outline = RoundedSprite("JournalOutline", 64, 14f, 3f);

        /// <summary>A plain white square.</summary>
        public static Sprite Solid => solid != null ? solid : solid = RoundedSprite("JournalSolid", 8, 0f, 0f);

        static Texture2D Make(int width, int height, string name, TextureWrapMode wrap, Func<int, int, Color> pixel)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = wrap,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixel(x, y);
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>Rounded box, filled (lineWidth 0) or as an outline; nine-sliced.</summary>
        static Sprite RoundedSprite(string name, int size, float radius, float lineWidth)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[size * size];
            float half = size * 0.5f - 1f;
            float r = Mathf.Min(radius, half);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var q = new Vector2(Mathf.Abs(x + 0.5f - size * 0.5f) - (half - r), Mathf.Abs(y + 0.5f - size * 0.5f) - (half - r));
                    float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (lineWidth > 0f)
                        alpha *= Mathf.Clamp01(lineWidth + 0.5f + d);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            float slice = Mathf.Max(1f, Mathf.Ceil(r + lineWidth + 1f));
            slice = Mathf.Min(slice, size * 0.5f - 1f);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
        }
    }
}
