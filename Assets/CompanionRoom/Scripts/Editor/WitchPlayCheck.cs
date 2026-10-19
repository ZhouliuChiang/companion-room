using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CompanionRoom.EditorTools
{
    /// <summary>
    /// Play-mode check of the pink witch. Create Temp/WitchCheck.request (or use the menu): it enters
    /// Play, lets her stand, sends her to several spots across the room, and records every frame whether
    /// an arm or hand is inside her body/skirt, whether a thigh pokes through the skirt, whether her
    /// footprint touches furniture, and how much her hips and chest actually move. Pictures and
    /// report.txt go to Artifacts/WitchCheck.
    /// </summary>
    [InitializeOnLoad]
    public static class WitchPlayCheck
    {
        const string Request = "Temp/WitchCheck.request";
        const string Flag = "WitchPlayCheck.Active";
        const string Folder = "Artifacts/WitchCheck";

        static IEnumerator run;
        static double resumeAt;
        static readonly StringBuilder report = new();
        static int problems;

        static WitchPlayCheck() => EditorApplication.update += Poll;

        [MenuItem("Tools/兔兔房間/檢查粉色女巫走動與穿模 (Play)", priority = 41)]
        static void Menu() => File.WriteAllText(Request, "check");

        static void Poll()
        {
            if (EditorApplication.isPlaying && SessionState.GetBool(Flag, false)) { Step(); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request))
                return;
            File.Delete(Request);
            Directory.CreateDirectory(Folder);
            foreach (var old in Directory.GetFiles(Folder, "*.png")) File.Delete(old);
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static void Step()
        {
            try
            {
                if (run == null)
                {
                    report.Clear(); problems = 0;
                    Application.logMessageReceived += OnLog;
                    run = Run();
                    resumeAt = EditorApplication.timeSinceStartup + 1.5;
                }
                if (EditorApplication.timeSinceStartup < resumeAt) return;
                if (!run.MoveNext()) { End(); return; }
                if (run.Current is float seconds) resumeAt = EditorApplication.timeSinceStartup + seconds;
            }
            catch (Exception e) { Problem("exception: " + e); End(); }
        }

        static void End()
        {
            Application.logMessageReceived -= OnLog;
            report.AppendLine(problems == 0 ? "RESULT: PASS" : $"RESULT: {problems} problem(s)");
            File.WriteAllText(Path.Combine(Folder, "report.txt"), report.ToString());
            run = null;
            SessionState.SetBool(Flag, false);
            EditorApplication.isPlaying = false;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) Problem($"[{type}] {message}");
            else if (message.StartsWith("[Witch")) report.AppendLine("log: " + message);
        }

        static void Problem(string message) { problems++; report.AppendLine("PROBLEM: " + message); }
        static void Log(string message) => report.AppendLine(message);

        class Stats
        {
            public float arm = float.MinValue, leg = float.MinValue, hipsMin = float.MaxValue, hipsMax = float.MinValue, chestTurnMax, footMin = float.MaxValue;
            public int frames, footprintHits;
        }

        static void Sample(Stats s, WitchLocomotion walker, WitchBodyMotion body, WitchSkirt skirt, Transform hips, Transform chest, Transform foot, Transform foot2)
        {
            s.frames++;
            s.arm = Mathf.Max(s.arm, body.ArmIntrusion());
            s.leg = Mathf.Max(s.leg, skirt.LegIntrusion());
            float h = hips.position.y - walker.transform.position.y;
            s.hipsMin = Mathf.Min(s.hipsMin, h); s.hipsMax = Mathf.Max(s.hipsMax, h);
            s.chestTurnMax = Mathf.Max(s.chestTurnMax, Vector3.Angle(Vector3.ProjectOnPlane(chest.forward, Vector3.up), Vector3.ProjectOnPlane(hips.forward, Vector3.up)));
            s.footMin = Mathf.Min(s.footMin, foot.position.y - walker.floor, foot2.position.y - walker.floor);
            if (!walker.Clear(walker.transform.position)) s.footprintHits++;
        }

        static string Describe(Stats s) =>
            $"frames {s.frames}: arm intrusion max {s.arm * 1000f:0.0} mm, thigh vs skirt max {s.leg * 1000f:0.0} mm, " +
            $"hips height range {(s.hipsMax - s.hipsMin) * 1000f:0.0} mm, chest-vs-pelvis turn max {s.chestTurnMax:0.0}°, " +
            $"lowest ankle above floor {s.footMin * 1000f:0.0} mm, footprint collisions {s.footprintHits}";

        static IEnumerator Run()
        {
            var walker = Object.FindFirstObjectByType<WitchLocomotion>();
            if (walker == null) { Problem("no WitchLocomotion in the scene"); yield break; }
            var body = walker.GetComponent<WitchBodyMotion>();
            var skirt = walker.GetComponent<WitchSkirt>();
            if (body == null || skirt == null) { Problem("WitchBodyMotion / WitchSkirt not attached"); yield break; }
            Log($"skirt rig ready: {skirt.Ready}  legLen {skirt.LegLength:0.000} legR {skirt.LegRadius:0.000} armR {skirt.ArmRadius:0.000} handR {skirt.HandRadius:0.000} (hips units, scale {skirt.Hips?.lossyScale.x:0.0000})");
            if (!skirt.Ready) { Problem("skirt rig not built"); yield break; }
            Transform Find(string n) { foreach (var t in walker.GetComponentsInChildren<Transform>()) if (t.name == n) return t; return null; }
            Transform hips = Find("Hips"), chest = Find("UpperChest"), lf = Find("Left_Foot"), rf = Find("Right_Foot"), lt = Find("Left_Toes");
            var toes = Vector3.ProjectOnPlane(lt.position - lf.position, Vector3.up).normalized;
            float facing = Vector3.Angle(toes, walker.transform.forward);
            Log($"model faces root forward within {facing:0.0}°");
            if (facing > 35f) Problem("model does not face the walking direction");

            // Standing: breathing.
            var idle = new Stats();
            for (int i = 0; i < 120; i++) { Sample(idle, walker, body, skirt, hips, chest, lf, rf); yield return 0.03f; }
            Log("idle " + Describe(idle));
            CaptureAround(walker, "idle");

            // Walking to spots across the room.
            var spots = new List<Vector3>();
            var b = walker.roomBounds;
            for (float x = b.min.x + .6f; x < b.max.x - .5f; x += .45f)
                for (float z = b.min.z + .6f; z < b.max.z - .5f; z += .45f)
                    spots.Add(new Vector3(x, walker.floor, z));
            spots.Sort((p, q) => (q - walker.transform.position).sqrMagnitude.CompareTo((p - walker.transform.position).sqrMagnitude));
            var walk = new Stats();
            int trips = 0, refused = 0;
            foreach (var spot in spots)
            {
                if (trips >= 4) break;
                if ((spot - walker.transform.position).magnitude < 1f) continue;
                if (!walker.GoTo(spot)) { refused++; continue; }
                trips++;
                var start = walker.transform.position; float started = Time.time; bool pictured = false;
                while (walker.IsWalking && Time.time - started < 25f)
                {
                    Sample(walk, walker, body, skirt, hips, chest, lf, rf);
                    if (!pictured && Time.time - started > 1.6f) { CaptureAround(walker, "walk" + trips); pictured = true; }
                    yield return 0f;
                }
                Log($"trip {trips}: {start} → {spot}, arrived at {walker.transform.position} in {Time.time - started:0.0}s, still walking: {walker.IsWalking}");
                if (walker.IsWalking) { Problem("trip did not finish"); walker.Stop(); }
                yield return 1.2f; // arrive, settle, turn to the camera
            }
            Log($"walk {Describe(walk)}; destinations refused before 4 trips: {refused}");
            if (trips == 0) Problem("no reachable destination found");

            // Clicking on furniture: snapped to a free spot nearby, or refused.
            foreach (var item in RoomItem.All)
            {
                var c = item.GetComponentInChildren<Collider>();
                if (c == null) continue;
                var p = c.bounds.center; p.y = walker.floor;
                bool went = false; Vector3 to = default;
                void Seen(Vector3 d, bool ok) { went = ok; to = d; }
                walker.DestinationChosen += Seen;
                walker.GoTo(p);
                walker.DestinationChosen -= Seen;
                walker.Stop();
                Log($"click on {item.name}: {(went ? "walks to " + to : "refused")}{(went ? $" ({(to - p).magnitude:0.00} m from the click)" : "")}");
            }

            const float tolerance = .0015f;
            if (idle.arm > tolerance || walk.arm > tolerance) Problem("an arm or hand goes into the body/skirt");
            if (idle.leg > tolerance || walk.leg > tolerance) Problem("a thigh pokes through the skirt");
            if (idle.footprintHits + walk.footprintHits > 0) Problem("her footprint touched furniture or a wall");
            if (walk.frames > 0 && walk.hipsMax - walk.hipsMin < .004f) Problem("hips barely move while walking");
            if (idle.hipsMax - idle.hipsMin > .01f) Problem("hips move too much while standing");
            if (Mathf.Min(idle.footMin, walk.footMin) < -.005f) Problem("an ankle sank below the floor");
        }

        /// <summary>Close-up pictures from the front and the side.</summary>
        static void CaptureAround(WitchLocomotion walker, string name)
        {
            var go = new GameObject("WitchCheckCamera");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 32f; cam.nearClipPlane = .05f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.93f, .9f, .92f);
            var t = walker.transform; var target = t.position + Vector3.up * .55f;
            foreach (var (label, dir) in new[] { ("front", t.forward), ("side", t.right), ("back34", -t.forward + t.right * .8f) })
            {
                go.transform.position = target + dir.normalized * 2.3f + Vector3.up * .15f;
                go.transform.LookAt(target);
                Capture(cam, $"{name}_{label}");
            }
            Object.DestroyImmediate(go);
            Capture(Camera.main, name + "_room");
        }

        static void Capture(Camera cam, string name)
        {
            if (cam == null) return;
            const int width = 900, height = 900;
            var rt = new RenderTexture(width, height, 24);
            var prevTarget = cam.targetTexture; var prevActive = RenderTexture.active;
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), image.EncodeToPNG());
            cam.targetTexture = prevTarget; RenderTexture.active = prevActive;
            rt.Release(); Object.DestroyImmediate(image); Object.DestroyImmediate(rt);
        }
    }
}
