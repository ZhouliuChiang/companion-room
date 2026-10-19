using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using CompanionRoom.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CompanionRoom.EditorTools
{
    /// <summary>Imports the supplied models into a character-free placement workspace.</summary>
    public static class UserModelWorkspace
    {
        public const string ModelRoot = "Assets/CompanionRoom/Art/UserModels";
        public const string ManifestPath = ModelRoot + "/import_manifest.json";
        public const string PrefabRoot = "Assets/CompanionRoom/Prefabs/UserModels";
        public const string ScenePath = "Assets/CompanionRoom/Scenes/CompanionRoom.unity";
        static string ReportFolder => Path.GetFullPath("Artifacts/UserModelsImport");

        [Serializable] public class Manifest { public AssetInfo[] assets; }
        [Serializable] public class AssetInfo
        {
            public string key, label, source, fbx;
            public float[] size;
            public float targetWidth;
            public int meshes, triangles;
            public MaterialInfo[] materials;
        }
        [Serializable] public class MaterialInfo
        {
            public string name;
            public float[] baseColor;
            public float roughness;
            public TextureInfo textures;
        }
        [Serializable] public class TextureInfo { public string baseColor, normal, roughness, metallic, emission; }

        [MenuItem("Tools/兔兔房間/開啟新素材擺放場景", priority = 1)]
        public static void OpenWorkspace()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            var room = Object.FindObjectsByType<ImportedRoomAsset>(FindObjectsSortMode.None).FirstOrDefault(a => a.assetId == "Room");
            if (room != null)
            {
                Selection.activeGameObject = room.gameObject;
                if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Frame(BoundsOf(room.gameObject), false);
            }
        }

        // This entry is deliberately not a menu item: rerunning a rebuild must not erase the user's layout.
        public static void ImportFromCommandLine()
        {
            try { ImportAndCreateWorkspace(); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void ImportAndCreateWorkspace()
        {
            Directory.CreateDirectory(ReportFolder);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest?.assets == null || manifest.assets.Length != 12) throw new Exception("Expected 12 supplied assets.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(PrefabRoot);
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var info in manifest.assets)
            {
                Debug.Log("[UserModels] Importing " + info.label);
                prefabs[info.key] = PreparePrefab(info);
            }
            // Recoverable scene snapshot lives outside Assets, so it is never loaded into the new workspace.
            string backup = Path.Combine(ReportFolder, "BeforeReplacement_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(backup);
            if (File.Exists(ScenePath)) File.Copy(ScenePath, Path.Combine(backup, "CompanionRoom.unity"));
            File.Copy("ProjectSettings/EditorBuildSettings.asset", Path.Combine(backup, "EditorBuildSettings.asset"));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var roomRoot = new GameObject("房間（新模型）");
            var room = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["Room"], roomRoot.transform);
            room.transform.localPosition = Vector3.zero;
            // Imported props are staged OUTSIDE the room; the user owns their final layout.
            var staging = new GameObject("待擺放家具（逐件移入房間）");
            int index = 0;
            foreach (var info in manifest.assets.Where(a => a.key != "Room"))
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[info.key], staging.transform);
                instance.transform.localPosition = new Vector3(5.2f + (index % 3) * 2.25f, 0, 3.7f - (index / 3) * 2.55f);
                index++;
            }
            var infrastructure = new GameObject("系統（保留功能，等待擺放後綁定）");
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.transform.SetParent(infrastructure.transform);
            cam.tag = "MainCamera";
            Bounds roomBounds = BoundsOf(room);
            Vector3 target = roomBounds.center;
            cam.transform.position = target + new Vector3(7.3f, 6.0f, -8.5f);
            cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position);
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(3.2f, roomBounds.size.y * .80f + roomBounds.size.z * .30f);
            cam.nearClipPlane = .05f; cam.farClipPlane = 100;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.96f, .87f, .89f);
            cam.gameObject.AddComponent<AudioListener>();
            var cameraData = cam.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            var rig = cam.gameObject.AddComponent<RoomCameraRig>(); rig.Configure(target);
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.transform.SetParent(infrastructure.transform);
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowBias = .025f;
            var fill = new GameObject("Fill").AddComponent<Light>(); fill.transform.SetParent(infrastructure.transform);
            fill.type = LightType.Directional; fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(40, -55, 0);
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat;
            var lighting = infrastructure.AddComponent<RoomLighting>();
            lighting.Configure(sun, fill, cam, null, Array.Empty<Renderer>());
            lighting.Apply(lighting.Get(TimeOfDay.Morning));
            var appObject = new GameObject("CompanionRoomApp（無人物）"); appObject.transform.SetParent(infrastructure.transform);
            var audio = appObject.AddComponent<RoomAudio>();
            var app = appObject.AddComponent<CompanionRoomApp>();
            var skin = AssetDatabase.LoadAssetAtPath<RoomUISkin>("Assets/CompanionRoom/Settings/RoomUISkin.asset");
            app.Configure(skin, null, lighting, rig, audio);
            var serialized = new SerializedObject(app);
            serialized.FindProperty("greetOnStart").boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo();
            appObject.AddComponent<RoomInteractor>().Configure(cam);
            EditorSceneManager.SaveScene(scene, ScenePath);
            // Old character demo and sample scene are not included in the player.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Validate();
            RenderPreview(cam, Path.Combine(ReportFolder, "EmptyRoom_Unity.png"), 1400, 1100);
            var originalPosition = cam.transform.position; var originalRotation = cam.transform.rotation; float originalSize = cam.orthographicSize;
            cam.transform.position = new Vector3(7.6f, 12.0f, -15.0f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(7.4f, .5f, .1f) - cam.transform.position);
            cam.orthographicSize = 6.6f;
            RenderPreview(cam, Path.Combine(ReportFolder, "ImportedFurniture_Unity.png"), 1600, 1200);
            cam.transform.SetPositionAndRotation(originalPosition, originalRotation); cam.orthographicSize = originalSize;
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = room;
            Debug.Log("[UserModels] COMPLETE: 12 models, 0 old characters, 0 old placeholder renderers.");
        }

        static GameObject PreparePrefab(AssetInfo info)
        {
            var importer = AssetImporter.GetAtPath(info.fbx) as ModelImporter;
            if (importer == null) throw new Exception("FBX not imported: " + info.fbx);
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.isReadable = false; importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            string folder = Path.GetDirectoryName(info.fbx).Replace('\\', '/');
            Directory.CreateDirectory(folder + "/Materials");
            foreach (var source in info.materials)
            {
                string path = folder + "/Materials/" + source.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetColor("_BaseColor", source.textures != null && !string.IsNullOrEmpty(source.textures.baseColor)
                    ? Color.white : new Color(source.baseColor[0], source.baseColor[1], source.baseColor[2], 1));
                material.SetFloat("_Smoothness", Mathf.Clamp01(1 - source.roughness));
                material.SetFloat("_Metallic", 0);
                if (source.textures != null)
                {
                    if (!string.IsNullOrEmpty(source.textures.baseColor)) material.SetTexture("_BaseMap", PrepareTexture(source.textures.baseColor, false));
                    if (!string.IsNullOrEmpty(source.textures.normal))
                    {
                        material.SetTexture("_BumpMap", PrepareTexture(source.textures.normal, true));
                        material.SetFloat("_BumpScale", .65f); material.EnableKeyword("_NORMALMAP");
                    }
                }
                EditorUtility.SetDirty(material);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), material);
            }
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(info.fbx);
            var root = new GameObject(info.label);
            var marker = root.AddComponent<ImportedRoomAsset>(); marker.assetId = info.key; marker.sourceFile = info.source;
            var container = new GameObject("Model"); container.transform.SetParent(root.transform, false);
            container.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, container.transform);
            var bounds = BoundsOf(root);
            float scale = info.targetWidth / Mathf.Max(.00001f, bounds.size.x);
            container.transform.localScale = Vector3.one * scale;
            // Each prop pivots at the center of its bottom, making manual placement predictable.
            container.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
            bounds = BoundsOf(root);
            if (info.key != "Room")
            {
                var collider = root.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
            }
            var focus = new GameObject("FocusPoint"); focus.transform.SetParent(root.transform, false); focus.transform.localPosition = bounds.center;
            string prefabPath = PrefabRoot + "/" + info.label + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Texture2D PrepareTexture(string path, bool normal)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) throw new Exception("Missing texture " + path);
            bool change = ti.textureType != (normal ? TextureImporterType.NormalMap : TextureImporterType.Default) || ti.alphaIsTransparency || ti.maxTextureSize != 2048;
            if (change)
            {
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.alphaIsTransparency = false; ti.sRGBTexture = !normal; ti.maxTextureSize = 2048; ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new Exception("No renderers: " + go.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        public static void Validate()
        {
            var assets = Object.FindObjectsByType<ImportedRoomAsset>(FindObjectsSortMode.None);
            var required = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)).assets.Select(a => a.key).ToList();
            if (File.Exists(ModelRoot + "/additional_manifest.json"))
                required.AddRange(JsonUtility.FromJson<Manifest>(File.ReadAllText(ModelRoot + "/additional_manifest.json")).assets.Select(a => a.key));
            foreach (string id in required)
                if (!assets.Any(a => a.assetId == id)) throw new Exception("Missing supplied asset: " + id);
            if (Object.FindObjectsByType<RoomCompanion>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 0) throw new Exception("An old character is still in the scene.");
            if (Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 0) throw new Exception("Unexpected character Animator.");
            var sb = new StringBuilder("New model workspace validation\n");
            foreach (var asset in assets.OrderBy(a => a.assetId))
            {
                foreach (var r in asset.GetComponentsInChildren<Renderer>())
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null || m.shader.name != "Universal Render Pipeline/Lit") throw new Exception("Missing URP material: " + asset.name);
                        if (m.GetTexture("_BaseMap") == null) throw new Exception("Missing base texture: " + asset.name);
                    }
                    if (!AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>()?.sharedMesh).StartsWith(ModelRoot, StringComparison.Ordinal)) throw new Exception("Old mesh still referenced: " + r.name);
                }
                var b = BoundsOf(asset.gameObject);
                sb.AppendLine($"{asset.assetId}: {asset.name}, dimensions {b.size}, source {asset.sourceFile}");
            }
            sb.AppendLine($"{assets.Length} supplied model instances with URP textures; no old model/character references. User placement is preserved.");
            sb.AppendLine("Only CompanionRoom.unity is in build settings. Interactions retained in code; binding deferred until layout is ready.");
            File.WriteAllText(Path.Combine(ReportFolder, "unity_validation.txt"), sb.ToString());
        }

        /// <summary>Add only the extra props to the scene currently being edited, without reloading it.</summary>
        public static void AppendAdditionalModels()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new Exception("Open the room placement scene before appending props.");
            var existing = Object.FindObjectsByType<ImportedRoomAsset>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var snapshots = existing.ToDictionary(a => a, a => (a.transform.localPosition, a.transform.localRotation, a.transform.localScale));
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ModelRoot + "/additional_manifest.json"));
            var pending = manifest.assets.Where(info => !existing.Any(a => a.assetId == info.key)).ToArray();
            if (pending.Length == 0) return;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var staging = GameObject.Find("待擺放家具（逐件移入房間）");
            if (staging == null) staging = new GameObject("待擺放家具（逐件移入房間）");
            string groupName = "新增：" + string.Join("・", pending.Select(a => a.label));
            var addedGroup = staging.transform.Find(groupName);
            if (addedGroup == null)
            {
                addedGroup = new GameObject(groupName).transform;
                addedGroup.SetParent(staging.transform, false);
            }
            int index = 0;
            foreach (var info in pending)
            {
                var present = existing.FirstOrDefault(a => a.assetId == info.key);
                if (present != null) { index++; continue; }
                var prefab = PreparePrefab(info);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, addedGroup);
                instance.transform.position = new Vector3(5.2f + index * 1.8f, 0, -8.4f);
                instance.transform.rotation = Quaternion.identity;
                // Reserve free staging space without shifting any furniture the user placed.
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    var placementBounds = BoundsOf(instance);
                    placementBounds.Expand(.12f);
                    if (!existing.Any(a => BoundsOf(a.gameObject).Intersects(placementBounds))) break;
                    instance.transform.position += new Vector3(0, 0, -1.5f);
                }
                index++;
            }
            foreach (var asset in existing)
            {
                var before = snapshots[asset];
                if (asset.transform.localPosition != before.Item1 || asset.transform.localRotation != before.Item2 || asset.transform.localScale != before.Item3)
                    throw new Exception("Existing placement unexpectedly changed: " + asset.name);
            }
            AssetDatabase.SaveAssets();
            Validate();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Path.Combine(ReportFolder, "additional_import_complete.txt"),
                "Added " + string.Join(", ", pending.Select(a => a.key)) + " to current scene. Original instance transforms unchanged. Original source .blend files untouched.\n");
            Debug.Log("[UserModels] Added " + string.Join("、", pending.Select(a => a.label)) + "; existing placement unchanged.");
        }

        static void RenderPreview(Camera camera, string path, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            camera.targetTexture = previousTarget; RenderTexture.active = previous;
            rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture);
        }
    }
}
