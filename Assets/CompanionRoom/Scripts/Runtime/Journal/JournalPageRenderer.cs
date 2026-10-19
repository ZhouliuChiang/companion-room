using System;
using CompanionRoom.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CompanionRoom.Journal
{
    /// <summary>
    /// Bakes uGUI layouts (journal pages, covers, cards) into textures for the 3D book: the layout is
    /// built on a world-space canvas far below the room, an orthographic camera that sees nothing else
    /// renders it once into a RenderTexture, and the layout is thrown away again.
    /// </summary>
    sealed class JournalPageRenderer : IDisposable
    {
        const float UnitsPerPixel = 0.001f;

        readonly GameObject rig;
        readonly Camera camera;
        readonly RectTransform canvasRect;

        public JournalPageRenderer()
        {
            int layer = LayerMask.NameToLayer("UI");
            rig = new GameObject("JournalPageRenderer") { hideFlags = HideFlags.DontSave };
            rig.transform.position = new Vector3(0f, -800f, 0f);

            var cameraObject = new GameObject("PageCamera");
            cameraObject.transform.SetParent(rig.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -1f);
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.cullingMask = 1 << layer;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 4f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;

            var canvasObject = new GameObject("PageCanvas", typeof(RectTransform)) { layer = layer };
            canvasObject.transform.SetParent(rig.transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.localScale = Vector3.one * UnitsPerPixel;
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>A texture to bake into: sRGB with mipmaps, so it stays crisp while the book is small.</summary>
        public static RenderTexture NewTarget(int width, int height, string name)
        {
            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24)
            {
                sRGB = true,
                useMipMap = true,
                autoGenerateMips = true,
                msaaSamples = 1,
            };
            var texture = new RenderTexture(descriptor)
            {
                name = name,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.Create();
            return texture;
        }

        /// <summary>
        /// Builds a layout of the target's size in pixels (origin at the top-left corner, children of
        /// <c>page</c> may use any anchors) and renders it over <paramref name="background"/>.
        /// </summary>
        public void Render(RenderTexture target, Color background, Action<RectTransform> build)
        {
            canvasRect.sizeDelta = new Vector2(target.width, target.height);
            camera.orthographicSize = target.height * UnitsPerPixel * 0.5f;
            camera.aspect = target.width / (float)target.height;
            camera.backgroundColor = background;
            camera.targetTexture = target;

            var page = UIFactory.Rect("Page", canvasRect).Stretch();
            try
            {
                build(page);
                Canvas.ForceUpdateCanvases();
                Submit(target);
            }
            finally
            {
                page.gameObject.SetActive(false);
                Object.Destroy(page.gameObject);
                camera.targetTexture = null;
            }
        }

        void Submit(RenderTexture target)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
                camera.Render();
        }

        public void Dispose()
        {
            if (rig != null)
                Object.Destroy(rig);
        }
    }
}
