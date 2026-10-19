using System.Collections;

namespace CompanionRoom
{
    /// <summary>窗戶: each click quietly cycles the light — 早晨 → 夕陽 → 夜晚 → 早晨.</summary>
    public class WindowItem : RoomItem
    {
        public override IEnumerator Interact(CompanionRoomApp app)
        {
            if (app.Lighting == null)
                yield break;
            app.Lighting.Cycle();
            // Let the app register the interaction before it finishes on the next frame.
            yield return null;
        }
    }
}
