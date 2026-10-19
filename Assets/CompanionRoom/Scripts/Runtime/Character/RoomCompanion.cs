using UnityEngine;

namespace CompanionRoom
{
    /// <summary>
    /// 凡安 in the room: idles, turns to face whatever the player is using, and plays her talking
    /// motion while a line is being said. Her renderers are put on the "Companion" layer so the
    /// dialogue portrait camera can film her alone. Works with any Animator whose state names are
    /// set below (missing states are simply skipped).
    /// </summary>
    public class RoomCompanion : MonoBehaviour
    {
        public const string LayerName = "Companion";

        [SerializeField] Animator animator;
        [Header("Animator 狀態名稱")]
        [SerializeField] string idleState = "Idle";
        [SerializeField] string talkState = "Wave";
        [Tooltip("How long the talking motion plays after a line starts before she settles back to idle.")]
        [SerializeField] float talkSeconds = 2.2f;
        [SerializeField] float crossFade = 0.25f;

        [Header("轉向")]
        [SerializeField] float turnDegreesPerSecond = 300f;
        [Tooltip("Where she faces when nothing is going on (usually the camera). Empty = her starting direction.")]
        [SerializeField] Transform restLookTarget;

        float targetYaw;
        float talkUntil = -1f;
        bool talking;
        int layer = -1;
        Renderer[] renderers;

        public int Layer => layer;
        public Animator Animator => animator;

        /// <summary>World bounds of her visible model.</summary>
        public Bounds Bounds
        {
            get
            {
                renderers ??= GetComponentsInChildren<Renderer>();
                bool any = false;
                var bounds = new Bounds(transform.position, Vector3.zero);
                foreach (var r in renderers)
                {
                    if (r == null || !r.enabled || HoverOutline.IsShellObject(r.gameObject))
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

        void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            layer = LayerMask.NameToLayer(LayerName);
            if (layer >= 0)
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = layer;
            targetYaw = transform.eulerAngles.y;
            walker = GetComponent<WitchLocomotion>();
        }

        WitchLocomotion walker;

        void Start()
        {
            FaceRest();
            Play(idleState, 0f);
        }

        public void FaceTowards(Vector3 point)
        {
            var flat = point - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0004f)
                targetYaw = Quaternion.LookRotation(flat).eulerAngles.y;
        }

        public void FaceRest()
        {
            if (restLookTarget != null)
                FaceTowards(restLookTarget.position);
            else if (walker != null && Camera.main != null)
                FaceTowards(Camera.main.transform.position); // a walking companion turns back to the player
            else if (walker != null)
                targetYaw = transform.eulerAngles.y;
        }

        /// <summary>A line started: play (or keep playing) the talking motion for a moment.</summary>
        public void Talk()
        {
            talkUntil = Time.time + talkSeconds;
            if (talking)
                return;
            talking = true;
            Play(talkState, crossFade);
        }

        public void Relax()
        {
            talkUntil = -1f;
            if (talking)
            {
                talking = false;
                Play(idleState, crossFade);
            }
        }

        void Play(string state, float fade)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(state))
                return;
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash))
                return;
            if (fade > 0f)
                animator.CrossFadeInFixedTime(hash, fade, 0);
            else
                animator.Play(hash, 0, 0f);
        }

        void Update()
        {
            if (talking && Time.time > talkUntil)
                Relax();
            // While walking, the walker steers. Her footprint is round, so turning in place is always safe.
            if (walker != null && walker.IsWalking) return;
            float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetYaw, turnDegreesPerSecond * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
