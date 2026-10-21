using RaidCommander.Abilities;
using RaidCommander.Combat;
using RaidCommander.Movement;
using UnityEngine;
using UnityEngine.AI;

namespace RaidCommander.Units
{
    /// <summary>Boss combat targeting, spacing and swings; UnitMovement executes navigation.</summary>
    [DefaultExecutionOrder(-10), DisallowMultipleComponent, RequireComponent(typeof(CombatUnit))]
    public sealed class BossBasicAttack : MonoBehaviour
    {
        [SerializeField] private CombatSystem combat;
        [SerializeField] private CombatUnit tank;
        [SerializeField] private CombatUnit[] party;
        [SerializeField, Min(0.1f)] private float attackInterval = 3f;
        [SerializeField, Min(0.1f)] private float forwardSpeed = 3f;
        [SerializeField, Min(0.01f)] private float rangeTolerance = 0.35f;
        [SerializeField, Min(0.05f)] private float decisionInterval = 0.2f;
        private CombatUnit unit;
        private UnitMovement movement;
        private NavMeshAgent agent;
        private BossAbilityRunner abilities;
        private float nextAttack, nextDecision, nextSearch;
        private bool tankLocked;
        private CombatUnit spacingTarget;
        private int restoreTargetThroughFrame = -1;
        public CombatUnit CombatTarget { get; private set; }
        public bool IsChasing { get; private set; }
        public bool IsRetreating { get; private set; }
        public string State { get; private set; } = "Idle";

