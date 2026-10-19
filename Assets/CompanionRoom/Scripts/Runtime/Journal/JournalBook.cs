using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CompanionRoom.Journal
{
    /// <summary>
    /// The record journal as a 3D object built from code: two covers, a block of pages on each side,
    /// the spine, one bending leaf for page turns, and pop-up cards that stand up from the left page.
    /// Book space (1 unit = one page height): the open book lies in the XY plane facing -Z, towards
    /// the reader; the spine runs along Y at x = 0 and the right-hand pages are on +X. "Height" above
    /// the back cover grows towards -Z. The front cover, left page block and pop-ups all turn together
    /// around one hinge, so the book closes over whatever page it is open at.
    /// </summary>
    sealed class JournalBook : MonoBehaviour
    {
        public const float PageW = JournalLayout.PageWidth;
        public const float PageH = JournalLayout.PageHeight;
        public const float CoverW = PageW + 0.026f;
        public const float CoverH = PageH + 0.04f;
        public const float CoverT = 0.02f;
        public const float PagesT = 0.07f;
        const float Gap = 0.004f;          // between the spine and the page blocks
        const float PopUpAngle = 58f;      // how far the cards stand up from the page
        const int LeafColumns = 28;
        const int LeafRows = 6;

        public static float HingeHeight => CoverT + PagesT * 0.5f;

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int CullId = Shader.PropertyToID("_Cull");

        sealed class PopUp
        {
            public Transform pivot;
            public Material face;
            public float angle;
            public float velocity;
            public float delay;
        }

        Material template;
        Transform hinge;
        Transform spine;
        MeshFilter leftBlock;
        MeshFilter rightBlock;
        MeshRenderer leftBlockRenderer;
        MeshFilter leaf;
        MeshRenderer leafRenderer;
        Transform popUpRoot;
        readonly List<PopUp> popUps = new();
        readonly List<Material> materials = new();
        Material frontOuter, frontInner, backOuter, leftPage, rightPage, leafFront, leafBack, cardBack;
        Vector3[] leafVertices;
        Vector3[] leafNormals;
        float leftThickness;
        float rightThickness = PagesT;
        float open;

        public float LeftThickness => leftThickness;
        public float RightThickness => rightThickness;
        public bool HasPopUps => popUps.Count > 0;

        /// <summary>Where the middle of the book is, in book space, for a given openness (keeps it centred while it opens).</summary>
        public static Vector3 CenterOffset(float openness) => new(CoverW * 0.5f * (1f - openness), 0f, -HingeHeight);

        /// <summary>Builds the book; every part (pop-up cards included) goes on <paramref name="layer"/>.</summary>
        public static JournalBook Create(Material paperTemplate, Color coverColor, Color paperColor, int layer = 0)
        {
            var go = new GameObject("RecordJournalBook") { layer = layer };
            var book = go.AddComponent<JournalBook>();
            book.Build(paperTemplate, coverColor, paperColor);
            return book;
        }

        void Build(Material paperTemplate, Color coverColor, Color paperColor)
        {
            template = paperTemplate;
            var coverSide = NewMaterial(Texture2D.whiteTexture, coverColor);
            var pageEdge = NewMaterial(JournalArt.PageEdges, paperColor);
            var paper = NewMaterial(Texture2D.whiteTexture, paperColor);
            // Pages a touch under full white, so the room's bloom doesn't haze the writing.
            var pageTint = new Color(0.95f, 0.95f, 0.95f, 1f);
            frontOuter = NewMaterial(Texture2D.whiteTexture, Color.white);
            frontInner = NewMaterial(Texture2D.whiteTexture, pageTint);
            backOuter = NewMaterial(Texture2D.whiteTexture, Color.white);
            leftPage = NewMaterial(Texture2D.whiteTexture, pageTint);
            rightPage = NewMaterial(Texture2D.whiteTexture, pageTint);
            leafFront = NewMaterial(Texture2D.whiteTexture, pageTint);
            leafBack = NewMaterial(Texture2D.whiteTexture, pageTint);
            cardBack = NewMaterial(Texture2D.whiteTexture, Color.white);

            float zh = HingeHeight;

            // Back cover and right-hand pages stay put.
            var back = Part("BackCover", transform, Vector3.zero);
            var kit = new MeshKit(3);
            kit.Box(new Vector3(0f, -CoverH * 0.5f, -CoverT), new Vector3(CoverW, CoverH * 0.5f, 0f), 0, 1, 2,
                p => new Vector2(p.x / CoverW, p.y / CoverH + 0.5f),
                p => new Vector2(1f - p.x / CoverW, p.y / CoverH + 0.5f));
            Finish(back, kit, paper, backOuter, coverSide);

            rightBlock = Part("RightPages", transform, Vector3.zero).GetComponent<MeshFilter>();
            rightBlock.GetComponent<MeshRenderer>().sharedMaterials = new[] { rightPage, pageEdge };

            spine = Part("Spine", transform, Vector3.zero).transform;
            kit = new MeshKit(1);
            kit.Box(new Vector3(-CoverT, -CoverH * 0.5f, -1f), new Vector3(0f, CoverH * 0.5f, 0f), 0, 0, 0, null, null);
            Finish(spine.gameObject, kit, coverSide);

            // Everything on the left turns around the hinge.
            hinge = new GameObject("LeftHalf") { layer = gameObject.layer }.transform;
            hinge.SetParent(transform, false);
            hinge.localPosition = new Vector3(0f, 0f, -zh);
            var front = Part("FrontCover", hinge, Vector3.zero);
            kit = new MeshKit(3);
            kit.Box(new Vector3(-CoverW, -CoverH * 0.5f, zh - CoverT), new Vector3(0f, CoverH * 0.5f, zh), 0, 1, 2,
                p => new Vector2((p.x + CoverW) / CoverW, p.y / CoverH + 0.5f),
                p => new Vector2(-p.x / CoverW, p.y / CoverH + 0.5f));
            Finish(front, kit, frontInner, frontOuter, coverSide);

            leftBlock = Part("LeftPages", hinge, Vector3.zero).GetComponent<MeshFilter>();
            leftBlockRenderer = leftBlock.GetComponent<MeshRenderer>();
            leftBlockRenderer.sharedMaterials = new[] { leftPage, pageEdge };

            popUpRoot = new GameObject("PopUps") { layer = gameObject.layer }.transform;
            popUpRoot.SetParent(hinge, false);

            // The turning leaf.
            var leafObject = Part("Leaf", transform, Vector3.zero);
            leaf = leafObject.GetComponent<MeshFilter>();
            leafRenderer = leafObject.GetComponent<MeshRenderer>();
            leafRenderer.sharedMaterials = new[] { leafFront, leafBack };
            BuildLeaf();
            leafRenderer.enabled = false;

            SetThickness(0f, PagesT);
            SetOpenness(0f);
        }

        // ------------------------------------------------------------------ textures

        public void SetCoverTextures(Texture outsideFront, Texture outsideBack, Texture insideFront)
        {
            SetTexture(frontOuter, outsideFront);
            SetTexture(backOuter, outsideBack);
            SetTexture(frontInner, insideFront);
        }

        public void SetPages(Texture left, Texture right)
        {
            if (left != null)
                SetTexture(leftPage, left);
            if (right != null)
                SetTexture(rightPage, right);
        }

        public void SetCardBack(Texture texture) => SetTexture(cardBack, texture);

        static void SetTexture(Material material, Texture texture) => material.SetTexture(BaseMapId, texture != null ? texture : Texture2D.whiteTexture);

        // ------------------------------------------------------------------ shape

        /// <summary>0 = closed (front cover on top), 1 = lying open.</summary>
        public void SetOpenness(float amount)
        {
            open = Mathf.Clamp01(amount);
            hinge.localRotation = Quaternion.Euler(0f, -180f * (1f - open), 0f);
            float height = (2f * CoverT + PagesT) * (1f - open);
            spine.gameObject.SetActive(height > 0.002f);
            spine.localScale = new Vector3(1f, 1f, Mathf.Max(0.001f, height));
        }

        /// <summary>How thick the stacks of pages on each side are (they shift as you read on).</summary>
        public void SetThickness(float left, float right)
        {
            left = Mathf.Max(0f, left);
            right = Mathf.Max(0.004f, right);
            if (Mathf.Abs(left - leftThickness) < 1e-5f && Mathf.Abs(right - rightThickness) < 1e-5f && rightBlock.sharedMesh != null)
                return;
            leftThickness = left;
            rightThickness = right;
            float zh = HingeHeight;

            var kit = new MeshKit(2);
            kit.Box(new Vector3(Gap, -PageH * 0.5f, -(CoverT + right)), new Vector3(Gap + PageW, PageH * 0.5f, -CoverT), 0, 1, 1,
                p => new Vector2((p.x - Gap) / PageW, p.y / PageH + 0.5f), null, right * 60f);
            kit.Apply(Mesh(rightBlock));

            bool hasLeft = left > 0.0005f;
            leftBlockRenderer.enabled = hasLeft;
            if (hasLeft)
            {
                kit = new MeshKit(2);
                kit.Box(new Vector3(-Gap - PageW, -PageH * 0.5f, zh - (CoverT + left)), new Vector3(-Gap, PageH * 0.5f, zh - CoverT), 0, 1, 1,
                    p => new Vector2((p.x + Gap + PageW) / PageW, p.y / PageH + 0.5f), null, left * 60f);
                kit.Apply(Mesh(leftBlock));
            }
            popUpRoot.localPosition = new Vector3(0f, 0f, zh - (CoverT + left) - 0.0008f);
        }

        static Mesh Mesh(MeshFilter filter)
        {
            if (filter.sharedMesh == null)
                filter.sharedMesh = new Mesh { name = filter.name };
            return filter.sharedMesh;
        }

        /// <summary>Is the point under the pointer on the open book? side: -1 left page, +1 right page.</summary>
        public bool HitTest(Ray ray, out int side)
        {
            side = 0;
            float height = CoverT + Mathf.Max(leftThickness, rightThickness);
            var plane = new Plane(transform.TransformDirection(Vector3.back), transform.TransformPoint(new Vector3(0f, 0f, -height)));
            if (!plane.Raycast(ray, out float distance))
                return false;
            var local = transform.InverseTransformPoint(ray.GetPoint(distance));
            if (Mathf.Abs(local.y) > CoverH * 0.5f || Mathf.Abs(local.x) > CoverW)
                return false;
            side = local.x >= 0f ? 1 : -1;
            return true;
        }

        // ------------------------------------------------------------------ the turning leaf

        public void ShowLeaf(Texture front, Texture back)
        {
            SetTexture(leafFront, front);
            SetTexture(leafBack, back);
            leafRenderer.enabled = true;
        }

        public void HideLeaf() => leafRenderer.enabled = false;

        /// <summary>
        /// Bends the leaf for a turn <paramref name="progress"/> (0..1) of the way across. Forward turns
        /// (<paramref name="direction"/> 1) lift the right page from its outer corner and lay it down on
        /// the left; backward turns do the opposite. The corner leads and the page curls over.
        /// </summary>
        public void BendLeaf(float progress, int direction)
        {
            progress = Mathf.Clamp01(progress);
            float eased = progress * progress * (3f - 2f * progress);
            float root = Mathf.PI * (direction >= 0 ? eased : 1f - eased);
            float lead = (direction >= 0 ? 1f : -1f) * 1.15f * Mathf.Sin(Mathf.PI * progress);
            float rightTop = CoverT + rightThickness;
            float leftTop = CoverT + leftThickness;
            float rootHeight = Mathf.Lerp(rightTop, leftTop, root / Mathf.PI) + 0.0016f;
            float rootX = Gap * Mathf.Cos(root);
            float segment = PageW / LeafColumns;
            int stride = LeafColumns + 1;
            int half = stride * (LeafRows + 1);

            for (int j = 0; j <= LeafRows; j++)
            {
                float v = j / (float)LeafRows;
                float y = (v - 0.5f) * PageH;
                // The top corner leads a little more than the bottom: the page is lifted by its corner.
                float twist = 0.8f + 0.4f * v;
                float x = rootX, h = rootHeight;
                for (int i = 0; i <= LeafColumns; i++)
                {
                    float u = i / (float)LeafColumns;
                    float angle = Mathf.Clamp(root + lead * Mathf.Pow(u, 1.6f) * twist, 0f, Mathf.PI);
                    if (i > 0)
                    {
                        float mid = Mathf.Clamp(root + lead * Mathf.Pow(u - 0.5f / LeafColumns, 1.6f) * twist, 0f, Mathf.PI);
                        x += Mathf.Cos(mid) * segment;
                        h += Mathf.Sin(mid) * segment;
                    }
                    int index = j * stride + i;
                    var position = new Vector3(x, y, -h);
                    var normal = new Vector3(-Mathf.Sin(angle), 0f, -Mathf.Cos(angle));
                    leafVertices[index] = position;
                    leafNormals[index] = normal;
                    leafVertices[half + index] = position;
                    leafNormals[half + index] = -normal;
                }
            }
            var mesh = leaf.sharedMesh;
            mesh.vertices = leafVertices;
            mesh.normals = leafNormals;
            mesh.RecalculateBounds();
        }

        void BuildLeaf()
        {
            int stride = LeafColumns + 1;
            int count = stride * (LeafRows + 1);
            leafVertices = new Vector3[count * 2];
            leafNormals = new Vector3[count * 2];
            var uv = new Vector2[count * 2];
            var front = new List<int>();
            var back = new List<int>();
            for (int j = 0; j <= LeafRows; j++)
                for (int i = 0; i <= LeafColumns; i++)
                {
                    float u = i / (float)LeafColumns, v = j / (float)LeafRows;
                    int index = j * stride + i;
                    uv[index] = new Vector2(u, v);
                    uv[count + index] = new Vector2(1f - u, v);
                    leafVertices[index] = leafVertices[count + index] = new Vector3(Gap + u * PageW, (v - 0.5f) * PageH, -(CoverT + PagesT));
                }
            // Flat on the right the front faces the reader (-Z): (x+, y+) quads wound so their normal is -Z.
            for (int j = 0; j < LeafRows; j++)
                for (int i = 0; i < LeafColumns; i++)
                {
                    int a = j * stride + i, b = a + 1, c = a + stride, d = c + 1;
                    front.AddRange(new[] { a, c, b, b, c, d });
                    back.AddRange(new[] { count + a, count + b, count + c, count + b, count + d, count + c });
                }
            var mesh = new Mesh { name = "JournalLeaf" };
            mesh.MarkDynamic();
            mesh.vertices = leafVertices;
            mesh.uv = uv;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(front, 0);
            mesh.SetTriangles(back, 1);
            mesh.RecalculateNormals();
            leaf.sharedMesh = mesh;
            BendLeaf(0f, 1);
        }

        // ------------------------------------------------------------------ pop-up cards

        /// <summary>Lays one card per footprint flat on the left page; <see cref="StepPopUps"/> stands them up.</summary>
        public void SetPopUps(IReadOnlyList<(Rect footprint, Texture face)> cards)
        {
            ClearPopUps();
            for (int i = 0; i < cards.Count; i++)
            {
                var (footprint, face) = cards[i];
                float width = footprint.width * PageW;
                float height = footprint.height * PageH;
                var pivot = new GameObject("PopUpCard" + i) { layer = gameObject.layer }.transform;
                pivot.SetParent(popUpRoot, false);
                pivot.localPosition = new Vector3(-Gap - PageW + footprint.center.x * PageW, footprint.yMin * PageH - PageH * 0.5f, 0f);

                var card = Part("Card", pivot, Vector3.zero);
                var kit = new MeshKit(2);
                var a = new Vector3(-width * 0.5f, 0f, -0.0006f);
                var b = new Vector3(width * 0.5f, 0f, -0.0006f);
                var c = new Vector3(width * 0.5f, height, -0.0006f);
                var d = new Vector3(-width * 0.5f, height, -0.0006f);
                kit.Quad(0, a, b, c, d, Vector3.back, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
                var shift = new Vector3(0f, 0f, 0.0004f);
                kit.Quad(1, a + shift, b + shift, c + shift, d + shift, Vector3.forward, new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f));
                var faceMaterial = NewMaterial(face, Color.white);
                Finish(card, kit, faceMaterial, cardBack);
                popUps.Add(new PopUp { pivot = pivot, face = faceMaterial, delay = i * 0.08f });
            }
        }

        public void ClearPopUps()
        {
            foreach (var popUp in popUps)
            {
                materials.Remove(popUp.face);
                Destroy(popUp.face);
                Destroy(popUp.pivot.gameObject);
            }
            popUps.Clear();
        }

        /// <summary>Springs the cards towards standing (or folded), never above <paramref name="limit"/> (0..1).</summary>
        public void StepPopUps(float dt, bool standing, float limit = 1f)
        {
            float target = standing ? PopUpAngle : 0f;
            float cap = PopUpAngle * Mathf.Clamp01(limit);
            foreach (var popUp in popUps)
            {
                if (standing && popUp.delay > 0f)
                {
                    popUp.delay -= dt;
                    continue;
                }
                // Lively spring with a little overshoot, stepped finely so it stays stable.
                for (float t = 0f; t < dt; t += 1f / 240f)
                {
                    float step = Mathf.Min(1f / 240f, dt - t);
                    float accel = 260f * (target - popUp.angle) - 17f * popUp.velocity;
                    popUp.velocity += accel * step;
                    popUp.angle += popUp.velocity * step;
                }
                if (popUp.angle > cap)
                {
                    popUp.angle = cap;
                    popUp.velocity = Mathf.Min(0f, popUp.velocity);
                }
                popUp.angle = Mathf.Max(0f, popUp.angle);
                popUp.pivot.localRotation = Quaternion.Euler(-popUp.angle, 0f, 0f);
            }
        }

        /// <summary>Gives one card a little hop (the one being talked about).</summary>
        public void NudgePopUp(int index)
        {
            if (index >= 0 && index < popUps.Count)
                popUps[index].velocity -= 160f;
        }

        public bool PopUpsFolded
        {
            get
            {
                foreach (var popUp in popUps)
                    if (popUp.angle > 0.8f)
                        return false;
                return true;
            }
        }

        public bool PopUpsStanding
        {
            get
            {
                foreach (var popUp in popUps)
                    if (popUp.angle < PopUpAngle * 0.5f)
                        return false;
                return true;
            }
        }

        // ------------------------------------------------------------------ building blocks

        Material NewMaterial(Texture texture, Color tint)
        {
            var material = new Material(template) { hideFlags = HideFlags.DontSave };
            material.SetTexture(BaseMapId, texture);
            material.SetColor(BaseColorId, tint);
            material.SetFloat(CullId, (float)CullMode.Back);
            materials.Add(material);
            return material;
        }

        static GameObject Part(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        static void Finish(GameObject part, MeshKit kit, params Material[] partMaterials)
        {
            var mesh = new Mesh { name = part.name };
            kit.Apply(mesh);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterials = partMaterials;
        }

        void OnDestroy()
        {
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    Destroy(filter.sharedMesh);
            foreach (var material in materials)
                if (material != null)
                    Destroy(material);
        }

        /// <summary>Collects quads into a mesh with a few submeshes; winding follows each quad's normal.</summary>
        sealed class MeshKit
        {
            readonly List<Vector3> vertices = new();
            readonly List<Vector3> normals = new();
            readonly List<Vector2> uvs = new();
            readonly List<int>[] triangles;

            public MeshKit(int submeshes)
            {
                triangles = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++)
                    triangles[i] = new List<int>();
            }

            public void Quad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                normals.AddRange(new[] { normal, normal, normal, normal });
                uvs.AddRange(new[] { ua, ub, uc, ud });
                var list = triangles[submesh];
                // Unity's front faces satisfy cross(b - a, c - a) · normal > 0.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0f)
                    list.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                else
                    list.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            /// <summary>
            /// A box: the -Z face (towards the reader) into <paramref name="top"/>, the +Z face into
            /// <paramref name="bottom"/>, the sides into <paramref name="sides"/>. Side UVs tile
            /// <paramref name="sideRepeat"/> times across the thickness (for the page-edge lines).
            /// </summary>
            public void Box(Vector3 min, Vector3 max, int top, int bottom, int sides,
                System.Func<Vector3, Vector2> topUv, System.Func<Vector3, Vector2> bottomUv, float sideRepeat = 1f)
            {
                Vector2 Uv(System.Func<Vector3, Vector2> map, Vector3 p) => map != null ? map(p) : new Vector2(p.x, p.y);
                var p000 = new Vector3(min.x, min.y, min.z);
                var p100 = new Vector3(max.x, min.y, min.z);
                var p110 = new Vector3(max.x, max.y, min.z);
                var p010 = new Vector3(min.x, max.y, min.z);
                var p001 = new Vector3(min.x, min.y, max.z);
                var p101 = new Vector3(max.x, min.y, max.z);
                var p111 = new Vector3(max.x, max.y, max.z);
                var p011 = new Vector3(min.x, max.y, max.z);
                Quad(top, p000, p100, p110, p010, Vector3.back, Uv(topUv, p000), Uv(topUv, p100), Uv(topUv, p110), Uv(topUv, p010));
                Quad(bottom, p001, p101, p111, p011, Vector3.forward, Uv(bottomUv, p001), Uv(bottomUv, p101), Uv(bottomUv, p111), Uv(bottomUv, p011));
                float r = Mathf.Max(0.01f, sideRepeat);
                float lengthX = (max.x - min.x) * 4f, lengthY = (max.y - min.y) * 4f;
                Quad(sides, p000, p100, p101, p001, Vector3.down, new Vector2(0f, 0f), new Vector2(lengthX, 0f), new Vector2(lengthX, r), new Vector2(0f, r));
                Quad(sides, p010, p110, p111, p011, Vector3.up, new Vector2(0f, 0f), new Vector2(lengthX, 0f), new Vector2(lengthX, r), new Vector2(0f, r));
                Quad(sides, p000, p010, p011, p001, Vector3.left, new Vector2(0f, 0f), new Vector2(lengthY, 0f), new Vector2(lengthY, r), new Vector2(0f, r));
                Quad(sides, p100, p110, p111, p101, Vector3.right, new Vector2(0f, 0f), new Vector2(lengthY, 0f), new Vector2(lengthY, r), new Vector2(0f, r));
            }

            public void Apply(Mesh mesh)
            {
                mesh.Clear();
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < triangles.Length; i++)
                    mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds();
            }
        }
    }
}
