using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace CompanionRoom
{
    /// <summary>
    /// Gives the witch's skirt its own ring of bones and keeps it outside her legs. The imported mesh
    /// has no skirt bones (the skirt is skinned to the hips), so at start-up the skirt vertices are
    /// found from the bind pose and re-weighted onto <see cref="BoneCount"/> radial bones hung from
    /// the hips. Each frame the bones flare out just enough to contain the thighs, and swing a little
    /// with the body's movement. The same bind-pose scan also records the torso + skirt outline, which
    /// <see cref="WitchBodyMotion"/> uses to keep the arms and hands outside the body.
    /// All measurements are taken from the mesh itself, in hips space, so they follow any rescale.
    /// </summary>
    public sealed class WitchSkirt : MonoBehaviour
    {
        public const int BoneCount = 16;
        const int Sectors = 32, Bins = 48;
        const float MaxFlare = 32f * Mathf.Deg2Rad, MaxSway = 9f * Mathf.Deg2Rad;

        [Tooltip("Natural frequency of the skirt's swing (Hz).")]
        [SerializeField] float swayFrequency = 1.5f;
        [SerializeField, Range(0f, 1f)] float swayDamping = 0.38f;
        [SerializeField] float swayGain = 0.55f;

        public bool Ready { get; private set; }
        public Transform Hips { get; private set; }
        /// <summary>Hips-space axis origin, up, forward and right of the body.</summary>
        public Vector3 Origin { get; private set; }
        public Vector3 Up { get; private set; }
        public Vector3 Forward { get; private set; }
        public Vector3 Right { get; private set; }
        /// <summary>Leg length (hip to ankle) in hips-space units.</summary>
        public float LegLength { get; private set; }
        public float LegRadius { get; private set; }
        public float ArmRadius { get; private set; }
        public float HandRadius { get; private set; }
        public float HemHeight => hem;
        public float ProfileTop => top;

        readonly Transform[] bones = new Transform[BoneCount];
        readonly Vector3[] radial = new Vector3[BoneCount];
        readonly float[] flare = new float[BoneCount], floor = new float[BoneCount];
        readonly float[] profile = new float[Sectors * Bins];
        float hem, top, pivotHeight, pivotRadius;
        Vector2 sway, swayVelocity;
        Vector3 lastHips, lastHipsVelocity;
        bool hasHistory;
        Transform[] leftLeg, rightLeg;

        void Awake() => Build();

        // ---------------------------------------------------------------- setup

        void Build()
        {
            var smr = GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null || !smr.sharedMesh.isReadable)
            {
                Debug.LogWarning("[WitchSkirt] Needs a readable SkinnedMeshRenderer; skirt bones disabled.", this);
                return;
            }
            var src = smr.sharedMesh;
            var smrBones = smr.bones;
            var binds = src.bindposes;
            int Find(string n) { for (int i = 0; i < smrBones.Length; i++) if (smrBones[i] != null && smrBones[i].name == n) return i; return -1; }
            int hipsIndex = Find("Hips");
            if (hipsIndex < 0) { Debug.LogWarning("[WitchSkirt] No Hips bone.", this); return; }
            Hips = smrBones[hipsIndex];
            var toHips = binds[hipsIndex];
            Vector3 Joint(string n) { int i = Find(n); return i < 0 ? Vector3.zero : toHips.MultiplyPoint3x4(binds[i].inverse.MultiplyPoint3x4(Vector3.zero)); }

            // Body axes from the skeleton: legs give "up", toes give "forward".
            Vector3 lHip = Joint("Left_UpperLeg"), rHip = Joint("Right_UpperLeg");
            Vector3 lKnee = Joint("Left_LowerLeg"), lAnkle = Joint("Left_Foot"), rAnkle = Joint("Right_Foot");
            Vector3 toes = (Joint("Left_Toes") + Joint("Right_Toes")) * .5f, ankles = (lAnkle + rAnkle) * .5f;
            Up = ((lHip + rHip) * .5f - ankles).normalized;
            Forward = Vector3.ProjectOnPlane(toes - ankles, Up).normalized;
            Right = Vector3.Cross(Up, Forward).normalized;
            if (Vector3.Dot(Right, rHip - lHip) < 0f) Right = -Right; // keep "right" on the character's right
            Origin = Vector3.zero;
            LegLength = (lKnee - lHip).magnitude + (lAnkle - lKnee).magnitude;

            var verts = src.vertices;
            var perVertex = src.GetBonesPerVertex();
            var weights = src.GetAllBoneWeights();
            int n = verts.Length;
            var local = new Vector3[n];
            var dominant = new int[n];
            var start = new int[n];
            for (int v = 0, w = 0; v < n; v++)
            {
                start[v] = w;
                local[v] = toHips.MultiplyPoint3x4(verts[v]);
                int best = -1; float bw = -1f;
                for (int k = 0; k < perVertex[v]; k++, w++)
                    if (weights[w].weight > bw) { bw = weights[w].weight; best = weights[w].boneIndex; }
                dominant[v] = best;
            }
            string NameOf(int i) => i >= 0 && smrBones[i] != null ? smrBones[i].name : "";
            bool IsTorso(string s) => s == "Hips" || s == "Spine" || s == "Chest" || s == "UpperChest";
            bool IsUpperLeg(string s) => s == "Left_UpperLeg" || s == "Right_UpperLeg";

            // Limb thickness: 85th percentile distance of each limb's vertices to its bone segment.
            float Thickness(string bone, string child)
            {
                int bi = Find(bone); Vector3 a = Joint(bone), b = Joint(child);
                var d = new List<float>();
                for (int v = 0; v < n; v++) if (dominant[v] == bi) d.Add(DistanceToSegment(local[v], a, b));
                if (d.Count == 0) return LegLength * .12f;
                d.Sort(); return d[Mathf.Min(d.Count - 1, (int)(d.Count * .85f))];
            }
            LegRadius = Mathf.Max(Thickness("Left_UpperLeg", "Left_LowerLeg"), Thickness("Right_UpperLeg", "Right_LowerLeg"));
            ArmRadius = Mathf.Max(Thickness("Left_LowerArm", "Left_Hand"), Thickness("Right_LowerArm", "Right_Hand"));
            HandRadius = Mathf.Max(Thickness("Left_Hand", "Left_MiddleProximal"), Thickness("Right_Hand", "Right_MiddleProximal"));

            // Heights: hem = lowest hips-skinned vertex, waist = lowest chest-skinned vertex,
            // profile top = the shoulder joints (arms are T-posed in the bind pose, so ignored).
            hem = float.MaxValue; float waist = float.MaxValue;
            for (int v = 0; v < n; v++)
            {
                string s = NameOf(dominant[v]); float h = Height(local[v]);
                if (s == "Hips") hem = Mathf.Min(hem, h);
                if (s == "Chest" || s == "UpperChest") waist = Mathf.Min(waist, h);
            }
            top = Height(Joint("Left_UpperArm"));
            if (hem >= waist || hem == float.MaxValue) { Debug.LogWarning("[WitchSkirt] Could not find the skirt.", this); return; }

            // Outline of torso + skirt.
            for (int v = 0; v < n; v++)
                if (IsTorso(NameOf(dominant[v])))
                {
                    int b = Bin(Height(local[v]));
                    if (b >= 0) { int i = b * Sectors + Sector(local[v]); profile[i] = Mathf.Max(profile[i], Radius(local[v])); }
                }
            FillProfile();

            // Skirt vertices: hips-skinned (or thigh-skinned but clearly away from the thigh) between hem
            // and upper skirt. Weight onto the ring ramps from 0 near the waist to 1 at the wide part.
            float span = waist - hem;
            pivotHeight = waist - span * .25f;
            float full = hem + span * .35f;
            pivotRadius = 0f;
            for (int s = 0; s < Sectors; s++) pivotRadius += ProfileAt(pivotHeight, s);
            pivotRadius = pivotRadius / Sectors * .85f;

            Vector3 lA = Joint("Left_UpperLeg"), lB = Joint("Left_LowerLeg"), rA = Joint("Right_UpperLeg"), rB = Joint("Right_LowerLeg");
            var newPerVertex = new byte[n];
            var newWeights = new List<BoneWeight1>(weights.Length + n);
            int firstBone = smrBones.Length, skirtVerts = 0;
            var tmp = new List<BoneWeight1>(8);
            for (int v = 0; v < n; v++)
            {
                tmp.Clear();
                for (int k = 0; k < perVertex[v]; k++) tmp.Add(weights[start[v] + k]);
                float h = Height(local[v]);
                string s = NameOf(dominant[v]);
                bool candidate = h >= hem - span * .03f && h < pivotHeight &&
                    (s == "Hips" || s == "Spine" ||
                     (IsUpperLeg(s) && Mathf.Min(DistanceToSegment(local[v], lA, lB), DistanceToSegment(local[v], rA, rB)) > LegRadius * 1.6f));
                if (candidate)
                {
                    float t = Mathf.InverseLerp(pivotHeight, full, h);
                    float ring = t * t * (3f - 2f * t);
                    if (ring > .001f)
                    {
                        float a = Angle(local[v]) / (2f * Mathf.PI) * BoneCount;
                        int i0 = Mathf.FloorToInt(a) % BoneCount, i1 = (i0 + 1) % BoneCount;
                        float f = a - Mathf.Floor(a);
                        for (int k = 0; k < tmp.Count; k++) { var bw = tmp[k]; bw.weight *= 1f - ring; tmp[k] = bw; }
                        tmp.Add(new BoneWeight1 { boneIndex = firstBone + i0, weight = ring * (1f - f) });
                        tmp.Add(new BoneWeight1 { boneIndex = firstBone + i1, weight = ring * f });
                        skirtVerts++;
                    }
                }
                // Keep the four strongest influences (default skin quality), renormalised, sorted.
                tmp.Sort((x, y) => y.weight.CompareTo(x.weight));
                int count = Mathf.Min(4, tmp.Count); float sum = 0f;
                for (int k = 0; k < count; k++) sum += tmp[k].weight;
                for (int k = 0; k < count; k++) { var bw = tmp[k]; bw.weight = sum > 0f ? bw.weight / sum : 1f / count; newWeights.Add(bw); }
                newPerVertex[v] = (byte)count;
            }
            perVertex.Dispose(); weights.Dispose();
            if (skirtVerts == 0) { Debug.LogWarning("[WitchSkirt] No skirt vertices found.", this); return; }

            // Ring of bones under the hips, rest pose = hips orientation. Bone i sits at the angle where
            // Angle() == i * 2π / BoneCount, matching the interpolation used for the weights above.
            var allBones = new Transform[firstBone + BoneCount];
            smrBones.CopyTo(allBones, 0);
            var allBinds = new Matrix4x4[firstBone + BoneCount];
            binds.CopyTo(allBinds, 0);
            for (int i = 0; i < BoneCount; i++)
            {
                float a = (float)i / BoneCount * 2f * Mathf.PI;
                radial[i] = (Mathf.Cos(a) * Right + Mathf.Sin(a) * Forward).normalized;
                var bone = new GameObject("SkirtBone_" + i).transform;
                bone.SetParent(Hips, false);
                bone.localPosition = Up * pivotHeight + radial[i] * pivotRadius;
                bone.localRotation = Quaternion.identity;
                bone.gameObject.layer = Hips.gameObject.layer;
                bones[i] = bone;
                allBones[firstBone + i] = bone;
                allBinds[firstBone + i] = Matrix4x4.Translate(-bone.localPosition) * toHips;
            }

            var mesh = Instantiate(src);
            mesh.name = src.name + " (skirt rig)";
            var perArray = new NativeArray<byte>(newPerVertex, Allocator.Temp);
            var wArray = new NativeArray<BoneWeight1>(newWeights.ToArray(), Allocator.Temp);
            mesh.SetBoneWeights(perArray, wArray);
            perArray.Dispose(); wArray.Dispose();
            mesh.bindposes = allBinds;
            smr.bones = allBones;
            smr.sharedMesh = mesh;
            smr.updateWhenOffscreen = true;

            leftLeg = new[] { Bone(smrBones, "Left_UpperLeg"), Bone(smrBones, "Left_LowerLeg"), Bone(smrBones, "Left_Foot") };
            rightLeg = new[] { Bone(smrBones, "Right_UpperLeg"), Bone(smrBones, "Right_LowerLeg"), Bone(smrBones, "Right_Foot") };
            Ready = true;
        }

        static Transform Bone(Transform[] list, string name)
        {
            foreach (var t in list) if (t != null && t.name == name) return t;
            return null;
        }

        void FillProfile()
        {
            // Empty cells borrow from their neighbours (first along height, then around).
            for (int pass = 0; pass < Bins; pass++)
            {
                bool changed = false;
                for (int b = 0; b < Bins; b++)
                    for (int s = 0; s < Sectors; s++)
                    {
                        int i = b * Sectors + s;
                        if (profile[i] > 0f) continue;
                        float m = 0f;
                        if (b > 0) m = Mathf.Max(m, profile[i - Sectors]);
                        if (b < Bins - 1) m = Mathf.Max(m, profile[i + Sectors]);
                        m = Mathf.Max(m, profile[b * Sectors + (s + 1) % Sectors], profile[b * Sectors + (s + Sectors - 1) % Sectors]);
                        if (m > 0f) { profile[i] = m; changed = true; }
                    }
                if (!changed) break;
            }
        }

        // ---------------------------------------------------------------- geometry helpers (hips space)

        public float Height(Vector3 p) => Vector3.Dot(p - Origin, Up);
        public float Radius(Vector3 p) => Vector3.ProjectOnPlane(p - Origin, Up).magnitude;
        float Angle(Vector3 p)
        {
            var q = p - Origin;
            float a = Mathf.Atan2(Vector3.Dot(q, Forward), Vector3.Dot(q, Right));
            return a < 0f ? a + 2f * Mathf.PI : a;
        }
        int Sector(Vector3 p) => Mathf.Min(Sectors - 1, (int)(Angle(p) / (2f * Mathf.PI) * Sectors));
        int Bin(float h)
        {
            if (h < hem || h > top) return -1;
            return Mathf.Min(Bins - 1, (int)((h - hem) / (top - hem) * Bins));
        }
        float ProfileAt(float h, int sector)
        {
            int b = Mathf.Clamp(Bin(Mathf.Clamp(h, hem, top)), 0, Bins - 1);
            return profile[b * Sectors + sector];
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>
        /// Body surface radius (hips space) at a hips-space point's height and direction, with the
        /// skirt's current flare included. Returns false above the shoulders or below the hem.
        /// </summary>
        public bool SurfaceRadius(Vector3 p, out float radius)
        {
            radius = 0f;
            if (!Ready) return false;
            float h = Height(p);
            if (h < hem || h > top) return false;
            int s = Sector(p);
            int b = Mathf.Min(Bins - 1, (int)((h - hem) / (top - hem) * Bins));
            radius = profile[b * Sectors + s];
            // Neighbouring sectors too, so a limb straddling a boundary sees the larger value.
            radius = Mathf.Max(radius, profile[b * Sectors + (s + 1) % Sectors], profile[b * Sectors + (s + Sectors - 1) % Sectors]);
            if (h < pivotHeight)
            {
                float a = Angle(p) / (2f * Mathf.PI) * BoneCount;
                int i0 = Mathf.FloorToInt(a) % BoneCount, i1 = (i0 + 1) % BoneCount;
                float phi = Mathf.Max(flare[i0], flare[i1]);
                radius = Mathf.Max(radius, Flared(radius, h, phi));
            }
            return true;
        }

        /// <summary>Radius of a rest skirt-surface point (radius r at height h) after flaring by phi.</summary>
        float Flared(float r, float h, float phi)
        {
            float dr = r - pivotRadius, dh = h - pivotHeight;
            return pivotRadius + dr * Mathf.Cos(phi) - dh * Mathf.Sin(phi);
        }

        // ---------------------------------------------------------------- per frame

        /// <summary>Runs after the body pose is final for this frame (called by WitchBodyMotion).</summary>
        public void Solve(float dt)
        {
            if (!Ready || dt <= 0f) return;

            // Swing: a damped pendulum driven by the hips' horizontal acceleration.
            var hipsPos = Hips.position;
            var vel = hasHistory ? (hipsPos - lastHips) / dt : Vector3.zero;
            var acc = hasHistory ? (vel - lastHipsVelocity) / dt : Vector3.zero;
            lastHips = hipsPos; lastHipsVelocity = vel; hasHistory = true;
            acc = Vector3.ClampMagnitude(acc, 6f);
            var accLocal = Hips.InverseTransformVector(acc) / Mathf.Max(1e-5f, Hips.lossyScale.x);
            float worldLeg = LegLength * Hips.lossyScale.x;
            var drive = new Vector2(Vector3.Dot(accLocal, Right), Vector3.Dot(accLocal, Forward)) * Hips.lossyScale.x / Mathf.Max(.05f, worldLeg) * swayGain;
            float w = 2f * Mathf.PI * swayFrequency, k = w * w, c = 2f * swayDamping * w;
            int steps = Mathf.CeilToInt(dt / (1f / 120f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                swayVelocity += (-k * sway - c * swayVelocity - drive) * h;
                sway += swayVelocity * h;
            }
            sway = Vector2.ClampMagnitude(sway, MaxSway);

            // Collision floor: each bone flares at least enough to contain both thighs.
            for (int i = 0; i < BoneCount; i++) floor[i] = 0f;
            Contain(leftLeg); Contain(rightLeg);

            for (int i = 0; i < BoneCount; i++)
            {
                var r = radial[i];
                float target = sway.x * Vector3.Dot(r, Right) + sway.y * Vector3.Dot(r, Forward);
                float phi = Mathf.Clamp(Mathf.Max(target, floor[i]), -MaxSway, MaxFlare);
                flare[i] = phi;
                var down = -Up;
                var dir = down * Mathf.Cos(phi) + r * Mathf.Sin(phi);
                bones[i].localRotation = Quaternion.FromToRotation(down, dir);
            }
        }

        void Contain(Transform[] leg)
        {
            if (leg == null || leg[0] == null || leg[1] == null || leg[2] == null) return;
            Vector3 hip = Hips.InverseTransformPoint(leg[0].position);
            Vector3 knee = Hips.InverseTransformPoint(leg[1].position);
            Vector3 ankle = Hips.InverseTransformPoint(leg[2].position);
            float margin = LegLength * .02f;
            const int Samples = 10;
            for (int j = 1; j <= Samples; j++)
            {
                float t = j / (float)Samples;
                var p = t <= .5f ? Vector3.Lerp(hip, knee, t * 2f) : Vector3.Lerp(knee, ankle, (t - .5f) * 2f);
                float h = Height(p);
                // Near the waist the skirt barely moves; near/below the hem there is nothing to contain.
                if (h > pivotHeight - (pivotHeight - hem) * .2f || h < hem) continue;
                float need = Radius(p) + LegRadius + margin;
                float a = Angle(p);
                float spread = Mathf.Atan2(LegRadius + margin, Mathf.Max(1e-4f, Radius(p)));
                for (int i = 0; i < BoneCount; i++)
                {
                    float boneAngle = (float)i / BoneCount * 2f * Mathf.PI;
                    float d = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, boneAngle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    if (d > spread + 2f * Mathf.PI / BoneCount) continue;
                    int s = Mathf.Min(Sectors - 1, (int)(boneAngle / (2f * Mathf.PI) * Sectors));
                    float rest = ProfileAt(h, s);
                    if (Flared(rest, h, floor[i]) >= need) continue;
                    // Smallest flare that clears this point (bisection; Flared is monotonic in phi here).
                    float lo = floor[i], hi = MaxFlare;
                    if (Flared(rest, h, hi) < need) { floor[i] = hi; continue; }
                    for (int it = 0; it < 12; it++) { float m = (lo + hi) * .5f; if (Flared(rest, h, m) >= need) hi = m; else lo = m; }
                    floor[i] = hi;
                }
            }
        }

        /// <summary>Deepest thigh intrusion through the skirt right now, in metres (≤ 0 = contained).</summary>
        public float LegIntrusion()
        {
            if (!Ready) return 0f;
            float worst = float.MinValue;
            foreach (var leg in new[] { leftLeg, rightLeg })
            {
                Vector3 hip = Hips.InverseTransformPoint(leg[0].position), knee = Hips.InverseTransformPoint(leg[1].position);
                for (int j = 1; j <= 10; j++)
                {
                    var p = Vector3.Lerp(hip, knee, j / 10f);
                    float h = Height(p);
                    if (h > pivotHeight - (pivotHeight - hem) * .2f || h < hem) continue;
                    if (SurfaceRadius(p, out float surface)) worst = Mathf.Max(worst, (Radius(p) + LegRadius - surface) * Hips.lossyScale.x);
                }
            }
            return worst == float.MinValue ? -1f : worst;
        }

        void OnDrawGizmosSelected()
        {
            if (!Ready) return;
            Gizmos.color = new Color(1f, .4f, .7f, .8f);
            for (int i = 0; i < BoneCount; i++)
            {
                var a = bones[i].position;
                var b = bones[i].TransformPoint(-Up * (pivotHeight - hem));
                Gizmos.DrawLine(a, b);
            }
        }
    }
}
