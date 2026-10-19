using System.Collections;
using System.Collections.Generic;
using CompanionRoom.Tarot;
using CompanionRoom.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CompanionRoom.Journal
{
    /// <summary>Where the journal stands on the shelf: its spine at <see cref="position"/>, facing <see cref="outward"/>.</summary>
    public readonly struct JournalSlot
    {
        public readonly Vector3 position;
        public readonly Vector3 outward;
        /// <summary>The book's page height on the shelf, in metres.</summary>
        public readonly float size;

        public JournalSlot(Vector3 position, Vector3 outward, float size)
        {
            this.position = position;
            outward.y = 0f;
            this.outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.forward;
            this.size = size;
        }
    }

    /// <summary>
    /// The tarot record book as a 3D pop-up journal. It slides out of the bookcase, turns to face you
    /// at eye level and opens there, then floats up in front of you. Each reading is a spread: the
    /// date and question on the left with its cards popping up off the page, the reading on the right.
    /// Turn pages by clicking a page, the ◀ ▶ buttons, the arrow keys or the mouse wheel; the page
    /// curls over as it turns. Clicking beside the book or × puts it back on the shelf; Esc (through
    /// <see cref="CompanionRoomApp.Interrupt"/>) closes it at once.
    /// The book has its own overlay camera stacked on the room camera: while it comes off the shelf
    /// the room still hides it, and once it is out it always draws over the room, however large it
    /// has to be (the room camera is orthographic).
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class RecordJournal : MonoBehaviour
    {
        enum State { Hidden, Opening, Reading, Closing }

        /// <summary>Unnamed layer only the journal's overlay camera renders.</summary>
        public const int JournalLayer = 30;
        const string MaterialResource = "CompanionRoom/M_JournalPaper";
        const float PeekAmount = 0.07f;
        const int CacheSize = 10;
        const float MidShare = 0.42f;       // share of the view's height while it opens
        const float MidTilt = 6f;           // opened at eye level, turned a little so the cover's swing reads in 3D
        const float MidYaw = 15f;
        const float ReadShare = 0.84f;      // ... and while you read
        const float ReadTilt = 20f;         // top edge leaning away, so the pop-ups read as standing
        const float TalkShare = 0.66f;      // on a reading's pages: smaller and higher, clear of the dialogue box
        static readonly Vector2 ReadViewport = new(0.5f, 0.53f);
        static readonly Vector2 TalkViewport = new(0.5f, 0.62f);
        static readonly float[] OpenSeconds = { 0.45f, 0.7f, 0.8f, 0.65f };
        static readonly float[] CloseSeconds = { 0.35f, 0.55f, 0.5f, 0.45f };

        CompanionRoomApp app;
        RoomUISkin skin;
        CanvasGroup controls;
        Text pageLabel;
        Button prevButton;
        Button nextButton;
        Button deleteButton;
        Text deleteLabel;
        Button listenButton;
        Coroutine reading;
        float talk;

        JournalBook book;
        JournalPageRenderer pageRenderer;
        Material paperTemplate;
        Canvas dim;
        Image dimImage;
        Camera view;
        Camera overlay;
        UniversalAdditionalCameraData overlayData;
        JournalSlot slot;

        State state;
        float sequence;
        List<TarotRecord> records = new();
        TarotDeck deck;
        int spread;

        readonly Dictionary<int, RenderTexture> pages = new();
        readonly List<int> recentPages = new();
        readonly HashSet<int> shownPages = new();
        readonly Dictionary<string, RenderTexture> cards = new();
        readonly Queue<System.Action> prefetch = new();
        RenderTexture coverFront;
        RenderTexture coverBack;
        RenderTexture cardBack;

        int flipDir;
        float flipProgress;
        float flipGoal;
        bool leafReady;
        bool turnSounded;
        int pendingFlip;
        bool foldingForClose;
        float leftThicknessGoal;
        float rightThicknessGoal = JournalBook.PagesT;
        bool confirmDelete;
        Vector2 pressPosition;
        bool pressed;
        float wheelCooldown;

        public bool IsOpen => state != State.Hidden;
        public bool IsReading => state == State.Reading;
        public bool IsTurning => flipDir != 0 && flipGoal >= 1f;
        public int Spread => spread;
        public int SpreadCount => 1 + records.Count;
        /// <summary>Mouse hover/click/wheel on the book (scripted checks turn it off so the real pointer can't interfere).</summary>
        public bool PointerInput { get; set; } = true;

        // ------------------------------------------------------------------ setup

        public static RecordJournal Build(Transform canvasRoot, CompanionRoomApp app, RoomUISkin skin)
        {
            var root = UIFactory.Rect("RecordJournal", canvasRoot).Stretch();
            var journal = root.gameObject.AddComponent<RecordJournal>();
            journal.app = app;
            journal.skin = skin;
            journal.controls = root.gameObject.AddComponent<CanvasGroup>();
            journal.controls.alpha = 0f;
            journal.controls.blocksRaycasts = false;
            journal.controls.interactable = false;

            var bar = UIFactory.Rect("Bar", root);
            bar.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(460f, 72f));
            journal.prevButton = UIFactory.RoundButton("Prev", bar, skin, "◀", 64f, () => journal.Flip(-1), 24);
            ((RectTransform)journal.prevButton.transform).Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            journal.nextButton = UIFactory.RoundButton("Next", bar, skin, "▶", 64f, () => journal.Flip(1), 24);
            ((RectTransform)journal.nextButton.transform).Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            var plate = UIFactory.Image("Plate", bar, skin.button, new Color(1f, 1f, 1f, 0.92f));
            plate.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 56f));
            journal.pageLabel = UIFactory.Text("Page", plate.transform, skin, 26, skin.ink, TextAnchor.MiddleCenter);
            journal.pageLabel.rectTransform.Stretch(8f, 2f, 8f, 4f);
            journal.pageLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            var close = UIFactory.RoundButton("Close", root, skin, "×", 64f, journal.Close, 38);
            ((RectTransform)close.transform).Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -36f), new Vector2(64f, 64f));
            journal.deleteButton = UIFactory.Button("Delete", root, skin, "刪除這則", new Vector2(200f, 60f), journal.DeleteCurrent, fontSize: 26);
            ((RectTransform)journal.deleteButton.transform).Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(200f, 60f));
            journal.deleteLabel = journal.deleteButton.GetComponentInChildren<Text>();
            journal.listenButton = UIFactory.Button("Listen", root, skin, "聽解讀", new Vector2(180f, 60f), () => journal.StartReading(0f), accent: true, fontSize: 26);
            ((RectTransform)journal.listenButton.transform).Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-260f, 40f), new Vector2(180f, 60f));
            return journal;
        }

        void OnDestroy()
        {
            ReleaseTextures();
            pageRenderer?.Dispose();
            if (book != null)
                Destroy(book.gameObject);
            if (dim != null)
                Destroy(dim.gameObject);
            if (overlay != null)
            {
                if (view != null)
                    view.GetUniversalAdditionalCameraData().cameraStack.Remove(overlay);
                Destroy(overlay.gameObject);
            }
        }

        bool Prepare()
        {
            view = app != null && app.CameraRig != null ? app.CameraRig.GetComponent<Camera>() : null;
            if (view == null)
                view = Camera.main;
            if (view == null)
                return false;
            PrepareOverlay();
            if (paperTemplate == null)
                paperTemplate = Resources.Load<Material>(MaterialResource);
            if (paperTemplate == null)
            {
                var shader = Shader.Find("CompanionRoom/JournalPaper");
                if (shader == null)
                {
                    Debug.LogError($"[CompanionRoom] Missing journal material Resources/{MaterialResource}");
                    return false;
                }
                paperTemplate = new Material(shader);
            }
            pageRenderer ??= new JournalPageRenderer();
            if (book == null)
            {
                book = JournalBook.Create(paperTemplate, JournalPages.CoverColor(skin), JournalPages.Paper * 0.96f, JournalLayer);
                book.gameObject.SetActive(false);
            }
            if (dim == null)
            {
                var go = new GameObject("RecordJournalDim", typeof(RectTransform)) { layer = JournalLayer };
                dim = go.AddComponent<Canvas>();
                dim.renderMode = RenderMode.WorldSpace;
                dimImage = UIFactory.Image("Shade", go.transform, null, new Color(0.12f, 0.07f, 0.09f, 0f));
                dimImage.gameObject.layer = JournalLayer;
                dimImage.rectTransform.Stretch();
                go.SetActive(false);
            }
            return true;
        }

        /// <summary>The journal's own camera, stacked on the room camera, which in turn stops drawing the journal layer.</summary>
        void PrepareOverlay()
        {
            view.cullingMask &= ~(1 << JournalLayer);
            if (overlay == null)
            {
                var go = new GameObject("RecordJournalCamera");
                overlay = go.AddComponent<Camera>();
                overlay.clearFlags = CameraClearFlags.Depth;
                overlay.cullingMask = 1 << JournalLayer;
                overlay.useOcclusionCulling = false;
                overlayData = overlay.GetUniversalAdditionalCameraData();
                overlayData.renderType = CameraRenderType.Overlay;
                overlayData.renderShadows = false;
                overlayData.renderPostProcessing = false;
                overlay.enabled = false;
            }
            if (overlay.transform.parent != view.transform)
            {
                overlay.transform.SetParent(view.transform, false);
                overlay.transform.localPosition = Vector3.zero;
                overlay.transform.localRotation = Quaternion.identity;
            }
            var stack = view.GetUniversalAdditionalCameraData().cameraStack;
            if (!stack.Contains(overlay))
                stack.Add(overlay);
            SyncOverlay();
        }

        void SyncOverlay()
        {
            overlay.orthographic = view.orthographic;
            overlay.orthographicSize = view.orthographicSize;
            overlay.fieldOfView = view.fieldOfView;
            overlay.nearClipPlane = view.nearClipPlane;
            overlay.farClipPlane = Mathf.Max(view.farClipPlane, AnchorDistance(ReadingScale) + ReadingScale * 4f);
            // Coming off the shelf the room camera draws the book, so the shelf still hides it; once it
            // is out, the overlay camera draws it over the room.
            if (book != null)
                SetLayer(book.transform, sequence > 1.1f ? JournalLayer : 0);
        }

        static void SetLayer(Transform root, int layer)
        {
            if (root.gameObject.layer == layer)
                return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        // ------------------------------------------------------------------ open and close

        /// <summary>Takes the journal off the shelf and waits until it is back.</summary>
        public IEnumerator Browse(JournalSlot from)
        {
            if (state != State.Hidden || !Prepare())
                yield break;
            slot = from;
            deck = app != null ? app.Deck : TarotDeck.Load();
            LoadRecords();
            spread = 0;
            flipDir = 0;
            pendingFlip = 0;
            leafReady = false;
            foldingForClose = false;
            book.HideLeaf();
            book.ClearPopUps();
            book.SetThickness(0f, JournalBook.PagesT);
            SetThicknessGoals();
            RenderCovers();
            ApplySpread(popUp: false);

            sequence = 0f;
            state = State.Opening;
            overlay.enabled = true;
            book.gameObject.SetActive(true);
            dim.gameObject.SetActive(true);
            ApplySequence();
            app?.Audio?.Play(RoomSound.Open, 0.7f);
            while (state != State.Hidden)
                yield return null;
        }

        /// <summary>Folds the cards, closes the book and puts it back on the shelf.</summary>
        public void Close()
        {
            if (state != State.Reading)
                return;
            state = State.Closing;
            StopReading();
            CancelLeafNow();
            pendingFlip = 0;
            foldingForClose = book.HasPopUps;
            SetControls(false);
            app?.Audio?.Play(RoomSound.Close, 0.6f);
        }

        /// <summary>Gone at once (Esc / interrupted).</summary>
        public void HideNow()
        {
            if (state == State.Hidden)
                return;
            state = State.Hidden;
            FinishHide();
        }

        void FinishHide()
        {
            state = State.Hidden;
            StopReading();
            talk = 0f;
            SetControls(false);
            flipDir = 0;
            pendingFlip = 0;
            leafReady = false;
            if (book != null)
            {
                book.HideLeaf();
                book.ClearPopUps();
                book.gameObject.SetActive(false);
            }
            if (dim != null)
                dim.gameObject.SetActive(false);
            if (overlay != null)
                overlay.enabled = false;
            prefetch.Clear();
            ReleaseTextures();
        }

        void SetControls(bool on)
        {
            controls.alpha = on ? 1f : 0f;
            controls.blocksRaycasts = on;
            controls.interactable = on;
            confirmDelete = false;
            if (deleteLabel != null)
                deleteLabel.text = "刪除這則";
        }

        // ------------------------------------------------------------------ the reading, spoken

        /// <summary>
        /// Reads the open reading's cards out in the dialogue box, card by card (the same lines as at
        /// the tarot table), when 聽解讀 is pressed; the card being talked about gives a little hop.
        /// </summary>
        void StartReading(float delay)
        {
            StopReading();
            if (app == null || state != State.Reading || spread == 0)
                return;
            reading = StartCoroutine(Read(records[spread - 1], delay));
        }

        void StopReading()
        {
            if (reading != null)
            {
                StopCoroutine(reading);
                reading = null;
                if (app != null && app.Dialogue.IsOpen)
                    app.Dialogue.Close();
            }
        }

        IEnumerator Read(TarotRecord record, float delay)
        {
            yield return CompanionRoomApp.Wait(delay);
            var lines = app.Lines;
            var dialogue = app.Dialogue;
            bool single = record.draws.Count == 1;
            for (int i = 0; i < record.draws.Count; i++)
            {
                var draw = record.draws[i];
                var card = deck?.Find(draw.cardId);
                if (card == null)
                    continue;
                book.NudgePopUp(i);
                string orientation = draw.reversed ? "逆位" : "正位";
                yield return dialogue.Say(lines.Format(single ? "tarot_reveal_single" : "tarot_reveal", draw.position, card.name, orientation));
                var meaning = card.Meaning(draw.reversed);
                yield return dialogue.Say(meaning.meaning);
                if (!string.IsNullOrEmpty(meaning.advice))
                    yield return dialogue.Say(meaning.advice);
            }
            if (record.draws.Count >= 3)
            {
                var names = new string[3];
                for (int i = 0; i < 3; i++)
                    names[i] = deck?.Find(record.draws[i].cardId)?.name ?? "?";
                yield return dialogue.Say(lines.Format("tarot_summary_three", names[0], names[1], names[2]));
            }
            dialogue.Close();
            reading = null;
        }

        public bool IsSpeaking => reading != null;

        // ------------------------------------------------------------------ pages

        void LoadRecords()
        {
            records = TarotRecordStore.Load();
            records.Reverse();
        }

        int PageCount => 2 + records.Count * 2;

        void RenderCovers()
        {
            var coverColor = JournalPages.CoverColor(skin);
            coverFront ??= JournalPageRenderer.NewTarget(JournalPages.Width, JournalPages.Height, "JournalCoverFront");
            coverBack ??= JournalPageRenderer.NewTarget(JournalPages.Width, JournalPages.Height, "JournalCoverBack");
            cardBack ??= JournalPageRenderer.NewTarget(JournalPages.CardWidth, JournalPages.CardHeight, "JournalCardBack");
            pageRenderer.Render(coverFront, coverColor, page => JournalPages.Cover(page, skin, front: true));
            pageRenderer.Render(coverBack, coverColor, page => JournalPages.Cover(page, skin, front: false));
            pageRenderer.Render(cardBack, Color.clear, page => JournalPages.CardBack(page, skin));
            book.SetCoverTextures(coverFront, coverBack, Page(0));
            book.SetCardBack(cardBack);
        }

        /// <summary>A page's texture, baked on first use. Record left pages come with their cards printed or not.</summary>
        RenderTexture Page(int index, bool printed = false)
        {
            index = Mathf.Clamp(index, 0, PageCount - 1);
            if (index < 2 || index % 2 == 1)
                printed = false;
            int key = index * 2 + (printed ? 1 : 0);
            if (!pages.TryGetValue(key, out var texture) || texture == null)
            {
                texture = JournalPageRenderer.NewTarget(JournalPages.Width, JournalPages.Height, "JournalPage" + index);
                pageRenderer.Render(texture, JournalPages.Paper, page => Layout(page, index, printed));
                pages[key] = texture;
            }
            recentPages.Remove(key);
            recentPages.Add(key);
            shownPages.Add(key);
            TrimCache();
            return texture;
        }

        void Layout(RectTransform page, int index, bool printed)
        {
            if (index == 0)
            {
                JournalPages.Title(page, skin, records);
                return;
            }
            if (index == 1)
            {
                JournalPages.Index(page, skin, records);
                return;
            }
            var record = records[(index - 2) / 2];
            if (index % 2 == 0)
                JournalPages.RecordLeft(page, skin, record, deck, index, printed);
            else
                JournalPages.RecordRight(page, skin, record, deck, index);
        }

        void TrimCache()
        {
            for (int i = 0; i < recentPages.Count && pages.Count > CacheSize; )
            {
                int key = recentPages[i];
                if (shownPages.Contains(key))
                {
                    i++;
                    continue;
                }
                recentPages.RemoveAt(i);
                if (pages.TryGetValue(key, out var texture))
                {
                    pages.Remove(key);
                    Release(texture);
                }
            }
        }

        RenderTexture CardTexture(TarotDraw draw)
        {
            string key = draw.cardId + (draw.reversed ? "/r" : "");
            if (cards.TryGetValue(key, out var texture) && texture != null)
                return texture;
            var card = deck?.Find(draw.cardId);
            texture = JournalPageRenderer.NewTarget(JournalPages.CardWidth, JournalPages.CardHeight, "JournalCard " + key);
            pageRenderer.Render(texture, Color.clear, page => JournalPages.CardFace(page, skin, card, draw.reversed, deck?.ArtFor(card)));
            cards[key] = texture;
            return texture;
        }

        void ReleaseTextures()
        {
            foreach (var texture in pages.Values)
                Release(texture);
            pages.Clear();
            recentPages.Clear();
            shownPages.Clear();
            foreach (var texture in cards.Values)
                Release(texture);
            cards.Clear();
            Release(coverFront);
            Release(coverBack);
            Release(cardBack);
            coverFront = coverBack = cardBack = null;
        }

        static void Release(RenderTexture texture)
        {
            if (texture == null)
                return;
            texture.Release();
            Destroy(texture);
        }

        /// <summary>Shows the current spread: its two pages and, for a reading, its cards lying ready to pop up.</summary>
        void ApplySpread(bool popUp)
        {
            shownPages.Clear();
            book.SetCoverTextures(coverFront, coverBack, Page(0));
            book.SetPages(spread > 0 ? Page(spread * 2) : null, Page(spread * 2 + 1));
            book.ClearPopUps();
            if (spread > 0)
            {
                var record = records[spread - 1];
                var list = new List<(Rect, Texture)>();
                for (int i = 0; i < record.draws.Count; i++)
                    list.Add((JournalLayout.CardFootprint(i, record.draws.Count), CardTexture(record.draws[i])));
                book.SetPopUps(list);
                if (popUp && list.Count > 0)
                    app?.Audio?.Play(RoomSound.Flip, 0.35f);
            }
            SetThicknessGoals();
            RefreshLabels();
            QueuePrefetch();
            // The reading is only spoken when asked for (聽解讀); a reading from the last page stops.
            StopReading();
        }

        void SetThicknessGoals()
        {
            int last = Mathf.Max(1, SpreadCount - 1);
            leftThicknessGoal = spread == 0 ? 0f : Mathf.Max(0.006f, JournalBook.PagesT * spread / last);
            rightThicknessGoal = Mathf.Max(0.006f, JournalBook.PagesT - leftThicknessGoal);
        }

        void QueuePrefetch()
        {
            prefetch.Clear();
            int s = spread;
            if (s + 1 < SpreadCount)
            {
                prefetch.Enqueue(() => Page(s * 2 + 2, printed: true));
                prefetch.Enqueue(() => Page(s * 2 + 3));
                prefetch.Enqueue(() => Page(s * 2 + 2));
                var next = records[s];
                prefetch.Enqueue(() => { foreach (var draw in next.draws) CardTexture(draw); });
            }
            if (s > 0)
            {
                prefetch.Enqueue(() => Page(s * 2, printed: true));
                prefetch.Enqueue(() => Page(s * 2 - 1));
                if (s > 1)
                    prefetch.Enqueue(() => Page(s * 2 - 2));
            }
        }

        void RefreshLabels()
        {
            pageLabel.text = spread == 0 ? "封面・目錄" : $"第 {spread} 則 / 共 {records.Count} 則";
            prevButton.interactable = spread > 0;
            nextButton.interactable = spread + 1 < SpreadCount;
            deleteButton.gameObject.SetActive(spread > 0);
            listenButton.gameObject.SetActive(spread > 0);
            confirmDelete = false;
            deleteLabel.text = "刪除這則";
        }

        void DeleteCurrent()
        {
            if (state != State.Reading || spread == 0 || flipDir != 0)
                return;
            if (!confirmDelete)
            {
                confirmDelete = true;
                deleteLabel.text = "再按一次確認";
                return;
            }
            TarotRecordStore.Delete(records[spread - 1].id);
            LoadRecords();
            foreach (var texture in pages.Values)
                Release(texture);
            pages.Clear();
            recentPages.Clear();
            spread = Mathf.Min(spread, records.Count);
            if (spread == 0)
                book.SetThickness(0f, book.RightThickness);
            ApplySpread(popUp: true);
            app?.Audio?.Play(RoomSound.Close, 0.6f);
        }

        // ------------------------------------------------------------------ turning pages

        bool CanFlip(int direction) => direction > 0 ? spread + 1 < SpreadCount : direction < 0 && spread > 0;

        /// <summary>Turns one page forwards (1) or back (-1).</summary>
        public void Flip(int direction)
        {
            if (state != State.Reading || direction == 0 || !CanFlip(direction))
                return;
            StopReading();
            confirmDelete = false;
            deleteLabel.text = "刪除這則";
            if (flipDir == direction && flipGoal < 1f)
            {
                flipGoal = 1f;
                return;
            }
            if (flipDir != 0)
            {
                if (flipDir != direction && flipGoal < 1f)
                    flipGoal = 0f;
                pendingFlip = direction;
                return;
            }
            StartLeaf(direction, 1f);
        }

        /// <summary>Lifts a page corner a little while the pointer rests on it.</summary>
        void Peek(int direction)
        {
            // A queued turn wins: the peek must settle so it can start.
            if (pendingFlip != 0)
                return;
            if (direction == 0 || !CanFlip(direction) || (direction < 0 && book.HasPopUps))
                direction = 0;
            if (flipDir == 0)
            {
                if (direction != 0)
                    StartLeaf(direction, PeekAmount);
                return;
            }
            if (flipGoal >= 1f)
                return;
            flipGoal = direction == flipDir ? PeekAmount : 0f;
        }

        void StartLeaf(int direction, float goal)
        {
            flipDir = direction;
            flipGoal = goal;
            flipProgress = 0f;
            leafReady = false;
            turnSounded = false;
        }

        /// <summary>Puts the right textures on the leaf and under it for the turn about to happen.</summary>
        void ReadyLeaf()
        {
            int s = spread;
            if (flipDir > 0)
            {
                book.ShowLeaf(Page(s * 2 + 1), Page(s * 2 + 2, printed: true));
                book.SetPages(null, Page(s * 2 + 3));
            }
            else
            {
                book.ShowLeaf(Page(s * 2 - 1), Page(s * 2, printed: true));
                if (s - 1 == 0)
                {
                    // Back to the start: the title page inside the cover shows under the lifting page.
                    leftThicknessGoal = 0f;
                    book.SetThickness(0f, book.RightThickness);
                }
                else
                {
                    book.SetPages(Page(s * 2 - 2), null);
                }
            }
            book.BendLeaf(0f, flipDir);
            leafReady = true;
        }

        void StepLeaf(float dt)
        {
            if (flipDir == 0)
                return;
            if (!leafReady)
            {
                // Turning back over a reading: its cards fold down flat before the page lifts.
                if (flipDir < 0 && book.HasPopUps)
                {
                    if (flipGoal < 1f)
                    {
                        flipDir = 0;
                        return;
                    }
                    if (!book.PopUpsFolded)
                        return;
                    book.SetPages(Page(spread * 2, printed: true), null);
                    book.ClearPopUps();
                }
                ReadyLeaf();
            }
            if (flipGoal >= 1f && !turnSounded)
            {
                turnSounded = true;
                app?.Audio?.Play(RoomSound.Flip, 0.7f);
            }
            float speed = flipGoal >= 1f ? 1f / 0.72f : 1f / 0.16f;
            flipProgress = Mathf.MoveTowards(flipProgress, flipGoal, dt * speed);
            book.BendLeaf(flipProgress, flipDir);
            if (flipProgress >= 1f)
            {
                spread += flipDir;
                flipDir = 0;
                leafReady = false;
                book.HideLeaf();
                ApplySpread(popUp: true);
                ContinuePending();
            }
            else if (flipProgress <= 0f && flipGoal <= 0f)
            {
                CancelLeafNow();
                ContinuePending();
            }
        }

        void ContinuePending()
        {
            int next = pendingFlip;
            pendingFlip = 0;
            if (next != 0)
                Flip(next);
        }

        /// <summary>Drops a peek (or an unfinished turn) and restores the spread as it was.</summary>
        void CancelLeafNow()
        {
            if (flipDir == 0 && !leafReady)
                return;
            bool restore = leafReady;
            flipDir = 0;
            leafReady = false;
            book.HideLeaf();
            if (restore)
            {
                // Cards already folded away for a backward turn come back printed on the page.
                if (spread > 0)
                    book.SetPages(Page(spread * 2, printed: !book.HasPopUps), Page(spread * 2 + 1));
                else
                    book.SetPages(null, Page(1));
                SetThicknessGoals();
            }
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (state == State.Hidden)
                return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            if (prefetch.Count > 0 && (state == State.Reading || state == State.Opening) && flipDir == 0)
                prefetch.Dequeue()();

            switch (state)
            {
                case State.Opening:
                    sequence += dt / OpenSeconds[Mathf.Clamp((int)sequence, 0, 3)];
                    if (sequence >= 4f)
                    {
                        sequence = 4f;
                        state = State.Reading;
                        SetControls(true);
                        RefreshLabels();
                    }
                    break;
                case State.Reading:
                    HandleInput(dt);
                    // The page buttons step aside while 凡安 is talking (the dialogue box sits there).
                    bool talking = app != null && app.Dialogue.IsOpen;
                    controls.alpha = Mathf.MoveTowards(controls.alpha, talking ? 0f : 1f, dt / 0.2f);
                    controls.blocksRaycasts = controls.interactable = !talking;
                    listenButton.interactable = reading == null;
                    break;
                case State.Closing:
                    if (foldingForClose)
                    {
                        if (book.PopUpsFolded)
                        {
                            foldingForClose = false;
                            if (spread > 0)
                                book.SetPages(Page(spread * 2, printed: true), null);
                            book.ClearPopUps();
                        }
                        break;
                    }
                    sequence -= dt / CloseSeconds[Mathf.Clamp(Mathf.CeilToInt(sequence) - 1, 0, 3)];
                    if (sequence <= 0f)
                    {
                        FinishHide();
                        return;
                    }
                    break;
            }

            // On a reading's pages the book sits higher and a little smaller, leaving room for the dialogue box.
            int shownSpread = flipDir != 0 && flipGoal >= 1f ? spread + flipDir : spread;
            bool roomForTalk = state == State.Reading && (shownSpread > 0 || (app != null && app.Dialogue.IsOpen));
            talk = Mathf.MoveTowards(talk, roomForTalk ? 1f : 0f, dt / 0.45f);

            StepLeaf(dt);
            // Forward peeks and turns keep the cards up (a turn folds them through the limit as the
            // page comes over); a backward turn lays them down before the page lifts.
            bool standing = state == State.Reading && !foldingForClose && flipDir >= 0;
            float limit = flipDir > 0 && leafReady ? 1f - Smooth(0.08f, 0.36f, flipProgress) : 1f;
            book.StepPopUps(dt, standing, limit);
            float left = Mathf.MoveTowards(book.LeftThickness, leftThicknessGoal, dt * 0.12f);
            float right = Mathf.MoveTowards(book.RightThickness, rightThicknessGoal, dt * 0.12f);
            book.SetThickness(left, right);
        }

        void HandleInput(float dt)
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame || keyboard.pageDownKey.wasPressedThisFrame)
                    Flip(1);
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame || keyboard.pageUpKey.wasPressedThisFrame)
                    Flip(-1);
            }
            if (mouse == null || !PointerInput)
                return;

            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            var position = mouse.position.ReadValue();
            int side = 0;
            bool onBook = !overUi && book.HitTest(view.ScreenPointToRay(position), out side);
            Peek(onBook ? side : 0);

            wheelCooldown -= dt;
            float wheel = mouse.scroll.ReadValue().y;
            if (!overUi && Mathf.Abs(wheel) > 0.01f && wheelCooldown <= 0f)
            {
                Flip(wheel < 0f ? 1 : -1);
                wheelCooldown = 0.25f;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressed = !overUi;
                pressPosition = position;
            }
            if (mouse.leftButton.wasReleasedThisFrame && pressed)
            {
                pressed = false;
                if ((position - pressPosition).magnitude <= 8f)
                {
                    if (onBook)
                        Flip(side);
                    else
                        Close();
                }
            }
        }

        /// <summary>
        /// How far the book has left the shelf: 0 on the shelf, 1 once it floats in front of the view.
        /// The desktop-pet window draws the book from the room's place for 0 and from the screen's
        /// middle for 1, so it leaves the shelf where the room is and is read in the middle.
        /// </summary>
        public static float Lift { get; private set; }

        void LateUpdate()
        {
            if (state == State.Hidden || view == null)
            {
                Lift = 0f;
                return;
            }
            SyncOverlay();
            ApplySequence();
        }

        // ------------------------------------------------------------------ poses

        struct Pose
        {
            public Vector3 position;
            public Quaternion rotation;
            public float scale;
        }

        /// <summary>Places the book for the open/close sequence: 0–1 slide out, 1–2 fly up, 2–3 open, 3–4 float closer.</summary>
        void ApplySequence()
        {
            float s = Mathf.Clamp(sequence, 0f, 4f);
            Lift = s <= 1f ? 0f : s <= 2f ? Ease(s - 1f) : 1f;
            float openness = 0f;
            Pose pose;
            var mid = Anchor(MidViewport(), MidShare, MidTilt, MidYaw);
            if (s <= 1f)
            {
                pose = Shelf(Ease(s));
            }
            else if (s <= 2f)
            {
                float e = Ease(s - 1f);
                pose = Blend(Shelf(1f), mid, e);
                pose.position += view.transform.up * (Mathf.Sin(Mathf.PI * e) * mid.scale * 0.25f);
            }
            else if (s <= 3f)
            {
                pose = mid;
                openness = Ease(s - 2f);
            }
            else
            {
                openness = 1f;
                var read = Blend(Anchor(ReadViewport, ReadShare, ReadTilt), Anchor(TalkViewport, TalkShare, ReadTilt), Ease(talk));
                pose = Blend(mid, read, Ease(s - 3f));
                // A slow breath while it floats in front of you.
                float t = Time.unscaledTime;
                float settle = s - 3f;
                pose.position += view.transform.up * (Mathf.Sin(t * 1.15f) * pose.scale * 0.006f * settle);
                pose.rotation *= Quaternion.Euler(Mathf.Sin(t * 0.8f) * 0.5f * settle, Mathf.Sin(t * 0.63f) * 0.7f * settle, 0f);
            }

            book.SetOpenness(openness);
            var bookTransform = book.transform;
            bookTransform.SetPositionAndRotation(pose.position - pose.rotation * (JournalBook.CenterOffset(openness) * pose.scale), pose.rotation);
            bookTransform.localScale = Vector3.one * pose.scale;
            PlaceDim(Mathf.Clamp01(s - 2.6f));
        }

        Pose Shelf(float slide)
        {
            float scale = slot.size / JournalBook.CoverH;
            var inward = -slot.outward;
            var rotation = Quaternion.LookRotation(Vector3.Cross(inward, Vector3.up), Vector3.up);
            float width = JournalBook.CoverW * scale;
            var center = slot.position + inward * (width * 0.5f + 0.01f) + slot.outward * ((width + 0.06f) * slide);
            return new Pose { position = center, rotation = rotation, scale = scale };
        }

        /// <summary>A pose held in front of the camera: at a viewport point, filling a share of the view's height.</summary>
        Pose Anchor(Vector2 viewport, float heightShare, float tilt, float yaw = 0f)
        {
            float scale = ScaleFor(heightShare);
            float distance = AnchorDistance(scale);
            var camTransform = view.transform;
            return new Pose
            {
                position = view.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, distance)),
                rotation = Quaternion.LookRotation(camTransform.forward, camTransform.up) * Quaternion.Euler(tilt, yaw, 0f),
                scale = scale,
            };
        }

        Vector2 MidViewport()
        {
            var shelf = view.WorldToViewportPoint(slot.position);
            var point = shelf.z > 0f ? new Vector2(shelf.x, shelf.y) : new Vector2(0.5f, 0.5f);
            return Vector2.Lerp(point, new Vector2(0.5f, 0.55f), 0.6f);
        }

        /// <summary>The book's scale (metres per page height) for filling a share of the view.</summary>
        float ScaleFor(float heightShare)
        {
            var (viewHeight, viewWidth) = ViewSize(PerspectiveDistance);
            return Mathf.Min(heightShare * viewHeight / JournalBook.CoverH, 0.94f * viewWidth / (2f * JournalBook.CoverW));
        }

        float ReadingScale => ScaleFor(ReadShare);

        float PerspectiveDistance => Mathf.Max(view.nearClipPlane * 4f, 1.2f);

        /// <summary>
        /// How far in front of the camera a book of this scale floats. A perspective view sizes it by
        /// distance; an orthographic one doesn't, so the book stands back far enough that a cover
        /// swinging open or a page curling over never reaches the near plane.
        /// </summary>
        float AnchorDistance(float scale) => view.orthographic ? view.nearClipPlane + 0.5f + scale * 1.2f : PerspectiveDistance;

        (float height, float width) ViewSize(float distance)
        {
            float height = view.orthographic ? view.orthographicSize * 2f : 2f * distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return (height, height * view.aspect);
        }

        /// <summary>A soft veil between the room and the book while you read (behind every part of the book).</summary>
        void PlaceDim(float amount)
        {
            float distance = AnchorDistance(ReadingScale) + ReadingScale * 1.5f;
            var (height, width) = ViewSize(distance);
            var rect = (RectTransform)dim.transform;
            var camTransform = view.transform;
            rect.SetPositionAndRotation(camTransform.position + camTransform.forward * distance, Quaternion.LookRotation(camTransform.forward, camTransform.up));
            rect.sizeDelta = new Vector2(width * 1.5f, height * 1.5f);
            var color = dimImage.color;
            color.a = 0.3f * amount;
            dimImage.color = color;
        }

        static Pose Blend(Pose a, Pose b, float t) => new()
        {
            position = Vector3.LerpUnclamped(a.position, b.position, t),
            rotation = Quaternion.SlerpUnclamped(a.rotation, b.rotation, t),
            scale = a.scale * Mathf.Pow(b.scale / Mathf.Max(1e-5f, a.scale), t),
        };

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static float Smooth(float from, float to, float t) => Ease(Mathf.InverseLerp(from, to, t));
    }
}
