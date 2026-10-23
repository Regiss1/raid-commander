using System.Collections.Generic;
using RaidCommander.Encounters;
using UnityEngine;
using UnityEngine.AI;

namespace RaidCommander.Abilities
{
    /// <summary>Encounter-local pending hazards; impact visuals never remain in this collection.</summary>
    public sealed class GroundHazards : MonoBehaviour
    {
        [SerializeField] private ArenaBoundary boundary;
        private sealed class Pending
        {
            public HazardFootprint Footprint;
            public string Identity;
        }
        private readonly Dictionary<object, Pending> pending = new Dictionary<object, Pending>();
        private int nextCastId = 1;
        public int Count => pending.Count;
        public void Register(object owner, HazardFootprint footprint, string name = null, int spellId = 0)
        {
            if (owner == null || footprint == null) return;
            if (pending.TryGetValue(owner, out Pending existing))
            {
                existing.Footprint = footprint;
                return;
            }
            int castId = nextCastId++;
            pending.Add(owner, new Pending { Footprint = footprint,
                Identity = (string.IsNullOrWhiteSpace(name) ? "Pending hazard" : name)
                    + " [cast " + castId + "]" + (spellId > 0 ? " (spell " + spellId + ")" : "") });
        }
        public void Remove(object owner) => pending.Remove(owner);
        public string[] ContainingIdentities(Vector3 point)
        {
            var names = new List<string>();
            foreach (Pending hazard in pending.Values)
                if (hazard.Footprint.Contains(point)) names.Add(hazard.Identity);
            names.Sort(System.StringComparer.Ordinal);
            return names.ToArray();
        }
        public bool IsUnsafe(Vector3 point, float clearance = 0f)
        {
            foreach (Pending hazard in pending.Values) if (hazard.Footprint.Contains(point, clearance)) return true;
            return false;
        }
        public bool PathIsSafe(NavMeshPath path, float clearance)
        {
            Vector3[] corners = path.corners;
            foreach (Pending hazard in pending.Values)
                for (int i = 1; i < corners.Length; i++)
                    if (hazard.Footprint.IntersectsSegment(corners[i - 1], corners[i], clearance)) return false;
            return true;
        }
        public bool TryEscape(NavMeshAgent agent, out Vector3 destination)
        {
            destination = default;
            if (agent == null || !agent.isOnNavMesh || boundary == null) return false;
            float clearance = agent.radius + 0.15f;
            Vector3 position = agent.transform.position;
            var candidates = new List<Vector3>();
            foreach (Pending hazard in pending.Values)
            {
                HazardFootprint footprint = hazard.Footprint;
                for (int i = 0; i < footprint.Vertices.Count; i++)
                {
                    Vector3 a = footprint.Vertices[i], b = footprint.Vertices[(i + 1) % footprint.Vertices.Count];
                    Vector3 edge = b - a;
                    float t = edge.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(position - a, edge) / edge.sqrMagnitude) : 0f;
                    Vector3 normal = footprint.Outward(i);
                    candidates.Add(a + edge * t + normal * (clearance + 0.03f));
                    // Alternatives along the edge find exits around cover and overlapping warnings.
                    for (int sample = 0; sample <= 8; sample++) candidates.Add(Vector3.Lerp(a, b, sample / 8f) + normal * (clearance + 0.03f));
                }
            }
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            var path = new NavMeshPath();
            float best = float.PositiveInfinity;
            foreach (Vector3 candidate in candidates)
            {
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.35f, filter)
                    || !boundary.Contains(hit.position, agent.radius) || IsUnsafe(hit.position, clearance)) continue;
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float length = PathLength(path);
                if (length >= best) continue;
                best = length; destination = hit.position;
            }
            return !float.IsPositiveInfinity(best);
        }
        public static float PathLength(NavMeshPath path)
        {
            float length = 0f; Vector3[] corners = path.corners;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }
        private void OnDisable() { pending.Clear(); nextCastId = 1; }
    }
}
