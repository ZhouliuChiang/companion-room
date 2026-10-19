using CompanionRoom.Journal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CompanionRoom.UI
{
    /// <summary>
    /// Builds the room's screen-space UI at startup from a <see cref="RoomUISkin"/>: tarot table,
    /// photo album, settings, the record journal's controls (the journal itself is 3D), the dialogue
    /// box on top of those, the hover name plate, and the first-run tutorial above everything.
    /// </summary>
    public class RoomUI : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public RoomUISkin Skin { get; private set; }
        public TarotStage Tarot { get; private set; }
        public PendulumPanel Pendulum { get; private set; }
        public PhotoViewerPanel Photos { get; private set; }
        public SettingsPanel Settings { get; private set; }
        public RecordJournal Journal { get; private set; }
        public DialogueBox Dialogue { get; private set; }
        public HoverTooltip Tooltip { get; private set; }
        public TutorialPanel Tutorial { get; private set; }

        public bool AnyWindowOpen => Pendulum.IsOpen || Photos.IsOpen || Settings.IsOpen || Journal.IsOpen || Tarot.IsOpen || Dialogue.IsOpen;

        public static RoomUI Build(CompanionRoomApp app, RoomUISkin skin, string speaker)
        {
            EnsureEventSystem();

            var go = new GameObject("RoomUI", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var ui = go.AddComponent<RoomUI>();
            ui.Skin = skin;
            ui.Canvas = go.AddComponent<Canvas>();
            ui.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ui.Canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var root = go.transform;
            ui.Tarot = TarotStage.Build(root, app, skin);
            ui.Pendulum = PendulumPanel.Build(root, app, skin);
            ui.Photos = PhotoViewerPanel.Build(root, app, skin);
            ui.Settings = SettingsPanel.Build(root, app, skin);
            ui.Journal = RecordJournal.Build(root, app, skin);
            ui.Dialogue = DialogueBox.Build(root, app, skin, speaker);
            ui.Tooltip = HoverTooltip.Build(root, skin);
            ui.Tutorial = TutorialPanel.Build(root, app, skin);
            return ui;
        }

        static void EnsureEventSystem()
        {
            var system = EventSystem.current != null ? EventSystem.current : FindAnyObjectByType<EventSystem>();
            if (system == null)
                system = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            if (!system.TryGetComponent<InputSystemUIInputModule>(out var module))
            {
                foreach (var old in system.GetComponents<BaseInputModule>())
                    Destroy(old);
                module = system.gameObject.AddComponent<InputSystemUIInputModule>();
            }
            if (module.actionsAsset == null)
                module.AssignDefaultActions();
        }

        /// <summary>Closes every window immediately (an interaction was interrupted).</summary>
        public void CloseAll()
        {
            Dialogue.HideNow();
            Tarot.HideNow();
            Pendulum.HideNow();
            Photos.HideNow();
            Settings.HideNow();
            Journal.HideNow();
            Tooltip.Set(null);
        }
    }
}
