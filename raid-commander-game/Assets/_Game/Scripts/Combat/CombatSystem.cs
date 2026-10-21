using System;
using UnityEngine;

namespace RaidCommander.Combat
{
    public enum CombatActionFailure { None, InvalidRequest, LineOfSight, NoEffect }

    /// <summary>Validates combat requests, applies effective results, then publishes outcomes.</summary>
    public sealed class CombatSystem : MonoBehaviour
    {
        [SerializeField] private CombatLogService logService;
        [SerializeField] private Transform obstacleRoot;
        [SerializeField] private CombatUnit debugBoss;
        private readonly CombatLineOfSight lineOfSight = new CombatLineOfSight();
        public event Action<ResolvedCombatEvent> EventResolved;
        public event Action SessionReset;

        public CombatLogService LogService => logService;
        public bool KeepBossAlive { get; set; }

        public bool HasLineOfSight(Vector3 source, Vector3 target) => lineOfSight.IsClear(obstacleRoot, source, target);

        public bool TryMeleeAttack(CombatUnit source, CombatUnit target) => TryMeleeAttack(source, target, out _);

        public bool TryMeleeAttack(CombatUnit source, CombatUnit target, out int reportedDamage)
        {
            reportedDamage = 0;
            if (!CanActOn(source, target) || source.Faction == target.Faction
                || PlanarDistance(source.transform.position, target.transform.position) > source.MeleeRange)
                return false;

            source.SetTarget(target);
            FaceTarget(source, target);
            reportedDamage = ApplyDamage(source, target, source.BasicDamage, CombatEventType.SwingDamage);
            return true;
        }

        public bool TryRangedAttack(CombatUnit source, CombatUnit target) => TryRangedAttack(source, target, out _);

        public bool TryRangedAttack(CombatUnit source, CombatUnit target, out CombatActionFailure failure)
            => TryRangedAttack(source, target, out failure, out _);

        public bool TryRangedAttack(CombatUnit source, CombatUnit target, out CombatActionFailure failure, out int reportedDamage)
        {
            reportedDamage = 0;
            failure = CombatActionFailure.InvalidRequest;
            if (!CanActOn(source, target) || source.Faction == target.Faction
                || PlanarDistance(source.transform.position, target.transform.position) > source.RangedRange)
                return false;

            if (!lineOfSight.IsClear(obstacleRoot, source.transform.position, target.transform.position))
            {
                failure = CombatActionFailure.LineOfSight;
                return false;
            }

            source.SetTarget(target);
            FaceTarget(source, target);
            reportedDamage = ApplyDamage(source, target, source.BasicDamage, CombatEventType.RangedDamage);
            bool applied = reportedDamage > 0;
            failure = applied ? CombatActionFailure.None : CombatActionFailure.NoEffect;
            return applied;
        }

        public bool TryHeal(CombatUnit source, CombatUnit target) => TryHeal(source, target, out _);

        public bool TryHeal(CombatUnit source, CombatUnit target, out CombatActionFailure failure)
        {
            failure = CombatActionFailure.InvalidRequest;
            if (source == null || target == null || !source.IsAlive || !target.IsAlive || source.Faction != target.Faction
                || PlanarDistance(source.transform.position, target.transform.position) > source.RangedRange)
                return false;

            if (!lineOfSight.IsClear(obstacleRoot, source.transform.position, target.transform.position))
            {
                failure = CombatActionFailure.LineOfSight;
                return false;
            }

            failure = CombatActionFailure.None;
            source.SetTarget(source == target ? null : target);
            FaceTarget(source, target);
            int effective = target.ApplyHealing(source.BasicHealing);
            if (effective > 0)
                Publish(new ResolvedCombatEvent(CombatEventType.SpellHeal, source, target, healing: effective));
            return true;
        }

        public int ApplyDamage(CombatUnit source, CombatUnit target, int amount,
            CombatEventType eventType = CombatEventType.SwingDamage)
        {
            if (target == null || !target.IsAlive || amount <= 0 || (source != null && !source.IsAlive))
                return 0;
            // Protection changes health loss only. A valid hit still resolves, logs and presents normally.
            bool protectedBoss = KeepBossAlive && target == debugBoss;
            int healthLoss = target.ApplyDamage(protectedBoss ? Mathf.Min(amount, Mathf.Max(0, target.CurrentHealth - 1)) : amount);
            int reportedDamage = protectedBoss ? amount : healthLoss;
            if (reportedDamage <= 0)
                return 0;

            Publish(new ResolvedCombatEvent(eventType, source, target, damage: reportedDamage));
            if (!target.IsAlive)
                Publish(new ResolvedCombatEvent(CombatEventType.UnitDied, source, target));
            return reportedDamage;
        }

        public int ApplyHealing(CombatUnit source, CombatUnit target, int amount)
        {
            if (target == null || amount <= 0 || (source != null && (!source.IsAlive || source.Faction != target.Faction)))
                return 0;
            int effective = target.ApplyHealing(amount);
            if (effective > 0)
                Publish(new ResolvedCombatEvent(CombatEventType.SpellHeal, source, target, healing: effective));
            return effective;
        }

        public void ResetSession(CombatUnit[] units)
        {
            if (units != null)
                foreach (CombatUnit unit in units)
                    if (unit != null) unit.ResetHealth();
            logService?.ResetSession();
            SessionReset?.Invoke();
        }

        private bool CanActOn(CombatUnit source, CombatUnit target) =>
            source != null && target != null && source != target && source.IsAlive && target.IsAlive;

        private void Publish(ResolvedCombatEvent combatEvent)
        {
            EventResolved?.Invoke(combatEvent);
            logService?.Record(combatEvent);
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static void FaceTarget(CombatUnit source, CombatUnit target)
        {
            Vector3 direction = target.transform.position - source.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                source.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}
