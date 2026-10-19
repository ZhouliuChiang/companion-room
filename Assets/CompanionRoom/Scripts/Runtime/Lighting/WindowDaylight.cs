using UnityEngine;

namespace CompanionRoom
{
    /// <summary>Supplementary light enters through the real aperture; dims with the room's sun.</summary>
    public sealed class WindowDaylight : MonoBehaviour
    {
        public Light daylight;
        public Light roomSun;
        public float multiplier = 1.6f;
        void LateUpdate()
        {
            if (daylight == null || roomSun == null) return;
            daylight.intensity = roomSun.intensity * multiplier;
            daylight.color = roomSun.color;
            daylight.enabled = daylight.intensity > .02f;
        }
    }
}
