using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ChibiSetup
{
    private const string Folder = "Assets/ChibiCompanion";
    [MenuItem("Chibi/Build Demo Scene")]
    public static void Build()
    {
        string path = Folder + "/ChibiCompanion.fbx";
        AssetDatabase.Refresh();
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.isReadable = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            string name = new[] { "Idle", "Wave", "Walk", "Run" }.FirstOrDefault(n => clip.name.EndsWith(n, StringComparison.OrdinalIgnoreCase));
            if (name == null) continue;
            clip.name = name;
            clip.loopTime = true;
            clip.loopPose = false;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        var motions = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
        foreach (string n in new[] { "Idle", "Wave", "Walk", "Run" })
            if (!motions.Any(c => c.name == n)) throw new Exception("Missing motion: " + n + " / " + string.Join(",", motions.Select(c => c.name)));
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Lavender.mat");
        if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, Folder + "/Lavender.mat"); }
        material.color = new Color(.56f, .38f, .73f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .25f);
        string controllerPath = Folder + "/ChibiActions.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
        var machine = controller.layers[0].stateMachine;
        foreach (var state in machine.states) machine.RemoveState(state.state);
        foreach (string n in new[] { "Idle", "Wave", "Walk", "Run" })
        {
            var state = machine.AddState(n); state.motion = motions.First(c => c.name == n);
            if (n == "Idle") machine.defaultState = state;
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        var root = new GameObject("Chibi Companion");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        model.transform.SetParent(root.transform, false);
        var animator = model.GetComponent<Animator>();
        if (!animator) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
            r.updateWhenOffscreen = true;
        }
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "/ChibiCompanion.prefab");
        var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); camObj.tag = "MainCamera";
        var cam = camObj.GetComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = .70f;
        cam.backgroundColor = new Color(.13f,.15f,.21f); cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0,.55f,2.5f); cam.transform.LookAt(new Vector3(0,.50f,0));
        var lightObj = new GameObject("Key Light", typeof(Light)); var light = lightObj.GetComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.8f; light.transform.rotation = Quaternion.Euler(35,150,0);
        RenderSettings.ambientLight = new Color(.65f,.65f,.7f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ground.name = "Display Platform";
        ground.transform.position = new Vector3(0,-.035f,0); ground.transform.localScale = new Vector3(1.15f,.025f,1.15f);
        var stageMat = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Stage.mat");
        if (!stageMat) { stageMat = new Material(shader); AssetDatabase.CreateAsset(stageMat, Folder + "/Stage.mat"); }
        stageMat.color = new Color(.22f,.25f,.32f); ground.GetComponent<Renderer>().sharedMaterial = stageMat;
        var demo = new GameObject("Demo Controls").AddComponent<ChibiDemo>(); demo.character = animator; demo.displayRoot = root.transform; demo.viewCamera = cam;
        string scenePath = Folder + "/ChibiDemo.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) }.Concat(EditorBuildSettings.scenes.Where(s => s.path != scenePath)).ToArray();
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(machine); EditorUtility.SetDirty(material); EditorUtility.SetDirty(stageMat);
        AssetDatabase.SaveAssets();
        // Sample every imported clip and verify that the rig actually deforms.
        var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
        var baked = new Mesh();
        string report = "Generic rig. No humanoid retargeting.\n" + "Renderer bounds: "+skin.bounds+" / scale: "+skin.transform.lossyScale+"\n";
        report += "Hierarchy: " + string.Join(";",model.GetComponentsInChildren<Transform>().Select(t=>AnimationUtility.CalculateTransformPath(t,model.transform)))+"\n";
        foreach (var clip in motions)
        {
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            for (int f=0; f<=30; f++)
            {
                clip.SampleAnimation(model, clip.length*f/30f); skin.BakeMesh(baked, false);
                foreach (var v in baked.vertices) { float y=(skin.transform.rotation*v).y; minY=Mathf.Min(minY,y); maxY=Mathf.Max(maxY,y); }
            }
            report += $"{clip.name}: {clip.length:F3}s, {clip.frameRate}fps, baked pose height range {minY:F4}..{maxY:F4}\n";
            report += "Bindings: "+string.Join(";",AnimationUtility.GetCurveBindings(clip).Take(8).Select(b=>b.path+":"+b.propertyName))+"\n";
        }
        motions.First(c=>c.name=="Idle").SampleAnimation(model,0);
        UnityEngine.Object.DestroyImmediate(baked);
        EditorSceneManager.SaveScene(scene, scenePath);
        File.WriteAllText("Artifacts/unity_validation.txt", report);
        Debug.Log("CHIBI_SETUP_OK\n" + report);
    }
    public static void Capture()
    {
        EditorSceneManager.OpenScene(Folder + "/ChibiDemo.unity", OpenSceneMode.Single);
        var demo = UnityEngine.Object.FindAnyObjectByType<ChibiDemo>();
        var clips = AssetDatabase.LoadAllAssetsAtPath(Folder+"/ChibiCompanion.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
        var camera = demo.viewCamera;
        var target = new RenderTexture(900,900,24);
        target.Create();
        demo.character.Rebind();
        var skin=demo.character.GetComponentInChildren<SkinnedMeshRenderer>();
        var preview=new GameObject("Baked Preview",typeof(MeshFilter),typeof(MeshRenderer));
        preview.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);
        preview.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
        var poseMesh=new Mesh(); preview.GetComponent<MeshFilter>().sharedMesh=poseMesh;
        foreach(var clip in clips)
        {
            demo.character.Play(clip.name,0,.3f); demo.character.Update(0);
            skin.BakeMesh(poseMesh,false); skin.enabled=false;
            Debug.Log("POSE "+clip.name+" "+string.Join(";",demo.character.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("UpperArm")).Select(t=>t.name+":"+t.localRotation)));
            foreach(int side in new[]{-1,1})
            {
                camera.transform.position = new Vector3(0,.55f,side*2.5f); camera.transform.LookAt(new Vector3(0,.5f,0));
                var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target};
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
                var previous=RenderTexture.active; RenderTexture.active=target;
                var texture = new Texture2D(900,900,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,900,900),0,0); texture.Apply();
                File.WriteAllBytes("Artifacts/Unity_"+clip.name+"_"+side+".png",texture.EncodeToPNG());
                RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        target.Release(); UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(preview); UnityEngine.Object.DestroyImmediate(poseMesh);
        Debug.Log("CHIBI_CAPTURE_OK");
    }
    public static void BuildAndCapture() { Build(); Capture(); }
}
