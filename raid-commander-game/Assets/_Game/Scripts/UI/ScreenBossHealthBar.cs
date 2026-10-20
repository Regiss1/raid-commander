using RaidCommander.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace RaidCommander.UI
{
    /// <summary>Screen-space boss health display, independent of the boss transform.</summary>
    public sealed class ScreenBossHealthBar : MonoBehaviour
    {
        [SerializeField] private CombatUnit boss;
        [SerializeField] private Image fill;
        [SerializeField] private Text nameLabel;
        [SerializeField] private string displayName = "Boss";

        private void OnEnable()
        {
            if (boss != null)
                boss.HealthChanged += Refresh;
            Refresh(boss);
        }

        private void Start() => Refresh(boss);

        private void OnDisable()
        {
            if (boss != null)
                boss.HealthChanged -= Refresh;
        }

        private void Refresh(CombatUnit _)
        {
            if (boss == null)
                return;

            if (fill != null)
                fill.fillAmount = boss.MaximumHealth <= 0
                    ? 0f
                    : Mathf.Clamp01((float)boss.CurrentHealth / boss.MaximumHealth);
            if (nameLabel != null)
                nameLabel.text = displayName;
        }

        private void OnValidate()
        {
            if (fill == null)
                fill = GetComponentInChildren<Image>(true);
            if (nameLabel == null)
                nameLabel = GetComponentInChildren<Text>(true);
        }
    }
}
