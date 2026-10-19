using UnityEngine;

namespace CompanionRoom.UI
{
    /// <summary>
    /// Procedural stand-ins for UI art: anti-aliased rounded frames (nine-sliced), soft shadows, the
    /// tarot card back and placeholder photos. Official PNGs in <see cref="RoomUISkin"/> replace them.
    /// </summary>
    public static class UISprites
    {
        static Texture2D NewTexture(int width, int height, string name) => new(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };

        /// <summary>Signed distance to a rounded box centred on the origin (negative inside).</summary>
        static float RoundedBox(Vector2 p, Vector2 half, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x) - (half.x - radius), Mathf.Abs(p.y) - (half.y - radius));
            var outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        static float Band(float s, float from, float width) =>
            width <= 0f ? 0f : Mathf.Clamp01(Mathf.Min(s - from + 0.5f, from + width - s + 0.5f));

        /// <summary>
        /// A nine-slice rounded frame: <paramref name="fill"/> inside a <paramref name="border"/> px
        /// rim, with an optional thin inner line <paramref name="lineInset"/> px inside the rim.
        /// </summary>
        public static Sprite Panel(int size, float radius, float border, Color fill, Color borderColor, float lineInset, Color lineColor, float lineWidth)
        {
            var tex = NewTexture(size, size, "UIPanel");
            var pixels = new Color32[size * size];
            var half = new Vector2(size * 0.5f - 1f, size * 0.5f - 1f);
            radius = Mathf.Min(radius, half.x);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                float d = RoundedBox(p, half, radius);
                float alpha = Mathf.Clamp01(0.5f - d);
                float inside = -d;
                var color = border > 0f ? Color.Lerp(fill, borderColor, Mathf.Clamp01(border - inside + 0.5f)) : fill;
                if (lineWidth > 0f)
                    color = Color.Lerp(color, new Color(lineColor.r, lineColor.g, lineColor.b, color.a), Band(inside, border + lineInset, lineWidth) * lineColor.a);
                color.a *= alpha;
                pixels[y * size + x] = color;
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            float slice = Mathf.Min(size * 0.5f - 1f, Mathf.Ceil(Mathf.Max(radius, border + lineInset + lineWidth) + 2f));
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
        }

        /// <summary>
        /// A rounded card with a dashed rim, for RPG-style answer choices. Draw it with
        /// <c>Image.Type.Tiled</c>: the corners stay put and the dashes repeat along the edges
        /// instead of stretching (the 32 px middle holds exactly two dash periods).
        /// </summary>
        public static Sprite DashedPanel(Color fill, Color line, float lineWidth = 2.5f, float dash = 8f)
        {
            const int size = 64, border = 16;
            const float radius = 12f, inset = 3f;
            var tex = NewTexture(size, size, "UIDashedPanel");
            var pixels = new Color32[size * size];
            var half = new Vector2(size * 0.5f - 1f, size * 0.5f - 1f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                float d = RoundedBox(p, half, radius);
                float rim = Band(-d, inset, lineWidth);
                bool corner = (x < border || x >= size - border) && (y < border || y >= size - border);
                if (!corner && rim > 0f)
                {
                    // Along the top/bottom edges the dashes follow x, along the sides y.
                    float along = Mathf.Abs(p.y) > Mathf.Abs(p.x) ? x : y;
                    float phase = Mathf.Repeat(along - border, dash * 2f);
                    rim *= Mathf.Clamp01(Mathf.Min(phase + 0.5f, dash - phase + 0.5f));
                }
                var color = Color.Lerp(fill, line, rim);
                color.a *= Mathf.Clamp01(0.5f - d);
                pixels[y * size + x] = color;
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        /// <summary>A soft drop shadow (black, nine-sliced) to sit behind a panel.</summary>
        public static Sprite Shadow(int size, float radius, float blur)
        {
            var tex = NewTexture(size, size, "UIShadow");
            var pixels = new Color32[size * size];
            var half = new Vector2(size * 0.5f - blur - 1f, size * 0.5f - blur - 1f);
            radius = Mathf.Min(radius, half.x);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                float d = RoundedBox(p, half, radius);
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-blur * 0.6f, blur, d));
                pixels[y * size + x] = new Color(0f, 0f, 0f, a * 0.5f);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            float slice = size * 0.5f - 2f;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
        }

        /// <summary>The placeholder tarot card back: lattice, moon ring and a four-point star.</summary>
        public static Sprite CardBack(int width, int height, Color baseColor, Color gold, Color dark)
        {
            var tex = NewTexture(width, height, "CardBack");
            var pixels = new Color32[width * height];
            var half = new Vector2(width * 0.5f - 1f, height * 0.5f - 1f);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f - width * 0.5f, y + 0.5f - height * 0.5f);
                float d = RoundedBox(p, half, 20f);
                float inside = -d;
                var color = Color.Lerp(baseColor, dark, Mathf.Clamp01(p.magnitude / (height * 0.6f)) * 0.6f);

