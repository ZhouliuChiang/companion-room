using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CompanionRoom
{
    /// <summary>
    /// Mouse play in the room: the item under the pointer gets a thin outline (no text, to keep the
    /// view clean), and a click on it starts its interaction. Nothing reacts while an interaction or
    /// window is open, over UI, or when the press turns into a drag.
    /// </summary>
    public class RoomInteractor : MonoBehaviour
    {
        [SerializeField] Camera roomCamera;
        [SerializeField] LayerMask clickMask = ~0;
        [Tooltip("A press that moves farther than this many pixels is a drag, not a click.")]
        [SerializeField] float clickSlop = 8f;

        readonly RaycastHit[] hits = new RaycastHit[16];
        static readonly Comparer<RaycastHit> ByDistance = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        CompanionRoomApp app;
        RoomItem hovered;
        RoomItem pressed;
        Vector2 pressPosition;
        float pressTravel;
        bool floorPress;

        public RoomItem Hovered => hovered;

        public void Configure(Camera cam) => roomCamera = cam;

        void Start()
        {
            app = CompanionRoomApp.Instance;
            if (roomCamera == null)
                roomCamera = Camera.main;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || roomCamera == null || app == null)
                return;

            bool blocked = app.IsBusy || app.UI.AnyWindowOpen || RoomWindowFrame.PointerOnFrame;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2 position = mouse.position.ReadValue();

            if (blocked)
            {
                pressed = null; floorPress = false;
                SetHovered(null);
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressPosition = position;
                pressTravel = 0f;
                var picked = overUi ? null : PickAt(position);
                pressed = Allowed(picked);
                floorPress = !overUi && picked == null;
            }
            if ((pressed != null || floorPress) && mouse.leftButton.isPressed)
                pressTravel = Mathf.Max(pressTravel, (position - pressPosition).magnitude);

            var target = overUi ? null : Allowed(PickAt(position));
            SetHovered(target);

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (pressed != null && pressed == target && pressTravel <= clickSlop)
                    Activate(target);
                else if (floorPress && !overUi && target == null && pressTravel <= clickSlop)
                {
                    var walker = FindFirstObjectByType<WitchLocomotion>();
                    if (walker != null)
                    {
                        var ray = roomCamera.ScreenPointToRay(position);
                        var plane = new Plane(Vector3.up, new Vector3(0, walker.floor, 0));
                        if (plane.Raycast(ray, out float distance))
                        {
                            var point = ray.GetPoint(distance);
                            // A visible object in front of the floor consumes the click.
                            bool occluded = false;
                            foreach (var hit in Physics.RaycastAll(ray, distance - .03f, clickMask, QueryTriggerInteraction.Collide))
                                if (!hit.transform.IsChildOf(walker.transform) && hit.collider.bounds.max.y > walker.floor + .06f) occluded = true;
                            if (!occluded) walker.GoTo(point);
                        }
                    }
                }
                floorPress = false;
                pressed = null;
            }
        }

        /// <summary>The item under a screen position, if any.</summary>
        public RoomItem PickAt(Vector2 screenPosition)
        {
            if (roomCamera == null)
                return null;
            int count = Physics.RaycastNonAlloc(roomCamera.ScreenPointToRay(screenPosition), hits, 100f, clickMask, QueryTriggerInteraction.Collide);
            if (count == 0)
                return null;
            Array.Sort(hits, 0, count, ByDistance);
            for (int i = 0; i < count; i++)
            {
                var item = hits[i].collider.GetComponentInParent<RoomItem>();
                if (item != null && item.isActiveAndEnabled)
                    return item;
            }
            return null;
        }

        // During the tutorial only the item it is pointing at can be used.
        RoomItem Allowed(RoomItem item) => item != null && app.UI.Tutorial.Allows(item) ? item : null;

        public bool Activate(RoomItem item)
        {
            SetHovered(null);
            return app != null && app.Interact(item);
        }

        void SetHovered(RoomItem item)
        {
            if (hovered == item)
                return;
            if (hovered != null)
                hovered.SetHovered(false);
            hovered = item;
            if (hovered != null)
                hovered.SetHovered(true);
        }
    }
}
