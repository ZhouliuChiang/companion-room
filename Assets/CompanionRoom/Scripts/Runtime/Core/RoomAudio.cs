using UnityEngine;

namespace CompanionRoom
{
    public enum RoomSound { Click, Switch, Flip, Shuffle, Reveal, Open, Close }

    /// <summary>
    /// Room sound effects and background music. Any clip left empty in the inspector is replaced by a
    /// soft, short mechanical click for subtle interaction feedback.
    /// Volumes follow <see cref="RoomSettings"/>.
    /// </summary>
    public class RoomAudio : MonoBehaviour
    {
        [Header("Music (optional)")]
        [SerializeField] AudioClip music;

        [Header("Sound effects (empty = soft mechanical clicks)")]
        [SerializeField] AudioClip click;
        [SerializeField] AudioClip lightSwitch;
        [SerializeField] AudioClip cardFlip;
        [SerializeField] AudioClip cardShuffle;
        [SerializeField] AudioClip reveal;
        [SerializeField] AudioClip panelOpen;
        [SerializeField] AudioClip panelClose;

        AudioSource sfxSource;
        AudioSource musicSource;
        float nextSoundTime;

        void Awake()
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;

            click = click != null ? click : Synth.Click();
            lightSwitch = lightSwitch != null ? lightSwitch : Synth.Switch();
            cardFlip = cardFlip != null ? cardFlip : Synth.Flip();
            cardShuffle = cardShuffle != null ? cardShuffle : Synth.Shuffle();
            reveal = reveal != null ? reveal : Synth.Reveal();
            panelOpen = panelOpen != null ? panelOpen : Synth.Open();
            panelClose = panelClose != null ? panelClose : Synth.Close();

            RoomSettings.Changed += ApplyVolumes;
            ApplyVolumes();
            if (music == null)
                music = Resources.Load<AudioClip>("CompanionRoom/Audio/WarmWindowLofi");
            if (music != null)
            {
                musicSource.clip = music;
                musicSource.Play();
            }
        }

        void OnDestroy() => RoomSettings.Changed -= ApplyVolumes;

        void ApplyVolumes()
        {
            AudioListener.volume = RoomSettings.MasterVolume;
            musicSource.volume = RoomSettings.MusicVolume * 0.2f;
            sfxSource.volume = RoomSettings.SfxVolume * 0.55f;
        }

        public void Play(RoomSound sound, float volume = 1f)
        {
            var clip = sound switch
            {
                RoomSound.Click => click,
                RoomSound.Switch => lightSwitch,
                RoomSound.Flip => cardFlip,
                RoomSound.Shuffle => cardShuffle,
                RoomSound.Reveal => reveal,
                RoomSound.Open => panelOpen,
                RoomSound.Close => panelClose,
                _ => null,
            };
            if (clip == null || volume <= 0f || Time.unscaledTime < nextSoundTime)
                return;
            // Suppress duplicate feedback from the same gesture and rapid stacking.
            nextSoundTime = Time.unscaledTime + 0.045f;
            sfxSource.pitch = Random.Range(0.985f, 1.015f);
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        /// <summary>Quiet, dry mechanical feedback (mono, 44.1 kHz).</summary>
        static class Synth
        {
            const int Rate = 44100;

            // Filtered noise supplies the texture; a delayed softer transient
            // supplies the second half of the click without an electronic beep.
            static AudioClip Make(string name, float seconds, float bodyHz,
                float separation, float secondLevel, int seed, int strokes = 1)
            {
                int count = Mathf.CeilToInt(seconds * Rate);
                var data = new float[count];
                uint state = (uint)seed;
                float low = 0f;
                for (int i = 0; i < count; i++)
                {
                    state = unchecked(state * 1664525u + 1013904223u);
                    float noise = (state >> 8) / 8388607.5f - 1f;
                    low += 0.24f * (noise - low);
                    float t = i / (float)Rate;
                    float value = 0f;
                    for (int stroke = 0; stroke < strokes; stroke++)
                    {
                        float local = t - stroke * 0.039f;
                        if (local < 0f) continue;
                        float strength = 1f - stroke * 0.18f;
                        value += strength * (Transient(local, low, bodyHz)
                            + secondLevel * Transient(local - separation, low, bodyHz * 0.78f));
                    }
                    float fade = Mathf.Clamp01((seconds - t) / 0.012f);
                    data[i] = Mathf.Clamp(value * fade * 0.24f, -0.3f, 0.3f);
                }
                var clip = AudioClip.Create(name, count, 1, Rate, false);
                clip.SetData(data, 0);
                return clip;
            }

            static float Transient(float t, float noise, float hz)
            {
                if (t < 0f) return 0f;
                float attack = Mathf.Clamp01(t / 0.0015f);
                float texture = noise * Mathf.Exp(-t * 155f) * 0.8f;
                float body = Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t * 235f) * 0.22f;
                return (texture + body) * attack;
            }

            public static AudioClip Click() => Make("Soft Click", 0.075f, 640f, 0.012f, 0.40f, 11);
            public static AudioClip Switch() => Make("Soft Switch", 0.095f, 540f, 0.022f, 0.62f, 23);
            public static AudioClip Flip() => Make("Soft Flip", 0.085f, 460f, 0.016f, 0.32f, 37);
            public static AudioClip Shuffle() => Make("Soft Shuffle", 0.17f, 420f, 0.011f, 0.25f, 47, 3);
            public static AudioClip Reveal() => Make("Soft Confirm", 0.10f, 720f, 0.024f, 0.45f, 59);
            public static AudioClip Open() => Make("Soft Open", 0.085f, 580f, 0.015f, 0.38f, 71);
            public static AudioClip Close() => Make("Soft Close", 0.08f, 440f, 0.012f, 0.34f, 83);
        }
    }
}
