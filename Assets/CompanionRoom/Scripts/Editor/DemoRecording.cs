using System;
using System.Collections;
using System.IO;
using System.Linq;
using CompanionRoom.Tarot;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CompanionRoom.EditorTools
{
    // Opt-in capture tool. Only runs when Temp/DemoRecording.request is created.
    [InitializeOnLoad]
    public static class DemoRecording
    {
        const string Flag = "CompanionRoom.DemoRecording";
        static DemoRecording() => EditorApplication.update += Poll;
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists("Temp/DemoRecording.stop"))
            {
                File.Delete("Temp/DemoRecording.stop");
                SessionState.SetBool(Flag, false);
                EditorApplication.isPlaying = false;
                return;
            }
            if (EditorApplication.isPlaying && SessionState.GetBool(Flag, false))
            {
                SessionState.SetBool(Flag, false);
                var view = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
                view.Show();
                view.Focus();
                new GameObject("Demo Recording (temporary)").AddComponent<DemoRecordingRunner>();
            }
            else if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Temp/DemoRecording.request"))
            {
                File.Delete("Temp/DemoRecording.request");
                SessionState.SetBool(Flag, true);
                EditorApplication.isPlaying = true;
            }
        }
    }

    /// <summary>
    /// Plays the room by itself for the gameplay video: the tutorial lighting up the lamp and the
    /// window, the time of day, chatting with and walking the witch, the settings, a tarot reading,
    /// the record journal and the pendulum. Writes a 1080p mp4 and saves stills for the store page.
    /// Player data is left as it was (records go to a separate demo folder, prefs are restored).
    /// </summary>
    public class DemoRecordingRunner : MonoBehaviour
    {
        const int Fps = 30, Width = 1920, Height = 1080;
        const string Folder = "Artifacts/Demo";
        const string StillFolder = "Artifacts/Itch";
        const string VideoName = "CompanionRoom_Gameplay_1080p.mp4";
        System.Diagnostics.Process encoder;
        RenderTexture target;
        Texture2D pixels;
        CompanionRoomApp app;
        WitchLocomotion walker;
        // Without the witch in the room, the chat and walk part (12 s) is left out.
        bool HasWitch => walker != null || RoomItem.All.OfType<CompanionTalkItem>().Any();
        int Seconds => HasWitch ? 75 : 63;
        float L(float seconds) => HasWitch ? seconds : seconds - 12f;
        int frame;
        int oldTime, oldLamp, oldTutorial;
        string oldDirectory;
        string stillName;
        bool cleaned;
        Text label;
        string encoderErrors = "";

        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory(StillFolder);
            File.WriteAllText(Folder + "/status.txt", "Preparing");
            yield return new WaitForSecondsRealtime(2);
            app = CompanionRoomApp.Instance;
            if (app == null) { File.WriteAllText(Folder + "/status.txt", "ERROR: no room app"); yield break; }
            app.Interrupt();
            walker = FindAnyObjectByType<WitchLocomotion>();
            oldTime = RoomSettings.GetInt("TimeOfDay", 0);
            oldLamp = RoomSettings.GetInt("LampOn", 0);
            oldTutorial = RoomSettings.GetInt("TutorialDone", 0);
            oldDirectory = TarotRecordStore.DirectoryOverride;
            TarotRecordStore.DirectoryOverride = Path.GetFullPath(Folder + "/records");
            // Seed only the separate demo record folder, without touching player data.
            if (TarotRecordStore.Load().Count == 0)
            {
                var r = new TarotRecord { castAtUtc = DateTime.UtcNow.ToString("o"), spread = "three", timeOfDay = "morning" };
                var positions = TarotSpread.Find("three").positions;
                string[] ids = { "star", "sun", "world" };
                for (int i = 0; i < 3; i++) r.draws.Add(new TarotDraw { cardId = ids[i], position = positions[i] });
                TarotRecordStore.Append(r);
            }
            app.Lighting.Set(TimeOfDay.Morning, true);
            var lamp = RoomItem.All.OfType<WallLampItem>().FirstOrDefault();
            if (lamp != null && lamp.IsOn) lamp.Toggle();
            app.UI.Journal.PointerInput = false;
            var go = new GameObject("Demo caption", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(app.UI.Canvas.transform, false);
            label = go.GetComponent<Text>();
            label.font = app.UI.Skin.font;
            label.fontSize = 34;
            label.color = new Color(1f, .96f, .91f);
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(44, -30);
            rect.sizeDelta = new Vector2(1400, 70);
            go.GetComponent<Outline>().effectColor = new Color(.2f, .1f, .18f, .8f);
            target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
            pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var start = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Path.GetFullPath("Tools/demo_deps/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe"),
                Arguments = $"-y -hide_banner -loglevel warning -f rawvideo -pixel_format rgb24 -video_size {Width}x{Height} -framerate {Fps} -i pipe:0 -vf vflip -an -c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -movflags +faststart \"" + Path.GetFullPath(Folder + "/" + VideoName) + "\"",
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardError = true
            };
            encoder = System.Diagnostics.Process.Start(start);
            encoder.ErrorDataReceived += (_, e) => { if (e.Data != null) encoderErrors += e.Data + "\n"; };
            encoder.BeginErrorReadLine();
            Time.captureFramerate = Fps;
            Vector2 source = Vector2.zero;
            for (frame = 0; frame < Fps * Seconds; frame++)
            {
                stillName = null;
                Drive(frame);
                yield return new WaitForEndOfFrame();
                var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                source = new Vector2(screenshot.width, screenshot.height);
                // Centre-crop the game view to 16:9 so nothing is stretched.
                float aspect = (float)screenshot.width / screenshot.height, wanted = (float)Width / Height;
                var scale = aspect > wanted ? new Vector2(wanted / aspect, 1f) : new Vector2(1f, aspect / wanted);
                Graphics.Blit(screenshot, target, scale, (Vector2.one - scale) * 0.5f);
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                pixels.Apply(false);
                RenderTexture.active = previous;
                Destroy(screenshot);
                var bytes = pixels.GetRawTextureData<byte>().ToArray();
                encoder.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
                if (stillName != null)
                    File.WriteAllBytes(StillFolder + "/screenshot_" + stillName + ".png", pixels.EncodeToPNG());
                if (frame % (Fps * 5) == 0)
                    File.WriteAllText(Folder + "/status.txt", "Recording " + (frame / Fps) + "/" + Seconds + " seconds");
            }
            Cleanup();
            File.WriteAllText(Folder + "/status.txt", $"DONE: {Seconds} seconds, {Width}x{Height}, {Fps} fps, game view {source.x}x{source.y}, "
                + new FileInfo(Folder + "/" + VideoName).Length + " bytes\n" + encoderErrors);
            EditorApplication.isPlaying = false;
        }

        bool At(int f, float seconds) => f == Mathf.RoundToInt(seconds * Fps);

        void Still(string name) => stillName = name;

        // Moves the dialogue on: picks an answer when asked, otherwise skips to the next line.
        void Talk(int choice = 1)
        {
            var d = app.Dialogue;
            if (d.IsChoosing) d.Pick(Mathf.Min(choice, d.ChoiceCount - 1));
            else if (d.IsOpen && !d.IsTyping) d.Advance();
        }

        void Walk(Vector3 offset)
        {
            if (walker == null) return;
            var c = walker.roomBounds.center;
            walker.GoTo(new Vector3(c.x + offset.x, walker.floor, c.z + offset.z));
        }

        void Drive(int f)
        {
            var tutorial = app.UI.Tutorial;
            // 0–15 s: the first-run tutorial lights up the lamp, then the window.
            if (At(f, 0)) { label.text = "Companion Room  ·  3D 桌面陪伴"; tutorial.Begin(); }
            if (At(f, 3)) { label.text = "新手教學  ·  一次點亮一樣，親手試試看"; tutorial.Next(); }
            if (At(f, 5)) Still("1_tutorial");
            if (At(f, 6.5f)) app.Interact(RoomItem.All.OfType<WallLampItem>().FirstOrDefault());
            if (f > 6.5f * Fps && f < 10 * Fps && f % 20 == 0) Talk();
            if (At(f, 10)) { app.Interrupt(); label.text = "時間變化  ·  早晨 / 夕陽 / 夜晚"; app.Interact(RoomItem.All.OfType<WindowItem>().FirstOrDefault()); }
            if (At(f, 11.5f)) tutorial.Hide();
            if (At(f, 12)) app.Interact(RoomItem.All.OfType<WindowItem>().FirstOrDefault());
            if (At(f, 13.5f)) Still("2_night");
            if (At(f, 14.5f)) app.Interact(RoomItem.All.OfType<WindowItem>().FirstOrDefault());

            // 16–27 s: the witch chats, then walks around the room.
            if (HasWitch)
            {
            if (At(f, 16)) { app.Interrupt(); label.text = "和她聊天  ·  點擊角色互動"; app.Interact(RoomItem.All.OfType<CompanionTalkItem>().FirstOrDefault()); }
            if (f > 16 * Fps && f < 22 * Fps && f % 45 == 0) Talk();
            if (At(f, 22)) { app.Interrupt(); label.text = "自由走動  ·  點擊地板讓她走過去"; Walk(new Vector3(-0.45f, 0f, 0.15f)); }
            if (At(f, 24.5f)) { Walk(new Vector3(0.4f, 0f, -0.1f)); }
            if (At(f, 26)) Still("3_witch");
            }

            // 28–32 s: settings on the computer.
            if (At(f, L(28))) { label.text = "電腦設定  ·  音量、文字速度與重看教學"; app.Interact(RoomItem.All.OfType<ComputerItem>().FirstOrDefault()); }
            if (f > L(28) * Fps && f < L(32) * Fps && f % 20 == 0 && app.Dialogue.IsOpen && !app.UI.Settings.IsOpen) Talk();
            if (At(f, L(31))) Still("3_settings");

            // 32–52 s: a three-card tarot reading.
            if (At(f, L(32))) { app.Interrupt(); label.text = "塔羅占卜  ·  三張牌指引"; app.Interact(RoomItem.All.OfType<TarotDeckItem>().FirstOrDefault()); }
            if (f > L(32) * Fps && f < L(52) * Fps && f % 22 == 0)
            {
                var d = app.Dialogue;
                if (d.IsChoosing) d.Pick(d.ChoiceCount == 3 ? 1 : (app.UI.Tarot.Slots.Count == 3 ? 1 : 0));
                else
                {
                    var slots = app.UI.Tarot.Slots;
                    int clickable = -1;
                    for (int i = 0; i < slots.Count; i++) if (slots[i].Clickable) { clickable = i; break; }
                    if (clickable >= 0) app.UI.Tarot.ClickCard(clickable);
                    else if (d.IsOpen && !d.IsTyping) d.Advance();
                }
            }
            if (At(f, L(46))) Still("4_tarot");

            // 52–62 s: the record journal comes off the shelf.
            if (At(f, L(52))) { app.Interrupt(); label.text = "占卜手帳  ·  立體卡牌與翻頁"; app.Interact(RoomItem.All.OfType<RecordCabinetItem>().FirstOrDefault()); }
            if (f > L(52) * Fps && f < L(56) * Fps && f % 20 == 0 && !app.UI.Journal.IsReading) Talk();
            if (At(f, L(56)) && app.UI.Journal.IsReading) app.UI.Journal.Flip(1);
            if (At(f, L(55.5f))) Still("5_journal");
            if (At(f, L(58))) Still("5_journal_b");
            if (At(f, L(59)) && app.UI.Journal.IsReading) app.UI.Journal.Flip(app.UI.Journal.SpreadCount > 2 ? 1 : -1);
            if (At(f, L(61.5f))) app.UI.Journal.Close();

            // 62–72 s: the pendulum.
            if (At(f, L(62))) { app.Interrupt(); label.text = "靈擺互動  ·  校正與提問"; app.Interact(RoomItem.All.OfType<PendulumItem>().FirstOrDefault()); }
            if (f > L(62) * Fps && f < L(72) * Fps && f % Fps == 0 && app.Dialogue.IsChoosing)
                app.Dialogue.Pick(app.Dialogue.ChoiceCount > 1 ? 1 : 0);
            if (At(f, L(64.5f))) Still("6_pendulum");
            if (At(f, L(67))) Still("6_pendulum_b");
            if (At(f, L(70))) Still("6_pendulum_c");

            if (At(f, L(72))) { app.Interrupt(); label.text = "Companion Room  ·  日常陪伴與占卜時光"; }
        }

        void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            Time.captureFramerate = 0;
            if (encoder != null)
            {
                encoder.StandardInput.Close();
                encoder.WaitForExit(60000);
                encoder.Dispose();
            }
            TarotRecordStore.DirectoryOverride = oldDirectory;
            RoomSettings.SetInt("TimeOfDay", oldTime);
            RoomSettings.SetInt("LampOn", oldLamp);
            RoomSettings.SetInt("TutorialDone", oldTutorial);
            if (target != null) { target.Release(); Destroy(target); }
            if (pixels != null) Destroy(pixels);
        }
        void OnDestroy() => Cleanup();
    }
}
