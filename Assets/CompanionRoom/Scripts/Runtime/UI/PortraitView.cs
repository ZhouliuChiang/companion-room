using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CompanionRoom.UI
{
    /// <summary>
    /// The 立繪 beside the dialogue box. With a portrait sprite in the skin it shows that picture;
    /// otherwise a small camera films the live 3D companion head-on (only her layer, on a clear
    /// background) so she keeps moving while she talks.
    /// </summary>
    public class PortraitView : MonoBehaviour
    {
        [SerializeField] Vector2Int textureSize = new(752, 960);
        [Tooltip("How much of her height is framed, measured down from the top of her head (1 = whole body).")]
        [SerializeField, Range(0.3f, 1f)] float frameFromTop = 1f;
        [SerializeField] float fieldOfView = 18f;

        RawImage live;
        Image still;
        Camera portraitCamera;
        RenderTexture texture;
        RoomCompanion companion;

        public bool IsLive => companion != null;

        public void Init(RawImage liveImage, Image stillImage, Sprite stillSprite)
        {
            live = liveImage;
            still = stillImage;
            still.sprite = stillSprite;
            still.preserveAspect = true;
            live.enabled = false;
            still.enabled = false;
        }

        public void Show(RoomCompanion target)
        {
            if (still.sprite != null)
            {
                still.enabled = true;
                live.enabled = false;
                return;
            }
            companion = target;
            if (companion == null)
            {
                live.enabled = false;
                return;
            }
            EnsureCamera();
            int layer = companion.Layer;
            portraitCamera.cullingMask = layer >= 0 ? 1 << layer : ~0;
            portraitCamera.enabled = true;
            live.texture = texture;
            live.enabled = true;
            Frame();
        }

        public void Hide()
        {
            companion = null;
            if (portraitCamera != null)
                portraitCamera.enabled = false;
            if (live != null)
                live.enabled = false;
            if (still != null)
                still.enabled = false;
        }

        void OnDestroy()
        {
            if (portraitCamera != null)
                Destroy(portraitCamera.gameObject);
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }

        void LateUpdate()
        {
            if (companion != null)
                Frame();
        }

        void EnsureCamera()
        {
            if (portraitCamera != null)
                return;
            texture = new RenderTexture(textureSize.x, textureSize.y, 24, RenderTextureFormat.ARGB32)
            {
                name = "CompanionPortrait",
                antiAliasing = 4,
            };
            var go = new GameObject("PortraitCamera") { hideFlags = HideFlags.DontSave };
            portraitCamera = go.AddComponent<Camera>();
            portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            portraitCamera.backgroundColor = Color.clear;
            portraitCamera.allowHDR = false;
            portraitCamera.allowMSAA = true;
            portraitCamera.targetTexture = texture;
            portraitCamera.fieldOfView = fieldOfView;
            portraitCamera.nearClipPlane = 0.05f;
            portraitCamera.farClipPlane = 20f;
            portraitCamera.depth = -10f;
            var data = portraitCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = true;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
        }

        /// <summary>Frames her upper body head-on, whichever way she is facing.</summary>
        void Frame()
        {
            if (companion == null || portraitCamera == null)
                return;
            var bounds = companion.Bounds;
            float height = Mathf.Max(0.2f, bounds.size.y);
            var forward = Vector3.ProjectOnPlane(companion.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            float framed = height * frameFromTop;
            var center = new Vector3(bounds.center.x, bounds.max.y - framed * 0.5f + height * 0.02f, bounds.center.z);
            float distance = framed * 0.5f / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) + bounds.extents.z;
            var position = center + forward * distance + Vector3.up * height * 0.04f;
            portraitCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(center - position, Vector3.up));
            portraitCamera.aspect = textureSize.x / (float)textureSize.y;
        }
    }
}
