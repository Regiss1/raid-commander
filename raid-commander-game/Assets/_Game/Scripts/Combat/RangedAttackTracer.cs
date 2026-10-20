using UnityEngine;

namespace RaidCommander.Combat
{
    /// <summary>Presentation of resolved ranged hits; never calculates or delays damage.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CombatUnit), typeof(LineRenderer))]
    public sealed class RangedAttackTracer : MonoBehaviour
    {
        [SerializeField] private CombatSystem combat;
        [SerializeField, Min(0.01f)] private float duration = 0.2f;
        private CombatUnit unit;
        private LineRenderer line;
        private float hideAt;

        private void Awake()
        {
            unit = GetComponent<CombatUnit>();
            line = GetComponent<LineRenderer>();
            line.enabled = false;
        }

        private void OnEnable()
        {
            if (combat != null) combat.EventResolved += OnResolved;
        }

        private void OnDisable()
        {
            if (combat != null) combat.EventResolved -= OnResolved;
            if (line != null) line.enabled = false;
        }

        private void OnResolved(ResolvedCombatEvent outcome)
        {
            if (outcome.Type != CombatEventType.RangedDamage || outcome.Source != unit || outcome.Target == null)
                return;
            line.SetPosition(0, unit.transform.position);
            line.SetPosition(1, outcome.Target.transform.position);
            line.enabled = true;
            hideAt = Time.time + duration;
        }

        private void Update()
        {
            if (line.enabled && Time.time >= hideAt) line.enabled = false;
        }
    }
}
