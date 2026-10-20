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
        [SerializeField] private WorldHealthBar healthBar;

        public int EncounterId => encounterId;
        public CombatFaction Faction => faction;
        public int MaximumHealth => stats != null ? stats.MaximumHealth : fallbackMaximumHealth;
        public int CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0;
        public int BasicDamage => stats != null ? stats.BasicDamage : fallbackBasicDamage;
        public int BasicHealing => stats != null ? stats.BasicHealing : fallbackBasicHealing;
        public float MeleeRange => stats != null ? stats.MeleeRange : fallbackMeleeRange;
        public float RangedRange => stats != null ? stats.RangedRange : fallbackRangedRange;
        public CombatUnit CurrentTarget { get; private set; }
        public event Action<CombatUnit> Died;
        public event Action<CombatUnit> HealthChanged;

        private void Awake()
        {
            CurrentHealth = MaximumHealth;
            if (healthBar != null)
                healthBar.SetHealth(CurrentHealth, MaximumHealth);
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
            {
                CurrentTarget = null;
                UnitMovement movement = GetComponent<UnitMovement>();
                if (movement != null)
                {
                    movement.Cancel();
                    movement.enabled = false;
                }
                var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled)
                    agent.isStopped = true;
                Died?.Invoke(this);
            }
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
                movement.enabled = true;
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled)
                agent.isStopped = false;
            NotifyHealthChanged();
        }

        private void NotifyHealthChanged()
        {
            if (healthBar != null)
                healthBar.SetHealth(CurrentHealth, MaximumHealth);
            HealthChanged?.Invoke(this);
        }

        private void OnValidate()
        {
            encounterId = Mathf.Max(1, encounterId);
            fallbackMaximumHealth = Mathf.Max(1, fallbackMaximumHealth);
            if (healthBar == null)
                healthBar = GetComponentInChildren<WorldHealthBar>(true);
        }
    }
}
