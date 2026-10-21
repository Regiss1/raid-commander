using UnityEngine;

namespace RaidCommander.Combat
{
    [CreateAssetMenu(menuName = "Raid Commander/Combat Stats", fileName = "CombatStats")]
    public sealed class CombatStatsDefinition : ScriptableObject
    {
        [SerializeField, Min(1)] private int maximumHealth = 100;
        [SerializeField, Min(0)] private int basicDamage = 15;
        [SerializeField, Min(0)] private int basicHealing = 20;
        [SerializeField, Min(0f)] private float meleeRange = 2.5f;
        [SerializeField, Min(0f)] private float rangedRange = 18f;
        [SerializeField, Min(0.01f)] private float attackSpeed = 1f;

        public int MaximumHealth => maximumHealth;
        public int BasicDamage => basicDamage;
        public int BasicHealing => basicHealing;
        public float MeleeRange => meleeRange;
        public float RangedRange => rangedRange;
        public float AttackSpeed => ValidAttackSpeed(attackSpeed);

        public static float ValidAttackSpeed(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? 1f : Mathf.Max(0.01f, value);
    }
}
