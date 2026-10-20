using UnityEngine;
using RaidCommander.UI;

namespace RaidCommander.Combat
{
    /// <summary>Positions a party bar; health updates belong to HealthBarBinding.</summary>
    [RequireComponent(typeof(HealthBarBinding))]
    [DefaultExecutionOrder(100)]
    public sealed class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.8f, 0f);
        [SerializeField, Min(1f)] private float screenHeight = 10f;
        private RectTransform rect;

        private void Awake() => rect = GetComponent<RectTransform>();

        // Only position/orientation/projection are updated per frame. Health is never polled.
        private void LateUpdate()
        {
            if (followTarget != null)
                transform.position = followTarget.position + worldOffset;
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null || rect == null || viewCamera.pixelHeight <= 0) return;

            transform.rotation = viewCamera.transform.rotation;
            float viewHeight = viewCamera.orthographic
                ? 2f * viewCamera.orthographicSize
                : 2f * Mathf.Abs(Vector3.Dot(transform.position - viewCamera.transform.position,
                    viewCamera.transform.forward)) * Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float scale = viewHeight / viewCamera.pixelHeight * screenHeight / Mathf.Max(1f, rect.rect.height);
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(scale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                scale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                scale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
        }
    }
}
