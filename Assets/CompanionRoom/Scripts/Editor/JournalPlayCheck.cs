using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using CompanionRoom.Tarot;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CompanionRoom.EditorTools
{
    /// <summary>
    /// Play-mode check of the computer and the record journal in the open editor. Create
    /// Temp/JournalCheck.request (or use the menu): it enters Play, hovers and clicks the computer,
    /// opens the journal from the bookcase, turns pages both ways and puts it back, saving a picture
    /// of each step and a report to Artifacts/JournalCheck. Sample readings go to a test folder, never
    /// the player's own records.
    /// </summary>
    [InitializeOnLoad]
    public static class JournalPlayCheck
    {
        const string Request = "Temp/JournalCheck.request";
        const string Flag = "JournalPlayCheck.Active";
        const string Folder = "Artifacts/JournalCheck";

        static IEnumerator run;
        static double resumeAt;
        static Func<bool> waitUntil;
        static double waitLimit;
        static readonly StringBuilder report = new();
        static int problems;

        static JournalPlayCheck() => EditorApplication.update += Poll;

        [MenuItem("Tools/兔兔房間/檢查占卜手帳與電腦 (Play)", priority = 30)]
        static void Menu() => File.WriteAllText(Request, "check");

        static void Poll()
        {
            if (EditorApplication.isPlaying && SessionState.GetBool(Flag, false))
            {
                Step();
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request))
                return;
            File.Delete(Request);
            Directory.CreateDirectory(Folder);
            foreach (var old in Directory.GetFiles(Folder, "*.png"))
                File.Delete(old);
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static void Step()
        {
            try
            {
                if (run == null)
                {
                    report.Clear();
                    problems = 0;
                    Application.logMessageReceived += OnLog;
                    run = Run();
                    resumeAt = EditorApplication.timeSinceStartup + 1.5;
                }
                double now = EditorApplication.timeSinceStartup;
                if (waitUntil != null)
                {
                    if (!waitUntil() && now < waitLimit)
                        return;
                    if (!waitUntil())
                        Problem("timed out waiting");
                    waitUntil = null;
                }
                if (now < resumeAt)
                    return;
                if (!run.MoveNext())
                {
                    End();
                    return;
                }
                switch (run.Current)
                {
                    case float seconds:
                        resumeAt = now + seconds;
                        break;
                    case Func<bool> condition:
                        waitUntil = condition;
                        waitLimit = now + 15.0;
                        break;
                }
            }
            catch (Exception e)
            {
                Problem("exception: " + e);
                End();
            }
        }

        static void End()
        {
            Application.logMessageReceived -= OnLog;
            TarotRecordStore.DirectoryOverride = null;
            report.AppendLine(problems == 0 ? "RESULT: PASS" : $"RESULT: {problems} problem(s)");
            File.WriteAllText(Path.Combine(Folder, "report.txt"), report.ToString());
            run = null;
            waitUntil = null;
            SessionState.SetBool(Flag, false);
            EditorApplication.isPlaying = false;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
                Problem($"[{type}] {message}\n{stack}");
        }

        static void Problem(string message)
        {
            problems++;
            report.AppendLine("PROBLEM: " + message);
        }

        static void Log(string message) => report.AppendLine(message);

        static IEnumerator Run()
        {
            var app = CompanionRoomApp.Instance;
            if (app == null)
            {
                Problem("no CompanionRoomApp");
                yield break;
            }
            TarotRecordStore.DirectoryOverride = Path.GetFullPath(Path.Combine(Folder, "records"));
            TarotRecordStore.Clear();
            AddSample("single", "night", "這週要不要開始新的計畫？", ("star", false));
            AddSample("three", "morning", "這個月的心情會好嗎？", ("fool", false), ("moon", true), ("sun", false));
            AddSample("three", "sunset", "", ("lovers", false), ("hermit", true), ("world", false));

            var items = RoomItem.All.ToList();
            Log("items: " + string.Join(", ", items.Select(i => $"{i.name} → {i.GetType().Name}")));
            var computer = items.OfType<ComputerItem>().FirstOrDefault();
            var cabinet = items.OfType<RecordCabinetItem>().FirstOrDefault();
            if (computer == null)
                Problem("computer has no ComputerItem");
            if (cabinet == null)
                Problem("bookcase has no RecordCabinetItem");
            Capture("00_room");

            if (computer != null)
            {
                computer.SetHovered(true);
                yield return 0.4f;
                Capture("01_hover_computer");
                computer.SetHovered(false);
                yield return (Func<bool>)(() => !app.IsBusy);
                bool started = app.Interact(computer);
                Log($"computer interaction started: {started} (active item: {(app.ActiveItem != null ? app.ActiveItem.name : "none")})");
                yield return (Func<bool>)(() => app.UI.Settings.IsOpen);
                yield return 0.5f;
                Log($"settings open: {app.UI.Settings.IsOpen}, dialogue shown: {app.Dialogue.IsOpen}");
                if (app.Dialogue.IsOpen)
                    Problem("a dialogue line popped up with the settings");
                app.UI.Settings.Hide();
                yield return (Func<bool>)(() => !app.IsBusy);
                yield return 0.6f;
            }

            // Tarot: 凡安 asks RPG-style whether the question is in mind; two dashed answers at the lower right.
            var dialogue = app.Dialogue;
            var tarot = app.UI.Tarot;
            bool? answer = null;
            app.Run(null, TarotDeckItem.AskReady(app, a => answer = a));
            yield return (Func<bool>)(() => dialogue.IsChoosing);
            yield return 0.6f;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Folder, "tarot_ready_question_screen.png")));
            yield return 0.4f;
            bool field = tarot.GetComponentInChildren<UnityEngine.UI.InputField>(true) != null;
            Log($"tarot question: {dialogue.Line}, answers: {dialogue.ChoiceCount}, deck on table: {tarot.IsShowingDeck}, question field: {field}");
            if (dialogue.ChoiceCount != 2 || field)
                Problem("expected two answers and no question field");
            dialogue.Pick(0);
            yield return (Func<bool>)(() => answer != null);
            Log($"想好了，請發牌 → deal: {answer}");
            if (answer != true)
                Problem("想好了，請發牌 did not continue to dealing");
            yield return (Func<bool>)(() => !app.IsBusy);
            yield return 0.4f;

            answer = null;
            app.Run(null, TarotDeckItem.AskReady(app, a => answer = a));
            yield return (Func<bool>)(() => dialogue.IsChoosing);
            yield return 0.3f;
            dialogue.Pick(1);
            yield return (Func<bool>)(() => answer != null);
            Log($"退出 → deal: {answer}");
            if (answer != false)
                Problem("退出 did not leave the reading");
            yield return (Func<bool>)(() => !app.IsBusy);
            yield return 0.4f;

            if (cabinet == null)
                yield break;
            var journal = app.UI.Journal;
            journal.PointerInput = false;
            app.Interact(cabinet);
            yield return 0.3f;
            Capture("02_slide_out");
            yield return 0.75f;
            Capture("03_fly_up");
            yield return 0.75f;
            Capture("04_opening");
            yield return (Func<bool>)(() => journal.IsReading);
            yield return 0.5f;
            Capture("05_reading_contents");
            Log($"journal spreads: {journal.SpreadCount}");
            if (journal.SpreadCount != 4)
                Problem("expected 4 spreads (contents + 3 readings)");

            journal.Flip(1);
            yield return 0.35f;
            Capture("06_turning_forward");
            yield return (Func<bool>)(() => journal.Spread == 1 && !journal.IsTurning);
            yield return 0.6f;
            Capture("07_reading_single_popup");
            yield return (Func<bool>)(() => app.Dialogue.IsOpen);
            yield return 1.2f;
            Capture("07b_reading_spoken");
            Log($"dialogue open: {app.Dialogue.IsOpen}, line: {app.Dialogue.Line.Replace('\n', ' ')}");
            if (!app.Dialogue.IsOpen)
                Problem("the reading was not spoken in the dialogue box");

            journal.Flip(1);
            yield return (Func<bool>)(() => journal.Spread == 2 && !journal.IsTurning);
            yield return 0.9f;
            Capture("08_reading_three_popup");

            journal.Flip(-1);
            yield return 0.55f;
            Capture("09_turning_back");
            yield return (Func<bool>)(() => journal.Spread == 1 && !journal.IsTurning);
            yield return 0.5f;

            journal.Close();
            yield return 0.9f;
            Capture("10_closing");
            yield return (Func<bool>)(() => !app.IsBusy && !journal.IsOpen);
            yield return 0.3f;
            Capture("11_back_on_shelf");
            Log($"after close: busy={app.IsBusy}, journal open={journal.IsOpen}, dialogue open={app.Dialogue.IsOpen}");
            if (app.Dialogue.IsOpen)
                Problem("dialogue left open after the journal closed");
        }

        static void AddSample(string spread, string time, string question, params (string id, bool reversed)[] cards)
        {
            var positions = TarotSpread.Find(spread)?.positions ?? new[] { "指引" };
            var record = new TarotRecord
            {
                castAtUtc = DateTime.UtcNow.AddMinutes(-cards.Length * 7).ToString("o"),
                spread = spread,
                question = question,
                timeOfDay = time,
            };
            for (int i = 0; i < cards.Length; i++)
                record.draws.Add(new TarotDraw { cardId = cards[i].id, reversed = cards[i].reversed, position = positions[Mathf.Min(i, positions.Length - 1)] });
            TarotRecordStore.Append(record);
        }

        /// <summary>Renders the room camera (3D only; screen-space buttons are not in the picture).</summary>
        static void Capture(string name)
        {
            var camera = Camera.main;
            if (camera == null)
                return;
            const int width = 1600, height = 900;
            var target = new RenderTexture(width, height, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), image.EncodeToPNG());
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            target.Release();
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            Log("captured " + name);
        }
    }
}
