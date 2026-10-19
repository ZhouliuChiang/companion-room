using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace CompanionRoom.UI
{
    /// <summary>Rounded card surface with real depth vertices, rotated around Y in the UI canvas.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TarotSolidGraphic : MaskableGraphic
    {
        public enum Surface { Front, Back, Edge }
        public const float ThicknessRatio = 1.6f / 70f;
        readonly Vector2[] ring = new Vector2[36];
        Sprite sprite;
        Surface surface;
        float angle = 180f, lift;
        bool reversed;
        public override Texture mainTexture => sprite != null ? sprite.texture : Texture2D.whiteTexture;

        public void Configure(Surface value, Sprite art)
        {
            surface = value; sprite = art;
            raycastTarget = value != Surface.Edge;
            SetAllDirty();
        }

        public void SetPose(float degrees, float verticalLift, bool upsideDown)
        {
            if (angle == degrees && lift == verticalLift && reversed == upsideDown) return;
            angle = degrees; lift = verticalLift; reversed = upsideDown;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float c = Mathf.Cos(angle * Mathf.Deg2Rad), s = Mathf.Sin(angle * Mathf.Deg2Rad);
            if ((surface == Surface.Front && c <= .00001f) || (surface == Surface.Back && c >= -.00001f)) return;
            var rect = GetPixelAdjustedRect();
            float w = rect.width, h = rect.height, r = w * 4f / 70f, z = w * ThicknessRatio * .5f;
            if (w <= 0 || h <= 0) return;
            for (int corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner == 0 || corner == 3 ? w * .5f - r : -w * .5f + r,
                    corner < 2 ? h * .5f - r : -h * .5f + r);
                for (int j = 0; j <= 8; j++)
                {
                    float a = (corner * 90f + j * 90f / 8) * Mathf.Deg2Rad;
                    ring[corner * 9 + j] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                }
            }
            Vector3 Rotate(Vector2 p, float depth) => new Vector3(p.x * c + depth * s + rect.center.x,
                p.y + lift + rect.center.y, -p.x * s + depth * c);
            if (surface == Surface.Edge)
            {
                for (int i = 0; i < ring.Length; i++)
                {
                    var p = ring[i]; var q = ring[(i + 1) % ring.Length]; var d = q - p;
                    var normal = new Vector2(d.y, -d.x).normalized;
                    if (normal.x * s <= .00001f) continue;
                    var tint = color * Mathf.Lerp(.72f, 1f, Mathf.Abs(normal.x * s)); tint.a = color.a;
                    int v = vh.currentVertCount;
                    vh.AddVert(Rotate(p, -z), tint, Vector2.zero); vh.AddVert(Rotate(p, z), tint, Vector2.zero);
                    vh.AddVert(Rotate(q, z), tint, Vector2.zero); vh.AddVert(Rotate(q, -z), tint, Vector2.zero);
                    vh.AddTriangle(v, v + 1, v + 2); vh.AddTriangle(v, v + 2, v + 3);
                }
                return;
            }
            var bounds = sprite != null ? DataUtility.GetOuterUV(sprite) : new Vector4(0, 0, 1, 1);
            Vector2 UV(Vector2 p)
            {
                float u = .5f + p.x / w * (surface == Surface.Back ? -1f : 1f), v = .5f + p.y / h;
                if (surface == Surface.Front && reversed) { u = 1 - u; v = 1 - v; }
                return new Vector2(Mathf.Lerp(bounds.x, bounds.z, u), Mathf.Lerp(bounds.y, bounds.w, v));
            }
            float faceZ = surface == Surface.Front ? -z : z;
            vh.AddVert(Rotate(Vector2.zero, faceZ), color, UV(Vector2.zero));
            foreach (var p in ring) vh.AddVert(Rotate(p, faceZ), color, UV(p));
            for (int i = 0; i < ring.Length; i++) vh.AddTriangle(0, i + 1, (i + 1) % ring.Length + 1);
        }
    }
}
