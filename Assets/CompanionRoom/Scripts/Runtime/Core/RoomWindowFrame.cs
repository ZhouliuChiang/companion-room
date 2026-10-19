using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

namespace CompanionRoom
{
    /// <summary>
    /// The exported room sits on the desktop like a desktop pet. The window covers the whole screen
    /// (without the taskbar) and draws nothing but the room and the UI; clicks on empty parts fall
    /// through to whatever is behind. Dragging a wall (anywhere on the room that isn't clickable) moves
    /// the room, dragging one of its six outer corners scales it. Only the room moves, by shifting and
    /// zooming the camera, so the dialogue box and the tarot table stay at the bottom middle of the
    /// screen. No frame or markers are drawn. Where the room was and how big are remembered.
    /// Also keeps the window above others while <see cref="RoomSettings.AlwaysOnTop"/> is on.
    /// Only the Windows player does this; in the editor nothing changes.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class RoomWindowFrame : MonoBehaviour
    {
        const float CornerReach = 36f;    // pixels around a corner that grab it
        const float OpaqueAlpha = 0.1f;
        const float MinScale = 0.25f, MaxScale = 1.6f, DefaultScale = 0.62f;

        /// <summary>The pointer is on one of the room's corners, or the room is being dragged, so room clicks and camera drags wait.</summary>
        public static bool PointerOnFrame
        {
            get
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return moving || resizing || hoveredCorner >= 0 || onEdge;
#else
                return false;
#endif
            }
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential)]
        struct Point { public int x, y; }
        [StructLayout(LayoutKind.Sequential)]
        struct WinRect { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)]
        struct Margins { public int left, right, top, bottom; }
        [StructLayout(LayoutKind.Sequential)]
        struct MonitorInfo { public int size; public WinRect monitor, work; public uint flags; }
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr window, ref Point point);
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out WinRect rect);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr window, uint key, byte alpha, uint flags);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        const int GwlStyle = -16, GwlExStyle = -20;
        const long WsPopup = 0x80000000L, WsCaption = 0x00C00000L, WsSysMenu = 0x00080000L;
        const long WsThickFrame = 0x00040000L, WsMinimizeBox = 0x00020000L, WsMaximizeBox = 0x00010000L;
        const long WsExLayered = 0x00080000L, WsExTransparent = 0x00000020L;
        const uint SwpNoSize = 0x0001, SwpNoMove = 0x0002, SwpNoZOrder = 0x0004, SwpNoActivate = 0x0010, SwpFrameChanged = 0x0020;

        static IntPtr window;
        static bool clickThrough;
        static bool hidden;
        int framesDrawn;
        static bool moving;
        static bool resizing;
        static int hoveredCorner = -1;
        static bool onEdge;

        Camera roomCamera;
        RoomInteractor interactor;
        float baseOrthoSize;
        bool hasRoom;
        readonly Vector3[] boxCorners = new Vector3[8];   // world space, top corners on the real wall tops
        readonly List<Vector2> projected = new();
        readonly List<Vector2> corners = new();     // the room's outline corners, window pixels from the top left
        readonly List<RaycastResult> uiHits = new();
        Texture2D probe;

        // Where the room is: its offset from the screen centre (in screen heights) and its scale.
        Vector2 roomOffset;
        float roomScale = DefaultScale;
        Vector3 riggedPosition, placedPosition;

        Point cursor;                // screen pixels
        Vector2 pointer;             // window pixels from the top left
        bool pointerInside;
        bool overRoom;               // something is drawn under the pointer
        bool wasDown;
        bool? topmost;
        float nextStyleCheck;

        Vector2 dragCursor, dragOffset;
        float dragScale;
        Vector2 resizeFrom, resizeAnchor;

        // As early as the window exists: off with the title bar and onto the whole screen, so the
        // startup doesn't show a normal window first.
        // The window also stays fully see-through until the room has been drawn, instead of showing
        // a blank white rectangle while the scene loads.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void PrepareEarly()
        {
            if (!FindWindow())
                return;
            SetWindowLongPtr(window, GwlExStyle, new IntPtr(GetWindowLongPtr(window, GwlExStyle).ToInt64() | WsExLayered));
            SetLayeredWindowAttributes(window, 0, 0, 2);   // LWA_ALPHA, fully transparent
            hidden = true;
            KeepPetStyle(force: true);
        }

        // Once frames are coming: dropping the layered style clears the hiding, and putting it back
        // (without attributes) keeps the per-pixel see-through and click-through working.
        static void Reveal()
        {
            hidden = false;
            long ex = GetWindowLongPtr(window, GwlExStyle).ToInt64();
            SetWindowLongPtr(window, GwlExStyle, new IntPtr(ex & ~WsExLayered));
            SetWindowLongPtr(window, GwlExStyle, new IntPtr(ex | WsExLayered));
        }

        // Next time the window starts small, so even the moment before the code above runs shows
        // next to nothing (Unity reopens at the last size, which would be the whole screen).
        void OnApplicationQuit()
        {
            PlayerPrefs.SetInt("Screenmanager Resolution Width", 64);
            PlayerPrefs.SetInt("Screenmanager Resolution Height", 36);
            PlayerPrefs.Save();
        }

        void Awake()
        {
            // HDR only keeps an alpha channel at 64-bit precision, and the empty background needs it.
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
                pipeline.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits;
            probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            roomOffset = new Vector2(RoomSettings.GetInt("RoomX", 0), RoomSettings.GetInt("RoomY", -800)) / 10000f;
            roomScale = Mathf.Clamp(RoomSettings.GetInt("RoomScale", Mathf.RoundToInt(DefaultScale * 10000f)) / 10000f, MinScale, MaxScale);
        }

        void Start()
        {
            roomCamera = Camera.main;
            if (roomCamera != null)
                baseOrthoSize = roomCamera.orthographicSize;
            interactor = FindAnyObjectByType<RoomInteractor>();
            foreach (var asset in FindObjectsByType<ImportedRoomAsset>(FindObjectsSortMode.None))
            {
                if (asset.assetId != "Room")
                    continue;
                // The shell's collider matches the walls; the renderers also reach the sky outside the window.
                var colliders = asset.GetComponentsInChildren<Collider>();
                var bounds = new Bounds();
                foreach (var b in colliders.Length > 0 ? Array.ConvertAll(colliders, c => c.bounds) : Array.ConvertAll(asset.GetComponentsInChildren<Renderer>(), r => r.bounds))
                {
                    if (!hasRoom) bounds = b;
                    else bounds.Encapsulate(b);
                    hasRoom = true;
                }
                if (hasRoom)
                    FindRoomCorners(bounds, colliders);
            }
            RenderPipelineManager.beginCameraRendering += PlaceRoom;
            StartCoroutine(SamplePointer());
        }

        void OnDestroy() => RenderPipelineManager.beginCameraRendering -= PlaceRoom;

        // The room box's corners. The walls are gabled, so a box's top corners float above the wall
        // ends: each one is dropped onto the room's real top surface below it.
        void FindRoomCorners(Bounds bounds, Collider[] colliders)
        {
            // A few spots just inside each corner, since trim and sills can stick out past the wall.
            float[] insets = { 0.08f, 0.2f, 0.4f };
            Vector3 min = bounds.min, max = bounds.max, center = bounds.center;
            var missing = new List<int>();
            float wallEnd = float.PositiveInfinity;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                boxCorners[i] = corner;
                if ((i & 2) == 0)
                    continue;
                float top = float.NegativeInfinity;
                foreach (float ix in insets)
                    foreach (float iz in insets)
                    {
                        var down = new Ray(new Vector3(Mathf.MoveTowards(corner.x, center.x, ix), max.y + 1f, Mathf.MoveTowards(corner.z, center.z, iz)), Vector3.down);
                        foreach (var collider in colliders)
                            if (collider.Raycast(down, out var hit, bounds.size.y + 2f))
                                top = Mathf.Max(top, hit.point.y);
                    }
                // Only the floor or a sill below: the open front corner, or a wall end the rays missed.
                if (top < center.y)
                {
                    missing.Add(i);
                    continue;
                }
                boxCorners[i].y = top;
                wallEnd = Mathf.Min(wallEnd, top);
            }
            // Those take the lower wall-end height from the others (the open front corner then lands
            // inside the outline, where it doesn't count).
            if (!float.IsInfinity(wallEnd))
                foreach (int i in missing)
                    boxCorners[i].y = wallEnd;
        }

        void Update()
        {
            if (window == IntPtr.Zero && !FindWindow())
                return;
            if (Screen.fullScreen)
            {
                SetClickThrough(false);
                return;
            }
            if (Time.unscaledTime >= nextStyleCheck)
            {
                nextStyleCheck = Time.unscaledTime + 1f;
                KeepPetStyle(force: false);
            }
            KeepOnTop();
            ReadPointer();
            UpdateCorners();

            bool down = ButtonDown();
            bool pressed = down && !wasDown;
            wasDown = down;
            if (moving || resizing)
            {
                if (!down) EndDrag();
                else if (moving) Move();
                else Resize();
                return;
            }
            hoveredCorner = pointerInside ? CornerAt(pointer) : -1;
            // The edge only counts where nothing clickable (UI, furniture) is under the pointer.
            onEdge = hoveredCorner < 0 && pointerInside && OnEdge(pointer) && !OverUi()
                && (interactor == null || interactor.PickAt(UnityPointer) == null);
            if (!pressed || !pointerInside || !UnderPointer())
                return;
            dragCursor = new Vector2(cursor.x, cursor.y);
            dragOffset = roomOffset;
            dragScale = roomScale;
            if (hoveredCorner >= 0)
                BeginResize();
            else if (onEdge)
                moving = true;
        }

        void LateUpdate()
        {
            // Lighting tints the background per time of day; here it stays empty so the desktop shows.
            if (roomCamera == null || Screen.fullScreen)
                return;
            roomCamera.clearFlags = CameraClearFlags.SolidColor;
            roomCamera.backgroundColor = Color.clear;
        }

        // Right before the room camera draws: shift and zoom it so the room sits where it was put.
        // The camera rig sets its own position every frame; that is kept as the base.
        void PlaceRoom(ScriptableRenderContext context, Camera camera)
        {
            if (camera != roomCamera || Screen.fullScreen || Screen.height <= 0)
                return;
            var t = camera.transform;
            if (t.position != placedPosition)
                riggedPosition = t.position;
            camera.orthographicSize = baseOrthoSize / roomScale;
            float unitsPerScreenHeight = camera.orthographicSize * 2f;
            placedPosition = riggedPosition - (t.right * roomOffset.x + t.up * roomOffset.y) * unitsPerScreenHeight;
            t.position = placedPosition;
            // Stacked cameras (the record journal's book) are laid out against the unshifted view:
            // drawn from the room's place while the book sits on the shelf, from the middle once it floats.
            foreach (var overlay in camera.GetUniversalAdditionalCameraData().cameraStack)
                if (overlay != null)
                    overlay.transform.position = Vector3.Lerp(placedPosition, riggedPosition, Journal.RecordJournal.Lift);
        }

        static bool FindWindow()
        {
            window = GetActiveWindow();
            if (window == IntPtr.Zero)
                window = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            return window != IntPtr.Zero;
        }

        Vector2 UnityPointer => new(pointer.x, Screen.height - pointer.y);

        void ReadPointer()
        {
            pointerInside = false;
            if (!GetCursorPos(out cursor))
                return;
            var client = cursor;
            if (!ScreenToClient(window, ref client))
                return;
            pointer = new Vector2(client.x, client.y);
            pointerInside = client.x >= 0 && client.y >= 0 && client.x < Screen.width && client.y < Screen.height;
        }

        // The window really is under the pointer (not another window above it).
        static bool UnderPointer(Point at) => GetAncestor(WindowFromPoint(at), 2) == window;   // GA_ROOT
        bool UnderPointer() => UnderPointer(cursor);

        static bool ButtonDown()
        {
            int button = GetSystemMetrics(23) != 0 ? 0x02 : 0x01;   // SM_SWAPBUTTON: VK_RBUTTON / VK_LBUTTON
            return (GetAsyncKeyState(button) & 0x8000) != 0;
        }

        bool OverUi()
        {
            var events = EventSystem.current;
            if (events == null)
                return false;
            uiHits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = UnityPointer }, uiHits);
            return uiHits.Count > 0;
        }

        // Reads the pixel under the pointer once the frame is drawn: empty pixels let clicks through.
        IEnumerator SamplePointer()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                if (window == IntPtr.Zero || Screen.fullScreen)
                    continue;
                if (hidden && ++framesDrawn >= 3)
                    Reveal();
                overRoom = false;
                if (pointerInside)
                {
                    float x = Mathf.Clamp(pointer.x, 0f, Screen.width - 1);
                    float y = Mathf.Clamp(Screen.height - 1 - pointer.y, 0f, Screen.height - 1);
                    probe.ReadPixels(new Rect(x, y, 1f, 1f), 0, 0, false);
                    overRoom = probe.GetPixel(0, 0).a > OpaqueAlpha;
                }
                SetClickThrough(!overRoom && !PointerOnFrame);
            }
        }

        static void SetClickThrough(bool on)
        {
            if (clickThrough == on || window == IntPtr.Zero)
                return;
            clickThrough = on;
            long ex = GetWindowLongPtr(window, GwlExStyle).ToInt64();
            ex = on ? ex | WsExTransparent : ex & ~WsExTransparent;
            SetWindowLongPtr(window, GwlExStyle, new IntPtr(ex | WsExLayered));
        }

        // No title bar or border, the rendered alpha shows the desktop through, and the window fills
        // its screen's work area. Unity puts its own frame back after a full-screen switch, and the
        // work area changes with the taskbar or displays, so this is checked again now and then.
        static void KeepPetStyle(bool force)
        {
            var info = new MonitorInfo { size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(MonitorFromWindow(window, 2), ref info))   // MONITOR_DEFAULTTONEAREST
                return;
            var work = info.work;
            long style = GetWindowLongPtr(window, GwlStyle).ToInt64();
            bool framed = (style & (WsCaption | WsThickFrame)) != 0;
            GetWindowRect(window, out var now);
            bool placed = now.left == work.left && now.top == work.top && now.right == work.right && now.bottom == work.bottom;
            if (!force && !framed && placed)
                return;

            style = (style & ~(WsCaption | WsThickFrame | WsMaximizeBox)) | WsPopup | WsSysMenu | WsMinimizeBox;
            SetWindowLongPtr(window, GwlStyle, new IntPtr(style));
            long ex = GetWindowLongPtr(window, GwlExStyle).ToInt64() | WsExLayered;
            SetWindowLongPtr(window, GwlExStyle, new IntPtr(clickThrough ? ex | WsExTransparent : ex & ~WsExTransparent));
            var margins = new Margins { left = -1, right = -1, top = -1, bottom = -1 };
            DwmExtendFrameIntoClientArea(window, ref margins);
            // Windows 11 would otherwise outline and round the (invisible) window rectangle.
            int noBorder = unchecked((int)0xFFFFFFFE), squareCorners = 1;
            DwmSetWindowAttribute(window, 34, ref noBorder, sizeof(int));        // DWMWA_BORDER_COLOR: none
            DwmSetWindowAttribute(window, 33, ref squareCorners, sizeof(int));   // DWMWA_WINDOW_CORNER_PREFERENCE: don't round
            SetWindowPos(window, IntPtr.Zero, work.left, work.top, work.right - work.left, work.bottom - work.top, SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        }

        // Applies the setting once the window handle is known, and again whenever it is flipped.
        void KeepOnTop()
        {
            bool wanted = RoomSettings.AlwaysOnTop;
            if (topmost == wanted)
                return;
            // HWND_TOPMOST / HWND_NOTOPMOST, without moving, sizing or activating.
            SetWindowPos(window, new IntPtr(wanted ? -1 : -2), 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate);
            topmost = wanted;
        }

        void Move()
        {
            var moved = (new Vector2(cursor.x, cursor.y) - dragCursor) / Screen.height;
            roomOffset = ClampOffset(dragOffset + new Vector2(moved.x, -moved.y));
        }

        void EndDrag()
        {
            moving = resizing = false;
            if (Mouse.current != null)
                InputSystem.ResetDevice(Mouse.current);
            RoomSettings.SetInt("RoomX", Mathf.RoundToInt(roomOffset.x * 10000f));
            RoomSettings.SetInt("RoomY", Mathf.RoundToInt(roomOffset.y * 10000f));
            RoomSettings.SetInt("RoomScale", Mathf.RoundToInt(roomScale * 10000f));
        }

        // The room's centre stays on screen.
        static Vector2 ClampOffset(Vector2 offset)
        {
            float halfWidth = 0.5f * Screen.width / Mathf.Max(1, Screen.height);
            return new Vector2(Mathf.Clamp(offset.x, -halfWidth, halfWidth), Mathf.Clamp(offset.y, -0.5f, 0.5f));
        }

        // The room box's corners on screen; their outline is the hexagon the room is drawn in.
        void UpdateCorners()
        {
            corners.Clear();
            if (!hasRoom || roomCamera == null)
                return;
            projected.Clear();
            foreach (var corner in boxCorners)
            {
                var p = roomCamera.WorldToScreenPoint(corner);
                projected.Add(new Vector2(p.x, Screen.height - p.y));
            }
            Outline(projected, corners);
        }

        // Convex hull (monotone chain), so only the outline's corners are kept.
        static void Outline(List<Vector2> points, List<Vector2> hull)
        {
            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                for (int k = 0; k < points.Count; k++)
                {
                    var p = points[pass == 0 ? k : points.Count - 1 - k];
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0.5f)
                        hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);
            }
        }

        float RoomHeight()
        {
            float top = float.MaxValue, bottom = float.MinValue;
            foreach (var c in corners)
            {
                top = Mathf.Min(top, c.y);
                bottom = Mathf.Max(bottom, c.y);
            }
            return Mathf.Max(1f, bottom - top);
        }

        // The corners that scale the room: the top, the bottom, and the lower corner on each side
        // (the upper wall ends belong to the edge that moves it).
        int CornerAt(Vector2 position)
        {
            if (corners.Count < 4)
                return -1;
            int top = 0, bottom = 0;
            for (int i = 1; i < corners.Count; i++)
            {
                if (corners[i].y < corners[top].y) top = i;
                if (corners[i].y > corners[bottom].y) bottom = i;
            }
            float midX = (corners[top].x + corners[bottom].x) * 0.5f;
            int left = -1, right = -1;
            for (int i = 0; i < corners.Count; i++)
            {
                if (i == top || i == bottom) continue;
                if (corners[i].x < midX) { if (left < 0 || corners[i].y > corners[left].y) left = i; }
                else if (right < 0 || corners[i].y > corners[right].y) right = i;
            }
            float reach = Mathf.Max(CornerReach, RoomHeight() * 0.07f);
            int best = -1;
            float bestDistance = reach * reach;
            foreach (int i in new[] { top, bottom, left, right })
            {
                if (i < 0) continue;
                float d = (corners[i] - position).sqrMagnitude;
                if (d <= bestDistance)
                {
                    best = i;
                    bestDistance = d;
                }
            }
            return best;
        }

        // The band along the room's outline that moves it: the outer part of the walls and a little
        // outside them. The inside of the room is left to its furniture.
        bool OnEdge(Vector2 position)
        {
            if (corners.Count < 3)
                return false;
            float h = RoomHeight();
            float inner = Mathf.Max(24f, h * 0.09f), outer = Mathf.Max(14f, h * 0.035f);
            float distance = float.MaxValue, sign = 0f;
            bool inside = true;
            for (int i = 0; i < corners.Count; i++)
            {
                Vector2 a = corners[i], b = corners[(i + 1) % corners.Count];
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(position - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                distance = Mathf.Min(distance, (a + ab * t - position).magnitude);
                float cross = ab.x * (position.y - a.y) - ab.y * (position.x - a.x);
                if (sign == 0f) sign = Mathf.Sign(cross);
                else if (Mathf.Sign(cross) != sign) inside = false;
            }
            return distance <= (inside ? inner : outer);
        }

        // The corner being dragged scales the room; the corner across from it stays put on screen.
        void BeginResize()
        {
            var from = corners[hoveredCorner];
            var anchor = from;
            foreach (var c in corners)
                if ((c - from).sqrMagnitude > (anchor - from).sqrMagnitude)
                    anchor = c;
            resizeFrom = from;
            resizeAnchor = anchor;
            resizing = true;
        }

        void Resize()
        {
            var axis = resizeFrom - resizeAnchor;
            if (axis.sqrMagnitude < 1f)
                return;
            float ratio = Vector2.Dot(pointer - resizeAnchor, axis) / axis.sqrMagnitude;
            roomScale = Mathf.Clamp(dragScale * ratio, MinScale, MaxScale);
            ratio = roomScale / dragScale;
            // Keep the anchor corner where it is: the room grows away from it.
            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + new Vector2(dragOffset.x, -dragOffset.y) * Screen.height;
            var newCentre = resizeAnchor + (centre - resizeAnchor) * ratio;
            var offset = (newCentre - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)) / Screen.height;
            roomOffset = ClampOffset(new Vector2(offset.x, -offset.y));
        }
#endif
    }
}
