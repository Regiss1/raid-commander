using RaidCommander.Abilities;
using RaidCommander.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace RaidCommander.Units
{
    public enum MeleeSector { Rear, Side, Front }

    public readonly struct MeleePositionPlan
    {
        public Vector3 Destination { get; }
        public Vector3 FinalDestination { get; }
        public MeleeSector Sector { get; }
        public bool UsesDetour { get; }

        public MeleePositionPlan(Vector3 destination, Vector3 finalDestination, MeleeSector sector, bool usesDetour)
        { Destination = destination; FinalDestination = finalDestination; Sector = sector; UsesDetour = usesDetour; }
    }

    /// <summary>Bounded, role-only search. Body footprints are soft preferences, never NavMesh obstacles.</summary>
    public static class MeleeDpsPositionSearch
    {
        private const int SamplesPerRing = 24;
        private const float OrbitStep = Mathf.PI / 5f;

        public static MeleeSector Classify(Vector3 bossForward, Vector3 bossToPoint, float rearHalfAngle)
        {
            bossForward.y = bossToPoint.y = 0f;
            if (bossForward.sqrMagnitude < 0.0001f || bossToPoint.sqrMagnitude < 0.0001f) return MeleeSector.Front;
            float dot = Vector3.Dot(bossForward.normalized, bossToPoint.normalized);
            float threshold = Mathf.Cos(Mathf.Clamp(rearHalfAngle, 20f, 80f) * Mathf.Deg2Rad);
            if (dot <= -threshold) return MeleeSector.Rear;
            return dot < threshold ? MeleeSector.Side : MeleeSector.Front;
        }

        public static bool IsAcceptableRear(NavMeshAgent agent, CombatSystem combat, CombatUnit boss,
            CombatUnit tank, GroundHazards hazards, float range, float rearHalfAngle, float rangeInset,
            float holdTolerance, float bodyMargin)
        {
            if (agent == null || boss == null || combat == null || range <= 0f) return false;
            Vector3 point = agent.transform.position;
            float distance = CombatPositionSearch.PlanarDistance(point, boss.transform.position);
            return Classify(boss.transform.forward, point - boss.transform.position, rearHalfAngle) == MeleeSector.Rear
                && distance >= Mathf.Max(0f, range - rangeInset - holdTolerance)
                && distance <= range - 0.05f
                && combat.HasLineOfSight(point, boss.transform.position)
                && (hazards == null || !hazards.IsUnsafe(point, agent.radius + 0.15f))
                && !OverlapsBody(point, boss, agent.radius, bodyMargin)
                && !OverlapsBody(point, tank, agent.radius, bodyMargin);
        }

        public static bool TryFind(NavMeshAgent agent, CombatSystem combat, CombatUnit boss,
            CombatUnit tank, GroundHazards hazards, float range, float rearHalfAngle, float rangeInset,
            float bodyMargin, out MeleePositionPlan plan)
        {
            plan = default;
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh || combat == null
                || boss == null || range <= 0f) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            float preferred = Mathf.Max(0.2f, range - Mathf.Max(rangeInset, agent.stoppingDistance + 0.15f));
            float[] radii = { preferred, range * 0.78f, range * 0.56f };
            float bestScore = float.PositiveInfinity;
            int bestSector = int.MaxValue, bestDestinationOverlap = int.MaxValue, bestRouteOverlap = int.MaxValue;
            Vector3 forward = boss.transform.forward; forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            // The current point participates so a valid side/front fallback can hold steadily.
            for (int index = -1; index < radii.Length * SamplesPerRing; index++)
            {
                Vector3 sample;
                if (index < 0) sample = agent.transform.position;
                else
                {
                    float theta = index % SamplesPerRing * (2f * Mathf.PI / SamplesPerRing);
                    float radius = radii[index / SamplesPerRing];
                    sample = boss.transform.position + (forward * Mathf.Cos(theta) + right * Mathf.Sin(theta)) * radius;
                    sample.y = boss.transform.position.y - (boss.TryGetComponent(out NavMeshAgent bossAgent)
                        ? bossAgent.baseOffset * Mathf.Abs(boss.transform.lossyScale.y) : 0f);
                }
                if (!NavMesh.SamplePosition(sample, out NavMeshHit hit, 0.4f, filter)) continue;
                Vector3 candidate = hit.position;
                float distance = CombatPositionSearch.PlanarDistance(candidate, boss.transform.position);
                if (distance > range - 0.05f || distance < Mathf.Min(range * 0.35f, 0.5f)
                    || !combat.HasLineOfSight(candidate, boss.transform.position)) continue;
                if (hazards != null && hazards.IsUnsafe(candidate, agent.radius + 0.15f)) continue;
                var direct = new NavMeshPath();
                if (!agent.CalculatePath(candidate, direct) || direct.status != NavMeshPathStatus.PathComplete
                    || (hazards != null && !hazards.PathIsSafe(direct, agent.radius + 0.15f))) continue;
                // Search a little inside the rear sector; the wider hold boundary absorbs
                // NavMesh sampling and arrival tolerance without stranding an edge candidate.
                Vector3 sectorPoint = index < 0 ? agent.transform.position : candidate;
                MeleeSector sector = Classify(forward, sectorPoint - boss.transform.position,
                    Mathf.Max(20f, rearHalfAngle - 5f));
                int destinationOverlap = OverlapsBody(candidate, boss, agent.radius, bodyMargin)
                    || OverlapsBody(candidate, tank, agent.radius, bodyMargin) ? 1 : 0;
                bool routeOverlap = CrossesBody(direct, boss, agent.radius, bodyMargin)
                    || CrossesBody(direct, tank, agent.radius, bodyMargin);
                Vector3 firstStep = candidate;
                float routeLength = GroundHazards.PathLength(direct);
                bool detour = false;
                if (routeOverlap && destinationOverlap == 0
                    && TryDetour(agent, filter, candidate, boss, tank, hazards, bodyMargin,
                        out Vector3 waypoint, out float detourLength))
                {
                    firstStep = waypoint; routeLength = detourLength; routeOverlap = false; detour = true;
                }
                int routeRank = routeOverlap ? 1 : 0;
                float score = routeLength + Mathf.Abs(distance - preferred) * 2f;
                if ((int)sector > bestSector || ((int)sector == bestSector && destinationOverlap > bestDestinationOverlap)
                    || ((int)sector == bestSector && destinationOverlap == bestDestinationOverlap && routeRank > bestRouteOverlap)
                    || ((int)sector == bestSector && destinationOverlap == bestDestinationOverlap && routeRank == bestRouteOverlap
                        && score >= bestScore - 0.01f)) continue;
                bestSector = (int)sector; bestDestinationOverlap = destinationOverlap;
                bestRouteOverlap = routeRank; bestScore = score;
                plan = new MeleePositionPlan(firstStep, candidate, sector, detour);
            }
            return !float.IsPositiveInfinity(bestScore);
        }

        private static bool TryDetour(NavMeshAgent agent, NavMeshQueryFilter filter, Vector3 destination,
            CombatUnit boss, CombatUnit tank, GroundHazards hazards, float margin,
            out Vector3 firstStep, out float length)
        {
            firstStep = default; length = float.PositiveInfinity;
            CombatUnit obstruction = CrossesBodyPath(agent, destination, boss, filter, margin) ? boss : tank;
            if (obstruction == null) return false;
            Vector3 center = obstruction.transform.position;
            Vector3 from = agent.transform.position - center, to = destination - center;
            from.y = to.y = 0f;
            float clearance = BodyRadius(obstruction) + agent.radius + margin;
            if (from.magnitude <= clearance + 0.01f || to.magnitude <= clearance + 0.01f) return false;
            float start = Mathf.Atan2(from.z, from.x), end = Mathf.Atan2(to.z, to.x);
            float orbitRadius = clearance / Mathf.Cos(OrbitStep * 0.5f) + 0.15f;
            for (int direction = -1; direction <= 1; direction += 2)
            {
                float angle = direction > 0 ? Mathf.Repeat(end - start, 2f * Mathf.PI)
                    : Mathf.Repeat(start - end, 2f * Mathf.PI);
                int legs = Mathf.Max(2, Mathf.CeilToInt(angle / OrbitStep));
                Vector3 prior = agent.transform.position, first = default;
                float total = 0f;
                bool valid = true;
                for (int step = 1; step <= legs; step++)
                {
                    Vector3 point = destination;
                    if (step < legs)
                    {
                        float theta = start + direction * angle * step / legs;
                        point = center + new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta)) * orbitRadius;
                        point.y = destination.y;
                        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 0.4f, filter)) { valid = false; break; }
                        point = hit.position;
                    }
                    var path = new NavMeshPath();
                    bool complete = step == 1 ? agent.CalculatePath(point, path)
                        : NavMesh.CalculatePath(prior, point, filter, path);
                    if (!complete || path.status != NavMeshPathStatus.PathComplete
                        || (hazards != null && (hazards.IsUnsafe(point, agent.radius + 0.15f)
                            || !hazards.PathIsSafe(path, agent.radius + 0.15f)))
                        || CrossesBody(path, boss, agent.radius, margin)
                        || CrossesBody(path, tank, agent.radius, margin)) { valid = false; break; }
                    if (step == 1) first = point;
                    total += GroundHazards.PathLength(path);
                    prior = point;
                }
                if (!valid || total >= length) continue;
                length = total; firstStep = first;
            }
            return !float.IsPositiveInfinity(length);
        }

        private static bool CrossesBodyPath(NavMeshAgent agent, Vector3 destination, CombatUnit body,
            NavMeshQueryFilter filter, float margin)
        {
            if (body == null) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(agent.transform.position, destination, filter, path)
                && path.status == NavMeshPathStatus.PathComplete && CrossesBody(path, body, agent.radius, margin);
        }

        private static bool OverlapsBody(Vector3 point, CombatUnit body, float agentRadius, float margin)
            => body != null && CombatPositionSearch.PlanarDistance(point, body.transform.position)
                < BodyRadius(body) + agentRadius + margin;

        private static bool CrossesBody(NavMeshPath path, CombatUnit body, float agentRadius, float margin)
        {
            if (body == null) return false;
            Vector3[] corners = path.corners;
            float radius = BodyRadius(body) + agentRadius + margin;
            for (int i = 1; i < corners.Length; i++)
                if (SegmentDistance(corners[i - 1], corners[i], body.transform.position) < radius) return true;
            return false;
        }

        public static float SegmentDistance(Vector3 a, Vector3 b, Vector3 point)
        {
            a.y = b.y = point.y = 0f;
            Vector3 edge = b - a;
            float t = edge.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(point - a, edge) / edge.sqrMagnitude) : 0f;
            return Vector3.Distance(a + edge * t, point);
        }

        private static float BodyRadius(CombatUnit body)
        {
            if (body.TryGetComponent(out CapsuleCollider capsule))
                return capsule.radius * Mathf.Max(Mathf.Abs(body.transform.lossyScale.x), Mathf.Abs(body.transform.lossyScale.z));
            if (body.TryGetComponent(out Collider collider))
                return Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
            return body.TryGetComponent(out NavMeshAgent agent) ? agent.radius : 0f;
        }
    }
}
