using RaidCommander.Combat;
using UnityEngine;

namespace RaidCommander.Units
{
    /// <summary>Stationary boss command producer with a fixed Tank target and periodic melee.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CombatUnit))]
    public sealed class BossBasicAttack : MonoBehaviour
    {
        [SerializeField] private CombatSystem combat;
        [SerializeField] private CombatUnit tank;
        [SerializeField, Min(0.1f)] private float attackInterval = 2f;
        private CombatUnit unit;
        private float nextAttack;
        public string State { get; private set; } = "Idle";

        private void Awake() => unit = GetComponent<CombatUnit>();
        private void OnEnable()
        {
            ResetAttack();
            if (combat != null) combat.SessionReset += ResetAttack;
        }
        private void OnDisable() { if (combat != null) combat.SessionReset -= ResetAttack; }
        private void ResetAttack() => nextAttack = Time.time + attackInterval;
        private void Update()
        {
            if (!unit.IsAlive) { State = "Dead"; return; }
            if (combat == null || tank == null || !tank.IsAlive) { State = "Idle"; unit.SetTarget(null); return; }
            if (CombatPositionSearch.PlanarDistance(transform.position, tank.transform.position) > unit.MeleeRange
                || !combat.HasLineOfSight(transform.position, tank.transform.position))
            { State = "Waiting for Tank"; return; }
            State = "Attacking Tank";
            if (Time.time < nextAttack) return;
            if (combat.TryMeleeAttack(unit, tank)) nextAttack = Time.time + attackInterval;
        }
    }
}
