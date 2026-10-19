using System.Collections;
using CompanionRoom.Journal;
using CompanionRoom.Tarot;
using UnityEngine;

namespace CompanionRoom
{
    /// <summary>
    /// 書櫃／櫃子: the tarot record journal comes off the shelf — it slides out, opens facing you and
    /// floats up in front of you (see <see cref="RecordJournal"/>). An optional door swings open too.
    /// </summary>
    public class RecordCabinetItem : RoomItem
    {
        [Header("櫃門（可留空）")]
        [Tooltip("The door's hinge pivot. Empty = a child named \"Door\". It rotates by the angles below while open.")]
        [SerializeField] Transform door;
        [SerializeField] Vector3 openAngles = new(0f, -95f, 0f);
        [SerializeField] float doorSeconds = 0.45f;

        [Header("占卜手帳")]
        [Tooltip("Where the journal stands on the shelf: its spine sits here, and its blue (Z) axis points out of the shelf. Empty = the middle of the shelf's front, at the height below.")]
        [SerializeField] Transform bookSlot;
        [Tooltip("Height of the journal on the shelf when Book Slot is empty: 0 = bottom of the model, 1 = top.")]
        [SerializeField, Range(0f, 1f)] float bookHeight = 0.55f;
        [Tooltip("The journal's page height while it is on the shelf, in metres.")]
        [SerializeField, Min(0.05f)] float bookSize = 0.3f;

        Quaternion closedRotation;
        float open;
        float target;

        void Start()
        {
            door = FindPart(door, "Door");
            if (door != null)
                closedRotation = door.localRotation;
        }

        public override IEnumerator Interact(CompanionRoomApp app)
        {
            target = 1f;
            if (door != null)
                app.Audio?.Play(RoomSound.Open, 0.7f);
            yield return CompanionRoomApp.Wait(0.2f);
            if (app.Chatty)
            {
                yield return app.Dialogue.Say(app.Lines.Pick(TarotRecordStore.Load().Count == 0 ? "records_empty" : "records"));
                app.Dialogue.Close();
            }
            yield return app.UI.Journal.Browse(BookSlot(app));
            if (app.Chatty)
                yield return app.Dialogue.Say(app.Lines.Pick("records_after"));
        }

        public override void OnInteractionEnded(CompanionRoomApp app)
        {
            target = 0f;
        }

        /// <summary>
        /// The journal's place on the shelf. Without a Book Slot it is the middle of the model's front:
        /// the front is whichever of the item's own sides faces the middle of the room.
        /// </summary>
        public JournalSlot BookSlot(CompanionRoomApp app)
        {
            if (bookSlot != null)
                return new JournalSlot(bookSlot.position, bookSlot.forward, bookSize);
            var bounds = ModelBounds;
            var roomMiddle = app != null && app.CameraRig != null ? app.CameraRig.LookAt : Vector3.zero;
            var towardsRoom = roomMiddle - bounds.center;
            towardsRoom.y = 0f;
            var front = transform.forward;
            float best = float.NegativeInfinity;
            foreach (var axis in new[] { transform.forward, -transform.forward, transform.right, -transform.right })
            {
                var flat = new Vector3(axis.x, 0f, axis.z).normalized;
                float score = Vector3.Dot(flat, towardsRoom.normalized);
                if (score > best)
                {
                    best = score;
                    front = flat;
                }
            }
            float reach = Mathf.Abs(front.x) * bounds.extents.x + Mathf.Abs(front.z) * bounds.extents.z;
            var position = bounds.center + front * reach;
            position.y = bounds.min.y + bounds.size.y * bookHeight;
            return new JournalSlot(position, front, bookSize);
        }

        void Update()
        {
            if (door == null || Mathf.Approximately(open, target))
                return;
            open = Mathf.MoveTowards(open, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, doorSeconds));
            float eased = open * open * (3f - 2f * open);
            door.localRotation = closedRotation * Quaternion.Euler(openAngles * eased);
        }
    }
}
