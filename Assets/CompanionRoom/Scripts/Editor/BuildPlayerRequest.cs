using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CompanionRoom.EditorTools
{
    /// <summary>
    /// Builds the Windows player when Temp/BuildPlayer.request appears, so a build can be started
    /// from outside while the editor is open. Temp/BuildPlayer.result says building / ok / failed.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildPlayerRequest
    {
        const string Request = "Temp/BuildPlayer.request";
        const string Result = "Temp/BuildPlayer.result";
        const string Refreshed = "Temp/BuildPlayer.refreshed";
        static BuildPlayerRequest() => EditorApplication.update += Poll;

        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Pick up changed scripts first (the editor may be in the background); the build follows
            // on a later update, after any recompile.
            if (!File.Exists(Refreshed))
            {
                File.WriteAllText(Refreshed, "");
                AssetDatabase.Refresh();
                return;
            }
            File.Delete(Refreshed);
            File.Delete(Request);
            File.WriteAllText(Result, "building");
            // The build's log is kept so the result file says why a build failed.
            var errors = new StringBuilder();
            void Collect(string message, string stack, LogType type)
            {
                lock (errors)
                    errors.AppendLine($"[{type}] {message}");
            }
            Application.logMessageReceivedThreaded += Collect;
            try
            {
                bool ok = CompanionRoomTools.BuildPlayer(CompanionRoomTools.PlayerPath);
                File.WriteAllText(Result, (ok ? "ok\n" : "failed\n") + errors);
            }
            catch (Exception e)
            {
                File.WriteAllText(Result, "failed\n" + errors + e);
                Debug.LogException(e);
            }
            finally
            {
                Application.logMessageReceivedThreaded -= Collect;
            }
        }
    }
}
