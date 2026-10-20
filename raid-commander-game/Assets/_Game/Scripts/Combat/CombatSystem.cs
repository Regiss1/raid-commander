using System;
using UnityEngine;

namespace RaidCommander.Combat
{
    /// <summary>Validates combat requests, applies effective results, then publishes outcomes.</summary>
    public sealed class CombatSystem : MonoBehaviour
    {
        [SerializeField] private CombatLogService logService;
        public event Action<ResolvedCombatEvent> EventResolved;

        public CombatLogService LogService => logService;

        public bool TryMeleeAttack(CombatUnit source, CombatUnit target)
        {
            if (!CanActOn(source, target) || source.Faction == target.Faction
                || PlanarDistance(source.transform.position, target.transform.position) > source.MeleeRange)
                return false;

            source.SetTarget(target);
            FaceTarget(source, target);
            ApplyDamage(source, target, source.BasicDamage, CombatEventType.SwingDamage);
            return true;
        }

        public bool TryHeal(CombatUnit source, CombatUnit target)
        {
            if (!CanActOn(source, target) || source.Faction != target.Faction
                || PlanarDistance(source.transform.position, target.transform.position) > source.RangedRange)
                return false;

            source.SetTarget(target);
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
            int effective = target.ApplyDamage(amount);
            if (effective <= 0)
                return 0;

            Publish(new ResolvedCombatEvent(eventType, source, target, damage: effective));
            if (!target.IsAlive)
                Publish(new ResolvedCombatEvent(CombatEventType.UnitDied, source, target));
            return effective;
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
