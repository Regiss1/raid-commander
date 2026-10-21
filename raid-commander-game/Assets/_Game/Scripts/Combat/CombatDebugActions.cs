using UnityEngine;

namespace RaidCommander.Combat
{
    /// <summary>Development-only command producer for exercising the normal combat pipeline.</summary>
    public sealed class CombatDebugActions : MonoBehaviour
    {
        [SerializeField] private CombatSystem combat;
        [SerializeField] private CombatUnit tank;
        [SerializeField] private CombatUnit healer;
        [SerializeField] private CombatUnit dps;
        [SerializeField] private CombatUnit rangedDps;
        [SerializeField] private CombatUnit boss;

        public string LastStatus { get; private set; } = "Choose a combat action.";
        public bool KeepBossAlive
        {
            get => combat != null && combat.KeepBossAlive;
            set { if (combat != null) combat.KeepBossAlive = value; }
        }

        public void TankMelee() => TryMelee(tank, "Tank");
        public void HealerHeal()
        {
            if (combat == null || healer == null || tank == null)
            {
                LastStatus = "Heal unavailable: combat units are not configured.";
                return;
            }

            int healthBefore = tank.CurrentHealth;
            if (!combat.TryHeal(healer, tank, out CombatActionFailure failure))
            {
                LastStatus = failure == CombatActionFailure.LineOfSight
                    ? "Healer failed because of line of sight"
                    : DescribeRejectedAction(healer, tank, healer.RangedRange, "heal");
                return;
            }

            int effectiveHealing = tank.CurrentHealth - healthBefore;
            LastStatus = effectiveHealing > 0
                ? $"Healer restored {effectiveHealing} health to Tank."
                : "No healing: Tank is already at full health.";
        }

        public void DpsMelee() => TryMelee(dps, "DPS");

        public void RangedDpsAttack()
        {
            if (combat == null || rangedDps == null || boss == null)
            {
                LastStatus = "Ranged attack unavailable: combat units are not configured.";
                return;
            }

            int healthBefore = boss.CurrentHealth;
            if (!combat.TryRangedAttack(rangedDps, boss, out CombatActionFailure failure))
            {
                LastStatus = failure == CombatActionFailure.LineOfSight
                    ? "Ranged DPS failed because of line of sight"
                    : DescribeRejectedAction(rangedDps, boss, rangedDps.RangedRange, "ranged attack");
                return;
            }

            LastStatus = $"Ranged DPS hit the boss for {healthBefore - boss.CurrentHealth}.";
        }

        public void DamageTank()
        {
            if (combat == null || tank == null || boss == null)
            {
                LastStatus = "Damage unavailable: combat units are not configured.";
                return;
            }
            if (!boss.IsAlive)
            {
                LastStatus = "Damage unavailable: the boss is defeated. Reset the encounter first.";
                return;
            }
            if (!tank.IsAlive)
            {
                LastStatus = "Damage unavailable: Tank is defeated. Reset the encounter first.";
                return;
            }

            int effectiveDamage = combat.ApplyDamage(boss, tank, boss.BasicDamage);
            LastStatus = effectiveDamage > 0
                ? $"Boss damaged Tank for {effectiveDamage}."
                : "No damage was applied.";
        }

        public void KillBoss()
        {
            if (combat == null || tank == null || boss == null)
            {
                LastStatus = "Kill unavailable: combat units are not configured.";
                return;
            }
            int effectiveDamage = combat.ApplyDamage(tank, boss, boss.CurrentHealth);
            LastStatus = effectiveDamage > 0
                ? "Tank defeated the boss. Reset the encounter to continue."
                : "No damage was applied. Check whether the boss is already defeated.";
        }

        public void ResetEncounter()
        {
            if (combat == null)
            {
                LastStatus = "Reset unavailable: combat system is not configured.";
                return;
            }
            combat.ResetSession(new[] { tank, healer, dps, rangedDps, boss });
            LastStatus = "Unit health and encounter log reset.";
        }

        private void TryMelee(CombatUnit source, string sourceName)
        {
            if (combat == null || source == null || boss == null)
            {
                LastStatus = "Melee unavailable: combat units are not configured.";
                return;
            }

            int healthBefore = boss.CurrentHealth;
            if (!combat.TryMeleeAttack(source, boss))
            {
                LastStatus = DescribeRejectedAction(source, boss, source.MeleeRange, "melee attack");
                return;
            }

            int effectiveDamage = healthBefore - boss.CurrentHealth;
            LastStatus = $"{sourceName} hit the boss for {effectiveDamage}.";
        }

        private static string DescribeRejectedAction(CombatUnit source, CombatUnit target, float range, string action)
        {
            if (!source.IsAlive)
                return $"No {action}: {source.name} is defeated. Reset the encounter first.";
            if (!target.IsAlive)
                return $"No {action}: {target.name} is defeated. Reset the encounter first.";
            if (action == "heal" && source.Faction != target.Faction)
                return "No heal: Healer and Tank must be on the same faction.";
            if (action != "heal" && source.Faction == target.Faction)
                return $"No {action}: source and target must be on opposing factions.";

            float distance = PlanarDistance(source.transform.position, target.transform.position);
            if (distance > range)
                return $"No {action}: {source.name} is {distance:F1} units away; range is {range:F1}. Move closer.";
            return $"No {action}: the request was rejected.";
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }
}
