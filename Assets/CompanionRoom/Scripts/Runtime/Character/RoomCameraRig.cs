using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace CompanionRoom
{
    /// <summary>
    /// The room camera: a fixed diorama view that drifts slightly with the pointer, and eases in
    /// towards an item while it is in use (same viewing angle, just closer).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RoomCameraRig : MonoBehaviour
    {
        [Tooltip("The point the resting view looks at.")]
        [SerializeField] Vector3 lookAt = new(-0.3f, 0.8f, 0.3f);
        [Tooltip("How far the view drifts with the pointer, in metres.")]
        [SerializeField] float parallax = 0f;
        [Tooltip("Maximum pointer orbit in degrees: horizontal / vertical.")]
        [SerializeField] Vector2 pointerOrbit = new(5f, 3f);
        [Tooltip("How much closer the camera gets at full focus (0.5 = halfway).")]
        [SerializeField, Range(0f, 0.9f)] float maxDolly = 0.5f;
        [SerializeField] float smoothTime = 0.5f;

        Vector3 homePosition;
        Vector3 direction;
        float distance;
        Vector3 focusPoint;
        float focusAmount;

        Vector3 currentLook;
        Vector3 lookVelocity;
        float currentDolly;
        float dollyVelocity;
        Vector2 drift;
        Vector2 driftVelocity;
        Vector2 dragOrigin, dragStart, dragTarget;
        bool dragging;

        public Vector3 LookAt => lookAt;

        public void Configure(Vector3 look) => lookAt = look;

        void Start()
        {
            // Apply the requested limits to existing scenes as well as newly built rooms.
            pointerOrbit = new Vector2(5f, 3f);
            parallax = 0f;
            homePosition = transform.position;
            var offset = lookAt - homePosition;
            distance = offset.magnitude;
            direction = offset / Mathf.Max(0.001f, distance);
            currentLook = lookAt;
        }

        public void Focus(Vector3 point, float amount)
        {
            focusPoint = point;
            focusAmount = Mathf.Clamp01(amount);
        }

        public void Release() => focusAmount = 0f;

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            var targetLook = Vector3.Lerp(lookAt, focusPoint, focusAmount);
            currentLook = Vector3.SmoothDamp(currentLook, targetLook, ref lookVelocity, smoothTime, Mathf.Infinity, dt);
            currentDolly = Mathf.SmoothDamp(currentDolly, focusAmount * maxDolly, ref dollyVelocity, smoothTime, Mathf.Infinity, dt);

            var mouse = Mouse.current;
            var app = CompanionRoomApp.Instance;
            if (mouse != null && Application.isFocused && focusAmount <= 0f
                && (app == null || (!app.IsBusy && !app.UI.AnyWindowOpen))
                && !RoomWindowFrame.PointerOnFrame && Screen.width > 0 && Screen.height > 0)
            {
                var p = mouse.position.ReadValue();
                bool inside = p.x >= 0f && p.y >= 0f && p.x <= Screen.width && p.y <= Screen.height;
                if (mouse.leftButton.wasPressedThisFrame && inside
                    && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                {
                    dragging = true;
                    dragOrigin = p;
                    dragStart = dragTarget;
                }
                if (dragging && mouse.leftButton.isPressed && inside)
                {
                    var delta = p - dragOrigin;
                    if (delta.magnitude > 8f)
                        dragTarget = new Vector2(
                            Mathf.Clamp(dragStart.x + delta.x / (Screen.width * 0.25f), -1f, 1f),
                            Mathf.Clamp(dragStart.y + delta.y / (Screen.height * 0.25f), -1f, 1f));
                }
                if (!mouse.leftButton.isPressed || !inside) dragging = false;
            }
            else dragging = false;
            if (!dragging) dragTarget = Vector2.zero;
            drift = Vector2.SmoothDamp(drift, dragTarget, ref driftVelocity,
                dragging ? 0.15f : 0.9f, Mathf.Infinity, dt);

            var right = Vector3.Cross(Vector3.up, direction).normalized;
            var up = Vector3.Cross(direction, right);
            var orbit = Quaternion.AngleAxis(drift.x * pointerOrbit.x, Vector3.up)
                * Quaternion.AngleAxis(-drift.y * pointerOrbit.y, right);
            var position = currentLook - orbit * direction * distance * (1f - currentDolly)
                + (right * drift.x + up * drift.y * 0.5f) * parallax;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(currentLook - position, Vector3.up));
        }
    }
}
