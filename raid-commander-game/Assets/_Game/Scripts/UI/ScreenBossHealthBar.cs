using TMPro;
using UnityEngine;

namespace RaidCommander.UI
{
    /// <summary>Screen-space boss layout; HealthBarBinding owns reactive health rendering.</summary>
    [RequireComponent(typeof(HealthBarBinding))]
    public sealed class ScreenBossHealthBar : MonoBehaviour
    {
        [SerializeField] private RectTransform barRect;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private string displayName = "Boss";
        private Canvas canvas;
        private HealthBarBinding healthBar;
        private float previousScale = -1f;
        private float previousWidth = -1f;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            healthBar = GetComponent<HealthBarBinding>();
        }

        private void OnEnable()
        {
            if (nameLabel != null) nameLabel.text = displayName;
            previousScale = -1f;
            Canvas.preWillRenderCanvases += UpdateLayout;
        }

        private void OnDisable() => Canvas.preWillRenderCanvases -= UpdateLayout;
        private void OnDestroy() => Canvas.preWillRenderCanvases -= UpdateLayout;

        private void UpdateLayout()
        {
            if (canvas == null || barRect == null) return;
            float scale = Mathf.Max(0.001f, canvas.scaleFactor);
            float availableWidth = ((RectTransform)canvas.transform).rect.width;
            if (Mathf.Approximately(scale, previousScale) && Mathf.Approximately(availableWidth, previousWidth)) return;
            previousScale = scale;
            previousWidth = availableWidth;

            float height = Mathf.Max(32f, 26f / scale);
            barRect.sizeDelta = new Vector2(Mathf.Max(1f, Mathf.Min(760f, availableWidth - 32f / scale)), height);
            barRect.anchoredPosition = new Vector2(0f, -20f / scale);
            float fontSize = Mathf.Max(20f, 14f / scale);
            if (healthBar.PercentageLabel != null) healthBar.PercentageLabel.fontSize = fontSize;
            if (healthBar.HealthValueLabel != null) healthBar.HealthValueLabel.fontSize = fontSize;
            if (nameLabel != null)
            {
                nameLabel.fontSize = fontSize;
                nameLabel.rectTransform.anchoredPosition = new Vector2(0f, barRect.anchoredPosition.y - height - 4f / scale);
                nameLabel.rectTransform.sizeDelta = new Vector2(barRect.sizeDelta.x, Mathf.Max(28f, 22f / scale));
            }
        }
    }
}
