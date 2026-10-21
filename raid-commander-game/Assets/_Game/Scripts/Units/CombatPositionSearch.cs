using RaidCommander.Combat;
using RaidCommander.Abilities;
using UnityEngine;
using UnityEngine.AI;

namespace RaidCommander.Units
{
    /// <summary>Bounded position search; candidates must have a complete route and clear XZ sight.</summary>
    public static class CombatPositionSearch
    {
        public static bool TryFind(NavMeshAgent agent, CombatSystem combat, CombatUnit target,
            float range, float preferredDistance, CombatUnit danger, float safeDistance, out Vector3 destination, GroundHazards hazards = null, float minimumDistance = 0f)
        {
            destination = default;
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh || target == null || range <= 0f)
                return false;

            Vector3 origin = agent.transform.position;
            Vector3 direction = origin - target.transform.position;
            direction.y = 0f;
            float angle = direction.sqrMagnitude > 0.001f ? Mathf.Atan2(direction.z, direction.x) : 0f;
            float bestScore = float.PositiveInfinity;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            var path = new NavMeshPath();
            // Prefer the desired ring, but allow closer/farther positions when obstacles restrict it.
            for (int ring = 0; ring < 3; ring++)
            {
                float radius = ring == 0 ? Mathf.Min(preferredDistance, range * 0.95f)
                    : ring == 1 ? range * 0.5f : range * 0.9f;
                for (int i = 0; i < 16; i++)
                {
                    float theta = angle + i * Mathf.PI / 8f;
                    Vector3 candidate = target.transform.position + new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta)) * radius;
                    // The target's pivot can be elevated above the surface.
                    candidate.y = target.transform.position.y - (target.TryGetComponent(out NavMeshAgent targetAgent) ? targetAgent.baseOffset * Mathf.Abs(target.transform.lossyScale.y) : 0f);
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, filter)) continue;
                    float distance = PlanarDistance(hit.position, target.transform.position);
                    if (distance < minimumDistance || distance > range * 0.95f || !combat.HasLineOfSight(hit.position, target.transform.position)) continue;
                    if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    if (hazards != null && (hazards.IsUnsafe(hit.position, agent.radius + 0.15f) || !hazards.PathIsSafe(path, agent.radius + 0.15f))) continue;
                    float length = 0f;
                    Vector3[] corners = path.corners;
                    for (int c = 1; c < corners.Length; c++) length += Vector3.Distance(corners[c - 1], corners[c]);
                    float unsafePenalty = danger != null ? Mathf.Max(0f, safeDistance - PlanarDistance(hit.position, danger.transform.position)) * 100f : 0f;
                    float score = length + Mathf.Abs(distance - preferredDistance) * 2f + unsafePenalty;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    destination = hit.position;
                }
            }
            return !float.IsPositiveInfinity(bestScore);
        }

        public static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
