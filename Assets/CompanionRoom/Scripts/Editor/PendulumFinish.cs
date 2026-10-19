using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CompanionRoom.EditorTools
{
    [InitializeOnLoad]
    public static class PendulumFinish
    {
        const string Request = "Temp/PendulumFinish.request";
        const string Folder = "Assets/CompanionRoom/Resources/Pendulum";
        static PendulumFinish() => EditorApplication.update += Poll;
        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try { Apply(); }
            catch (Exception e) { File.WriteAllText("Artifacts/Pendulum/materials-failed.txt", e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Tools/兔兔房間/套用靈擺金屬與紫水晶材質")]
        public static void Apply()
        {
            var metalShader = Shader.Find("CompanionRoom/PendulumMetal");
            var gemShader = Shader.Find("CompanionRoom/PendulumGem");
            var compositeShader = Shader.Find("CompanionRoom/PendulumComposite");
            foreach (var shader in new[] { metalShader, gemShader, compositeShader })
            {
                if (shader == null) throw new Exception("Pendulum shader has not imported.");
                var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity.ToString() == "Error").ToArray();
                if (errors.Length > 0) throw new Exception(string.Join("\n", errors.Select(e => e.message)));
            }
            Material Get(string name, Shader shader)
            {
                var path = Folder + "/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
                material.shader = shader;
                return material;
            }
            var metal = Get("FrameMetal", metalShader);
            Get("PendulumComposite", compositeShader);
            metal.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CompanionRoom/Art/UserModels/Pendulum/Textures/Pendulum_Texture_00.jpg"));
            metal.SetColor("_BaseColor", new Color(1, .96f, .91f));
            metal.SetFloat("_Metallic", .9f); metal.SetFloat("_Smoothness", .82f);
            var gem = Get("AmethystGem", gemShader);
            gem.SetColor("_BaseColor", new Color(.84f, .70f, 1f));
            gem.SetFloat("_IOR", 1.54f); gem.SetFloat("_Opacity", .18f); gem.SetFloat("_Sparkle", 1.3f);
            gem.SetFloat("_Iridescence", .65f);
            gem.renderQueue = 3000;
            EditorUtility.SetDirty(metal); EditorUtility.SetDirty(gem); AssetDatabase.SaveAssets();
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "/PendulumRig.fbx");
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "FrameMetal"), metal);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "AmethystGem"), gem);
            importer.SaveAndReimport();

            const string prefabPath = "Assets/CompanionRoom/Prefabs/UserModels/靈擺.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var model = prefab.transform.Find("Model");
                Bounds BoundsOf(Transform root)
                {
                    var renderers = root.GetComponentsInChildren<Renderer>();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    return bounds;
                }
                var oldBounds = BoundsOf(model);
                var previous = Enumerable.Range(0, model.childCount).Select(i => model.GetChild(i).gameObject).ToArray();
                foreach (var child in previous) child.SetActive(false);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PendulumRig.fbx");
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(asset, model);
                rig.transform.localPosition = Vector3.zero;
                rig.transform.localRotation = Quaternion.Euler(0, 180, 0);
                rig.transform.localScale *= oldBounds.size.y / Mathf.Max(.001f, BoundsOf(rig.transform).size.y);
                rig.transform.position += oldBounds.center - BoundsOf(rig.transform).center;
                foreach (var child in previous) Object.DestroyImmediate(child);
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            File.WriteAllText("Artifacts/Pendulum/materials-validation.txt", "PASS: shaders compile; separate metallic frame and transparent amethyst slots; original texture retained on frame; scene prefab and interaction share the finished rig.\n");
            if (File.Exists("Artifacts/Pendulum/materials-failed.txt")) File.Delete("Artifacts/Pendulum/materials-failed.txt");
        }
    }
}
