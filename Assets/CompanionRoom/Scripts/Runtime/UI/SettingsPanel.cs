using System.Collections;
using CompanionRoom.Tarot;
using UnityEngine;
using UnityEngine.UI;

namespace CompanionRoom.UI
{
    /// <summary>Settings opened from the computer: volumes, text speed, lighting, window, records and the tutorial.</summary>
    public class SettingsPanel : RoomPanel
    {
        Slider master;
        Slider music;
        Slider sfx;
        Slider textSpeed;
        Toggle alwaysOnTop;
        Text clearLabel;
        Text quitLabel;
        bool confirmClear;
        bool confirmQuit;

        public static SettingsPanel Build(Transform parent, CompanionRoomApp app, RoomUISkin skin)
        {
            var root = UIFactory.Rect("Settings", parent).Stretch();
            var panel = root.gameObject.AddComponent<SettingsPanel>();
            UIFactory.Backdrop(root, skin);

            var card = UIFactory.Panel("Card", root, skin);
            card.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(900f, 784f));

            var title = UIFactory.Panel("Title", card, skin, skin.titlePlate, withShadow: false);
            title.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 64f));
            UIFactory.Text("Label", title, skin, 32, skin.accentInk, TextAnchor.MiddleCenter, "設定").rectTransform.Stretch(0f, 0f, 0f, 3f);

            var close = UIFactory.RoundButton("Close", card, skin, "×", 68f, panel.Hide, 40);
            close.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-14f, -14f), new Vector2(68f, 68f));

            float y = -84f;
            RectTransform Row(string label)
            {
                var row = UIFactory.Rect(label, card);
                row.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(760f, 64f));
                var text = UIFactory.Text("Label", row, skin, 30, skin.ink, TextAnchor.MiddleLeft, label);
                text.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(600f, 60f));
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                y -= 76f;
                return row;
            }
            void Right(Component control, Vector2 size)
            {
                ((RectTransform)control.transform).Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, size);
            }

            panel.master = UIFactory.Slider("Master", Row("總音量"), skin, 0f, 1f, RoomSettings.MasterVolume, v =>
            {
                RoomSettings.SetMasterVolume(v);
                app?.Audio?.Play(RoomSound.Click, 0.6f);
            });
            Right(panel.master, new Vector2(420f, 40f));
            panel.music = UIFactory.Slider("Music", Row("音樂音量"), skin, 0f, 1f, RoomSettings.MusicVolume, RoomSettings.SetMusicVolume);
            Right(panel.music, new Vector2(420f, 40f));
            panel.sfx = UIFactory.Slider("Sfx", Row("音效音量"), skin, 0f, 1f, RoomSettings.SfxVolume, v =>
            {
                RoomSettings.SetSfxVolume(v);
                app?.Audio?.Play(RoomSound.Click, 0.6f);
            });
            Right(panel.sfx, new Vector2(420f, 40f));
            panel.textSpeed = UIFactory.Slider("TextSpeed", Row("文字速度"), skin, RoomSettings.MinTextSpeed, RoomSettings.MaxTextSpeed, RoomSettings.TextSpeed, RoomSettings.SetTextSpeed);
            Right(panel.textSpeed, new Vector2(420f, 40f));

            panel.alwaysOnTop = UIFactory.Toggle("AlwaysOnTop", Row("始終顯示在最上層"), skin, RoomSettings.AlwaysOnTop, RoomSettings.SetAlwaysOnTop);
            Right(panel.alwaysOnTop, new Vector2(42f, 42f));

            var recordsRow = Row("占卜紀錄");
            var clear = UIFactory.Button("Clear", recordsRow, skin, "清除全部紀錄", new Vector2(300f, 60f), panel.ClearRecords, fontSize: 28);
            Right(clear, new Vector2(300f, 60f));
            panel.clearLabel = clear.GetComponentInChildren<Text>();

            var tutorial = UIFactory.Button("Tutorial", Row("新手教學"), skin, "重新觀看教學", new Vector2(300f, 60f), () => app?.ReplayTutorial(), fontSize: 28);
            Right(tutorial, new Vector2(300f, 60f));

            var quit = UIFactory.Button("Quit", card, skin, "結束陪伴", new Vector2(260f, 70f), panel.Quit);
            ((RectTransform)quit.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 44f), new Vector2(260f, 70f));
            panel.quitLabel = quit.GetComponentInChildren<Text>();
            var done = UIFactory.Button("Done", card, skin, "完成", new Vector2(260f, 70f), panel.Hide, accent: true);
            ((RectTransform)done.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150f, 44f), new Vector2(260f, 70f));

            panel.InitPanel(app, skin, card);
            return panel;
        }

        /// <summary>Opens the settings and waits until they are closed.</summary>
        public IEnumerator Open()
        {
            Show();
            yield return WaitUntilClosed();
        }

        protected override void OnShown()
        {
            master.SetValueWithoutNotify(RoomSettings.MasterVolume);
            music.SetValueWithoutNotify(RoomSettings.MusicVolume);
            sfx.SetValueWithoutNotify(RoomSettings.SfxVolume);
            textSpeed.SetValueWithoutNotify(RoomSettings.TextSpeed);
            alwaysOnTop.SetIsOnWithoutNotify(RoomSettings.AlwaysOnTop);
            confirmClear = confirmQuit = false;
            clearLabel.text = "清除全部紀錄";
            quitLabel.text = "結束陪伴";
        }

        void ClearRecords()
        {
            if (!confirmClear)
            {
                confirmClear = true;
                clearLabel.text = "再按一次確認";
                return;
            }
            TarotRecordStore.Clear();
            confirmClear = false;
            clearLabel.text = "已清除";
        }

        void Quit()
        {
            if (!confirmQuit)
            {
                confirmQuit = true;
                quitLabel.text = "再按一次離開";
                return;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
