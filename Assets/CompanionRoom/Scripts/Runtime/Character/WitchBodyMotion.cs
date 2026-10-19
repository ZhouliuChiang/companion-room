using UnityEngine;

namespace CompanionRoom
{
    /// <summary>
    /// Procedural layer on top of the witch's Walk / Idle clips, applied after the Animator each frame:
    /// - walking: the hips dip on each step and shift over the standing leg, the pelvis turns and
    ///   tilts with the stride, the chest turns against it, the arms swing opposite the legs and the
    ///   head stays steady, so the whole body moves and not just the feet;
    /// - standing: slow breathing in the chest and shoulders, and a slight weight shift;
    /// - legs: two-bone IK keeps the feet where the clip put them, so the hip motion never lifts a
    ///   foot off the floor or pushes it through;
    /// - arms: after the skirt has been solved, every arm and hand point is pushed outside the body
    ///   and skirt outline (<see cref="WitchSkirt"/>) by swinging the upper arm outward.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class WitchBodyMotion : MonoBehaviour
    {
        [Header("走路律動")]
        [SerializeField, Tooltip("Knee bend while walking, as a fraction of leg length.")] float crouch = .03f;
        [SerializeField, Tooltip("Extra dip at each footfall, fraction of leg length.")] float stepDip = .022f;
        [SerializeField, Tooltip("Side-to-side weight shift, fraction of leg length.")] float weightShift = .025f;
        [SerializeField] float pelvisTurn = 5f, pelvisTilt = 3.5f, forwardLean = 3f;
        [SerializeField] float armSwing = 13f, elbowBend = 9f;
        [SerializeField, Range(0f, 1f)] float headSteady = .65f;

        [Header("待機呼吸")]
        [SerializeField] float breathSeconds = 3.8f;
        [SerializeField] float breathChest = 1.6f, breathShoulders = 1.4f;
        [SerializeField] float idleSwaySeconds = 7.5f;
        [SerializeField, Tooltip("Idle weight shift, fraction of leg length.")] float idleSway = .012f;

        [Header("防穿模")]
        [SerializeField, Tooltip("Clearance kept between arms and body/skirt, fraction of leg length.")] float armClearance = .02f;
        [SerializeField] float maxArmCorrection = 40f;

        struct Arm { public Transform upper, lower, hand, tip, thumb; }
        struct Leg { public Transform upper, lower, foot; }

        Animator animator;
        WitchSkirt skirt;
        WitchLocomotion walker;
        Transform hips, spine, chest, upperChest, head, lShoulder, rShoulder;
        Arm left, right;
        Leg lLeg, rLeg;
        bool ready;
        float walkWeight, phase, phaseSign = 1f, legLength;
        Vector3 lastRoot;

        /// <summary>Make sure every walker in a loaded scene gets the skirt rig and the body layer.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToWalkers()
        {
            foreach (var w in FindObjectsByType<WitchLocomotion>(FindObjectsSortMode.None))
            {
                if (w.GetComponent<WitchSkirt>() == null) w.gameObject.AddComponent<WitchSkirt>();
                if (w.GetComponent<WitchBodyMotion>() == null) w.gameObject.AddComponent<WitchBodyMotion>();
                if (w.GetComponent<WitchClickMarker>() == null) w.gameObject.AddComponent<WitchClickMarker>();
            }
        }

        void Start()
        {
            animator = GetComponentInChildren<Animator>();
            skirt = GetComponent<WitchSkirt>();
            walker = GetComponent<WitchLocomotion>();
            Transform Find(string n)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
                return null;
            }
            hips = Find("Hips"); spine = Find("Spine"); chest = Find("Chest"); upperChest = Find("UpperChest"); head = Find("Head");
            lShoulder = Find("Left_Shoulder"); rShoulder = Find("Right_Shoulder");
            left = new Arm { upper = Find("Left_UpperArm"), lower = Find("Left_LowerArm"), hand = Find("Left_Hand"), tip = Find("Left_MiddleDistalEnd"), thumb = Find("Left_ThumbDistalEnd") };
            right = new Arm { upper = Find("Right_UpperArm"), lower = Find("Right_LowerArm"), hand = Find("Right_Hand"), tip = Find("Right_MiddleDistalEnd"), thumb = Find("Right_ThumbDistalEnd") };
            lLeg = new Leg { upper = Find("Left_UpperLeg"), lower = Find("Left_LowerLeg"), foot = Find("Left_Foot") };
            rLeg = new Leg { upper = Find("Right_UpperLeg"), lower = Find("Right_LowerLeg"), foot = Find("Right_Foot") };
            ready = hips && spine && chest && upperChest && head && left.upper && left.lower && left.hand && right.upper && right.lower && right.hand
                    && lLeg.upper && lLeg.lower && lLeg.foot && rLeg.upper && rLeg.lower && rLeg.foot;
            if (!ready) { Debug.LogWarning("[WitchBodyMotion] Missing bones; procedural motion disabled.", this); return; }
            if (left.tip == null) left.tip = left.hand;
            if (right.tip == null) right.tip = right.hand;
            lastRoot = transform.position;
            touched = new[] { hips, spine, chest, upperChest, head, lShoulder, rShoulder, left.upper, left.lower, right.upper, right.lower,
                              lLeg.upper, lLeg.lower, lLeg.foot, rLeg.upper, rLeg.lower, rLeg.foot };
            savedPos = new Vector3[touched.Length];
            savedRot = new Quaternion[touched.Length];
        }

