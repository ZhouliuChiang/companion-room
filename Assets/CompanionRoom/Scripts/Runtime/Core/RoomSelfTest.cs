using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using CompanionRoom.Tarot;
using UnityEngine;

namespace CompanionRoom
{
    /// <summary>
    /// Scripted tour for checking a build: start the player with <c>-roomSelfTest &lt;folder&gt;</c> and it
    /// clicks through every item (window ×3, lamp, photos, computer, tarot, cabinet, 凡安), saves a
    /// screenshot at each step and a report, then quits. Records go to the test folder, never the
    /// player's own history.
    /// </summary>
    public class RoomSelfTest : MonoBehaviour
    {
        const string Flag = "-roomSelfTest";

        public static bool Requested => OutputFolder != null;

        static string OutputFolder
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, Flag);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }
        }

        static readonly string[] SavedKeys = { "TimeOfDay", "LampOn", "PhotoIndex" };

        readonly StringBuilder report = new();
        readonly int[] savedValues = new int[SavedKeys.Length];
        int errors;
        string folder;
        CompanionRoomApp app;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Requested)
                new GameObject("RoomSelfTest").AddComponent<RoomSelfTest>();
        }

        void Awake()
        {
            folder = Path.GetFullPath(OutputFolder);
            Directory.CreateDirectory(folder);
            TarotRecordStore.DirectoryOverride = Path.Combine(folder, "records");
            TarotRecordStore.Clear();
            for (int i = 0; i < SavedKeys.Length; i++)
                savedValues[i] = RoomSettings.GetInt(SavedKeys[i], int.MinValue);
            Application.logMessageReceived += OnLog;
        }

        /// <summary>Puts back the lamp / time / photo choices the tour changed.</summary>
        void RestorePreferences()
        {
            for (int i = 0; i < SavedKeys.Length; i++)
            {
                if (savedValues[i] == int.MinValue)
                    PlayerPrefs.DeleteKey("CompanionRoom." + SavedKeys[i]);
                else
                    RoomSettings.SetInt(SavedKeys[i], savedValues[i]);
            }
            PlayerPrefs.Save();
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void OnLog(string message, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
            {
                errors++;
                report.AppendLine($"[{type}] {message}\n{stack}");
            }
        }

        void Log(string line)
        {
            report.AppendLine(line);
            Debug.Log("[RoomSelfTest] " + line);
        }

        IEnumerator Start()
        {
            yield return null;
            app = CompanionRoomApp.Instance;
            if (app == null)
            {
                Log("FAIL: no CompanionRoomApp in the scene");
                Finish();
                yield break;
            }
            yield return Run();
            Finish();
        }

        void Finish()
        {
            RestorePreferences();
            Log(errors == 0 ? "RESULT: PASS (no errors logged)" : $"RESULT: {errors} error(s) logged");
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        T Item<T>() where T : RoomItem => RoomItem.All.OfType<T>().FirstOrDefault();

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return null;
            yield return null;
            Log("captured " + name);
        }

        IEnumerator WaitFor(Func<bool> condition, float timeout, string what)
        {
            for (float t = 0f; t < timeout; t += Time.unscaledDeltaTime)
            {
                if (condition())
                    yield break;
                yield return null;
            }
            errors++;
            Log("TIMEOUT waiting for " + what);
        }

        /// <summary>Clicks through dialogue until the condition holds.</summary>
        IEnumerator AdvanceUntil(Func<bool> condition, float timeout, string what)
        {
            var dialogue = app.Dialogue;
            for (float t = 0f; t < timeout; t += Time.unscaledDeltaTime)
            {
                if (condition())
                    yield break;
                if (dialogue.IsOpen && !dialogue.IsChoosing && (dialogue.IsTyping || dialogue.IsWaiting))
                    dialogue.Advance();
                yield return Pause(0.06f);
            }
            errors++;
            Log("TIMEOUT advancing until " + what);
        }

        static IEnumerator Pause(float seconds) => CompanionRoomApp.Wait(seconds);

        IEnumerator FinishInteraction(string what) => AdvanceUntil(() => !app.IsBusy, 30f, what + " to end");

        IEnumerator Run()
        {
            var dialogue = app.Dialogue;
            Log($"Screen {Screen.width}x{Screen.height}, items: {string.Join(", ", RoomItem.All.Select(i => i.DisplayName))}");
            if (app.Lighting != null)
                app.Lighting.Set(TimeOfDay.Morning, instant: true);
            yield return Pause(1.5f);
            yield return FinishInteraction("greeting");
            yield return Pause(0.6f);
            yield return Capture("01_room_morning");

            var photo = Item<PhotoFrameItem>();
            if (photo != null)
            {
                photo.SetHovered(true);
                yield return Pause(0.4f);
                yield return Capture("02_hover_outline");
                photo.SetHovered(false);
            }

            var window = Item<WindowItem>();
            if (window != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    app.Interact(window);
                    yield return WaitFor(() => dialogue.IsWaiting && !dialogue.IsTyping || !app.IsBusy, 10f, "window comment");
                    yield return Pause(0.3f);
                    yield return Capture($"03_window_{i + 1}_{RoomLighting.Key(app.Lighting.Current)}");
                    yield return FinishInteraction("window");
                }
                Log("time after 3 window clicks: " + app.Lighting.Current);
            }

            // Night now: try the lamp.
            if (app.Lighting != null && !app.Lighting.IsNight)
                app.Lighting.Set(TimeOfDay.Night, instant: true);
            var lamp = Item<WallLampItem>();
            if (lamp != null)
            {
                if (lamp.IsOn)
                    lamp.Toggle();
                yield return Pause(0.5f);
                app.Interact(lamp);
                yield return WaitFor(() => dialogue.IsWaiting && !dialogue.IsTyping || !app.IsBusy, 10f, "lamp comment");
                yield return Pause(0.3f);
                yield return Capture("04_lamp_on_night");
                yield return FinishInteraction("lamp");
                Log("lamp on: " + lamp.IsOn);
            }
            app.Lighting?.Set(TimeOfDay.Morning, instant: true);
            yield return Pause(0.5f);

            if (photo != null)
            {
                app.Interact(photo);
                yield return WaitFor(() => dialogue.IsWaiting && !dialogue.IsTyping, 10f, "photo line");
                yield return Pause(0.4f);
                yield return Capture("05_dialogue");
                yield return AdvanceUntil(() => app.UI.Photos.IsOpen, 15f, "photo viewer");
                yield return Pause(0.6f);
                yield return Capture("06_photos");
                app.UI.Photos.Step(1);
                yield return Pause(0.5f);
                yield return Capture("07_photos_next");
                app.UI.Photos.Hide();
                yield return FinishInteraction("photo");
            }

            var computer = Item<ComputerItem>();
            if (computer != null)
            {
                app.Interact(computer);
                yield return AdvanceUntil(() => app.UI.Settings.IsOpen, 15f, "settings");
                yield return Pause(0.6f);
                yield return Capture("08_settings");
                app.UI.Settings.Hide();
                yield return FinishInteraction("computer");
            }

            var tarot = Item<TarotDeckItem>();
            if (tarot != null)
            {
                app.Interact(tarot);
                yield return AdvanceUntil(() => dialogue.IsChoosing, 15f, "spread choice");
                yield return Pause(0.4f);
                yield return Capture("09_tarot_choice");
                dialogue.Pick(1);
                // Let the spread question finish before waiting for the next one.
                yield return Pause(0.3f);
                yield return AdvanceUntil(() => dialogue.IsChoosing && app.UI.Tarot.IsShowingDeck, 10f, "ready question");
                yield return Pause(0.6f);
                yield return Capture("10_tarot_ready_question");
                dialogue.Pick(0);
                yield return WaitFor(() => app.UI.Tarot.Slots.Count == 3 && app.UI.Tarot.Slots[0].Clickable, 10f, "dealt cards");
                yield return Pause(0.4f);
                yield return Capture("11_tarot_dealt");
                for (int i = 0; i < 3; i++)
                {
                    int index = i;
                    yield return WaitFor(() => app.UI.Tarot.Slots[index].Clickable, 30f, "card " + index + " clickable");
                    app.UI.Tarot.ClickCard(index);
                    yield return WaitFor(() => dialogue.IsWaiting, 5f, "reveal line");
                    if (index == 2)
                    {
                        yield return Pause(1.2f);
                        yield return Capture("12_tarot_reading");
                    }
                    if (index < 2)
                        yield return AdvanceUntil(() => app.UI.Tarot.Slots[index + 1].Clickable, 30f, "next card");
                }
                yield return AdvanceUntil(() => dialogue.IsChoosing, 30f, "again choice");
                dialogue.Pick(1);
                yield return FinishInteraction("tarot");
                Log("records after tarot: " + TarotRecordStore.Load().Count);
                if (tarot.LastDraws != null)
                    Log("drawn: " + string.Join(", ", tarot.LastDraws.Select(d => $"{d.position}={d.cardId}{(d.reversed ? "(R)" : "")}")));
            }

            var cabinet = Item<RecordCabinetItem>();
            if (cabinet != null)
            {
                var journal = app.UI.Journal;
                app.Interact(cabinet);
                yield return AdvanceUntil(() => journal.IsReading || !app.IsBusy, 15f, "record journal");
                yield return Pause(0.7f);
                yield return Capture("13_records");
                if (journal.SpreadCount > 1)
                {
                    journal.Flip(1);
                    yield return WaitFor(() => journal.Spread == 1 && !journal.IsTurning, 5f, "journal page turn");
                    yield return Pause(0.8f);
                    yield return Capture("13b_records_reading");
                }
                journal.Close();
                yield return FinishInteraction("cabinet");
            }

            var companion = Item<CompanionTalkItem>();
            if (companion != null)
            {
                app.Interact(companion);
                yield return WaitFor(() => dialogue.IsWaiting && !dialogue.IsTyping, 10f, "chat");
                yield return Pause(0.3f);
                yield return Capture("14_chat");
                yield return FinishInteraction("chat");
            }

            // Esc-style interrupt in the middle of a reading must close everything.
            if (tarot != null)
            {
                app.Interact(tarot);
                yield return AdvanceUntil(() => dialogue.IsChoosing, 15f, "spread choice (interrupt test)");
                app.Interrupt();
                yield return Pause(0.5f);
                Log($"after interrupt: busy={app.IsBusy}, windows open={app.UI.AnyWindowOpen}");
                if (app.IsBusy || app.UI.AnyWindowOpen)
                {
                    errors++;
                    Log("FAIL: interrupt left something open");
                }
            }
            yield return Capture("15_after_tour");
        }
    }
}
