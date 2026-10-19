using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CompanionRoom.EditorTools
{
    [InitializeOnLoad]
    public static class RoomPolish
    {
        const string Request = "Temp/RoomPolish.request";
        public const string Folder = "Artifacts/RoomPolish";
        [Serializable] public class Snapshot { public Prop[] props; public string scene; public string backup; }
        [Serializable] public class Prop
        {
            public string key, name; public int index;
            public Vector3 position, angles, scale, boundsMin, boundsMax;
        }
        static RoomPolish() { Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/editor_tool_version.txt", "lighting-final-v4"); EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.isPlaying && SessionState.GetBool("RoomPolish.PlayCheck", false)) { PlayCheck(); return; }
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string action = File.ReadAllText(Request).Trim(); File.Delete(Request);
            try
            {
                if (action == "snapshot") Capture();
                else if (action == "details") Details();
                else if (action == "reference-camera") ReferenceCamera();
                else if (action == "apply") Apply();
                else if (action == "finish") FinishLighting();
                else if (action == "test")
                {
                    SessionState.SetBool("RoomPolish.PlayCheck", true); SessionState.SetInt("RoomPolish.Stage", 0);
                    EditorApplication.isPlaying = true;
                }
            }
            catch (Exception e) { Debug.LogException(e); Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/failed.txt", e.ToString()); }
        }
        static void ReferenceCamera()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != UserModelWorkspace.ScenePath) throw new Exception("Room scene must be active.");
            Directory.CreateDirectory(Folder);
            EditorSceneManager.SaveScene(scene);
            File.Copy(scene.path, Folder + "/BeforeReferenceCamera_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
            var camera = Camera.main;
            var look = new Vector3(0f, 1.9f, 0f);
            camera.transform.SetPositionAndRotation(look + new Vector3(8f, 6.532f, 8f), Quaternion.Euler(30f, 225f, 0f));
            camera.orthographic = true;
            camera.orthographicSize = 3.65f;
            camera.GetComponent<RoomCameraRig>().Configure(look);
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(camera.transform);
            EditorUtility.SetDirty(camera.GetComponent<RoomCameraRig>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RenderTest(camera, Folder + "/ReferenceCamera.png");
            File.WriteAllText(Folder + "/reference_camera_complete.txt", "Saved reference camera: orthographic, elevation 30 degrees, balanced walls.");
        }
        static ImportedRoomAsset[] Props() => Object.FindObjectsByType<ImportedRoomAsset>(FindObjectsSortMode.None).OrderBy(a => a.assetId).ThenBy(a => a.name).ThenBy(a => a.transform.position.x).ToArray();
        public static Bounds BoundsOf(GameObject go)
        {
            var rr = go.GetComponentsInChildren<Renderer>().Where(r => !HoverOutline.IsShellObject(r.gameObject)).ToArray();
            var b = rr[0].bounds; foreach (var r in rr.Skip(1)) b.Encapsulate(r.bounds); return b;
        }
        static void Capture()
        {
            Directory.CreateDirectory(Folder);
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != UserModelWorkspace.ScenePath) throw new Exception("Room placement scene is not active.");
            EditorSceneManager.SaveScene(scene);
            string backup = Folder + "/Before_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"); Directory.CreateDirectory(backup);
            File.Copy(scene.path, backup + "/CompanionRoom.unity"); File.Copy(scene.path + ".meta", backup + "/CompanionRoom.unity.meta");
            var props = Props();
            var snapshot = new Snapshot { scene = scene.path, backup = backup, props = props.Select((a, i) =>
            { var b = BoundsOf(a.gameObject); return new Prop { key = a.assetId, name = a.name, index = i, position = a.transform.position, angles = a.transform.eulerAngles, scale = a.transform.lossyScale, boundsMin = b.min, boundsMax = b.max }; }).ToArray() };
            File.WriteAllText(Folder + "/snapshot.json", JsonUtility.ToJson(snapshot, true));
            foreach (string key in new[] { "Room", "Window", "WallLamp", "SmallTable", "GamingDesk", "Bookcase", "WallShelf" })
                foreach (var prop in props.Where(a => a.assetId == key))
                {
                    int i = Array.IndexOf(props, prop);
                    ExportMesh(prop, Folder + "/" + key + "_" + i + ".bin");
                }
            File.WriteAllText(Folder + "/snapshot_complete.txt", "Scene saved and backed up. World-space meshes and placements captured.");
            Debug.Log("[RoomPolish] Saved and backed up user layout.");
        }
        static void ExportMesh(ImportedRoomAsset prop, string path)
        {
            var filters = prop.GetComponentsInChildren<MeshFilter>();
            foreach (var f in filters)
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(f.sharedMesh)) as ModelImporter;
                if (importer != null && !importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            }
            using var writer = new BinaryWriter(File.Open(path, FileMode.Create));
            writer.Write(filters.Length);
            foreach (var f in filters)
            {
                var mesh = f.sharedMesh; var vv = mesh.vertices; var nn = mesh.normals; var uv = mesh.uv; var tt = mesh.triangles;
                writer.Write(vv.Length); writer.Write(tt.Length);
                for (int j = 0; j < vv.Length; j++)
                {
                    var p = f.transform.TransformPoint(vv[j]); var n = f.transform.TransformDirection(nn[j]); var u = uv.Length == vv.Length ? uv[j] : Vector2.zero;
                    // Stable right-handed Blender world coordinates.
                    writer.Write(p.x); writer.Write(-p.z); writer.Write(p.y); writer.Write(n.x); writer.Write(-n.z); writer.Write(n.y); writer.Write(u.x); writer.Write(u.y);
                }
                foreach (int j in tt) writer.Write(j);
            }
        }
        static void Details()
        {
            var lines = new List<string>();
            foreach (var p in Props())
            {
                lines.Add("PROP " + p.assetId + " / " + p.name + " PARENT " + p.transform.parent?.name);
                foreach (var t in p.GetComponentsInChildren<Transform>())
                {
                    string row = t.name + " parent=" + t.parent?.name + " localP=" + t.localPosition.ToString("F3") + " localS=" + t.localScale.ToString("F3");
                    if (t.TryGetComponent<Renderer>(out var r)) row += " worldBounds=" + r.bounds.ToString("F3");
                    lines.Add(row);
                }
            }
            File.WriteAllLines(Folder + "/hierarchy.txt", lines);
        }
        [Serializable] public class Adjustment { public string name, key; public Vector3 before, after; public string reason; }
        [Serializable] public class AdjustmentReport { public List<Adjustment> adjustments = new(); public string meshReport; public Vector3 cameraPosition; }
        static AdjustmentReport changes;
        static string AssetFolder = "Assets/CompanionRoom/Art/UserModels/Polished";
        static Mesh ReadProcessed(string path, Transform space, string label)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            int nv = reader.ReadInt32(), ni = reader.ReadInt32();
            var vertices = new Vector3[nv]; var normals = new Vector3[nv]; var uv = new Vector2[nv];
            var normalMatrix = space.localToWorldMatrix.transpose;
            for (int j = 0; j < nv; j++)
            {
                float x = reader.ReadSingle(), y = reader.ReadSingle(), z = reader.ReadSingle();
                vertices[j] = space.InverseTransformPoint(new Vector3(x, z, -y));
                x = reader.ReadSingle(); y = reader.ReadSingle(); z = reader.ReadSingle();
                normals[j] = normalMatrix.MultiplyVector(new Vector3(x, z, -y)).normalized;
                uv[j] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            }
            var indices = new int[ni]; for (int j = 0; j < ni; j++) indices[j] = reader.ReadInt32();
            var groups = new int[ni / 3]; for (int j = 0; j < groups.Length; j++) groups[j] = reader.ReadInt32();
            var ordinary = new List<int>(); var special = new List<int>();
            for (int j = 0; j < groups.Length; j++)
            { var list = groups[j] == 0 ? ordinary : special; list.Add(indices[j * 3]); list.Add(indices[j * 3 + 1]); list.Add(indices[j * 3 + 2]); }
            var mesh = new Mesh { name = label, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.subMeshCount = 2;
            mesh.SetTriangles(ordinary, 0); mesh.SetTriangles(special, 1); mesh.RecalculateBounds();
            string assetPath = AssetFolder + "/" + label + ".asset";
            var prior = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (prior != null) { EditorUtility.CopySerialized(mesh, prior); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(prior); return prior; }
            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }
        static Bounds VisibleBounds(ImportedRoomAsset prop)
        {
            var rr = prop.GetComponentsInChildren<Renderer>().Where(r => !HoverOutline.IsShellObject(r.gameObject));
            if (prop.assetId == "MochiBunny") rr = rr.Where(r => !r.name.EndsWith(" (1)", StringComparison.Ordinal));
            var list = rr.ToArray(); var b = list[0].bounds; foreach (var r in list.Skip(1)) b.Encapsulate(r.bounds); return b;
        }
        static void Move(ImportedRoomAsset prop, Vector3 delta, string reason)
        {
            if (delta.sqrMagnitude < .000001f) return;
            // The user placed a duplicate rabbit body on another shelf; do not drag it with the main rabbit.
            var remote = prop.assetId == "MochiBunny" ? prop.GetComponentsInChildren<Renderer>().Where(r => r.name.EndsWith(" (1)", StringComparison.Ordinal)).Select(r => (r.transform, r.transform.position)).ToArray() : Array.Empty<(Transform, Vector3)>();
            var before = prop.transform.position; prop.transform.position += delta;
            foreach (var t in remote) t.Item1.position = t.Item2;
            changes.adjustments.Add(new Adjustment { key = prop.assetId, name = prop.name, before = before, after = prop.transform.position, reason = reason });
        }
        static void BottomAt(ImportedRoomAsset prop, float height, string reason)
        { Move(prop, Vector3.up * (height - VisibleBounds(prop).min.y), reason); }
        static void Apply()
        {
            if (!File.Exists(Folder + "/mesh_processing_report.json")) throw new Exception("Window cut geometry is not ready.");
            if (File.Exists(Folder + "/apply_complete.txt")) throw new Exception("Polish already applied; refusing to move the layout twice.");
            Directory.CreateDirectory(AssetFolder); AssetDatabase.Refresh();
            var props = Props(); var room = props.Single(a => a.assetId == "Room");
            var window = props.Single(a => a.assetId == "Window"); var lamp = props.Single(a => a.assetId == "WallLamp");
            var roomFilter = room.GetComponentInChildren<MeshFilter>(); var roomRenderer = roomFilter.GetComponent<Renderer>();
            var roomBase = roomRenderer.sharedMaterial;
            var cut = ReadProcessed(Folder + "/RoomCut_processed.bin", roomFilter.transform, "Room_RealWindowOpening");
            roomFilter.sharedMesh = cut;
            var glow = AssetDatabase.LoadAssetAtPath<Material>(AssetFolder + "/RoofStarsGlow.mat");
            if (glow == null) { glow = new Material(roomBase) { name = "Roof stars - soft warm glow" }; AssetDatabase.CreateAsset(glow, AssetFolder + "/RoofStarsGlow.mat"); }
            glow.EnableKeyword("_EMISSION"); glow.SetTexture("_EmissionMap", glow.GetTexture("_BaseMap")); glow.SetColor("_EmissionColor", new Color(1, .78f, .39f) * .8f);
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            roomRenderer.sharedMaterials = new[] { roomBase, glow };
            var twinkle = room.GetComponent<SoftRoofGlow>(); if (twinkle == null) twinkle = room.gameObject.AddComponent<SoftRoofGlow>(); twinkle.target = roomRenderer;
            var roomCollider = roomFilter.gameObject.GetComponent<MeshCollider>(); if (roomCollider == null) roomCollider = roomFilter.gameObject.AddComponent<MeshCollider>();
            roomCollider.sharedMesh = cut;
            var wf = window.GetComponentInChildren<MeshFilter>(); var wr = wf.GetComponent<Renderer>();
            var frameMaterial = wr.sharedMaterial;
            wf.sharedMesh = ReadProcessed(Folder + "/WindowGlass_processed.bin", wf.transform, "Window_SeparatedGlass");
            var glass = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Clear window glass", renderQueue = 3000 };
            glass.SetColor("_BaseColor", new Color(.84f, .94f, 1, .11f)); glass.SetFloat("_Surface", 1); glass.SetFloat("_Blend", 0);
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite", 0); glass.SetFloat("_Cull", (float)CullMode.Off); glass.SetFloat("_Smoothness", .76f);
            glass.SetFloat("_ReceiveShadows", 0); glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            glass.SetOverrideTag("RenderType", "Transparent"); glass.SetShaderPassEnabled("ShadowCaster", false);
            AssetDatabase.CreateAsset(glass, AssetDatabase.GenerateUniqueAssetPath(AssetFolder + "/ClearWindowGlass.mat"));
            wr.sharedMaterials = new[] { frameMaterial, glass };
            changes = new AdjustmentReport();
            float floor = .468f;
            var rug = props.Single(a => a.assetId == "Rug");
            BottomAt(rug, floor + .004f, "Place carpet underside on floor");
            // User layout stays intact: corrections are millimetres to a few centimetres, not a rearrangement.
            foreach (string id in new[] { "Bed", "GamingDesk", "GamingChair", "Bookcase" })
                foreach (var p in props.Where(a => a.assetId == id)) BottomAt(p, floor + .005f, "Set feet on floor");
            var bed = props.Single(a => a.assetId == "Bed"); var desk = props.Single(a => a.assetId == "GamingDesk");
            var cabinet = props.Single(a => a.assetId == "Bookcase"); var table = props.Single(a => a.assetId == "SmallTable");
            Move(bed, new Vector3(0, 0, -2.202f - VisibleBounds(bed).min.z), "Bring headboard close to back wall");
            Move(desk, new Vector3(0, 0, -2.202f - VisibleBounds(desk).min.z), "Bring gaming desk close to wall");
            float cabinetDx = -2.317f - VisibleBounds(cabinet).min.x;
            Move(cabinet, new Vector3(cabinetDx, 0, 0), "Align bookcase with left wall");
            // A circular rug's rotated renderer bounds overestimate its footprint; preserve its horizontal placement.
            BottomAt(table, VisibleBounds(rug).max.y + .004f, "Set low table on carpet");
            float tableTop = .94f + (table.transform.position.y - .527f);
            foreach (var p in props.Where(a => a.assetId == "TarotCards")) BottomAt(p, tableTop + .004f, "Place tarot cards on tabletop");
            float deskTop = 1.23f + (desk.transform.position.y - .464f);
            var pc = props.Single(a => a.assetId == "Computer");
            Move(pc, new Vector3(0, 0, desk.transform.position.z + 1.690f), "Keep monitor aligned with adjusted desk");
            BottomAt(pc, deskTop + .006f, "Seat computer stand on desk");
            var shelves = props.Where(a => a.assetId == "WallShelf").OrderBy(a => VisibleBounds(a).center.z).ToArray();
            var shelfMoves = new Dictionary<ImportedRoomAsset, Vector3>();
            foreach (var shelf in shelves)
            {
                var b = VisibleBounds(shelf); Vector3 shift;
                if (b.center.x < -1) shift = new Vector3(-2.327f - b.min.x, 0, 0);
                else shift = new Vector3(0, 0, -2.219f - b.min.z);
                Move(shelf, shift, "Seat wall shelf brackets against wall"); shelfMoves[shelf] = shift;
            }
            foreach (var p in props.Where(a => a.assetId == "FlowerPlant" || a.assetId == "GreenPlant"))
            {
                var b = VisibleBounds(p); float y; Vector3 carry = Vector3.zero;
                if (b.min.y < 1.05f) y = tableTop + .004f;
                else if (b.min.y < 1.4f) { y = deskTop + .004f; carry.z = desk.transform.position.z + 1.690f; }
                else if (b.min.y < 2.05f) { y = VisibleBounds(cabinet).max.y + .004f; carry.x = cabinetDx; }
                else
                {
                    var shelf = shelves.OrderBy(s => Vector2.Distance(new Vector2(VisibleBounds(s).center.x, VisibleBounds(s).center.z), new Vector2(b.center.x, b.center.z))).First();
                    carry = shelfMoves[shelf]; y = VisibleBounds(shelf).max.y + .004f;
                }
                Move(p, carry, "Keep plant with its supporting furniture"); BottomAt(p, y, "Rest pot on actual support surface");
            }
            var rabbit = props.Single(a => a.assetId == "MochiBunny");
            Move(rabbit, new Vector3(cabinetDx, 0, 0), "Keep rabbit on bookcase");
            BottomAt(rabbit, VisibleBounds(cabinet).max.y + .004f, "Rest main mochi rabbit on cabinet");
            // The separately positioned copy of the rabbit body remains where the user put it.
            var pendulum = props.Single(a => a.assetId == "Pendulum");
            var pendShelf = shelves.OrderBy(s => Vector3.Distance(VisibleBounds(s).center, VisibleBounds(pendulum).center)).First();
            pendulum.transform.localScale *= .80f;
            var pb = VisibleBounds(pendulum); var sb = VisibleBounds(pendShelf);
            Move(pendulum, new Vector3(sb.center.x - pb.center.x, 0, sb.center.z - pb.center.z), "Fit pendulum base safely onto shelf");
            BottomAt(pendulum, sb.max.y + .004f, "Set pendulum on shelf");
            foreach (var p in props.Where(a => a.assetId.StartsWith("WallArt", StringComparison.Ordinal)))
            {
                var b = VisibleBounds(p);
                Move(p, b.center.x < -2 ? new Vector3(-2.330f - b.min.x, 0, 0) : new Vector3(0, 0, -2.222f - b.min.z), "Place picture back against wall");
            }
            Move(lamp, new Vector3(0, 0, -2.237f - VisibleBounds(lamp).min.z), "Mount rabbit sconce backplate against wall");
            var lightObject = new GameObject("Rabbit lamp warm light"); lightObject.transform.SetParent(lamp.transform, false);
            var lb = VisibleBounds(lamp); lightObject.transform.position = new Vector3(lb.center.x, lb.center.y + .025f, lb.max.z + .10f);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1, .75f, .48f); light.range = 2.5f; light.intensity = 1.5f; light.shadows = LightShadows.Soft;
            var item = lamp.gameObject.AddComponent<WallLampItem>();
            var so = new SerializedObject(item);
            so.FindProperty("displayName").stringValue = "兔兔壁燈"; so.FindProperty("cameraFocus").floatValue = 0;
            so.FindProperty("lamp").objectReferenceValue = light; so.FindProperty("intensity").floatValue = 1.5f;
            so.FindProperty("glow").floatValue = .85f; so.FindProperty("startOn").boolValue = true;
            var glowParts = so.FindProperty("glowParts"); var lampRenderers = lamp.GetComponentsInChildren<Renderer>(); glowParts.arraySize = lampRenderers.Length;
            for (int j = 0; j < lampRenderers.Length; j++) glowParts.GetArrayElementAtIndex(j).objectReferenceValue = lampRenderers[j];
            so.ApplyModifiedPropertiesWithoutUndo(); item.FitCollider();
            var daylightObject = new GameObject("Daylight through real window"); daylightObject.transform.position = new Vector3(-3.1f, 2.35f, -.054f);
            daylightObject.transform.rotation = Quaternion.LookRotation(new Vector3(1, -.42f, .12f));
            var daylight = daylightObject.AddComponent<Light>(); daylight.type = LightType.Spot; daylight.range = 7; daylight.spotAngle = 53; daylight.innerSpotAngle = 35; daylight.intensity = 2; daylight.shadows = LightShadows.Soft; daylight.shadowBias = .015f;
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.type == LightType.Directional && l.name == "Sun");
            var daylightControl = daylightObject.AddComponent<WindowDaylight>(); daylightControl.daylight = daylight; daylightControl.roomSun = sun;
            // Original camera was on the exterior side. The room opens towards +Z and +X.
            var camera = Camera.main; var look = new Vector3(0, 1.9f, 0);
            // Match the reference's lower viewing elevation and wider view of the window wall.
            camera.transform.position = look + new Vector3(8.5f, 5.1f, 7.2f);
            camera.transform.rotation = Quaternion.LookRotation(look - camera.transform.position);
            camera.orthographic = true; camera.orthographicSize = 3.65f; camera.useOcclusionCulling = false;
            camera.GetComponent<RoomCameraRig>().Configure(look); camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(.20f); bloom.threshold.Override(.90f); bloom.scatter.Override(.55f);
            AssetDatabase.CreateAsset(profile, AssetDatabase.GenerateUniqueAssetPath(AssetFolder + "/SoftGlowProfile.asset"));
            AssetDatabase.AddObjectToAsset(bloom, profile);
            var post = new GameObject("Subtle roof and lamp bloom").AddComponent<Volume>(); post.isGlobal = true; post.priority = 3; post.sharedProfile = profile;
            // Collider centers follow moved model children, even when their outer pivots are far away.
            foreach (var p in props.Where(p => p.assetId != "Room"))
            {
                var b = VisibleBounds(p); var collider = p.GetComponent<BoxCollider>();
                if (collider == null) continue;
                var local = new Bounds(p.transform.InverseTransformPoint(b.min), Vector3.zero);
                for (int j = 0; j < 8; j++)
                {
                    var corner = new Vector3((j & 1) == 0 ? b.min.x : b.max.x, (j & 2) == 0 ? b.min.y : b.max.y, (j & 4) == 0 ? b.min.z : b.max.z);
                    local.Encapsulate(p.transform.InverseTransformPoint(corner));
                }
                collider.center = local.center; collider.size = local.size;
            }
            changes.meshReport = File.ReadAllText(Folder + "/mesh_processing_report.json"); changes.cameraPosition = camera.transform.position;
            File.WriteAllText(Folder + "/adjustments.json", JsonUtility.ToJson(changes, true));
            EditorUtility.SetDirty(glow);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            File.WriteAllText(Folder + "/apply_complete.txt", "Layout refined; real arched aperture; transparent shadow-free glass; toggleable rabbit light; roof glow; front elevated camera.\n");
            Debug.Log("[RoomPolish] Saved polished room, camera and lighting.");
        }
        static double testAt;
        static bool originalLampState;
        static readonly List<string> checks = new();
        static void FinishLighting()
        {
            var glow = Object.FindAnyObjectByType<SoftRoofGlow>();
            glow.strength = 1.5f; glow.variation = .08f;
            var stars = glow.target.sharedMaterials[1]; stars.SetColor("_EmissionColor", glow.color * glow.strength); EditorUtility.SetDirty(stars);
            stars.EnableKeyword("_EMISSION");
            stars.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            var lamp = Object.FindAnyObjectByType<WallLampItem>();
            foreach (var r in lamp.GetComponentsInChildren<Renderer>())
            {
                var material = new Material(r.sharedMaterial) { name = "Rabbit lamp textured emission" };
                material.EnableKeyword("_EMISSION"); material.SetTexture("_EmissionMap", material.GetTexture("_BaseMap"));
                material.SetColor("_EmissionColor", Color.black);
                AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(AssetFolder + "/RabbitLampEmission.mat"));
                r.sharedMaterial = material;
            }
            var fields = new SerializedObject(lamp); fields.FindProperty("glow").floatValue = .65f; fields.ApplyModifiedPropertiesWithoutUndo();
            lamp.GetComponentInChildren<Light>().shadowResolution = LightShadowResolution.Low;
            EditorUtility.SetDirty(glow);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            File.WriteAllText(Folder + "/lighting_finished.txt", "Textured lamp emission preserves face details; roof glow visible but subtle.\n");
        }
        static void PlayCheck()
        {
            try
            {
                int stage = SessionState.GetInt("RoomPolish.Stage", 0);
                if (stage == 0) { testAt = EditorApplication.timeSinceStartup; SessionState.SetInt("RoomPolish.Stage", 1); return; }
                if (EditorApplication.timeSinceStartup - testAt < 1.0) return;
                var app = CompanionRoomApp.Instance; var item = Object.FindAnyObjectByType<WallLampItem>(); var camera = Camera.main;
                if (app == null || item == null) throw new Exception("Room app or lamp interaction missing.");
                if (stage == 1)
                {
                    if (app.Companion != null) throw new Exception("Retired companion has reappeared.");
                    checks.Clear(); checks.Add("No old character; room UI initialized without a companion.");
                    // Preview in daylight without changing the user's saved clock/time preference.
                    app.Lighting.Apply(app.Lighting.Get(TimeOfDay.Morning));
                    var collider = Props().Single(a => a.assetId == "Room").GetComponentInChildren<MeshCollider>();
                    foreach (var p in new[] { new Vector3(-3, 1.48f, -.284f), new Vector3(-3, 1.48f, .176f), new Vector3(-3, 1.97f, -.284f), new Vector3(-3, 1.97f, .176f) })
                        if (collider.Raycast(new Ray(p, Vector3.right), out _, .9f)) throw new Exception("An opaque wall face still blocks the aperture.");
                    if (!collider.Raycast(new Ray(new Vector3(-3, 1.70f, -.804f), Vector3.right), out _, .9f)) throw new Exception("Wall outside the aperture is missing.");
                    checks.Add("Four aperture rays pass; surrounding wall still blocks a control ray.");
                    var glass = Props().Single(a => a.assetId == "Window").GetComponentInChildren<Renderer>().sharedMaterials[1];
                    if (glass.GetShaderPassEnabled("ShadowCaster")) throw new Exception("Transparent pane still casts an opaque shadow.");
                    checks.Add("Glass is transparent and does not cast an opaque pane shadow.");
                    var glow = Object.FindAnyObjectByType<SoftRoofGlow>();
                    if (glow == null || glow.target.sharedMaterials.Length != 2) throw new Exception("Roof glow missing.");
                    checks.Add("Roof ornament emission and gentle variation active.");
                    var picker = app.GetComponent<RoomInteractor>();
                    var hit = picker.PickAt(camera.WorldToScreenPoint(item.ModelBounds.center + Vector3.up * .04f));
                    if (hit != item) throw new Exception("Lamp is not pickable through the room camera.");
                    originalLampState = item.IsOn;
                    if (item.IsOn) item.Toggle(app);
                    SessionState.SetInt("RoomPolish.Stage", 2); testAt = EditorApplication.timeSinceStartup; return;
                }
                if (stage == 2)
                {
                    if (item.IsOn || item.GetComponentInChildren<Light>().enabled) throw new Exception("Lamp does not switch off.");
                    RenderTest(camera, Folder + "/Room_LampOff.png");
                    if (!app.GetComponent<RoomInteractor>().Activate(item)) throw new Exception("Lamp click interaction could not start.");
                    SessionState.SetInt("RoomPolish.Stage", 3); testAt = EditorApplication.timeSinceStartup; return;
                }
                if (stage == 3)
                {
                    if (!item.IsOn || !item.GetComponentInChildren<Light>().enabled) throw new Exception("Lamp does not switch on.");
                    if (app.IsBusy || app.Dialogue.IsOpen) throw new Exception("Lamp switch opened unwanted character dialogue or stayed busy.");
                    RenderTest(camera, Folder + "/Room_LampOn.png");
                    checks.Add("Camera click target activates lamp; off/on changes light and emission; no character dialogue.");
                    checks.Add("Front elevated camera: " + camera.transform.position + ", orthographic size " + camera.orthographicSize);
                    if (item.IsOn != originalLampState) item.Toggle(app);
                    File.WriteAllLines(Folder + "/play_validation.txt", checks.Concat(new[] { "PASS" }));
                    SessionState.SetBool("RoomPolish.PlayCheck", false); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception e)
            {
                File.WriteAllText(Folder + "/play_validation_failed.txt", e.ToString()); Debug.LogException(e);
                SessionState.SetBool("RoomPolish.PlayCheck", false); EditorApplication.isPlaying = false;
            }
        }
        static void RenderTest(Camera camera, string path)
        {
            var old = camera.targetTexture; var previous = RenderTexture.active;
            var rt = new RenderTexture(1500, 1200, 24); rt.Create(); camera.targetTexture = rt;
            camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(1500, 1200, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1500, 1200), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG()); camera.targetTexture = old; RenderTexture.active = previous;
            rt.Release(); Object.Destroy(image); Object.Destroy(rt);
        }
    }
}