        // Bones the clip does not key would otherwise keep last frame's offsets and drift, so the
        // pose the Animator produced is put back before it runs again.
        Transform[] touched;
        Vector3[] savedPos;
        Quaternion[] savedRot;
        bool saved;

        void Update()
        {
            if (!saved) return;
            for (int i = 0; i < touched.Length; i++)
                if (touched[i]) { touched[i].localPosition = savedPos[i]; touched[i].localRotation = savedRot[i]; }
            saved = false;
        }

        void Save()
        {
            for (int i = 0; i < touched.Length; i++)
                if (touched[i]) { savedPos[i] = touched[i].localPosition; savedRot[i] = touched[i].localRotation; }
            saved = true;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (!ready || dt <= 0f) return;
            Save();
            if (legLength <= 0f)
                legLength = (lLeg.lower.position - lLeg.upper.position).magnitude + (lLeg.foot.position - lLeg.lower.position).magnitude;

            // How much she is walking, from her real ground speed (smooth start and stop).
            var delta = transform.position - lastRoot; delta.y = 0f;
            lastRoot = transform.position;
            float target = walker != null ? Mathf.Clamp01(delta.magnitude / dt / Mathf.Max(.05f, walker.speed)) : 0f;
            walkWeight = Mathf.Lerp(walkWeight, target, 1f - Mathf.Exp(-dt * 7f));
            float w = walkWeight, idle = 1f - w;

            Vector3 up = transform.up;
            Vector3 fwd = skirt != null && skirt.Ready ? Vector3.ProjectOnPlane(hips.TransformDirection(skirt.Forward), up) : Vector3.ProjectOnPlane(transform.forward, up);
            if (fwd.sqrMagnitude < 1e-6f) fwd = transform.forward;
            fwd.Normalize();
            Vector3 side = Vector3.Cross(up, fwd); // character's right

            // Stride phase from the Walk clip: 0 / .5 = feet passing, .25 / .75 = footfalls.
            UpdatePhase();
            float stride = Vector3.Dot(rLeg.foot.position - lLeg.foot.position, fwd);
            float sinP = Mathf.Sin(2f * Mathf.PI * phase);
            if (w > .5f && Mathf.Abs(sinP) > .7f && Mathf.Abs(stride) > legLength * .1f)
                phaseSign = Mathf.Sign(stride * sinP); // +1: right foot leads at .25
            float s = sinP * phaseSign;                                        // +1 right foot forward
            float c = Mathf.Sin(2f * Mathf.PI * (phase - .125f)) * phaseSign;  // +1 standing on the right
            float dip = .5f - .5f * Mathf.Cos(4f * Mathf.PI * phase);          // 1 at each footfall
            float time = Time.time;
            float breath = .5f + .5f * Mathf.Sin(2f * Mathf.PI * time / breathSeconds);
            float breathWeight = idle + .3f * w;

            // Pose the clip gave us, to restore feet and steady the head afterwards.
            Vector3 lFootPos = lLeg.foot.position, rFootPos = rLeg.foot.position;
            Quaternion lFootRot = lLeg.foot.rotation, rFootRot = rLeg.foot.rotation, headRot = head.rotation;

            // Hips: dip, weight shift, turn and tilt with the stride.
            float drop = legLength * (crouch * w + stepDip * w * dip);
            float shift = legLength * (weightShift * w * c + idleSway * idle * Mathf.Sin(2f * Mathf.PI * time / idleSwaySeconds));
            hips.position += -up * drop + side * shift;
            hips.rotation = Quaternion.AngleAxis(-pelvisTurn * w * s, up) * Quaternion.AngleAxis(pelvisTilt * w * c, fwd) * hips.rotation;

            // Spine and chest counter the pelvis; a slight lean into the walk; breathing lifts the chest.
            spine.rotation = Quaternion.AngleAxis(forwardLean * .5f * w, side) * Quaternion.AngleAxis(pelvisTurn * .6f * w * s, up) * Quaternion.AngleAxis(-pelvisTilt * .6f * w * c, fwd) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(forwardLean * .5f * w, side) * Quaternion.AngleAxis(pelvisTurn * .9f * w * s, up) * Quaternion.AngleAxis(-pelvisTilt * .4f * w * c, fwd) * chest.rotation;
            upperChest.rotation = Quaternion.AngleAxis(-breathChest * breath * breathWeight, side) * upperChest.rotation;
            float shrug = breathShoulders * breath * breathWeight;
            if (lShoulder) lShoulder.rotation = Quaternion.AngleAxis(-shrug, fwd) * lShoulder.rotation;
            if (rShoulder) rShoulder.rotation = Quaternion.AngleAxis(shrug, fwd) * rShoulder.rotation;

            // Arms swing against the legs, elbows bend a little more on the forward swing.
            left.upper.rotation = Quaternion.AngleAxis(-armSwing * w * s, side) * left.upper.rotation;
            right.upper.rotation = Quaternion.AngleAxis(armSwing * w * s, side) * right.upper.rotation;
            left.lower.rotation = Quaternion.AngleAxis(-elbowBend * w * (.5f + Mathf.Max(0f, s)), side) * left.lower.rotation;
            right.lower.rotation = Quaternion.AngleAxis(-elbowBend * w * (.5f + Mathf.Max(0f, -s)), side) * right.lower.rotation;

            // Head: mostly keep the clip's orientation, with a slow idle glance.
            head.rotation = Quaternion.Slerp(head.rotation, headRot, headSteady);
            head.rotation = Quaternion.AngleAxis(2f * idle * Mathf.Sin(2f * Mathf.PI * time / 9f), up) * head.rotation;

            // Feet back where the clip planted them.
            SolveLeg(lLeg, lFootPos, lFootRot, fwd);
            SolveLeg(rLeg, rFootPos, rFootRot, fwd);

            // Skirt follows the final leg pose, then the arms are kept outside body and skirt.
            if (skirt != null && skirt.Ready)
            {
                skirt.Solve(dt);
                KeepOutside(left);
                KeepOutside(right);
            }
        }

