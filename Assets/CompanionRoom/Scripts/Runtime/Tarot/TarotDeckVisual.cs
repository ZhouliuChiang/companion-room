using System.Collections.Generic;
using UnityEngine;

namespace CompanionRoom
{
    /// <summary>Uses the illustrated moon-rabbit deck on the imported tabletop prop at runtime.</summary>
    public sealed class TarotDeckVisual : MonoBehaviour
    {
        Mesh mesh;
        Material paper, back;

        void Awake()
        {
            var texture = Resources.Load<Texture2D>("CompanionRoom/Tarot/Art/card_back");
            if (texture == null) return;
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            foreach (var renderer in renderers) renderer.enabled = false;
            mesh = CreateMesh();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            paper = new Material(shader); paper.SetColor("_BaseColor", new Color(.96f, .86f, .73f));
            back = new Material(shader); back.SetTexture("_BaseMap", texture); back.SetFloat("_Smoothness", .18f);
            // Preserve the placed prop's footprint and tabletop contact, independent of import scale.
            float scale = Mathf.Clamp(Mathf.Max(bounds.size.x / .07f, bounds.size.z / .115f), .65f, 1.6f);
            var modelParent = transform.Find(RoomItem.ModelChild) ?? transform;
            for (int i = 0; i < 12; i++)
            {
                var card = new GameObject("PastelDeckCard" + i, typeof(MeshFilter), typeof(MeshRenderer));
                card.layer = gameObject.layer;
                card.transform.SetParent(modelParent, false);
                card.transform.position = new Vector3(bounds.center.x, bounds.min.y + (.0008f + i * .0016f) * scale, bounds.center.z);
                card.transform.rotation = Quaternion.Euler(90, 12, 0);
                // Compensate for imported model scaling so the physical thickness stays consistent.
                var parentScale = modelParent.lossyScale;
                card.transform.localScale = new Vector3(scale / Mathf.Abs(parentScale.x), scale / Mathf.Abs(parentScale.y), scale / Mathf.Abs(parentScale.z));
                card.GetComponent<MeshFilter>().sharedMesh = mesh;
                card.GetComponent<MeshRenderer>().sharedMaterials = new[] { paper, back, paper };
            }
            // The original prop collider remains the interaction target; include the new stack height.
            var hit = gameObject.AddComponent<BoxCollider>();
            var localCenter = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y + .0096f * scale, bounds.center.z));
            hit.center = localCenter;
            var s = transform.lossyScale;
            hit.size = new Vector3(.1f * scale / Mathf.Abs(s.x), .025f * scale / Mathf.Abs(s.y), .15f * scale / Mathf.Abs(s.z));
        }

        static Mesh CreateMesh()
        {
            var ring = new List<Vector2>();
            for (int k = 0; k < 4; k++)
            {
                var center = new Vector2(k == 0 || k == 3 ? .031f : -.031f, k < 2 ? .0535f : -.0535f);
                for (int j = 0; j <= 8; j++)
                {
                    float a = (k * 90 + j * 90f / 8) * Mathf.Deg2Rad;
                    ring.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * .004f);
                }
            }
            var v = new List<Vector3>(); var uv = new List<Vector2>();
            var front = new List<int>(); var rear = new List<int>(); var edge = new List<int>(); int n = ring.Count;
            for (int side = 0; side < 2; side++)
            {
                int start = v.Count;
                v.Add(new Vector3(0, 0, side == 0 ? .0008f : -.0008f)); uv.Add(new Vector2(.5f, .5f));
                foreach (var p in ring)
                {
                    v.Add(new Vector3(p.x, p.y, side == 0 ? .0008f : -.0008f));
                    uv.Add(new Vector2(.5f + p.x / .07f * (side == 0 ? -1 : 1), .5f + p.y / .115f));
                }
                for (int i = 0; i < n; i++)
                {
                    var t = side == 0 ? front : rear;
                    t.Add(start); t.Add(start + 1 + (side == 0 ? i : (i + 1) % n));
                    t.Add(start + 1 + (side == 0 ? (i + 1) % n : i));
                }
            }
            for (int i = 0; i < n; i++)
            {
                var p = ring[i]; var q = ring[(i + 1) % n]; int start = v.Count;
                v.Add(new Vector3(p.x, p.y, .0008f)); v.Add(new Vector3(p.x, p.y, -.0008f));
                v.Add(new Vector3(q.x, q.y, -.0008f)); v.Add(new Vector3(q.x, q.y, .0008f));
                uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one); uv.Add(Vector2.right);
                edge.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            var mesh = new Mesh { name = "SolidTarotDeckCard", subMeshCount = 3 };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(front, 0); mesh.SetTriangles(rear, 1); mesh.SetTriangles(edge, 2);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (paper != null) Destroy(paper);
            if (back != null) Destroy(back);
        }
    }
}
