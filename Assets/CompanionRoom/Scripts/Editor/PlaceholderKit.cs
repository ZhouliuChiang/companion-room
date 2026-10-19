using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CompanionRoom.EditorTools
{
    /// <summary>
    /// Building blocks for the placeholder room: soft rounded boxes (saved as mesh sub-assets),
    /// built-in primitives without colliders, URP Lit materials and two painted textures.
    /// </summary>
    internal sealed class PlaceholderKit
    {
        public const string ArtFolder = "Assets/CompanionRoom/Art/Placeholder";
        public const string MaterialFolder = ArtFolder + "/Materials";
        const string MeshAssetPath = ArtFolder + "/PlaceholderMeshes.asset";

        readonly Dictionary<string, Mesh> rounded = new();
        readonly Dictionary<string, Material> materials = new();
        readonly Shader lit;

        public PlaceholderKit()
        {
            lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
                throw new InvalidOperationException("URP Lit shader not found.");
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.DeleteAsset(MeshAssetPath);
            var container = new Mesh { name = "PlaceholderMeshes" };
            AssetDatabase.CreateAsset(container, MeshAssetPath);
        }

        // ---------- materials ----------

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        public Material Mat(string name, string hex, float smoothness = 0.18f, float metallic = 0f, Texture texture = null, Vector2? tiling = null, bool emissive = false)
        {
            if (materials.TryGetValue(name, out var cached))
                return cached;
            string path = $"{MaterialFolder}/M_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(lit);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = lit;
            var color = Hex(hex);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetTexture("_BaseMap", texture);
            m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
            materials[name] = m;
            return m;
        }

        public Material Glowing(string name, string hex, string glowHex, float glow)
        {
            var m = Mat(name, hex, 0.3f, 0f, null, null, true);
            m.SetColor("_EmissionColor", Hex(glowHex) * glow);
            return m;
        }

        public static Material ShaderMaterial(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"Shader {shaderName} not found (did the shaders import?).");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------- textures ----------

        public static Texture2D PaintTexture(string name, int width, int height, Func<int, int, Color> pixel)
        {
            Directory.CreateDirectory(ArtFolder);
            string path = $"{ArtFolder}/T_{name}.png";
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixel(x, y);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Noise(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        /// <summary>Light wooden planks running along U, with seams and a soft grain.</summary>
        public static Texture2D FloorPlanks() => PaintTexture("FloorPlanks", 512, 512, (x, y) =>
        {
            const int rows = 6;
            float rowHeight = 512f / rows;
            int row = Mathf.FloorToInt(y / rowHeight);
            float offset = (row * 197) % 512;
            float u = (x + offset) % 512f;
            int plank = Mathf.FloorToInt(u / 256f) + row * 7;
            float tint = 0.94f + Noise(plank, row, 3) * 0.08f;
            var color = Hex("#F0D3AE") * tint;
            float grain = Mathf.Sin((u * 0.05f) + Mathf.Sin(y * 0.21f + plank) * 2.5f) * 0.5f + 0.5f;
            color = Color.Lerp(color, Hex("#E2BC93"), grain * 0.18f);
            float inRow = y - row * rowHeight;
            if (inRow < 2f || u % 256f < 2f)
                color = Color.Lerp(color, Hex("#C99E77"), 0.55f);
            color.a = 1f;
            return color;
        });

        /// <summary>Soft pink wallpaper: wide stripes and tiny dots.</summary>
        public static Texture2D Wallpaper() => PaintTexture("Wallpaper", 256, 256, (x, y) =>
        {
            var color = (x / 32) % 2 == 0 ? Hex("#F5CDD0") : Hex("#F2C5C9");
            int cx = x % 32, cy = y % 32;
            float d = new Vector2(cx - 16f, cy - 16f).magnitude;
            if ((x / 32) % 2 == 0 && d < 2.6f)
                color = Color.Lerp(color, Hex("#FFF4F2"), Mathf.Clamp01(2.6f - d));
            color.a = 1f;
            return color;
        });

        // ---------- geometry ----------

        GameObject Part(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale, Quaternion rotation, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A box of exactly <paramref name="size"/> with rounded edges (radius &lt;= 0 = sharp cube).</summary>
        public GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, float radius = 0.03f, Vector3? euler = null, bool castShadows = true)
        {
            var rotation = Quaternion.Euler(euler ?? Vector3.zero);
            if (radius <= 0f)
                return Part(name, parent, Builtin("Cube"), material, center, size, rotation, castShadows);
            radius = Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f - 0.0005f);
            return Part(name, parent, RoundedBox(size, Mathf.Max(0.001f, radius)), material, center, Vector3.one, rotation, castShadows);
        }

        public GameObject Cylinder(string name, Transform parent, Vector3 center, float diameter, float height, Material material, Vector3? euler = null) =>
            Part(name, parent, Builtin("Cylinder"), material, center, new Vector3(diameter, height * 0.5f, diameter), Quaternion.Euler(euler ?? Vector3.zero), true);

        /// <summary>A smooth, many-sided cylinder (rugs and other round things seen up close).</summary>
        public GameObject Disc(string name, Transform parent, Vector3 center, float diameter, float height, Material material) =>
            Part(name, parent, SmoothCylinder(), material, center, new Vector3(diameter, height, diameter), Quaternion.identity, true);

        Mesh SmoothCylinder()
        {
            const string key = "SmoothCylinder96";
            if (rounded.TryGetValue(key, out var cached))
                return cached;
            const int sides = 96;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            // Side: unit diameter, unit height, centred.
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(n * 0.5f + Vector3.down * 0.5f);
                vertices.Add(n * 0.5f + Vector3.up * 0.5f);
                normals.Add(n);
                normals.Add(n);
                uvs.Add(new Vector2(i / (float)sides, 0f));
                uvs.Add(new Vector2(i / (float)sides, 1f));
                if (i < sides)
                {
                    int b = i * 2;
                    triangles.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 });
                }
            }
            // Caps.
            foreach (float y in new[] { 0.5f, -0.5f })
            {
                int center = vertices.Count;
                var normal = y > 0f ? Vector3.up : Vector3.down;
                vertices.Add(new Vector3(0f, y, 0f));
                normals.Add(normal);
                uvs.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * 0.5f, y, Mathf.Sin(a) * 0.5f));
                    normals.Add(normal);
                    uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                    if (i < sides)
                    {
                        int v = center + 1 + i;
                        if (y > 0f)
                            triangles.AddRange(new[] { center, v + 1, v });
                        else
                            triangles.AddRange(new[] { center, v, v + 1 });
                    }
                }
            }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.AddObjectToAsset(mesh, MeshAssetPath);
            rounded[key] = mesh;
            return mesh;
        }

        public GameObject Sphere(string name, Transform parent, Vector3 center, Vector3 size, Material material, Vector3? euler = null) =>
            Part(name, parent, Builtin("Sphere"), material, center, size, Quaternion.Euler(euler ?? Vector3.zero), true);

        /// <summary>A capsule <paramref name="height"/> tall (end to end) and <paramref name="diameter"/> wide.</summary>
        public GameObject Capsule(string name, Transform parent, Vector3 center, float diameter, float height, Material material, Vector3? euler = null) =>
            Part(name, parent, Builtin("Capsule"), material, center, new Vector3(diameter, height * 0.5f, diameter), Quaternion.Euler(euler ?? Vector3.zero), true);

        public GameObject Quad(string name, Transform parent, Vector3 center, Vector2 size, Material material, Vector3? euler = null, bool castShadows = false) =>
            Part(name, parent, Builtin("Quad"), material, center, new Vector3(size.x, size.y, 1f), Quaternion.Euler(euler ?? Vector3.zero), castShadows);

        static Mesh Builtin(string primitive) => Resources.GetBuiltinResource<Mesh>(primitive + ".fbx");

        Mesh RoundedBox(Vector3 size, float radius)
        {
            string key = $"RoundedBox_{size.x:0.###}x{size.y:0.###}x{size.z:0.###}_r{radius:0.###}";
            if (rounded.TryGetValue(key, out var cached))
                return cached;
            var mesh = BuildRoundedBox(size, radius, 4);
            mesh.name = key;
            AssetDatabase.AddObjectToAsset(mesh, MeshAssetPath);
            rounded[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// Each face is a grid whose lines bunch up near the edges; every vertex is then pulled onto
        /// a rounded box (inner box + radius along the normal), giving smooth, seamless corners.
        /// </summary>
        static Mesh BuildRoundedBox(Vector3 size, float radius, int segments)
        {
            var half = size * 0.5f;
            var inner = half - Vector3.one * radius;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            float[] Coordinates(float innerH)
            {
                var list = new List<float>();
                for (int k = 0; k <= segments; k++)
                    list.Add(-innerH - radius * Mathf.Cos(k / (float)segments * Mathf.PI * 0.5f));
                for (int k = segments; k >= 0; k--)
                    list.Add(innerH + radius * Mathf.Cos(k / (float)segments * Mathf.PI * 0.5f));
                return list.ToArray();
            }

            var axes = new (Vector3 n, Vector3 u, Vector3 v)[]
            {
                (Vector3.right, Vector3.up, Vector3.forward),
                (Vector3.left, Vector3.forward, Vector3.up),
                (Vector3.up, Vector3.forward, Vector3.right),
                (Vector3.down, Vector3.right, Vector3.forward),
                (Vector3.forward, Vector3.right, Vector3.up),
                (Vector3.back, Vector3.up, Vector3.right),
            };
            static float Abs(Vector3 axis, Vector3 v) => Mathf.Abs(Vector3.Dot(axis, v));

            foreach (var (n, u, v) in axes)
            {
                float hu = Abs(u, half), hv = Abs(v, half), hn = Abs(n, half);
                var cu = Coordinates(Abs(u, inner));
                var cv = Coordinates(Abs(v, inner));
                int start = vertices.Count;
                for (int i = 0; i < cu.Length; i++)
                    for (int j = 0; j < cv.Length; j++)
                    {
                        var p = n * hn + u * cu[i] + v * cv[j];
                        var clamped = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        var dir = (p - clamped).normalized;
                        vertices.Add(clamped + dir * radius);
                        normals.Add(dir);
                        uvs.Add(new Vector2((cu[i] + hu) / (2f * hu), (cv[j] + hv) / (2f * hv)));
                    }
                int columns = cv.Length;
                for (int i = 0; i < cu.Length - 1; i++)
                    for (int j = 0; j < columns - 1; j++)
                    {
                        int a = start + i * columns + j;
                        int b = a + 1;
                        int c = a + columns;
                        int d = c + 1;
                        triangles.AddRange(new[] { a, d, b, a, c, d });
                    }
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        public void Save() => AssetDatabase.SaveAssets();
    }
}
