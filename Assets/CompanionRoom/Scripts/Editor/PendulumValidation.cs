using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CompanionRoom.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CompanionRoom.EditorTools
{
    [InitializeOnLoad]
    public static class PendulumValidation
    {
        static PendulumValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            if (SessionState.GetBool("Pendulum.FlowCheck", false)) { CheckFlow(); return; }
            if (File.Exists("Temp/PendulumFlow.request") && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete("Temp/PendulumFlow.request");
                SessionState.SetBool("Pendulum.FlowCheck", true);
                SessionState.SetInt("Pendulum.FlowStep", 0);
                SessionState.SetFloat("Pendulum.FlowTime", 0);
                EditorApplication.isPlaying = true;
                return;
            }
            const string request = "Temp/PendulumValidation.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.ReadAllText(request).Trim() == "flow")
            {
                File.Delete(request);
                SessionState.SetBool("Pendulum.FlowCheck", true);
                SessionState.SetInt("Pendulum.FlowStep", 0);
                SessionState.SetFloat("Pendulum.FlowTime", 0);
                EditorApplication.isPlaying = true;
                return;
            }
            File.Delete(request);
            var previous = SceneManager.GetActiveScene();
            var temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try { Run(); }
            catch (Exception e) { File.WriteAllText("Artifacts/Pendulum/validation-failed.txt", e.ToString()); Debug.LogException(e); }
            finally { RenderTexture.active = null; EditorSceneManager.CloseScene(temporary, true); SceneManager.SetActiveScene(previous); }
        }
        static void CheckFlow()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                var app = CompanionRoomApp.Instance;
                if (app == null || app.UI == null) return;
                var dialogue = app.Dialogue;
                int step = SessionState.GetInt("Pendulum.FlowStep", 0);
                float now = (float)EditorApplication.timeSinceStartup;
                float elapsed = now - SessionState.GetFloat("Pendulum.FlowTime", now);
                void Next() { SessionState.SetInt("Pendulum.FlowStep", step + 1); SessionState.SetFloat("Pendulum.FlowTime", now); }
                void Choose() => dialogue.Pick(0);
                if (step == 0)
                {
                    app.Interrupt();
                    if (!app.Interact(Object.FindAnyObjectByType<PendulumItem>())) throw new Exception("Scene pendulum cannot open.");
                    Next(); return;
                }
                if (elapsed > 20) throw new Exception("RPG flow timed out at step " + step + ": " + dialogue.Line);
                if (step == 1 && dialogue.IsChoosing && dialogue.Line == "校正是") { Choose(); Next(); }
                else if (step == 2 && dialogue.IsChoosing && dialogue.Line == "校正否")
                {
                    if (elapsed < 1.35f) throw new Exception("Yes calibration skipped animation.");
                    Choose(); Next();
                }
                else if (step == 3 && dialogue.IsChoosing && dialogue.Line == "心中默念問題")
                {
                    if (elapsed < 1.35f) throw new Exception("No calibration skipped animation.");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Artifacts/Pendulum/RPG_screen.png"));
                    Choose(); Next();
                }
                else if (step == 4 && dialogue.IsChoosing && dialogue.ChoiceCount == 2)
                {
                    if (elapsed < 4.9f) throw new Exception("Reading skipped animation.");
                    app.Interrupt();
                    if (app.UI.Pendulum.IsOpen || dialogue.IsOpen || app.IsBusy) throw new Exception("Exit left a modal open.");
                    File.WriteAllText("Artifacts/Pendulum/flow-validation.txt", "PASS: scene click entry; 校正是 → 校正否 → 心中默念問題; right-hand choices; 1.4-second calibrations; 5-second reading; clean exit.\n");
                    SessionState.SetBool("Pendulum.FlowCheck", false);
                    EditorApplication.isPlaying = false;
                }
            }
            catch (Exception e)
            {
                File.WriteAllText("Artifacts/Pendulum/flow-validation-failed.txt", e.ToString());
                SessionState.SetBool("Pendulum.FlowCheck", false);
                EditorApplication.isPlaying = false;
                Debug.LogException(e);
            }
        }
        public static void Run()
        {
            const string folder = "Artifacts/Pendulum";
            Directory.CreateDirectory(folder);
            var app = new GameObject("PreviewApp").AddComponent<CompanionRoomApp>();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CompanionRoom/Prefabs/UserModels/靈擺.prefab");
            var source = Object.Instantiate(asset);
            var item = source.AddComponent<PendulumItem>();
            var skin = AssetDatabase.LoadAssetAtPath<RoomUISkin>("Assets/CompanionRoom/Settings/RoomUISkin.asset").Resolve();
            var root = new GameObject("PreviewUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");
            var canvas = root.GetComponent<Canvas>();
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var panel = PendulumPanel.Build(root.transform, app, skin);
            panel.Open(item);
            panel.GetComponent<CanvasGroup>().alpha = 1;
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var dialogue = DialogueBox.Build(root.transform, app, skin, "");
            dialogue.PortraitEnabled = false;
            dialogue.Show("校正是");
            dialogue.GetComponent<CanvasGroup>().alpha = 1;
            ((Text)typeof(DialogueBox).GetField("lineText", flags).GetValue(dialogue)).text = "校正是";
            typeof(DialogueBox).GetMethod("SetChoices", flags).Invoke(dialogue, new object[] { new[] { "開始校正" } });
            if (root.GetComponentsInChildren<InputField>(true).Length != 0) throw new Exception("Question input is still present.");
            var swing = (Transform)typeof(PendulumPanel).GetField("swing", flags).GetValue(panel);
            var previewCamera = (Camera)typeof(PendulumPanel).GetField("previewCamera", flags).GetValue(panel);
            var skinned = swing.parent.GetComponentInChildren<SkinnedMeshRenderer>();
            var lightingBlock = new MaterialPropertyBlock();
            skinned.GetPropertyBlock(lightingBlock);
            if (lightingBlock.GetFloat("_PreviewLighting") != 1f) throw new Exception("Presentation fill is missing.");
            if (skinned.sharedMaterials.Any(m => m.GetFloat("_PreviewLighting") != 0f)) throw new Exception("Presentation fill changed shared room materials.");
            var crystal = skinned.sharedMaterials.Single(m => m.shader.name == "CompanionRoom/PendulumGem");
            if (crystal.GetFloat("_Opacity") >= .3f || crystal.GetFloat("_Iridescence") <= 0) throw new Exception("Transparent iridescent crystal finish is missing.");
            foreach (var material in skinned.sharedMaterials)
                if (ShaderUtil.GetShaderMessages(material.shader).Any(m => m.severity.ToString() == "Error")) throw new Exception("Pendulum shader compile error.");
            var fixedPart = skinned.bones.Single(b => b.name == "FixedFrame");
            var baked = new Mesh();
            skinned.BakeMesh(baked);
            var resting = baked.vertices;
            var weights = skinned.sharedMesh.boneWeights;
            int movingBone = Array.FindIndex(skinned.bones, b => b.name == "SwingingAssembly");
            float MovingWeight(BoneWeight w) => (w.boneIndex0 == movingBone ? w.weight0 : 0) + (w.boneIndex1 == movingBone ? w.weight1 : 0) + (w.boneIndex2 == movingBone ? w.weight2 : 0) + (w.boneIndex3 == movingBone ? w.weight3 : 0);
            var fixedMatrix = fixedPart.localToWorldMatrix;
            float maxMovement = 0;
            var image = root.GetComponentsInChildren<RawImage>().Single(r => r.name == "ModelPreview");
            var uiCamera = new GameObject("CaptureCamera", typeof(Camera)).GetComponent<Camera>();
            uiCamera.transform.position = new Vector3(40000, 40000, -40000);
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = new Color(.12f, .09f, .16f);
            uiCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            uiCamera.orthographic = true;
            uiCamera.orthographicSize = 540;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = uiCamera;
            ((RectTransform)root.transform).sizeDelta = new Vector2(1920, 1080);
            root.transform.position = uiCamera.transform.position + Vector3.forward * 10;
            root.transform.localScale = Vector3.one;
            var target = new RenderTexture(1920, 1080, 24);
            uiCamera.targetTexture = target;
            foreach (int angle in new[] { 0, -14, 14 })
            {
                swing.localRotation = Quaternion.Euler(0, 0, angle);
                Canvas.ForceUpdateCanvases();
                foreach (var graphic in root.GetComponentsInChildren<Graphic>()) graphic.Rebuild(CanvasUpdate.PreRender);
                previewCamera.Render();
                RenderTexture.active = previewCamera.targetTexture;
                var modelImage = new Texture2D(840, 798, TextureFormat.RGB24, false);
                modelImage.ReadPixels(new Rect(0, 0, 840, 798), 0, 0);
                modelImage.Apply();
                File.WriteAllBytes($"{folder}/Model_{angle}.png", modelImage.EncodeToPNG());
                Object.DestroyImmediate(modelImage);
                uiCamera.Render();
                RenderTexture.active = target;
                var screenshot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                screenshot.Apply();
                File.WriteAllBytes($"{folder}/UI_{angle}.png", screenshot.EncodeToPNG());
                Object.DestroyImmediate(screenshot);
                if (fixedPart.localToWorldMatrix != fixedMatrix) throw new Exception("Frame moved with pendant.");
                skinned.BakeMesh(baked);
                var deformed = baked.vertices;
                if (angle == 0) resting = deformed;
                for (int i = 0; i < resting.Length; i += 97)
                {
                    float delta = Vector3.Distance(resting[i], deformed[i]);
                    maxMovement = Mathf.Max(maxMovement, delta);
                    if (MovingWeight(weights[i]) == 0 && delta > .0001f) throw new Exception("Fixed vertex moved: " + delta);
                }
            }
            if (maxMovement < .05f) throw new Exception("Pendant did not move.");
            Object.DestroyImmediate(baked);
            var rect = image.rectTransform;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var center = uiCamera.WorldToViewportPoint((corners[0] + corners[2]) * .5f);
            if (Vector2.Distance(new Vector2(center.x, center.y), Vector2.one * .5f) > .005f) throw new Exception("Model view is not centered.");
            File.WriteAllText(folder + "/validation.txt", "PASS: continuous skinned model; preview centered; fixed frame and outer vertices unchanged at -14/0/+14 degrees; suspended assembly moves.\n");
            if (File.Exists(folder + "/validation-failed.txt")) File.Delete(folder + "/validation-failed.txt");
            RenderTexture.active = null;
            uiCamera.targetTexture = null;
            target.Release(); Object.DestroyImmediate(target);
        }
    }
}