                // Diamond lattice inside the inner frame.
                if (inside > 22f)
                {
                    float u = (p.x + p.y) / 22f, v = (p.x - p.y) / 22f;
                    float grid = Mathf.Min(Mathf.Abs(u - Mathf.Round(u)), Mathf.Abs(v - Mathf.Round(v))) * 22f;
                    color = Color.Lerp(color, dark, Mathf.Clamp01(1.4f - grid) * 0.45f);
                }

                // Moon ring and star in the middle.
                float r = p.magnitude;
                color = Color.Lerp(color, gold, Band(r, 50f, 4f));
                color = Color.Lerp(color, dark, Mathf.Clamp01(46.5f - r) * 0.55f);
                // Four-point star: points reach r / 0.22 along the axes, r / 1.22 on the diagonals.
                float ax = Mathf.Abs(p.x), ay = Mathf.Abs(p.y);
                float star = Mathf.Min(ax * 0.22f + ay, ay * 0.22f + ax) - 17f;
                color = Color.Lerp(color, gold, Mathf.Clamp01(0.5f - star));
                float diag = Mathf.Min(Mathf.Abs(p.x - p.y), Mathf.Abs(p.x + p.y)) * 0.7071f;
                if (r < 30f)
                    color = Color.Lerp(color, gold, Mathf.Clamp01(1.5f - diag) * (1f - r / 30f));

                // Small stars at the corners of the inner frame.
                foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                {
                    var c = new Vector2(corner.x * (half.x - 34f), corner.y * (half.y - 34f));
                    var q = p - c;
                    float s = Mathf.Min(Mathf.Abs(q.x) * 0.3f + Mathf.Abs(q.y), Mathf.Abs(q.y) * 0.3f + Mathf.Abs(q.x)) - 4.5f;
                    color = Color.Lerp(color, gold, Mathf.Clamp01(0.5f - s));
                }

                color = Color.Lerp(color, gold, Mathf.Clamp01(6f - inside + 0.5f));
                color = Color.Lerp(color, gold, Band(inside, 14f, 2f));
                color.a = Mathf.Clamp01(0.5f - d);
                pixels[y * width + x] = color;
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>A soft painted-looking landscape for the photo viewer while no photos exist.</summary>
        public static Texture2D PlaceholderPhoto(int index)
        {
            const int w = 960, h = 640;
            var tex = NewTexture(w, h, "PlaceholderPhoto" + index);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[w * h];
            Color top, bottom, hill, hill2, disc;
            Vector2 discPos;
            float stars;
            switch (index % 3)
            {
                case 0:
                    top = new Color(0.62f, 0.8f, 0.96f); bottom = new Color(1f, 0.9f, 0.92f);
                    hill = new Color(0.66f, 0.82f, 0.62f); hill2 = new Color(0.82f, 0.9f, 0.7f);
                    disc = new Color(1f, 0.98f, 0.9f); discPos = new Vector2(0.72f, 0.74f); stars = 0f; break;
                case 1:
                    top = new Color(0.5f, 0.37f, 0.6f); bottom = new Color(1f, 0.7f, 0.5f);
                    hill = new Color(0.45f, 0.33f, 0.45f); hill2 = new Color(0.72f, 0.5f, 0.55f);
                    disc = new Color(1f, 0.78f, 0.55f); discPos = new Vector2(0.38f, 0.42f); stars = 0f; break;
                default:
                    top = new Color(0.08f, 0.1f, 0.24f); bottom = new Color(0.25f, 0.24f, 0.45f);
                    hill = new Color(0.12f, 0.12f, 0.24f); hill2 = new Color(0.2f, 0.2f, 0.36f);
                    disc = new Color(0.98f, 0.95f, 0.85f); discPos = new Vector2(0.25f, 0.76f); stars = 1f; break;
            }
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                var color = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, v));
                var dp = new Vector2((u - discPos.x) * w / h, v - discPos.y);
                float dr = dp.magnitude;
                color = Color.Lerp(color, disc, Mathf.Clamp01((0.075f - dr) * 400f));
                color += disc * Mathf.Clamp01(0.25f - dr) * 0.6f;
                if (stars > 0f)
                {
                    uint hsh = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                    hsh = (hsh ^ (hsh >> 13)) * 1274126177u;
                    if ((hsh & 1023u) == 0u && v > 0.35f)
                        color = Color.Lerp(color, Color.white, 0.85f);
                }
                float far = 0.36f + 0.06f * Mathf.Sin(u * 6.3f + index) + 0.03f * Mathf.Sin(u * 15f + 1.7f * index);
                float near = 0.2f + 0.08f * Mathf.Sin(u * 3.4f + 2.1f + index) + 0.02f * Mathf.Sin(u * 21f);
                if (v < far)
                    color = Color.Lerp(color, hill2, Mathf.Clamp01((far - v) * 300f) * 0.9f);
                if (v < near)
                    color = Color.Lerp(hill2, hill, Mathf.Clamp01((near - v) * 300f));
                float vignette = Mathf.Clamp01(1.2f - new Vector2(u - 0.5f, v - 0.5f).magnitude * 1.1f);
                color *= Mathf.Lerp(0.85f, 1f, vignette);
                color.a = 1f;
                pixels[y * w + x] = color;
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            return tex;
        }
    }
}
