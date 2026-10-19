using System.Collections;
using CompanionRoom.Tarot;
using CompanionRoom.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CompanionRoom
{
    /// <summary>
    /// The room's conductor. Builds the UI, loads 凡安's lines and the tarot deck, and runs one
    /// interaction at a time: 凡安 turns to the item, the camera eases in, the item's coroutine
    /// plays out (dialogue, windows...), then everything settles back. Esc interrupts.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CompanionRoomApp : MonoBehaviour
    {
        public static CompanionRoomApp Instance { get; private set; }

        [SerializeField] RoomUISkin skin;
        [SerializeField] RoomCompanion companion;
        [SerializeField] RoomLighting lighting;
        [SerializeField] RoomCameraRig cameraRig;
        [SerializeField] RoomAudio roomAudio;
        [Tooltip("凡安 says hello when the room opens.")]
        [SerializeField] bool greetOnStart = true;

        Coroutine flow;
        RoomItem activeItem;

        public RoomUI UI { get; private set; }
        public DialogueBox Dialogue => UI.Dialogue;
        public DialogueLibrary Lines { get; private set; }
        public TarotDeck Deck { get; private set; }
        public RoomCompanion Companion => companion;
        public RoomLighting Lighting => lighting;
        public RoomCameraRig CameraRig => cameraRig;
        public RoomAudio Audio => roomAudio;
        public bool IsBusy => flow != null;
        public RoomItem ActiveItem => activeItem;
        /// <summary>巫然 can speak through her dialogue portrait even before her 3D model is placed.</summary>
        /// <summary>After any interaction is over (finished or interrupted); the item is null for the greeting.</summary>
        public event System.Action<RoomItem> InteractionEnded;
        public bool Chatty =>companion != null || (UI != null && UI.Skin.portrait != null);

        public void Configure(RoomUISkin uiSkin, RoomCompanion roomCompanion, RoomLighting roomLighting, RoomCameraRig rig, RoomAudio audioPlayer)
        {
            skin = uiSkin;
            companion = roomCompanion;
            lighting = roomLighting;
            cameraRig = rig;
            roomAudio = audioPlayer;
        }

        void Awake()
        {
            Instance = this;
            RoomSettings.Load();
            if (GetComponent<RoomWindowFrame>() == null)
                gameObject.AddComponent<RoomWindowFrame>();
            Lines = DialogueLibrary.Load();
            Deck = TarotDeck.Load();
            if (roomAudio == null)
                roomAudio = gameObject.AddComponent<RoomAudio>();
            if (cameraRig == null && Camera.main != null)
                cameraRig = Camera.main.GetComponent<RoomCameraRig>();
            RoomItemBinder.BindPlacedProps();
            UI =RoomUI.Build(this, skin != null ? skin.Resolve() : RoomUISkin.CreateDefault(), Lines.Speaker);
            Dialogue.LineStarted += _ =>
            {
                if (companion != null)
                    companion.Talk();
            };
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            if ((greetOnStart || !TutorialPanel.Done) && !RoomSelfTest.Requested)
                Run(null, Greet());
        }

        IEnumerator Greet()
        {
            yield return Wait(0.8f);
            if (greetOnStart)
            {
                if (RoomSettings.GetInt("WuranIntroduced", 0) == 0)
                {
                    yield return Dialogue.Say(Lines.Pick("introduce"));
                    RoomSettings.SetInt("WuranIntroduced", 1);
                }
                else
                {
                    string time = lighting != null ? RoomLighting.Key(lighting.Current) : "morning";
                    yield return Dialogue.Say(Lines.Pick("greet_" + time, "greet"));
                }
            }
            // The first launch ends with the guide; after that it only comes back from the settings.
            if (!TutorialPanel.Done)
                UI.Tutorial.Begin();
        }

        /// <summary>Closes whatever is open (the settings, usually) and plays the tutorial again.</summary>
        public void ReplayTutorial()
        {
            Interrupt();
            UI.Tutorial.Begin();
        }

        /// <summary>Starts an item's interaction unless another one is running.</summary>
        public bool Interact(RoomItem item)
        {
            if (item == null || IsBusy)
                return false;
            roomAudio?.Play(RoomSound.Click, 0.7f);
            return Run(item, item.Interact(this));
        }

        /// <summary>Runs any interaction routine with the usual turn-to / focus / settle steps.</summary>
        public bool Run(RoomItem item, IEnumerator routine)
        {
            if (IsBusy)
                return false;
            flow = StartCoroutine(Flow(item, routine));
            return true;
        }

        IEnumerator Flow(RoomItem item, IEnumerator routine)
        {
            activeItem = item;
            if (item != null)
            {
                if (companion != null && item.GetComponentInParent<RoomCompanion>() == null)
                    companion.FaceTowards(item.FocusPosition);
                if (cameraRig != null && item.CameraFocus > 0f)
                    cameraRig.Focus(item.FocusPosition, item.CameraFocus);
            }
            yield return routine;
            Finish();
        }

        /// <summary>Stops the running interaction and closes everything it opened.</summary>
        public void Interrupt()
        {
            if (flow == null)
                return;
            StopCoroutine(flow);
            Finish();
        }

        void Finish()
        {
            var item = activeItem;
            flow = null;
            activeItem = null;
            UI.CloseAll();
            if (cameraRig != null)
                cameraRig.Release();
            if (companion != null)
            {
                companion.Relax();
                companion.FaceRest();
            }
            if (item != null)
                item.OnInteractionEnded(this);
            InteractionEnded?.Invoke(item);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && IsBusy)
                Interrupt();
        }

        public static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
                yield return null;
        }
    }
}
