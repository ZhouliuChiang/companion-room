using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace CompanionRoom.UI
{
    /// <summary>The centered model stage; the shared RPG dialogue drives the session.</summary>
    public class PendulumPanel : RoomPanel
    {
        RawImage image;
        Transform preview, swing;
        Camera previewCamera;
        RenderTexture texture;
        float amplitude, phase;
        int motion;

        public static PendulumPanel Build(Transform parent, CompanionRoomApp app, RoomUISkin skin)
        {
            var root = UIFactory.Rect("PendulumStage", parent).Stretch();
            var panel = root.gameObject.AddComponent<PendulumPanel>();
            var backdrop = UIFactory.Image("QuietRoom", root, null, new Color(.035f, .025f, .055f, .92f), true);
            backdrop.rectTransform.Stretch();
            var view = UIFactory.Rect("ModelPreview", root);
            view.Place(Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(800, 760));
            panel.image = view.gameObject.AddComponent<RawImage>();
            panel.image.raycastTarget = false;
            panel.image.material = Resources.Load<Material>("Pendulum/PendulumComposite");
            panel.InitPanel(app, skin, null);
            return panel;
        }

        public void Open(PendulumItem item)
        {
            ReleasePreview();
            BuildPreview(item);
            Show();
            ResetSwing();
        }

        void BuildPreview(PendulumItem item)
        {
            // Continuous skinning preserves the original mesh across the mounting joint.
            preview = new GameObject("PendulumPreview").transform;
            preview.position = new Vector3(18000, 18000, 18000);
            swing = new GameObject("SuspensionPoint").transform;
            swing.SetParent(preview, false);
            var rigAsset = Resources.Load<GameObject>("Pendulum/PendulumRig");
            if (rigAsset == null)
            {
                Debug.LogError("Missing Pendulum/PendulumRig: reimport the split pendulum FBX.");
                return;
            }
            var rig = Instantiate(rigAsset, preview, false);
            var renderers = rig.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            rig.transform.localScale *= 2f / Mathf.Max(.001f, bounds.size.y);
            // Keep the imported metal and crystal material slots, including transparency.
            // Per-renderer fill lights only this copy; shared room materials stay untouched.
            var presentationLight = new MaterialPropertyBlock();
            int presentationLightId = Shader.PropertyToID("_PreviewLighting");
            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(presentationLight);
                presentationLight.SetFloat(presentationLightId, 1f);
                renderer.SetPropertyBlock(presentationLight);
            }
            foreach (var bone in rig.GetComponentsInChildren<Transform>())
                if (bone.name == "SwingingAssembly") { bone.SetParent(swing, true); break; }
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var cameraObject = new GameObject("PendulumPreviewCamera", typeof(Camera));
            cameraObject.transform.SetParent(preview, false);
            cameraObject.transform.position = bounds.center + Vector3.back * 5;
            previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 1.5f;
            previewCamera.nearClipPlane = .1f;
            previewCamera.farClipPlane = 10;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.clear;
            previewCamera.GetUniversalAdditionalCameraData().requiresColorTexture = true;
            texture = new RenderTexture(840, 798, 24);
            previewCamera.targetTexture = texture;
            image.texture = texture;
        }

        public IEnumerator PlaySignal(int direction, float duration)
        {
            motion = direction;
            phase = 0;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                amplitude = Mathf.SmoothStep(0, 1, Mathf.Min(t / .3f, (duration - t) / .4f));
                yield return null;
            }
            ResetSwing();
        }

        public void ResetSwing()
        {
            amplitude = phase = 0;
            if (swing != null) swing.localRotation = Quaternion.identity;
        }

        protected override void Update()
        {
            base.Update();
            phase += Time.unscaledDeltaTime * 5f;
            if (swing != null)
            {
                float angle = Mathf.Sin(phase) * amplitude * 14;
                swing.localRotation = Quaternion.Euler(motion == 1 ? angle : 0, 0, motion == 0 ? angle : 0);
            }
        }

        protected override void OnHidden() { ResetSwing(); ReleasePreview(); }
        void OnDestroy() => ReleasePreview();
        static void ReleaseObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
        void ReleasePreview()
        {
            if (preview != null) ReleaseObject(preview.gameObject);
            preview = swing = null;
            if (image != null) image.texture = null;
            if (texture != null) { texture.Release(); ReleaseObject(texture); texture = null; }
        }
    }
}
