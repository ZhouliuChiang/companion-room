using System.Collections;
using UnityEngine;

namespace CompanionRoom
{
    /// <summary>Clicking 凡安 herself: she turns to you and chats a little (some lines depend on the time of day).</summary>
    public class CompanionTalkItem : RoomItem
    {
        [Tooltip("Where she looks while chatting (usually the main camera). Empty = the main camera.")]
        [SerializeField] Transform lookAtWhileTalking;

        public override IEnumerator Interact(CompanionRoomApp app)
        {
            var look = lookAtWhileTalking != null ? lookAtWhileTalking : Camera.main != null ? Camera.main.transform : null;
            if (look != null && app.Companion != null)
                app.Companion.FaceTowards(look.position);
            string time = app.Lighting != null ? RoomLighting.Key(app.Lighting.Current) : "morning";
            var lines = Random.value < 0.4f
                ? app.Lines.Pick("chat_" + time, "chat")
                : app.Lines.Pick("chat");
            yield return app.Dialogue.Say(lines);
        }
    }
}
