using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CompanionRoom.Tarot;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CompanionRoom.EditorTools
{
    /// <summary>Windows build, preview pictures and a scene check for the 凡安 room.</summary>
    public static class CompanionRoomTools
    {
        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string PlayerPath => Path.Combine(ProjectRoot, "Builds", "CompanionRoom", "CompanionRoom.exe");

        // ------------------------------------------------------------------ build

        [MenuItem("Tools/兔兔房間/2. 輸出 Windows 版 (Build Player)", priority = 2)]
        static void BuildPlayerMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (BuildPlayer(PlayerPath))
                EditorUtility.RevealInFinder(PlayerPath);
        }

        public static void BuildPlayerFromCommandLine()
        {
            if (!BuildPlayer(PlayerPath))
                EditorApplication.Exit(1);
        }

        /// <summary>Rebuilds the scene, then the player (one batch-mode launch).</summary>
        public static void BuildAllFromCommandLine()
        {
            try
            {
                CompanionRoomBuilder.Build();
                if (!BuildPlayer(PlayerPath))
                    EditorApplication.Exit(1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static bool BuildPlayer(string path)
        {
            if (!File.Exists(CompanionRoomBuilder.ScenePath))
                CompanionRoomBuilder.Build();
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            BuildReport report;
            // The transparent desktop-pet window needs D3D11 and URP alpha output, for the build only.
            using (new DesktopPetBuildSettings())
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { CompanionRoomBuilder.ScenePath },
                    locationPathName = path,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
            Debug.Log($"[CompanionRoom] Build {report.summary.result}: {path} ({report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors)");
            return report.summary.result == BuildResult.Succeeded;
        }

        // ------------------------------------------------------------------ previews

        [MenuItem("Tools/兔兔房間/3. 拍攝預覽圖 (Capture Previews)", priority = 3)]
        static void CaptureMenu()
        {
            // Capturing opens the room scene, so offer to save whatever is open first.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var folder = Path.Combine(ProjectRoot, "Docs", "Preview");
            CapturePreviews(folder);
            EditorUtility.RevealInFinder(folder);
        }

        /// <summary>-executeMethod ...CapturePreviewsFromCommandLine -previewOut &lt;folder&gt;</summary>
        public static void CapturePreviewsFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-previewOut");
            var folder = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(ProjectRoot, "Docs", "Preview");
            try
            {
                CapturePreviews(folder);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Renders the room camera in each time of day (lamp lit at night) to PNGs.</summary>
        public static void CapturePreviews(string folder)
        {
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(CompanionRoomBuilder.ScenePath, OpenSceneMode.Single);
            var camera = Camera.main;
            var lighting = UnityEngine.Object.FindAnyObjectByType<RoomLighting>();
            var lamp = UnityEngine.Object.FindAnyObjectByType<WallLampItem>();
            var lampLight = lamp != null ? lamp.GetComponentInChildren<Light>(true) : null;
            var halo = lamp != null ? lamp.transform.Find("Halo")?.GetComponent<Renderer>() : null;
            if (camera == null || lighting == null)
                throw new InvalidOperationException("Open the room scene first (camera or RoomLighting missing).");

            // Off-screen renders outside the player loop can reuse one material's constants for a whole
            // SRP batch; drawing without the batcher keeps every material's own colors.
            bool batching = GraphicsSettings.useScriptableRenderPipelineBatching;
            GraphicsSettings.useScriptableRenderPipelineBatching = false;
            try
            {
                RenderMoods(folder, camera, lighting, lampLight, halo);
            }
            finally
            {
                GraphicsSettings.useScriptableRenderPipelineBatching = batching;
            }
            Debug.Log("[CompanionRoom] Previews saved to " + folder);
        }

        static void RenderMoods(string folder, Camera camera, RoomLighting lighting, Light lampLight, Renderer halo)
        {
            foreach (TimeOfDay time in Enum.GetValues(typeof(TimeOfDay)))
            {
                lighting.Apply(lighting.Get(time));
                bool lampOn = time == TimeOfDay.Night;
                if (lampLight != null)
                {
                    lampLight.enabled = lampOn;
                    lampLight.intensity = lampOn ? 2.4f : 0f;
                }
                if (halo != null)
                {
                    halo.enabled = lampOn;
                    var block = new MaterialPropertyBlock();
                    block.SetFloat("_Intensity", 0.45f);
                    halo.SetPropertyBlock(block);
                }
                Render(camera, Path.Combine(folder, $"room_{RoomLighting.Key(time)}.png"), 1920, 1080);
            }
            lighting.Apply(lighting.Get(TimeOfDay.Morning));
            if (lampLight != null)
                lampLight.enabled = false;
            if (halo != null)
                halo.enabled = false;
        }

        static void Render(Camera camera, string path, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24) { msaaSamples = 4, sRGB = true });
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = null;
            }
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            RenderTexture.ReleaseTemporary(rt);
        }

        // ------------------------------------------------------------------ validation

        [MenuItem("Tools/兔兔房間/4. 檢查場景 (Validate)", priority = 4)]
        static void ValidateMenu()
        {
            var report = Validate(out int problems);
            Debug.Log(report);
            EditorUtility.DisplayDialog("兔兔房間：檢查結果", problems == 0 ? "全部正常。\n（詳細內容在 Console）" : $"發現 {problems} 個問題，詳細內容在 Console。", "好");
        }

        public static void ValidateFromCommandLine()
        {
            EditorSceneManager.OpenScene(CompanionRoomBuilder.ScenePath, OpenSceneMode.Single);
            var report = Validate(out int problems);
            Debug.Log(report);
            File.WriteAllText(Path.Combine(ProjectRoot, "Logs", "CompanionRoomValidation.txt"), report);
            if (problems > 0)
                EditorApplication.Exit(2);
        }

        /// <summary>Checks the open scene: each item's model, click area and bindings, plus the data files.</summary>
        public static string Validate(out int problems)
        {
            var text = new StringBuilder("兔兔房間 scene check\n");
            int count = 0;
            void Problem(string message)
            {
                count++;
                text.AppendLine("  ✗ " + message);
            }
            void Ok(string message) => text.AppendLine("  ✓ " + message);

            var scene = EditorSceneManager.GetActiveScene();
            var items = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RoomItem>(true)).ToList();
            var expected = new[] { typeof(PhotoFrameItem), typeof(ComputerItem), typeof(WallLampItem), typeof(WindowItem), typeof(TarotDeckItem), typeof(RecordCabinetItem), typeof(CompanionTalkItem) };
            foreach (var type in expected)
                if (!items.Any(i => i.GetType() == type))
                    Problem($"missing {type.Name}");

            foreach (var item in items)
            {
                text.AppendLine($"[{item.DisplayName}] {item.name} ({item.GetType().Name})");
                var model = item.transform.Find(RoomItem.ModelChild);
                if (model == null)
                    Problem("no \"Model\" child");
                var renderers = item.ModelRoot.GetComponentsInChildren<Renderer>(true).Where(r => r.GetComponentInParent<RoomFx>(true) == null).ToArray();
                if (renderers.Length == 0)
                    Problem("model has no renderers");
                else
                    Ok($"{renderers.Length} renderer(s), size {item.ModelBounds.size.ToString("0.00")} m");
                if (item.GetComponentsInChildren<Collider>(true).Length == 0)
                    Problem("no collider (use the component menu: 重新計算點擊範圍)");
                var placeholder = item.ModelRoot.Find("Placeholder");
                if (placeholder != null && placeholder.gameObject.activeSelf && model != null && model.childCount > 1)
                    Problem("both the placeholder and a new model are active — disable or delete Model/Placeholder");

                switch (item)
                {
                    case WallLampItem lamp:
                        Check(lamp, "lamp", "a Light", () => lamp.GetComponentInChildren<Light>(true) != null);
                        Check(lamp, "glowParts", "a glowing part named \"Bulb\"/\"Shade\"", () => Named(item, "Bulb", "Shade"));
                        break;
                    case ComputerItem computer:
                        Check(computer, "screen", "a renderer named \"Screen\"", () => Named(item, "Screen"));
                        break;
                    case PhotoFrameItem photo:
                        Check(photo, "photoSurface", "a renderer named \"Photo\"", () => Named(item, "Photo"));
                        break;
                    case RecordCabinetItem cabinet:
                        if (Field(cabinet, "door") == null && !Named(item, "Door"))
                            text.AppendLine("  · no door (optional): name the door's hinge pivot \"Door\" to animate it");
                        break;
                    case CompanionTalkItem:
                        var companion = item.GetComponent<RoomCompanion>();
                        if (companion == null)
                            Problem("RoomCompanion missing next to CompanionTalkItem");
                        else if (item.GetComponentInChildren<Animator>(true) == null)
                            text.AppendLine("  · no Animator: 凡安 will stand still (fine for a static model)");
                        break;
                }

                void Check(UnityEngine.Object component, string field, string what, Func<bool> fallback)
                {
                    var value = Field(component, field);
                    if (value != null || fallback())
                        Ok(field + " bound");
                    else
                        Problem($"{field}: assign it in the inspector or add {what}");
                }
            }

            // Data files.
            var lines = DialogueLibrary.Load();
            var needed = new[] { "greet", "chat", "photo", "photo_empty", "photo_after", "computer", "computer_after", "lamp_on", "lamp_on_night", "lamp_off",
                "window_morning", "window_sunset", "window_night", "tarot_intro", "tarot_again", "tarot_cancel", "tarot_question", "tarot_shuffle",
                "tarot_click_first", "tarot_click_next", "tarot_reveal", "tarot_reveal_single", "tarot_summary_three", "tarot_closing", "tarot_more", "tarot_end",
                "records", "records_empty", "records_after" };
            var missing = needed.Where(k => !lines.Has(k)).ToList();
            if (missing.Count > 0)
                Problem("dialogue topics missing: " + string.Join(", ", missing));
            else
                Ok($"dialogue: {lines.Keys.Count()} topics, speaker {lines.Speaker}");
            var deck = TarotDeck.Load();
            if (deck.Cards.Count < 3)
                Problem($"tarot deck has only {deck.Cards.Count} cards");
            else
                Ok($"tarot deck: {deck.Cards.Count} cards ({deck.Cards.Count(c => deck.ArtFor(c) != null)} with art)");
            if (Resources.Load<Material>(HoverOutline.MaterialResource) == null)
                Problem("outline material missing: Resources/" + HoverOutline.MaterialResource);
            if (LayerMask.NameToLayer(RoomCompanion.LayerName) < 0)
                Problem("layer \"Companion\" missing (rebuild the scene once to add it)");

            text.AppendLine(count == 0 ? "RESULT: OK" : $"RESULT: {count} problem(s)");
            problems = count;
            return text.ToString();
        }

        static UnityEngine.Object Field(UnityEngine.Object component, string field)
        {
            var property = new SerializedObject(component).FindProperty(field);
            if (property == null)
                return null;
            if (property.isArray)
                return property.arraySize > 0 ? property.GetArrayElementAtIndex(0).objectReferenceValue : null;
            return property.objectReferenceValue;
        }

        static bool Named(RoomItem item, params string[] names) =>
            item.ModelRoot.GetComponentsInChildren<Transform>(true).Any(t => names.Any(n => t.name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0));
    }
}
