using UnityEngine;

namespace CompanionRoom
{
    /// <summary>A small, slow variation on the roof ornament material only.</summary>
    public sealed class SoftRoofGlow : MonoBehaviour
    {
        public Renderer target;
        public int materialIndex = 1;
        public Color color = new(1f, .78f, .39f);
        public float strength = .80f;
        public float variation = .07f;
        MaterialPropertyBlock block;
        void Update()
        {
            if (target == null) return;
            block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(block, materialIndex);
            block.SetColor("_EmissionColor", color * (strength + variation * Mathf.Sin(Time.time * .65f)));
            target.SetPropertyBlock(block, materialIndex);
        }
    }
}
