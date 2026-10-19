using System.Collections;
using UnityEngine;

namespace CompanionRoom
{
    /// <summary>電腦: the screen wakes up, 凡安 opens the settings for you, and it sleeps again afterwards.</summary>
    public class ComputerItem : RoomItem
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("螢幕")]
        [Tooltip("The monitor's screen surface. Empty = a child named \"Screen\".")]
        [SerializeField] Renderer screen;
        [SerializeField] Color screenOff = new(0.17f, 0.16f, 0.21f);
        [SerializeField] Color screenOn = new(0.93f, 0.86f, 1f);
        [SerializeField, Min(0f)] float glow = 1.4f;
        [Tooltip("Optional desktop picture shown while the screen is on.")]
        [SerializeField] Texture2D wallpaper;
        [Tooltip("Optional soft light in front of the screen.")]
        [SerializeField] Light screenLight;
        [SerializeField] float screenLightIntensity = 0.6f;

        Material material;
        Texture originalTexture;
        float level;
        float target;

        void Start()
        {
            screen = FindPart(screen, "Screen");
            if (screen != null)
            {
                material = screen.material;
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                originalTexture = material.mainTexture;
            }
            Apply();
        }

        void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }

        public override IEnumerator Interact(CompanionRoomApp app)
        {
            SetScreen(true);
            app.Audio?.Play(RoomSound.Click, 0.6f);
            yield return CompanionRoomApp.Wait(0.25f);
            if (app.Chatty)
            {
                yield return app.Dialogue.Say(app.Lines.Pick("computer"));
                app.Dialogue.Close();
            }
            yield return app.UI.Settings.Open();
            if (app.Chatty)
                yield return app.Dialogue.Say(app.Lines.Pick("computer_after"));
        }

        public override void OnInteractionEnded(CompanionRoomApp app) => SetScreen(false);

        public void SetScreen(bool on)
        {
            target = on ? 1f : 0f;
            if (material != null)
                material.mainTexture = on && wallpaper != null ? wallpaper : originalTexture;
        }

        void Update()
        {
            if (Mathf.Approximately(level, target))
                return;
            level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime / 0.35f);
            Apply();
        }

        void Apply()
        {
            if (material != null)
            {
                var color = Color.Lerp(screenOff, screenOn, level);
                material.SetColor(BaseColorId, color);
                material.SetColor(EmissionColorId, screenOn * (glow * level));
            }
            if (screenLight != null)
            {
                screenLight.intensity = screenLightIntensity * level;
                screenLight.enabled = level > 0.01f;
            }
        }
    }
}
