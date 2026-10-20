using UnityEngine;
using UnityEngine.UI;

namespace RaidCommander.Combat
{
    /// <summary>Small world-space health display; it contains no combat calculations.</summary>
    public sealed class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private Transform cameraTransform;

        private void LateUpdate()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
            if (cameraTransform != null)
                transform.rotation = cameraTransform.rotation;
        }

        public void SetHealth(int current, int maximum)
        {
            if (fill != null)
                fill.fillAmount = maximum <= 0 ? 0f : Mathf.Clamp01((float)current / maximum);
        }

        private void OnValidate()
        {
            if (fill == null)
                fill = GetComponentInChildren<Image>(true);
        }
    }
}