        void UpdatePhase()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName("Walk")) info = next;
            }
            if (info.IsName("Walk")) phase = Mathf.Repeat(info.normalizedTime, 1f);
        }

        static void SolveLeg(Leg leg, Vector3 target, Quaternion footRotation, Vector3 knee)
        {
            Vector3 a = leg.upper.position, b = leg.lower.position, c = leg.foot.position;
            float la = (b - a).magnitude, lb = (c - b).magnitude;
            Vector3 toTarget = target - a;
            float d = Mathf.Clamp(toTarget.magnitude, 1e-4f, (la + lb) * .9999f);
            Vector3 dir = toTarget.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(knee, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(b - a, dir);
            bend.Normalize();
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            float angA = Mathf.Acos(cosA);
            Vector3 kneeTarget = a + (dir * Mathf.Cos(angA) + bend * Mathf.Sin(angA)) * la;
            leg.upper.rotation = Quaternion.FromToRotation(b - a, kneeTarget - a) * leg.upper.rotation;
            b = leg.lower.position; c = leg.foot.position;
            leg.lower.rotation = Quaternion.FromToRotation(c - b, a + dir * d - b) * leg.lower.rotation;
            leg.foot.rotation = footRotation;
        }

        void KeepOutside(Arm arm)
        {
            var hipsT = skirt.Hips;
            float unit = hipsT.lossyScale.x; // world size of one hips-space unit
            float clearance = skirt.LegLength * armClearance;
            Vector3 axisUp = hipsT.TransformDirection(skirt.Up);
            float total = 0f;
            for (int iteration = 0; iteration < 10; iteration++)
            {
                float worst = 0f; Vector3 worstPoint = default;
                void Check(Vector3 world, float radius)
                {
                    var p = hipsT.InverseTransformPoint(world);
                    if (!skirt.SurfaceRadius(p, out float surface)) return;
                    float pen = surface + radius + clearance - skirt.Radius(p);
                    if (pen > worst) { worst = pen; worstPoint = world; }
                }
                Vector3 sh = arm.upper.position, el = arm.lower.position, wr = arm.hand.position, tip = arm.tip.position;
                Check(Vector3.Lerp(sh, el, .65f), skirt.ArmRadius * 1.1f);
                Check(el, skirt.ArmRadius);
                Check(Vector3.Lerp(el, wr, .5f), skirt.ArmRadius);
                Check(wr, skirt.ArmRadius);
                Check(Vector3.Lerp(wr, tip, .45f), skirt.HandRadius);
                Check(tip, skirt.HandRadius * .6f);
                if (arm.thumb) Check(arm.thumb.position, skirt.HandRadius * .5f);
                if (worst <= 1e-6f) break;

                var center = hipsT.TransformPoint(skirt.Origin);
                var outward = Vector3.ProjectOnPlane(worstPoint - center, axisUp).normalized;
                var v = worstPoint - sh;
                var axis = Vector3.Cross(v, outward);
                if (axis.sqrMagnitude < 1e-10f || v.sqrMagnitude < 1e-10f) break;
                float lever = Mathf.Max(axis.magnitude, v.magnitude * .2f);
                float angle = Mathf.Min(worst * unit * 1.15f / lever * Mathf.Rad2Deg, maxArmCorrection - total);
                if (angle <= 0f) break;
                arm.upper.rotation = Quaternion.AngleAxis(angle, axis.normalized) * arm.upper.rotation;
                total += angle;
            }
        }

        /// <summary>Deepest arm/hand intrusion into the body or skirt right now, in metres (≤ 0 = clear).</summary>
        public float ArmIntrusion()
        {
            if (!ready || skirt == null || !skirt.Ready) return 0f;
            var hipsT = skirt.Hips; float worst = float.MinValue;
            foreach (var arm in new[] { left, right })
                foreach (var (t, r) in new[] { (arm.lower, skirt.ArmRadius), (arm.hand, skirt.ArmRadius), (arm.tip, skirt.HandRadius * .6f) })
                {
                    var p = hipsT.InverseTransformPoint(t.position);
                    if (skirt.SurfaceRadius(p, out float surface)) worst = Mathf.Max(worst, (surface + r - skirt.Radius(p)) * hipsT.lossyScale.x);
                }
            return worst == float.MinValue ? 0f : worst;
        }
    }
}
