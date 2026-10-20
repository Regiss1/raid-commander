using System;
using RaidCommander.Movement;
using UnityEngine;

namespace RaidCommander.Combat
{
    public enum CombatFaction { Friendly, Hostile }

    [DisallowMultipleComponent]
    public sealed class CombatUnit : MonoBehaviour
    {
        [SerializeField, Min(1)] private int encounterId;
        [SerializeField] private CombatFaction faction;
        [SerializeField] private CombatStatsDefinition stats;
        [SerializeField, Min(1)] private int fallbackMaximumHealth = 100;
        [SerializeField, Min(0)] private int fallbackBasicDamage = 15;
        [SerializeField, Min(0)] private int fallbackBasicHealing = 20;
        [SerializeField, Min(0f)] private float fallbackMeleeRange = 2.5f;
        [SerializeField, Min(0f)] private float fallbackRangedRange = 18f;
        private int maximumHealth;
        private bool healthInitialized;

        public int EncounterId => encounterId;
        public CombatFaction Faction => faction;
        public int MaximumHealth => healthInitialized ? maximumHealth : ConfiguredMaximumHealth;
        public int CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0;
        public int BasicDamage => stats != null ? stats.BasicDamage : fallbackBasicDamage;
        public int BasicHealing => stats != null ? stats.BasicHealing : fallbackBasicHealing;
        public float MeleeRange => stats != null ? stats.MeleeRange : fallbackMeleeRange;
        public float RangedRange => stats != null ? stats.RangedRange : fallbackRangedRange;
        public CombatUnit CurrentTarget { get; private set; }
        public event Action<CombatUnit> Died;
        public event Action<CombatUnit> HealthChanged;

        private int ConfiguredMaximumHealth => stats != null ? stats.MaximumHealth : fallbackMaximumHealth;

        private void Awake() => InitializeHealth();

        private void InitializeHealth()
        {
            if (healthInitialized) return;
            maximumHealth = Mathf.Max(0, ConfiguredMaximumHealth);
            healthInitialized = true;
            CurrentHealth = maximumHealth;
            NotifyHealthChanged();
        }

        /// <summary>Changes the runtime cap without healing or reviving the unit.</summary>
        public void SetMaximumHealth(int value)
        {
            InitializeHealth();
            value = Mathf.Max(0, value);
            if (maximumHealth == value) return;
            bool wasAlive = IsAlive;
            maximumHealth = value;
            CurrentHealth = Mathf.Min(CurrentHealth, maximumHealth);
            NotifyHealthChanged();
            if (wasAlive && !IsAlive)
                HandleDeath();
        }

        public bool SetTarget(CombatUnit target)
        {
            if (!IsAlive || target == this || (target != null && !target.IsAlive))
                return false;
            CurrentTarget = target;
            return true;
        }

        internal int ApplyDamage(int amount)
        {
            if (!IsAlive || amount <= 0)
                return 0;
            int effective = Mathf.Min(CurrentHealth, amount);
            CurrentHealth -= effective;
            NotifyHealthChanged();
            if (CurrentHealth == 0)
                HandleDeath();
            return effective;
        }

        internal int ApplyHealing(int amount)
        {
            if (!IsAlive || amount <= 0)
                return 0;
            int effective = Mathf.Min(MaximumHealth - CurrentHealth, amount);
            CurrentHealth += effective;
            if (effective > 0)
                NotifyHealthChanged();
            return effective;
        }

        internal void ResetHealth()
        {
            CurrentHealth = MaximumHealth;
            CurrentTarget = null;
            UnitMovement movement = GetComponent<UnitMovement>();
            if (movement != null)
                movement.enabled = IsAlive;
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                agent.isStopped = !IsAlive;
            NotifyHealthChanged();
        }

        private void NotifyHealthChanged()
        {
            HealthChanged?.Invoke(this);
        }

        private void HandleDeath()
        {
            CurrentTarget = null;
            UnitMovement movement = GetComponent<UnitMovement>();
            if (movement != null)
            {
                movement.Cancel();
                movement.enabled = false;
            }
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                agent.isStopped = true;
            Died?.Invoke(this);
        }

        private void OnValidate()
        {
            encounterId = Mathf.Max(1, encounterId);
            fallbackMaximumHealth = Mathf.Max(1, fallbackMaximumHealth);
        }
    }
}
