using System.Collections.Generic;
using System.Linq;
using RaidCommander.Combat;
using RaidCommander.Encounters;
using RaidCommander.Units;
using UnityEngine;

namespace RaidCommander.Abilities
{
    /// <summary>Per-boss readiness and single-cast lifetime; definitions contain no mutable encounter state.</summary>
    [DefaultExecutionOrder(-20), DisallowMultipleComponent, RequireComponent(typeof(CombatUnit))]
    public sealed class BossAbilityRunner : MonoBehaviour
    {
        [SerializeField] private CombatSystem combat;
        [SerializeField] private ArenaBoundary boundary;
        [SerializeField] private GroundHazards hazards;
        [SerializeField] private GroundHazardVisual presentation;
        [SerializeField] private PartyRoleBrain[] party;
        [SerializeField] private BossAbilityDefinition[] loadout;
        private sealed class Readiness
        {
            public BossAbilityDefinition Definition;
            public float IntervalAt, CooldownAt;
            public bool ThresholdPending, ThresholdConsumed, Valid;
            public string Error;
        }
        private readonly List<Readiness> states = new List<Readiness>();
        private CombatUnit unit;
        private BossBasicAttack controller;
        private Readiness active;
        private float activatedAt, nextDecision;
        private bool started;
        public bool IsCasting => active != null;
        public BossAbilityDefinition ActiveDefinition => active?.Definition;
        public CombatUnit CastTarget { get; private set; }
        public Vector3 CastOrigin { get; private set; }
        public Vector3 CastForward { get; private set; }
        public HazardFootprint PendingFootprint { get; private set; }
        public float CastEndsAt { get; private set; }
        public float LastCastStartedAt { get; private set; } = -1f;
        public int CastCount { get; private set; }
        public string SetupError { get; private set; }
        private void Awake() { unit = GetComponent<CombatUnit>(); controller = GetComponent<BossBasicAttack>(); }
        private void Start()
        {
            // All scene Awake/OnEnable callbacks must finish before validating peers.
            // activatedAt was already captured on enable; initialization adds no delay.
            started = true; SyncLoadout(); OnHealthChanged(unit);
        }
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (combat != null) combat.SessionReset += ResetEncounter;
            unit.Died += OnDeath; unit.HealthChanged += OnHealthChanged; ResetEncounter();
        }
        private void OnDisable()
        {
            if (combat != null) combat.SessionReset -= ResetEncounter;
            if (unit != null) { unit.Died -= OnDeath; unit.HealthChanged -= OnHealthChanged; }
            CancelCast(); if (presentation != null) presentation.Hide();
        }
        private void OnDeath(CombatUnit _) { CancelCast(); if (presentation != null) presentation.Hide(); }
        private void ResetEncounter()
        {
            CancelCast(false); if (presentation != null) presentation.Hide();
            activatedAt = Time.time; nextDecision = 0f; CastCount = 0; LastCastStartedAt = -1f;
            states.Clear(); SetupError = null;
            if (started) { SyncLoadout(); OnHealthChanged(unit); }
        }
        private void SyncLoadout()
        {
            BossAbilityDefinition[] definitions = loadout ?? new BossAbilityDefinition[0];
            var previous = states.ToArray(); states.Clear(); SetupError = null;
            var ids = new HashSet<int>();
            foreach (BossAbilityDefinition definition in definitions)
            {
                // Recheck at the decision cadence: references alone cannot tell whether
                // a boundary or edited definition has become valid (or invalid).
                Readiness state = previous.FirstOrDefault(s => s.Definition == definition && !states.Contains(s))
                    ?? new Readiness { Definition = definition,
                        IntervalAt = definition != null ? activatedAt + definition.InitialDelaySeconds : activatedAt };
                string error = ValidationError(definition);
                if (error == null && !ids.Add(definition.AbilityId)) error = "Duplicate ability ID in boss loadout.";
                if (error != null)
                {
                    if (SetupError == null) SetupError = error;
                    if (state.Error != error) Debug.LogWarning(name + ": " + error, this);
                }
                state.Error = error; state.Valid = error == null;
                states.Add(state);
            }
        }
        private string ValidationError(BossAbilityDefinition definition)
        {
            if (definition == null) return "Missing ability definition.";
            string error = definition.ValidationError(true);
            if (error != null || definition.DeliveryMode != AbilityDelivery.GroundArea) return error;
            if (boundary == null) return "Ground ability needs an arena boundary reference.";
            if (!boundary.IsValid) return "Ground ability needs an active, enabled arena boundary with positive finite XZ bounds and an enabled BoxCollider.";
            if (hazards == null) return "Ground ability needs a ground-hazard collection reference.";
            return null;
        }
        private void OnHealthChanged(CombatUnit _)
        {
            // This runner enables before CombatUnit.Awake initializes health.
            if (unit == null || !unit.IsAlive) return;
            foreach (Readiness state in states)
                if (state.Valid && state.Definition.TriggerMode == AbilityTrigger.HealthThreshold && !state.ThresholdConsumed
                    && unit.MaximumHealth > 0 && unit.CurrentHealth * 100f / unit.MaximumHealth <= state.Definition.HealthThresholdPercent)
                    state.ThresholdPending = true;
        }
        private void Update()
        {
            if (!unit.IsAlive || combat == null || (controller != null && !controller.isActiveAndEnabled)) { CancelCast(); return; }
            if (IsCasting)
            {
                if (ActiveDefinition.LockFacingOnCastStart) transform.rotation = Quaternion.LookRotation(CastForward);
                if (Time.time >= CastEndsAt) CompleteCast();
                return;
            }
            if (Time.time < nextDecision) return;
            nextDecision = Time.time + 0.1f; SyncLoadout(); OnHealthChanged(unit);
            foreach (Readiness state in states)
            {
                if (!state.Valid) continue;
                BossAbilityDefinition definition = state.Definition;
                if (Time.time < state.CooldownAt || Time.time < activatedAt + definition.InitialDelaySeconds) continue;
                if (definition.TriggerMode == AbilityTrigger.Interval ? Time.time < state.IntervalAt : !state.ThresholdPending || state.ThresholdConsumed) continue;
                // Selection counts eligible living units, not role buckets. Disabled brains still represent living units.
                var eligible = party == null ? new PartyRoleBrain[0] : party.Where(p => p != null && p.gameObject.activeInHierarchy
                    && p.GetComponent<CombatUnit>().IsAlive && p.GetComponent<CombatUnit>().Faction != unit.Faction && definition.Allows(p.Role)
                    && (definition.TargetRangeMode == AbilityRange.Unlimited || CombatPositionSearch.PlanarDistance(transform.position, p.transform.position) <= definition.TargetRange)).Distinct().ToArray();
                if (eligible.Length == 0) continue;
                BeginCast(state, eligible[Random.Range(0, eligible.Length)].GetComponent<CombatUnit>());
                if (definition.CastTimeSeconds == 0f) CompleteCast();
                break;
            }
        }
        private void BeginCast(Readiness readiness, CombatUnit target)
        {
            active = readiness; CastTarget = target;
            BossAbilityDefinition definition = readiness.Definition;
            readiness.ThresholdPending = false; readiness.ThresholdConsumed = definition.TriggerMode == AbilityTrigger.HealthThreshold;
            readiness.IntervalAt = Time.time + definition.IntervalSeconds;
            LastCastStartedAt = Time.time; CastEndsAt = Time.time + definition.CastTimeSeconds; CastCount++;
            Vector3 direction = target.transform.position - transform.position; direction.y = 0f;
            CastForward = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            CastForward = new Vector3(CastForward.x, 0f, CastForward.z).normalized;
            if (CastForward.sqrMagnitude < 0.001f) CastForward = Vector3.forward;
            transform.rotation = Quaternion.LookRotation(CastForward); unit.FacingLocked = definition.LockFacingOnCastStart;
            float radius = GetComponent<Collider>() != null ? Mathf.Max(GetComponent<Collider>().bounds.extents.x, GetComponent<Collider>().bounds.extents.z) : 0f;
            CastOrigin = transform.position + CastForward * radius;
            if (definition.DeliveryMode == AbilityDelivery.GroundArea)
            {
                CastOrigin = new Vector3(CastOrigin.x, boundary.GroundY, CastOrigin.z);
                float length = definition.LengthMode == AbilityLength.ToArenaBoundary ? boundary.Bounds.size.magnitude * 2f : definition.Length;
                PendingFootprint = HazardFootprint.Rectangle(CastOrigin, CastForward, definition.Width, length, boundary);
                hazards.Register(this, PendingFootprint, definition.DisplayName, definition.AbilityId);
                if (definition.ShowTelegraph && presentation != null) presentation.Show(PendingFootprint, definition.TelegraphColor);
            }
            if (controller != null) controller.CastStarted(definition.StopMovementDuringCast);
            combat.PublishCast(CombatEventType.SpellCastStart, unit, target, definition.AbilityId);
        }
        private void CompleteCast()
        {
            BossAbilityDefinition definition = ActiveDefinition;
            bool direct = definition.DeliveryMode == AbilityDelivery.DirectTarget;
            if (direct && (CastTarget == null || !CastTarget.IsAlive || !CastTarget.isActiveAndEnabled)) { CancelCast(); return; }
            // Snapshot victims before resolving any damage/subscriber side effects; body colliders are irrelevant.
            CombatUnit[] victims = direct ? new[] { CastTarget } : party.Where(p => p != null)
                .Select(p => p.GetComponent<CombatUnit>()).Where(p => p.IsAlive && p.Faction != unit.Faction && PendingFootprint.Contains(p.transform.position)).Distinct().ToArray();
            if (hazards != null) hazards.Remove(this);
            combat.PublishCast(CombatEventType.SpellCastSuccess, unit, CastTarget, definition.AbilityId);
            foreach (CombatUnit victim in victims) combat.ApplyDamage(unit, victim, definition.Damage, CombatEventType.SpellDamage, definition.AbilityId);
            if (PendingFootprint != null && definition.ShowTelegraph && presentation != null) presentation.Show(PendingFootprint, definition.ImpactColor, definition.ImpactDuration);
            active.CooldownAt = Time.time + definition.CooldownSeconds; ReleaseCast();
        }
        public void CancelCast()
        {
            CancelCast(true);
        }
        private void CancelCast(bool report)
        {
            if (!IsCasting) return;
            if (report && combat != null) combat.PublishCast(CombatEventType.SpellCastFailed, unit, CastTarget, ActiveDefinition.AbilityId);
            if (hazards != null) hazards.Remove(this);
            if (presentation != null) presentation.Hide(); ReleaseCast();
        }
        private void ReleaseCast()
        {
            active = null; PendingFootprint = null; CastTarget = null;
            if (unit != null) unit.FacingLocked = false;
            if (controller != null) controller.CastEnded();
        }
    }
}
