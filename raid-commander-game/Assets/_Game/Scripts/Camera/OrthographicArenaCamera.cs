using UnityEngine;
using UnityEngine.InputSystem;
using RaidCommander.UI;

namespace RaidCommander.CameraControl
{
    [RequireComponent(typeof(Camera))]
    public sealed class OrthographicArenaCamera : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float panSpeed = 16f;
        [SerializeField, Min(0.01f)] private float panSmoothTime = 0.12f;
        [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.1f;
        [SerializeField, Min(1f)] private float minimumZoom = 8f;
        [SerializeField, Min(1f)] private float maximumZoom = 30f;
        [SerializeField, Min(0f)] private float zoomSensitivity = 0.12f;

        private Camera controlledCamera;
        private InputAction panAction;
        private InputAction zoomAction;
        private Vector3 targetPosition;
        private Vector3 panVelocity;
        private float targetZoom;
        private float zoomVelocity;
        private Vector3 groundForward;
        private Vector3 groundRight;

        private void Awake()
        {
            controlledCamera = GetComponent<Camera>();
            controlledCamera.orthographic = true;

            targetPosition = transform.position;
            targetZoom = Mathf.Clamp(controlledCamera.orthographicSize, minimumZoom, maximumZoom);

            groundForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (groundForward.sqrMagnitude < 0.001f)
                groundForward = Vector3.forward;
            groundRight = Vector3.Cross(Vector3.up, groundForward).normalized;

            panAction = new InputAction("Camera Pan", InputActionType.Value, expectedControlType: "Vector2");
            panAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            panAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            zoomAction = new InputAction("Camera Zoom", InputActionType.Value, "<Mouse>/scroll", expectedControlType: "Vector2");
        }

        private void OnEnable()
        {
            panAction?.Enable();
            zoomAction?.Enable();
        }

        private void OnDisable()
        {
            panAction?.Disable();
            zoomAction?.Disable();
        }

        private void OnDestroy()
        {
            panAction?.Dispose();
            zoomAction?.Dispose();
        }

        private void Update()
        {
            Vector2 input = panAction.ReadValue<Vector2>();
            if (input.sqrMagnitude > 1f)
                input.Normalize();

            targetPosition += (groundRight * input.x + groundForward * input.y) * (panSpeed * Time.unscaledDeltaTime);

            float scroll = zoomAction.ReadValue<Vector2>().y;
            if (Mathf.Abs(scroll) > 0.01f
                && (Mouse.current == null || !UiPointerBlocker.Contains(Mouse.current.position.ReadValue())))
            {
                float wheelSteps = Mathf.Clamp(scroll, -120f, 120f) / 120f;
                targetZoom = Mathf.Clamp(
                    targetZoom * Mathf.Exp(-wheelSteps * zoomSensitivity),
                    minimumZoom,
                    maximumZoom);
            }
        }

        private void LateUpdate()
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref panVelocity,
                panSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            controlledCamera.orthographicSize = Mathf.SmoothDamp(
                controlledCamera.orthographicSize,
                targetZoom,
                ref zoomVelocity,
                zoomSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
        }
    }
}
