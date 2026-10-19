using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CompanionRoom
{
    public enum TimeOfDay { Morning = 0, Sunset = 1, Night = 2 }

    /// <summary>
    /// The room's three moods — 早晨, 夕陽, 夜晚 — and smooth changes between them: sun (through the
    /// window) and fill light, ambient color, the sky card outside the window, camera background
    /// and post-processing tint. The window cycles them; the choice is remembered.
    /// </summary>
    public class RoomLighting : MonoBehaviour
    {
        [Serializable]
        public class Preset
        {
            public string displayName;
            public Color sunColor = Color.white;
            public float sunIntensity = 1f;
            [Tooltip("Pitch and yaw of the sun in degrees. A yaw near 80 shines in through the left-wall window.")]
            public Vector2 sunAngles = new(35f, 80f);
            public Color fillColor = Color.white;
            public float fillIntensity = 0.4f;
            public Color ambient = Color.gray;
            [Header("窗外天空")]
            public Color skyTop = Color.cyan;
            public Color skyBottom = Color.white;
            public Color discColor = Color.white;
            [Tooltip("Sun / moon disc in the sky card: x, y (0..1) and radius.")]
            public Vector3 disc = new(0.7f, 0.7f, 0.08f);
            [Range(0f, 1f)] public float stars;
            [Header("畫面")]
            public Color background = Color.gray;
            public float exposure;
            public Color colorFilter = Color.white;
        }

        static readonly int SkyTopId = Shader.PropertyToID("_TopColor");
        static readonly int SkyBottomId = Shader.PropertyToID("_BottomColor");
        static readonly int DiscColorId = Shader.PropertyToID("_DiscColor");
        static readonly int DiscId = Shader.PropertyToID("_Disc");
        static readonly int StarsId = Shader.PropertyToID("_Stars");

        [SerializeField] Light sun;
        [SerializeField] Light fill;
        [SerializeField] Camera roomCamera;
        [SerializeField] Volume volume;
        [Tooltip("Renderers using the CompanionRoom/WindowSky shader (the view outside the window).")]
        [SerializeField] Renderer[] skyRenderers = Array.Empty<Renderer>();
        [SerializeField] float transitionSeconds = 1.6f;

        [SerializeField] Preset morning = DefaultMorning();
        [SerializeField] Preset sunset = DefaultSunset();
        [SerializeField] Preset night = DefaultNight();

        TimeOfDay current = TimeOfDay.Morning;
        Coroutine transition;
        MaterialPropertyBlock block;
        ColorAdjustments colorAdjustments;

        public TimeOfDay Current => current;
        public bool IsNight => current == TimeOfDay.Night;
        public event Action<TimeOfDay> Changed;

        public static string Key(TimeOfDay time) => time switch
        {
            TimeOfDay.Morning => "morning",
            TimeOfDay.Sunset => "sunset",
            _ => "night",
        };

        public Preset Get(TimeOfDay time) => time switch
        {
            TimeOfDay.Morning => morning,
            TimeOfDay.Sunset => sunset,
            _ => night,
        };

        public void Configure(Light sunLight, Light fillLight, Camera cam, Volume postVolume, Renderer[] sky)
        {
            sun = sunLight;
            fill = fillLight;
            roomCamera = cam;
            volume = postVolume;
            skyRenderers = sky;
        }

        void Awake()
        {
            RoomSettings.Load();
            if (volume != null && Application.isPlaying)
            {
                // volume.profile makes a runtime copy, so the shared asset is never edited.
                volume.profile.TryGet(out colorAdjustments);
            }
            current = RoomSettings.FollowRealClock ? FromClock(DateTime.Now) : (TimeOfDay)Mathf.Clamp(RoomSettings.GetInt("TimeOfDay", 0), 0, 2);
            Apply(Get(current));
        }

        public static TimeOfDay FromClock(DateTime now)
        {
            float hour = (float)now.TimeOfDay.TotalHours;
            if (hour >= 5f && hour < 16f)
                return TimeOfDay.Morning;
            if (hour >= 16f && hour < 19f)
                return TimeOfDay.Sunset;
            return TimeOfDay.Night;
        }

        /// <summary>Morning → sunset → night → morning. Returns the new time.</summary>
        public TimeOfDay Cycle()
        {
            Set((TimeOfDay)(((int)current + 1) % 3));
            return current;
        }

        public void Set(TimeOfDay time, bool instant = false)
        {
            var from = Snapshot();
            current = time;
            RoomSettings.SetInt("TimeOfDay", (int)time);
            if (transition != null)
                StopCoroutine(transition);
            if (instant || !Application.isPlaying || !isActiveAndEnabled)
                Apply(Get(time));
            else
                transition = StartCoroutine(Blend(from, Get(time)));
            Changed?.Invoke(time);
        }

        IEnumerator Blend(Preset from, Preset to)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, transitionSeconds))
            {
                Apply(Lerp(from, to, t * t * (3f - 2f * t)));
                yield return null;
            }
            Apply(to);
            transition = null;
        }

        Preset live;

        /// <summary>The look currently on screen (mid-transition values included).</summary>
        Preset Snapshot() => live != null ? Lerp(live, live, 0f) : Get(current);

        public void Apply(Preset p)
        {
            live = p;
            if (sun != null)
            {
                sun.color = p.sunColor;
                sun.intensity = p.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(p.sunAngles.x, p.sunAngles.y, 0f);
            }
            if (fill != null)
            {
                fill.color = p.fillColor;
                fill.intensity = p.fillIntensity;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = p.ambient;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(p.ambient.linear);
            RenderSettings.ambientProbe = probe;

            if (roomCamera != null)
                roomCamera.backgroundColor = p.background;
            block ??= new MaterialPropertyBlock();
            foreach (var r in skyRenderers)
            {
                if (r == null)
                    continue;
                r.GetPropertyBlock(block);
                block.SetColor(SkyTopId, p.skyTop);
                block.SetColor(SkyBottomId, p.skyBottom);
                block.SetColor(DiscColorId, p.discColor);
                block.SetVector(DiscId, new Vector4(p.disc.x, p.disc.y, p.disc.z, 0f));
                block.SetFloat(StarsId, p.stars);
                r.SetPropertyBlock(block);
            }
            if (colorAdjustments != null)
            {
                colorAdjustments.postExposure.Override(p.exposure);
                colorAdjustments.colorFilter.Override(p.colorFilter);
            }
        }

        static Preset Lerp(Preset a, Preset b, float t) => new()
        {
            displayName = t < 0.5f ? a.displayName : b.displayName,
            sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
            sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
            sunAngles = new Vector2(Mathf.LerpAngle(a.sunAngles.x, b.sunAngles.x, t), Mathf.LerpAngle(a.sunAngles.y, b.sunAngles.y, t)),
            fillColor = Color.Lerp(a.fillColor, b.fillColor, t),
            fillIntensity = Mathf.Lerp(a.fillIntensity, b.fillIntensity, t),
            ambient = Color.Lerp(a.ambient, b.ambient, t),
            skyTop = Color.Lerp(a.skyTop, b.skyTop, t),
            skyBottom = Color.Lerp(a.skyBottom, b.skyBottom, t),
            discColor = Color.Lerp(a.discColor, b.discColor, t),
            disc = Vector3.Lerp(a.disc, b.disc, t),
            stars = Mathf.Lerp(a.stars, b.stars, t),
            background = Color.Lerp(a.background, b.background, t),
            exposure = Mathf.Lerp(a.exposure, b.exposure, t),
            colorFilter = Color.Lerp(a.colorFilter, b.colorFilter, t),
        };

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        static Preset DefaultMorning() => new()
        {
            displayName = "早晨",
            sunColor = Hex("#FFF0DA"), sunIntensity = 2.2f, sunAngles = new Vector2(34f, 78f),
            fillColor = Hex("#FFE9E4"), fillIntensity = 0.55f,
            ambient = Hex("#D9C3C4"),
            skyTop = Hex("#86C1F0"), skyBottom = Hex("#FBE9EF"), discColor = Hex("#FFFBEA"), disc = new Vector3(0.72f, 0.78f, 0.09f), stars = 0f,
            background = Hex("#F6DDE0"), exposure = 0.15f, colorFilter = Color.white,
        };

        static Preset DefaultSunset() => new()
        {
            displayName = "夕陽",
            sunColor = Hex("#FF9A62"), sunIntensity = 2.4f, sunAngles = new Vector2(13f, 82f),
            fillColor = Hex("#FFB69C"), fillIntensity = 0.38f,
            ambient = Hex("#B98384"),
            skyTop = Hex("#7A5B98"), skyBottom = Hex("#FFAE78"), discColor = Hex("#FFD08A"), disc = new Vector3(0.42f, 0.26f, 0.12f), stars = 0.1f,
            background = Hex("#E6B3A8"), exposure = 0f, colorFilter = Hex("#FFEDE2"),
        };

        static Preset DefaultNight() => new()
        {
            displayName = "夜晚",
            sunColor = Hex("#8EA4E6"), sunIntensity = 0.55f, sunAngles = new Vector2(48f, 76f),
            fillColor = Hex("#6E78BE"), fillIntensity = 0.16f,
            ambient = Hex("#4A4868"),
            skyTop = Hex("#121838"), skyBottom = Hex("#332F62"), discColor = Hex("#FFF6D8"), disc = new Vector3(0.3f, 0.74f, 0.06f), stars = 1f,
            background = Hex("#2B2740"), exposure = -0.25f, colorFilter = Hex("#DDE2FF"),
        };
    }
}
