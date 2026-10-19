using UnityEngine;
using UnityEngine.Rendering;

namespace RaidCommander.Movement
{
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public sealed class DestinationMarker : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float duration = 1.25f;
        [SerializeField] private Color acceptedColor = new Color(0.15f, 1f, 0.65f);
        [SerializeField] private Color rejectedColor = new Color(1f, 0.15f, 0.1f);
        private MeshRenderer markerRenderer;
        private MaterialPropertyBlock properties;
        private float expiresAt;

        public bool IsVisible => markerRenderer != null && markerRenderer.enabled;
        public bool WasAccepted { get; private set; }

        private void Awake()
        {
            markerRenderer = GetComponent<MeshRenderer>();
            properties = new MaterialPropertyBlock();
            markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            markerRenderer.receiveShadows = false;
            Hide();
        }

        public void Show(Vector3 groundPosition, bool accepted)
        {
            WasAccepted = accepted;
            transform.SetPositionAndRotation(groundPosition + Vector3.up * 0.045f, Quaternion.identity);
            properties.SetColor("_BaseColor", accepted ? acceptedColor : rejectedColor);
            markerRenderer.SetPropertyBlock(properties);
            markerRenderer.enabled = true;
            expiresAt = Time.unscaledTime + duration;
        }

        private void Update()
        {
            if (IsVisible && Time.unscaledTime >= expiresAt)
                Hide();
        }

        public void Hide()
        {
            if (markerRenderer != null)
                markerRenderer.enabled = false;
        }

        private void OnDisable() => Hide();
    }
}
