using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CompanionRoom.EditorTools
{
    /// <summary>Bakes a shared rounded solid mesh and individually textured animation-ready cards.</summary>
    [InitializeOnLoad]
    public static class PastelTarotBuilder
    {
        const string Root = "Assets/CompanionRoom/Art/PastelTarot";
        const string ResourcesRoot = "Assets/CompanionRoom/Resources/CompanionRoom/Tarot/";
        [Serializable] class ArtCard { public string id; public string english; }
        [Serializable] class ArtManifest { public ArtCard[] cards; }

        static PastelTarotBuilder() => EditorApplication.update += Poll;

        static void Poll()
        {
            const string request = "Temp/PastelTarot.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Build(); }
            catch (Exception e)
            {
                Directory.CreateDirectory("Artifacts/PastelTarot");
                File.WriteAllText("Artifacts/PastelTarot/failed.txt", e.ToString());
                Debug.LogException(e);
            }
        }

        [MenuItem("Tools/兔兔房間/建立粉色塔羅牌模型")]
        public static void Build()
        {
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh();
            var deck = JsonUtility.FromJson<ArtManifest>(File.ReadAllText(ResourcesRoot + "art_manifest.json"));
            if (deck.cards.Length != 78) throw new InvalidOperationException("Expected all 78 tarot card illustrations.");
            var back = Material("Back", ResourcesRoot + "Art/card_back.png", Color.white);
            var edge = Material("Edge", null, new Color(0.94f, 0.82f, 0.73f));
            var mesh = RoundedCard();
            string meshPath = Root + "/RoundedCard.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
            foreach (var card in deck.cards)
            {
                var facePath = ResourcesRoot + "Cards/" + card.id + ".png";
                if (!File.Exists(facePath)) throw new FileNotFoundException(facePath);
                var front = Material(card.id, facePath, Color.white);
                var go = new GameObject(card.id);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[] { front, back, edge };
                go.AddComponent<BoxCollider>().size = new Vector3(.07f, .115f, .0016f);
                PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/" + card.id + ".prefab");
                UnityEngine.Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Artifacts/PastelTarot");
            RenderPreview(deck);
            File.WriteAllText("Artifacts/PastelTarot/build.txt", "PASS: 78 individual card prefabs; 70 x 115 x 1.6 mm; rounded corners; front/back/edge submeshes; centered pivots; separate materials.\n");
        }

        static void RenderPreview(ArtManifest deck)
        {
            var preview = new GameObject("TarotPreview");
            RenderTexture rt = null;
            Texture2D image = null;
            try
            {
                for (int i = 0; i < deck.cards.Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + deck.cards[i].id + ".prefab");
                    var card = UnityEngine.Object.Instantiate(prefab, preview.transform);
                    card.layer = 30;
                    card.transform.position = new Vector3((4.5f - i % 10) * .079f, (3.5f - i / 10) * .125f, 0);
                }
                var cameraObject = new GameObject("Camera"); cameraObject.transform.parent = preview.transform;
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, 1);
                camera.transform.rotation = Quaternion.Euler(0, 180, 0);
                camera.orthographic = true; camera.orthographicSize = .505f;
                camera.nearClipPlane = .01f; camera.farClipPlane = 3;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.28f, .23f, .25f);
                camera.cullingMask = 1 << 30;
                // Unlit preview materials preserve the original art colors regardless of scene lighting.
                foreach (var renderer in preview.GetComponentsInChildren<MeshRenderer>())
                {
                    var copies = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < copies.Length; i++)
                    {
                        var source = renderer.sharedMaterials[i];
                        copies[i] = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                        copies[i].SetColor("_BaseColor", source.GetColor("_BaseColor"));
                        copies[i].SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                    }
                    renderer.sharedMaterials = copies;
                }
                rt = new RenderTexture(3200, 4000, 24);
                camera.targetTexture = rt; camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes("Artifacts/PastelTarot/78-cards-preview.png", image.EncodeToPNG());
                foreach (var renderer in preview.GetComponentsInChildren<MeshRenderer>())
                    foreach (var material in renderer.sharedMaterials) UnityEngine.Object.DestroyImmediate(material);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preview);
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
            }
        }

        static Material Material(string name, string texturePath, Color color)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", .22f);
            if (texturePath != null)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // XY card, face +Z, back -Z. UVs on the back are mirrored geometrically so text
        // reads normally when viewed from behind. Edge vertices are separate for crisp normals.
        static Mesh RoundedCard()
        {
            const float w = .07f, h = .115f, r = .004f, z = .0008f;
            var ring = new List<Vector2>();
            for (int corner = 0; corner < 4; corner++)
            {
                float a = corner * 90f;
                var center = new Vector2(corner == 0 || corner == 3 ? w / 2 - r : -w / 2 + r,
                    corner < 2 ? h / 2 - r : -h / 2 + r);
                for (int j = 0; j <= 8; j++)
                {
                    float angle = (a + j * 90f / 8) * Mathf.Deg2Rad;
                    ring.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
                }
            }
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var front = new List<int>(); var back = new List<int>(); var edge = new List<int>();
            int n = ring.Count;
            for (int side = 0; side < 2; side++)
            {
                int start = vertices.Count;
                vertices.Add(new Vector3(0, 0, side == 0 ? z : -z)); uv.Add(new Vector2(.5f, .5f));
                foreach (var p in ring)
                {
                    vertices.Add(new Vector3(p.x, p.y, side == 0 ? z : -z));
                    uv.Add(new Vector2(.5f + (side == 0 ? -p.x : p.x) / w, .5f + p.y / h));
                }
                for (int i = 0; i < n; i++)
                {
                    var tris = side == 0 ? front : back;
                    tris.Add(start); tris.Add(start + 1 + (side == 0 ? i : (i + 1) % n));
                    tris.Add(start + 1 + (side == 0 ? (i + 1) % n : i));
                }
            }
            for (int i = 0; i < n; i++)
            {
                var p = ring[i]; var q = ring[(i + 1) % n]; int v = vertices.Count;
                vertices.Add(new Vector3(p.x, p.y, z)); vertices.Add(new Vector3(p.x, p.y, -z));
                vertices.Add(new Vector3(q.x, q.y, -z)); vertices.Add(new Vector3(q.x, q.y, z));
                uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one); uv.Add(Vector2.right);
                edge.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
            }
            var mesh = new Mesh { name = "RoundedTarot_70x115x1.6mm", subMeshCount = 3 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv);
            mesh.SetTriangles(front, 0); mesh.SetTriangles(back, 1); mesh.SetTriangles(edge, 2);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