        private bool Eligible(CombatUnit member) => member != null && member.IsAlive && member.isActiveAndEnabled && member.Faction != unit.Faction;
        private void Awake()
        {
            unit = GetComponent<CombatUnit>(); movement = GetComponent<UnitMovement>();
            agent = GetComponent<NavMeshAgent>(); abilities = GetComponent<BossAbilityRunner>();
        }
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (combat != null) { combat.EventResolved += OnCombatEvent; combat.SessionReset += ResetAttack; }
            unit.Died += OnDeath; ResetAttack();
        }
        private void OnDisable()
        {
            if (abilities != null) abilities.CancelCast();
            if (combat != null) { combat.EventResolved -= OnCombatEvent; combat.SessionReset -= ResetAttack; }
            if (unit != null) unit.Died -= OnDeath;
            StopMove(); RestoreSpeed(); CombatTarget = null; State = "Idle";
        }
        private void OnDeath(CombatUnit _) { StopMove(); RestoreSpeed(); State = "Dead"; CombatTarget = null; }
        private void ResetAttack()
        {
            StopMove(); RestoreSpeed(); tankLocked = false; CombatTarget = null;
            spacingTarget = null;
            restoreTargetThroughFrame = -1;
            nextAttack = Time.time + attackInterval; nextDecision = nextSearch = 0f;
        }
        private void OnCombatEvent(ResolvedCombatEvent outcome)
        {
            if (outcome.Target != unit || outcome.Source != tank || !Eligible(tank) || outcome.Damage.GetValueOrDefault() <= 0) return;
            tankLocked = true; CombatTarget = tank;
        }
        public void CastStarted(bool stopMovement) { if (stopMovement) StopMove(); RestoreSpeed(); }
        public void CastEnded()
        {
            restoreTargetThroughFrame = Time.frameCount;
            nextAttack = Time.time + attackInterval; nextDecision = nextSearch = 0f; RestoreSpeed();
        }
        private void LateUpdate()
        {
            if (!unit.IsAlive || unit.FacingLocked || !Eligible(CombatTarget)) return;
            Vector3 direction = CombatTarget.transform.position - transform.position; direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction);
        }
        private void Update()
        {
            if (!unit.IsAlive) { State = "Dead"; return; }
            if (combat == null) return;
            if (abilities == null) abilities = GetComponent<BossAbilityRunner>();
            if (Time.time >= nextDecision) { nextDecision = Time.time + decisionInterval; AcquireTarget(); DecideMovement(); }
            if (abilities != null && abilities.IsCasting && abilities.ActiveDefinition.SuppressAutoAttacksDuringCast) return;
            if (!Eligible(CombatTarget) || Time.time < nextAttack) return;
            if (CombatPositionSearch.PlanarDistance(transform.position, CombatTarget.transform.position) > unit.MeleeRange
                || !combat.HasLineOfSight(transform.position, CombatTarget.transform.position)) return;
            if (combat.TryMeleeAttack(unit, CombatTarget)) nextAttack = Time.time + attackInterval;
        }
        private void AcquireTarget()
        {
            if (tankLocked && Eligible(tank)) { CombatTarget = tank; return; }
            if (Eligible(CombatTarget) && ((abilities != null && abilities.IsCasting) || Time.frameCount == restoreTargetThroughFrame)) return;
            tankLocked = false; CombatTarget = null;
            float nearest = float.PositiveInfinity;
            if (party != null) foreach (CombatUnit member in party)
            {
                if (!Eligible(member)) continue;
                float distance = CombatPositionSearch.PlanarDistance(transform.position, member.transform.position);
                if (distance < nearest - 0.001f || (Mathf.Abs(distance - nearest) <= 0.001f
                    && (CombatTarget == null || member.EncounterId < CombatTarget.EncounterId))) { nearest = distance; CombatTarget = member; }
            }
        }
        private void DecideMovement()
        {
            if (spacingTarget != CombatTarget)
            { StopMove(); RestoreSpeed(); spacingTarget = CombatTarget; }
            if (!Eligible(CombatTarget)) { StopMove(); State = "Idle"; unit.SetTarget(null); return; }
            if (abilities != null && abilities.IsCasting && abilities.ActiveDefinition.StopMovementDuringCast)
            { StopMove(); State = "Casting " + abilities.ActiveDefinition.DisplayName; return; }
            if (agent == null || movement == null || !agent.isOnNavMesh) { State = "Navigation unavailable"; return; }
            agent.updateRotation = false;
            float tolerance = Mathf.Min(rangeTolerance, unit.MeleeRange * 0.3f);
            // A shorter-reach melee target must be able to enter its own range
            // without forcing us to keep backing away. Tolerance separates
            // retreat entry/release rather than defining a narrow holding ring.
            float lower = Mathf.Max(0f, Mathf.Min(unit.MeleeRange, CombatTarget.MeleeRange)) * 0.5f;
            float retreatEnd = Mathf.Min(unit.MeleeRange, lower + tolerance);
            float distance = CombatPositionSearch.PlanarDistance(transform.position, CombatTarget.transform.position);
            bool sight = combat.HasLineOfSight(transform.position, CombatTarget.transform.position);
            bool retreat = sight && (distance < lower || (IsRetreating && distance < retreatEnd));
            if (!retreat && distance <= unit.MeleeRange && sight) { StopMove(); RestoreSpeed(); State = "In melee range"; return; }
            IsChasing = !retreat; IsRetreating = retreat; agent.speed = retreat ? forwardSpeed * 0.5f : forwardSpeed;
            State = retreat ? "Retreating" : "Chasing " + CombatTarget.name;
            if (Time.time < nextSearch) return;
            nextSearch = Time.time + decisionInterval;
            // Aim beyond release by the agent's arrival margin so braking cannot
            // leave an otherwise completed retreat just short of its threshold.
            float desired = retreat ? Mathf.Min(unit.MeleeRange * 0.95f, retreatEnd + agent.stoppingDistance + 0.1f)
                : unit.MeleeRange * 0.9f;
            if (!CombatPositionSearch.TryFind(agent, combat, CombatTarget, unit.MeleeRange, desired, null, 0f, out Vector3 destination, minimumDistance: retreatEnd))
            { StopMove(); State = "No reachable spacing"; return; }
            if (movement.State == MovementState.Moving && CombatPositionSearch.PlanarDistance(movement.Destination, destination) < 0.15f) return;
            if (movement.TryMove(destination, MovementSource.Autonomous) != MoveResult.Accepted) { StopMove(); State = "No reachable spacing"; }
            IsChasing = !retreat && movement.State == MovementState.Moving;
        }
        private void StopMove()
        {
            if (movement != null && movement.Source == MovementSource.Autonomous) movement.Cancel();
            IsChasing = IsRetreating = false;
        }
        private void RestoreSpeed() { if (agent != null) agent.speed = forwardSpeed; }
    }
}
