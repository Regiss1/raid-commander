using System.Collections.Generic;
using UnityEngine;

namespace RaidCommander.Combat
{
    /// <summary>Tests solid obstacle footprints with 3D physics, independently of obstacle/unit height.</summary>
    internal sealed class CombatLineOfSight
    {
        // PhysX sweeps need a positive radius. This is a 1 mm tolerance around the sight segment.
        private const float QueryRadius = 0.001f;
        private readonly List<Collider> obstacles = new List<Collider>();

        public bool IsClear(Transform obstacleRoot, Vector3 source, Vector3 target)
        {
            if (obstacleRoot == null) return true;
            Physics.SyncTransforms();
            obstacleRoot.GetComponentsInChildren(false, obstacles);
            float bottom = float.PositiveInfinity;
            float top = float.NegativeInfinity;
            foreach (Collider obstacle in obstacles)
            {
                if (!IsBlocker(obstacle, obstacleRoot)) continue;
                Bounds bounds = obstacle.bounds;
                bottom = Mathf.Min(bottom, bounds.min.y);
                top = Mathf.Max(top, bounds.max.y);
            }
            if (float.IsPositiveInfinity(bottom)) return true;

            // A vertical capsule spanning every obstacle's full height sweeps its actual XZ footprint.
            // No collider is moved, resized, or converted into an axis-aligned footprint approximation.
            Vector3 lower = new Vector3(source.x, bottom - QueryRadius, source.z);
            Vector3 upper = new Vector3(source.x, top + QueryRadius, source.z);
            foreach (Collider overlap in Physics.OverlapCapsule(lower, upper, QueryRadius, ~0, QueryTriggerInteraction.Ignore))
                if (IsBlocker(overlap, obstacleRoot)) return false;

            Vector3 direction = target - source;
            direction.y = 0f;
            float distance = direction.magnitude;
            if (distance <= Mathf.Epsilon) return true;
            foreach (RaycastHit hit in Physics.CapsuleCastAll(lower, upper, QueryRadius, direction / distance,
                distance, ~0, QueryTriggerInteraction.Ignore))
                if (IsBlocker(hit.collider, obstacleRoot)) return false;
            return true;
        }

        private static bool IsBlocker(Collider collider, Transform root) =>
            collider != null && collider.enabled && collider.gameObject.activeInHierarchy
            && !collider.isTrigger && collider.transform.IsChildOf(root);
    }
}
