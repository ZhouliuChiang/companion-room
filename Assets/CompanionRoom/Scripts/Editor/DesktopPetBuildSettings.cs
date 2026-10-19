using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace CompanionRoom.EditorTools
{
    /// <summary>
    /// Settings the desktop-pet window needs, applied only while a Windows build runs: Windows only
    /// composites a window's rendered alpha from a Direct3D 11 bitblt swapchain, and URP only keeps the
    /// alpha through post-processing when the pipeline asset allows it (otherwise that shader variant is
    /// stripped). Everything is put back afterwards, so the editor keeps its own graphics setup.
    /// </summary>
    public sealed class DesktopPetBuildSettings : IDisposable
    {
        const BuildTarget Target = BuildTarget.StandaloneWindows64;
        const string AlphaOutput = "m_AllowPostProcessAlphaOutput";
        const string IconPath = "Assets/CompanionRoom/Art/Icon/AppIcon.png";

        readonly bool defaultApis;
        readonly Texture2D[] icons;
        readonly GraphicsDeviceType[] apis;
        readonly bool flipModel;
        readonly bool splash;
        readonly List<(SerializedObject asset, bool value)> pipelines = new();

        // The editor's own Build button (File > Build / Build Profiles) gets the same settings, so a
        // Windows build made from there also comes out see-through instead of on a black background.
        [InitializeOnLoadMethod]
        static void HookBuildButton() => BuildPlayerWindow.RegisterBuildPlayerHandler(options =>
        {
            if (options.target != Target)
            {
                BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
                return;
            }
            using (new DesktopPetBuildSettings())
                BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
        });

        public DesktopPetBuildSettings()
        {
            defaultApis = PlayerSettings.GetUseDefaultGraphicsAPIs(Target);
            apis = PlayerSettings.GetGraphicsAPIs(Target);
            flipModel = PlayerSettings.useFlipModelSwapchain;
            PlayerSettings.SetUseDefaultGraphicsAPIs(Target, false);
            PlayerSettings.SetGraphicsAPIs(Target, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.useFlipModelSwapchain = false;
            // A desktop pet just appears; no Unity splash in a box first.
            splash = PlayerSettings.SplashScreen.show;
            PlayerSettings.SplashScreen.show = false;
            // The exe's file and window icon.
            icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            else
                Debug.LogWarning($"[CompanionRoom] App icon not found at {IconPath}; the exe keeps Unity's icon.");

            foreach (var asset in PipelineAssets())
            {
                var so = new SerializedObject(asset);
                var property = so.FindProperty(AlphaOutput);
                if (property == null)
                    continue;
                pipelines.Add((so, property.boolValue));
                property.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public void Dispose()
        {
            PlayerSettings.SetGraphicsAPIs(Target, apis);
            PlayerSettings.SetUseDefaultGraphicsAPIs(Target, defaultApis);
            PlayerSettings.useFlipModelSwapchain = flipModel;
            PlayerSettings.SplashScreen.show = splash;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, icons, IconKind.Any);
            foreach (var (so, value) in pipelines)
            {
                so.Update();
                so.FindProperty(AlphaOutput).boolValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(so.targetObject);
            }
        }

        static IEnumerable<RenderPipelineAsset> PipelineAssets()
        {
            var seen = new HashSet<RenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline != null && seen.Add(GraphicsSettings.defaultRenderPipeline))
                yield return GraphicsSettings.defaultRenderPipeline;
            for (int i = 0; i < QualitySettings.count; i++)
            {
                var asset = QualitySettings.GetRenderPipelineAssetAt(i);
                if (asset != null && seen.Add(asset))
                    yield return asset;
            }
        }
    }
}
