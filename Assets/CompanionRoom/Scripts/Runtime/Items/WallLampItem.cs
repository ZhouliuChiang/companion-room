using System.Collections;
using UnityEngine;

namespace CompanionRoom
{
    /// <summary>壁燈: click to switch it on or off (remembered). 凡安 comments now and then.</summary>
    public class WallLampItem : RoomItem
    {
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [Header("燈")]
        [Tooltip("The lamp's light. Empty = a Light found under the item.")]
        [SerializeField] Light lamp;
        [SerializeField] float intensity = 2.4f;
        [Tooltip("Parts that glow while lit (bulb, shade). Empty = children named \"Bulb\" or \"Shade\".")]
        [SerializeField] Renderer[] glowParts = System.Array.Empty<Renderer>();
        [SerializeField] Color glowColor = new(1f, 0.78f, 0.52f);
        [SerializeField, Min(0f)] float glow = 1.4f;
        [Tooltip("Optional soft halo sprite (CompanionRoom/SoftGlow material).")]
        [SerializeField] Renderer halo;
        [SerializeField] float haloIntensity = 0.45f;
        [SerializeField] bool startOn;

        Material[] materials = System.Array.Empty<Material>();
        MaterialPropertyBlock block;
        bool on;
        float level;

        public bool IsOn => on;

        void Start()
        {
            if (lamp == null)
                lamp = GetComponentInChildren<Light>(true);
            if (glowParts == null || glowParts.Length == 0)
            {
                var part = FindPart<Renderer>(null, "Bulb", "Shade");
                glowParts = part != null ? new[] { part } : System.Array.Empty<Renderer>();
            }
            materials = new Material[glowParts.Length];
            for (int i = 0; i < glowParts.Length; i++)
            {
                if (glowParts[i] == null)
                    continue;
                materials[i] = glowParts[i].material;
                materials[i].EnableKeyword("_EMISSION");
                materials[i].globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            on = RoomSettings.GetInt("LampOn", startOn ? 1 : 0) == 1;
            level = on ? 1f : 0f;
            Apply();
        }

        void OnDestroy()
        {
            foreach (var m in materials)
                if (m != null)
                    Destroy(m);
        }

        public override IEnumerator Interact(CompanionRoomApp app)
        {
            Toggle(app);
            yield return CompanionRoomApp.Wait(0.35f);
            // The new room has no companion; switching a lamp should not open old character dialogue.
            if (app.Companion == null)
                yield break;
            if (!TakeCommentTurn())
                yield break;
            string key = on ? (app.Lighting != null && app.Lighting.IsNight ? "lamp_on_night" : "lamp_on") : "lamp_off";
            yield return app.Dialogue.Say(app.Lines.Pick(key, on ? "lamp_on" : "lamp_off"));
        }

        public void Toggle(CompanionRoomApp app = null)
        {
            on = !on;
            RoomSettings.SetInt("LampOn", on ? 1 : 0);
            app?.Audio?.Play(RoomSound.Switch);
            enabled = true;
        }

        void Update()
        {
            float target = on ? 1f : 0f;
            if (Mathf.Approximately(level, target))
                return;
            level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime / 0.25f);
            Apply();
        }

        void Apply()
        {
            // A quick warm-up curve, like a bulb catching.
            float eased = level * level * (3f - 2f * level);
            if (lamp != null)
            {
                lamp.intensity = intensity * eased;
                lamp.enabled = eased > 0.001f;
            }
            foreach (var m in materials)
                if (m != null)
                    m.SetColor(EmissionColorId, glowColor * (glow * eased));
            if (halo != null)
            {
                block ??= new MaterialPropertyBlock();
                halo.GetPropertyBlock(block);
                block.SetFloat(IntensityId, haloIntensity * eased);
                halo.SetPropertyBlock(block);
                halo.enabled = eased > 0.001f;
            }
        }
    }
}
