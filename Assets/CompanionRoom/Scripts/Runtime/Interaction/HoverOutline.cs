using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CompanionRoom
{
    /// <summary>
    /// Hover glow for any model: builds an inverted-hull "shell" next to each of the item's renderers
    /// (skinned ones included) with the CompanionRoom/HoverOutline material, and fades its width in
    /// and out. Readable meshes get smoothed normals so hard edges don't crack the outline; for
    /// imported models, tick Read/Write in the import settings for the cleanest result.
    /// </summary>
    [DisallowMultipleComponent]
    public class HoverOutline : MonoBehaviour
    {
        public const string MaterialResource = "CompanionRoom/M_HoverOutline";
        const string ShellName = "__HoverOutline";

        static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");
        static readonly Dictionary<Mesh, Mesh> SmoothedMeshes = new();
        static Material sharedMaterial;

        [Tooltip("HDR: values above 1 make the outline bloom (and look thicker).")]
        [SerializeField, ColorUsage(false, true)] Color color = new(1f, 0.84f, 0.5f, 1f);
        [Tooltip("Outline width in pixels at 1080p.")]
        [SerializeField] float width = 1.6f;
        [SerializeField] float fadeSeconds = 0.12f;

        readonly List<Renderer> shells = new();
        MaterialPropertyBlock block;
        float amount;
        float target;
        bool built;

        public bool IsShell(Renderer r) => r != null && r.gameObject.name == ShellName;

        public static bool IsShellObject(GameObject go) => go != null && go.name == ShellName;

        public void SetColor(Color value) => color = value;

        /// <summary>Builds the shells now, so a heavy model doesn't hitch on its first hover.</summary>
        public void Prepare()
        {
            if (!built)
                Build();
        }

        public void SetHighlighted(bool on)
        {
            if (on && !built)
                Build();
            target = on ? 1f : 0f;
            enabled = true;
        }

        /// <summary>Drops the shells (call after swapping the model; they rebuild on the next hover).</summary>
        public void Rebuild()
        {
            foreach (var shell in shells)
                if (shell != null)
                    Destroy(shell.gameObject);
            shells.Clear();
            built = false;
        }

        void Build()
        {
            built = true;
            if (sharedMaterial == null)
                sharedMaterial = Resources.Load<Material>(MaterialResource);
            if (sharedMaterial == null)
            {
                Debug.LogWarning($"[CompanionRoom] Missing outline material Resources/{MaterialResource}");
                return;
            }
            foreach (var source in GetComponentsInChildren<Renderer>(true))
            {
                if (IsShell(source) || source.GetComponentInParent<RoomFx>(true) != null)
                    continue;
                if (source is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                {
                    var go = new GameObject(ShellName) { layer = source.gameObject.layer };
                    go.transform.SetParent(skinned.transform.parent, false);
                    go.transform.SetLocalPositionAndRotation(skinned.transform.localPosition, skinned.transform.localRotation);
                    go.transform.localScale = skinned.transform.localScale;
                    var shell = go.AddComponent<SkinnedMeshRenderer>();
                    shell.sharedMesh = Smoothed(skinned.sharedMesh);
                    shell.bones = skinned.bones;
                    shell.rootBone = skinned.rootBone;
                    shell.localBounds = skinned.localBounds;
                    shell.updateWhenOffscreen = skinned.updateWhenOffscreen;
                    Setup(shell, skinned.sharedMesh.subMeshCount);
                }
                else if (source is MeshRenderer && source.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                {
                    var go = new GameObject(ShellName) { layer = source.gameObject.layer };
                    go.transform.SetParent(source.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = Smoothed(filter.sharedMesh);
                    Setup(go.AddComponent<MeshRenderer>(), filter.sharedMesh.subMeshCount);
                }
            }
        }

        void Setup(Renderer shell, int subMeshes)
        {
            var materials = new Material[Mathf.Max(1, subMeshes)];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = sharedMaterial;
            shell.sharedMaterials = materials;
            shell.shadowCastingMode = ShadowCastingMode.Off;
            shell.receiveShadows = false;
            shell.lightProbeUsage = LightProbeUsage.Off;
            shell.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shell.enabled = false;
            shells.Add(shell);
        }

        /// <summary>A copy whose normals are averaged across vertices that share a position.</summary>
        static Mesh Smoothed(Mesh mesh)
        {
            if (!mesh.isReadable)
                return mesh;
            if (SmoothedMeshes.TryGetValue(mesh, out var cached) && cached != null)
                return cached;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            if (normals == null || normals.Length != vertices.Length)
                return mesh;
            var sums = new Dictionary<Vector3Int, Vector3>();
            static Vector3Int Key(Vector3 v) => new(Mathf.RoundToInt(v.x * 10000f), Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f));
            for (int i = 0; i < vertices.Length; i++)
            {
                var key = Key(vertices[i]);
                sums[key] = sums.TryGetValue(key, out var sum) ? sum + normals[i] : normals[i];
            }
            var smooth = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var n = sums[Key(vertices[i])];
                smooth[i] = n.sqrMagnitude > 1e-8f ? n.normalized : normals[i];
            }
            var copy = Instantiate(mesh);
            copy.name = mesh.name + " (outline)";
            copy.hideFlags = HideFlags.DontSave;
            copy.normals = smooth;
            SmoothedMeshes[mesh] = copy;
            return copy;
        }

        void Update()
        {
            amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
            block ??= new MaterialPropertyBlock();
            float eased = amount * amount * (3f - 2f * amount);
            block.SetColor(ColorId, color);
            block.SetFloat(WidthId, width * eased);
            foreach (var shell in shells)
            {
                if (shell == null)
                    continue;
                shell.enabled = amount > 0f;
                shell.SetPropertyBlock(block);
            }
            if (Mathf.Approximately(amount, target))
                enabled = amount > 0f;
        }
    }

    /// <summary>
    /// Marks effect objects (glows, light shafts, sky cards) that are not part of an item's solid
    /// shape: they get no outline and don't count toward its click area.
    /// </summary>
    public class RoomFx : MonoBehaviour { }
}
