using System;
using UnityEngine;

namespace CompanionRoom
{
    /// <summary>
    /// Player preferences changed from the computer's settings window, kept in PlayerPrefs.
    /// Everything reads the static values; <see cref="Changed"/> fires after any change is applied.
    /// </summary>
    public static class RoomSettings
    {
        const string Prefix = "CompanionRoom.";

        public const float MinTextSpeed = 10f;
        public const float MaxTextSpeed = 70f;

        /// <summary>Scales every sound in the app (applied through AudioListener.volume).</summary>
        public static float MasterVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 0.6f;
        public static float SfxVolume { get; private set; } = 0.8f;
        /// <summary>Dialogue typing speed in characters per second.</summary>
        public static float TextSpeed { get; private set; } = 28f;
        /// <summary>On launch, pick morning / sunset / night from the computer's clock.</summary>
        public static bool FollowRealClock { get; private set; } = true;
        /// <summary>Keep the desktop window above other windows (Windows player only).</summary>
        public static bool AlwaysOnTop { get; private set; }
        // Compatibility for the existing tooltip component; the current room keeps name plates hidden.
        public static bool ShowItemNames => false;

        public static event Action Changed;

        static bool loaded;

        public static void Load()
        {
            if (loaded)
                return;
            loaded = true;
            MasterVolume = PlayerPrefs.GetFloat(Prefix + "Master", MasterVolume);
            MusicVolume = PlayerPrefs.GetFloat(Prefix + "Music", MusicVolume);
            SfxVolume = PlayerPrefs.GetFloat(Prefix + "Sfx", SfxVolume);
            TextSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "TextSpeed", TextSpeed), MinTextSpeed, MaxTextSpeed);
            FollowRealClock = PlayerPrefs.GetInt(Prefix + "FollowClock", FollowRealClock ? 1 : 0) == 1;
            AlwaysOnTop = PlayerPrefs.GetInt(Prefix + "AlwaysOnTop", AlwaysOnTop ? 1 : 0) == 1;
        }

        public static void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            SaveFloat("Master", MasterVolume);
        }

        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            SaveFloat("Music", MusicVolume);
        }

        public static void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            SaveFloat("Sfx", SfxVolume);
        }

        public static void SetTextSpeed(float value)
        {
            TextSpeed = Mathf.Clamp(value, MinTextSpeed, MaxTextSpeed);
            SaveFloat("TextSpeed", TextSpeed);
        }

        public static void SetFollowRealClock(bool value)
        {
            FollowRealClock = value;
            PlayerPrefs.SetInt(Prefix + "FollowClock", value ? 1 : 0);
            Commit();
        }

        public static void SetAlwaysOnTop(bool value)
        {
            AlwaysOnTop = value;
            PlayerPrefs.SetInt(Prefix + "AlwaysOnTop", value ? 1 : 0);
            Commit();
        }

        static void SaveFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, value);
            Commit();
        }

        static void Commit()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Small per-feature values (lamp on/off, chosen time of day, last photo...).</summary>
        public static int GetInt(string key, int fallback) => PlayerPrefs.GetInt(Prefix + key, fallback);

        public static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            PlayerPrefs.Save();
        }
    }
}
