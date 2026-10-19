using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CompanionRoom
{
    /// <summary>
    /// Something in the room the player can click. The item's root holds this component and a
    /// BoxCollider fitted around its model; the model itself lives under the child named "Model"
    /// (swap the placeholder there for the real one). Hovering outlines it; clicking runs
    /// <see cref="Interact"/> as a coroutine through <see cref="CompanionRoomApp"/>, which turns 凡安
    /// towards it and eases the camera in.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class RoomItem : MonoBehaviour
    {
        public const string ModelChild = "Model";

        static readonly List<RoomItem> all = new();
        public static IReadOnlyList<RoomItem> All => all;

        [Header("顯示")]
        [Tooltip("The item's name (logs and tests; nothing is shown on screen while hovering).")]
        [SerializeField] string displayName = "物品";

        [Header("鏡頭")]
        [Tooltip("Where the camera eases towards while this item is in use. Empty = the middle of the model.")]
        [SerializeField] Transform focusPoint;
        [Tooltip("0 = the camera stays put, 1 = it moves well in towards the item.")]
        [SerializeField, Range(0f, 1f)] float cameraFocus = 0.35f;

        [Header("點擊範圍")]
        [Tooltip("Refit the root BoxCollider around the model when the scene starts (handy after swapping models).")]
        [SerializeField] bool autoFitCollider = true;
        [SerializeField] Vector3 colliderPadding = new(0.04f, 0.04f, 0.04f);

        [Header("對話")]
        [Tooltip("After 凡安 comments on this item she stays quiet about it for this many seconds (0 = always comment).")]
        [SerializeField, Min(0f)] float commentCooldown = 0f;

        HoverOutline outline;
        float lastComment = float.NegativeInfinity;

        public string DisplayName => displayName;
        public float CameraFocus => cameraFocus;
        public HoverOutline Outline => outline;

        /// <summary>The model's container (the "Model" child, or the item itself when there is none).</summary>
        public Transform ModelRoot
        {
            get
            {
                var model = transform.Find(ModelChild);
                return model != null ? model : transform;
            }
        }

        public Vector3 FocusPosition => focusPoint != null ? focusPoint.position : ModelBounds.center;

        /// <summary>World bounds of the item's visible model (effects excluded).</summary>
        public Bounds ModelBounds
        {
            get
            {
                bool any = false;
                var bounds = new Bounds(transform.position, Vector3.zero);
                foreach (var r in ModelRoot.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || HoverOutline.IsShellObject(r.gameObject) || r.GetComponentInParent<RoomFx>(true) != null || r is ParticleSystemRenderer)
                        continue;
                    if (!any)
                        bounds = r.bounds;
                    else
                        bounds.Encapsulate(r.bounds);
                    any = true;
                }
                return bounds;
            }
        }

        protected virtual void Awake()
        {
            if (autoFitCollider)
                FitCollider();
            outline = GetComponent<HoverOutline>();
            if (outline == null)
                outline = gameObject.AddComponent<HoverOutline>();
            outline.Prepare();
            outline.enabled = false;
        }

        /// <summary>Sets the basics on an item added by code (see <see cref="RoomItemBinder"/>).</summary>
        public void Bind(string name, float focus)
        {
            displayName = name;
            cameraFocus = Mathf.Clamp01(focus);
            if (focusPoint == null)
                focusPoint = transform.Find("FocusPoint");
        }

        protected virtual void OnEnable() => all.Add(this);
        protected virtual void OnDisable() => all.Remove(this);

        /// <summary>Fits the root BoxCollider around the model, in the item's own space.</summary>
        [ContextMenu("重新計算點擊範圍 (Fit Collider)")]
        public void FitCollider()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null)
                box = gameObject.AddComponent<BoxCollider>();
            bool any = false;
            var local = new Bounds();
            foreach (var r in ModelRoot.GetComponentsInChildren<Renderer>())
            {
                if (HoverOutline.IsShellObject(r.gameObject) || r.GetComponentInParent<RoomFx>(true) != null || r is ParticleSystemRenderer)
                    continue;
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = transform.InverseTransformPoint(corner);
                    if (!any)
                        local = new Bounds(p, Vector3.zero);
                    else
                        local.Encapsulate(p);
                    any = true;
                }
            }
            if (!any)
                return;
            var scale = transform.lossyScale;
            var pad = new Vector3(colliderPadding.x / Mathf.Max(1e-4f, Mathf.Abs(scale.x)), colliderPadding.y / Mathf.Max(1e-4f, Mathf.Abs(scale.y)), colliderPadding.z / Mathf.Max(1e-4f, Mathf.Abs(scale.z)));
            box.center = local.center;
            box.size = local.size + pad * 2f;
        }

        public virtual void SetHovered(bool hovered)
        {
            if (outline != null)
                outline.SetHighlighted(hovered);
        }

        /// <summary>What happens when the item is clicked; runs until the interaction is over.</summary>
        public abstract IEnumerator Interact(CompanionRoomApp app);

        /// <summary>Called when the interaction ends, normally or because the player pressed Esc.</summary>
        public virtual void OnInteractionEnded(CompanionRoomApp app) { }

        /// <summary>True when 凡安 should comment now (and starts the cooldown).</summary>
        protected bool TakeCommentTurn()
        {
            if (commentCooldown > 0f && Time.unscaledTime - lastComment < commentCooldown)
                return false;
            lastComment = Time.unscaledTime;
            return true;
        }

        /// <summary>
        /// Finds a part of the model by name when the inspector slot is empty, so a newly imported
        /// model only needs its mesh named e.g. "Screen" or "Bulb".
        /// </summary>
        protected T FindPart<T>(T assigned, params string[] names) where T : Component
        {
            if (assigned != null)
                return assigned;
            foreach (var t in ModelRoot.GetComponentsInChildren<Transform>(true))
                foreach (var n in names)
                    if (t.name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0 && t.TryGetComponent<T>(out var found))
                        return found;
            return null;
        }

        protected Transform FindPart(Transform assigned, params string[] names)
        {
            if (assigned != null)
                return assigned;
            foreach (var t in ModelRoot.GetComponentsInChildren<Transform>(true))
                foreach (var n in names)
                    if (t.name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return t;
            return null;
        }
    }
}
