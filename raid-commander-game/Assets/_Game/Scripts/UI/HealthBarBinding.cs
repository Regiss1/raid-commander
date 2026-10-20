using System.Globalization;
using RaidCommander.Combat;
using TMPro;
using UnityEngine;

namespace RaidCommander.UI
{
    /// <summary>One reactive health-to-UI binding shared by world bars and the boss HUD.</summary>
    [DisallowMultipleComponent]
    public sealed class HealthBarBinding : MonoBehaviour
    {
        [SerializeField] private CombatUnit unit;
        [SerializeField] private UnityEngine.UI.Image fill;
        [SerializeField] private TMP_Text percentageLabel;
        [SerializeField] private TMP_Text healthValueLabel;
        private CombatUnit subscribedUnit;

        public CombatUnit Unit => unit;
        public UnityEngine.UI.Image FillImage => fill;
        public TMP_Text PercentageLabel => percentageLabel;
        public TMP_Text HealthValueLabel => healthValueLabel;

        private void OnEnable()
        {
            Subscribe();
            Refresh(unit);
        }

        // All unit Awake calls have finished by Start, regardless of scene activation order.
        private void Start() => Refresh(unit);
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        public void Bind(CombatUnit value)
        {
            Unsubscribe();
            unit = value;
            if (isActiveAndEnabled) Subscribe();
            Refresh(unit);
        }

        private void Subscribe()
        {
            if (unit == null || subscribedUnit == unit) return;
            subscribedUnit = unit;
            subscribedUnit.HealthChanged += Refresh;
        }

        private void Unsubscribe()
        {
            if (subscribedUnit != null)
                subscribedUnit.HealthChanged -= Refresh;
            subscribedUnit = null;
        }

        private void Refresh(CombatUnit changed)
        {
            int current = changed != null ? changed.CurrentHealth : 0;
            int maximum = changed != null ? changed.MaximumHealth : 0;
            float fraction = Fraction(current, maximum);
            if (fill != null) fill.fillAmount = fraction;
            if (percentageLabel != null)
                percentageLabel.text = Mathf.RoundToInt(fraction * 100f).ToString(CultureInfo.InvariantCulture) + "%";
            if (healthValueLabel != null)
                healthValueLabel.text = current.ToString("N0", CultureInfo.InvariantCulture) + " / "
                    + maximum.ToString("N0", CultureInfo.InvariantCulture);
        }

        public static float Fraction(int current, int maximum) =>
            maximum <= 0 ? 0f : Mathf.Clamp01((float)current / maximum);
    }
}
