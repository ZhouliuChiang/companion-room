using UnityEngine;

namespace CompanionRoom
{
    /// <summary>
    /// Floor feedback for click-to-walk: a pink ring ripples out where she is heading, or a red ring
    /// shrinks away where she cannot go.
    /// </summary>
    public sealed class WitchClickMarker : MonoBehaviour
    {
        const int Points = 40;
        static readonly Color Go = new(1f, .55f, .78f, 1f), NoGo = new(.9f, .3f, .35f, 1f);

        WitchLocomotion walker;
        LineRenderer ring;
        float shownAt = -10f;
        bool accepted;

        void OnEnable()
        {
            walker = GetComponent<WitchLocomotion>();
            if (walker != null) walker.DestinationChosen += Show;
        }

        void OnDisable()
        {
            if (walker != null) walker.DestinationChosen -= Show;
            if (ring != null) ring.enabled = false;
        }

        void OnDestroy()
        {
            if (ring != null) { Destroy(ring.sharedMaterial); Destroy(ring.gameObject); }
        }

        void Show(Vector3 point, bool ok)
        {
            if (ring == null)
            {
                // Not parented to her: the marker stays on the floor while she walks.
                var go = new GameObject("點擊位置標記");
                ring = go.AddComponent<LineRenderer>();
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = Points;
                ring.numCapVertices = 2;
                ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ring.receiveShadows = false;
                ring.alignment = LineAlignment.TransformZ;
                ring.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                for (int i = 0; i < Points; i++)
                {
                    float a = i * 2f * Mathf.PI / Points;
                    ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
                }
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat on the floor
            }
            ring.transform.position = new Vector3(point.x, walker.floor + .006f, point.z);
            accepted = ok;
            shownAt = Time.time;
            ring.enabled = true;
            Update();
        }

        void Update()
        {
            if (ring == null || !ring.enabled) return;
            float duration = accepted ? 1.1f : .75f;
            float t = (Time.time - shownAt) / duration;
            if (t >= 1f) { ring.enabled = false; return; }
            float ease = 1f - (1f - t) * (1f - t);
            float size = accepted ? Mathf.Lerp(.05f, .16f, ease) : Mathf.Lerp(.14f, .05f, ease);
            ring.transform.localScale = Vector3.one * size;
            ring.widthMultiplier = .014f / size; // positions are scaled; keep the line ~1.4 cm wide
            var color = accepted ? Go : NoGo;
            color.a = 1f - t;
            ring.startColor = ring.endColor = color;
        }
    }
}
