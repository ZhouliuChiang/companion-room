using System.Collections;
using UnityEngine;

namespace CompanionRoom
{
    public class PendulumItem : RoomItem
    {
        bool previousPortrait;
        public override IEnumerator Interact(CompanionRoomApp app)
        {
            app.UI.Pendulum.Open(this);
            var stage = app.UI.Pendulum;
            var dialogue = app.Dialogue;
            previousPortrait = dialogue.PortraitEnabled;
            dialogue.PortraitEnabled = false;
            yield return dialogue.Ask("校正是", new[] { "開始校正" }, _ => { });
            dialogue.Show("正在校正「是」……");
            yield return stage.PlaySignal(0, 1.4f);
            yield return dialogue.Ask("校正否", new[] { "開始校正" }, _ => { });
            dialogue.Show("正在校正「否」……");
            yield return stage.PlaySignal(1, 1.4f);
            while (true)
            {
                yield return dialogue.Ask("心中默念問題", new[] { "開始" }, _ => { });
                dialogue.Show("靜心觀察靈擺……");
                int answer = Random.Range(0, 2);
                yield return stage.PlaySignal(answer, 5f);
                int next = -1;
                yield return dialogue.Ask(answer == 0 ? "是。（左右擺動）" : "否。（前後擺動）",
                    new[] { "再問一次", "返回房間" }, choice => next = choice);
                if (next != 0) yield break;
            }
        }

        public override void OnInteractionEnded(CompanionRoomApp app)
        {
            app.Dialogue.PortraitEnabled = previousPortrait;
        }
    }
}
