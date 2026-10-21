using RaidCommander.Combat;
using RaidCommander.Movement;
using UnityEngine;
using UnityEngine.AI;

namespace RaidCommander.Units
{
    public enum CombatRole { Tank, MeleeDps, RangedDps, Healer }
    public enum RoleState { Idle, SeekingPosition, Fighting, Healing, Holding, ManualMove, NoReachablePosition, Dead }

    /// <summary>Produces autonomous movement/combat requests; never owns selection or path execution.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CombatUnit), typeof(UnitMovement))]
    public sealed class PartyRoleBrain : MonoBehaviour
    {
        [SerializeField] private CombatRole role;
        [SerializeField] private CombatSystem combat;
        [SerializeField] private CombatUnit boss;
        [SerializeField] private CombatUnit tank;
        [SerializeField] private CombatUnit[] party;
        [SerializeField, Min(0.05f)] private float decisionInterval = 0.2f;
        [SerializeField, Min(0.1f)] private float actionInterval = 1.5f;
        [SerializeField, Min(0.2f)] private float positionSearchInterval = 0.8f;
        [SerializeField, Min(0f)] private float preferredDistance = 12f;
        [SerializeField, Min(0f)] private float safeBossDistance = 6f;
        private CombatUnit unit;
        private UnitMovement movement;
        private NavMeshAgent agent;
        private float nextDecision, nextAction, nextPositionSearch;

        public RoleState State { get; private set; }
        public CombatRole Role => role;
        public CombatUnit Target { get; private set; }

        private void Awake()
        {
            unit = GetComponent<CombatUnit>();
            movement = GetComponent<UnitMovement>();
            agent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            nextDecision = nextAction = nextPositionSearch = 0f;
            if (combat != null) combat.SessionReset += ResetDecisions;
        }

        private void OnDisable()
        {
            if (combat != null) combat.SessionReset -= ResetDecisions;
            StopAutonomousMove();
            Target = null;
            State = RoleState.Idle;
        }

        private void ResetDecisions()
        {
            StopAutonomousMove();
            Target = null;
            nextDecision = nextAction = nextPositionSearch = 0f;
        }

        private void Update()
        {
            if (Time.time < nextDecision) return;
            nextDecision = Time.time + decisionInterval;
            if (!unit.IsAlive) { StopAutonomousMove(); State = RoleState.Dead; Target = null; return; }
            if (combat == null || boss == null || !boss.IsAlive)
            {
                StopAutonomousMove(); Target = null; unit.SetTarget(null); State = movement.HasManualOverride ? RoleState.ManualMove : RoleState.Idle; return;
            }

            bool healing = role == CombatRole.Healer;
            Target = healing ? ChoosePatient() : boss;
            CombatUnit positionTarget = Target != null ? Target : (tank != null && tank.IsAlive ? tank : boss);
            float range = healing || role == CombatRole.RangedDps ? unit.RangedRange : unit.MeleeRange;
            float distance = CombatPositionSearch.PlanarDistance(transform.position, positionTarget.transform.position);
            bool clear = combat.HasLineOfSight(transform.position, positionTarget.transform.position);
            bool inRange = distance <= range;

            // Manual movement owns positioning only; valid actions may still resolve while moving.
            if (Target != null && inRange && clear && Time.time >= nextAction)
            {
                bool acted = healing ? combat.TryHeal(unit, Target) : role == CombatRole.RangedDps
                    ? combat.TryRangedAttack(unit, Target) : combat.TryMeleeAttack(unit, Target);
                if (acted) nextAction = Time.time + actionInterval;
            }
            if (movement.HasManualOverride) { State = RoleState.ManualMove; return; }

            float desired = healing || role == CombatRole.RangedDps ? Mathf.Min(preferredDistance, range * 0.8f) : range * 0.75f;
            bool tooClose = healing ? CombatPositionSearch.PlanarDistance(transform.position, boss.transform.position) < safeBossDistance
                : role == CombatRole.RangedDps && distance < desired - 2f;
            bool tooFar = !inRange || (!healing && role == CombatRole.RangedDps && distance > desired + 2f);
            bool needsPosition = !clear || tooClose || tooFar;
            if (!needsPosition)
            {
                StopAutonomousMove();
                State = Target == null ? RoleState.Holding : healing ? RoleState.Healing : RoleState.Fighting;
                return;
            }

            if (Time.time < nextPositionSearch)
            {
                if (State != RoleState.NoReachablePosition) State = RoleState.SeekingPosition;
                return;
            }
            State = RoleState.SeekingPosition;
            nextPositionSearch = Time.time + positionSearchInterval;
            if (!CombatPositionSearch.TryFind(agent, combat, positionTarget, range, desired,
                healing ? boss : null, healing ? safeBossDistance : 0f, out Vector3 destination))
            {
                StopAutonomousMove(); State = RoleState.NoReachablePosition; return;
            }
            if (movement.State == MovementState.Moving && movement.Source == MovementSource.Autonomous
                && CombatPositionSearch.PlanarDistance(movement.Destination, destination) < 0.75f) return;
            if (movement.TryMove(destination, MovementSource.Autonomous) != MoveResult.Accepted)
                State = RoleState.NoReachablePosition;
        }

        private CombatUnit ChoosePatient()
        {
            if (Injured(tank)) return tank;
            CombatUnit patient = null;
            if (party != null)
                foreach (CombatUnit ally in party)
                    if (Injured(ally) && (patient == null || (float)ally.CurrentHealth / ally.MaximumHealth < (float)patient.CurrentHealth / patient.MaximumHealth))
                        patient = ally;
            return patient;
        }

        private static bool Injured(CombatUnit candidate) => candidate != null && candidate.IsAlive && candidate.CurrentHealth < candidate.MaximumHealth;

        private void StopAutonomousMove()
        {
            if (movement != null && movement.State == MovementState.Moving && movement.Source == MovementSource.Autonomous)
                movement.Cancel();
        }
    }
}
