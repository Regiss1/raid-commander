using System.Collections.Generic;
using RaidCommander.Encounters;
using UnityEngine;

namespace RaidCommander.Abilities
{
    /// <summary>Immutable convex XZ polygon shared by damage, escape and mesh presentation.</summary>
    public sealed class HazardFootprint
    {
        public IReadOnlyList<Vector3> Vertices { get; }
        private HazardFootprint(List<Vector3> vertices) { Vertices = vertices.AsReadOnly(); }
        public static HazardFootprint Rectangle(Vector3 origin, Vector3 forward, float width, float length, ArenaBoundary boundary)
        {
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.001f ? Vector3.forward : forward.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward) * width * 0.5f;
            origin.y = boundary.GroundY;
            var polygon = new List<Vector3> { origin - side, origin + side, origin + side + forward * length, origin - side + forward * length };
            Bounds b = boundary.Bounds;
            polygon = Clip(polygon, Vector3.right, b.min.x);
            polygon = Clip(polygon, Vector3.left, -b.max.x);
            polygon = Clip(polygon, Vector3.forward, b.min.z);
            polygon = Clip(polygon, Vector3.back, -b.max.z);
            return new HazardFootprint(polygon);
        }
        private static List<Vector3> Clip(List<Vector3> polygon, Vector3 normal, float limit)
        {
            var result = new List<Vector3>();
            if (polygon.Count == 0) return result;
            Vector3 previous = polygon[polygon.Count - 1];
            float before = Vector3.Dot(previous, normal) - limit;
            foreach (Vector3 current in polygon)
            {
                float after = Vector3.Dot(current, normal) - limit;
                if ((before >= 0f) != (after >= 0f)) result.Add(Vector3.Lerp(previous, current, before / (before - after)));
                if (after >= 0f) result.Add(current);
                previous = current; before = after;
            }
            return result;
        }
        public Vector3 Outward(int edge)
        {
            Vector3 delta = Vertices[(edge + 1) % Vertices.Count] - Vertices[edge];
            return Vector3.Cross(Vector3.up, delta).normalized;
        }
        public bool Contains(Vector3 point, float margin = 0f)
        {
            if (Vertices.Count < 3) return false;
            for (int i = 0; i < Vertices.Count; i++)
                if (Vector3.Dot(point - Vertices[i], Outward(i)) > margin + 0.001f) return false;
            return true;
        }
        public bool IntersectsSegment(Vector3 a, Vector3 b, float margin)
        {
            float enter = 0f, exit = 1f;
            if (Vertices.Count < 3) return false;
            for (int i = 0; i < Vertices.Count; i++)
            {
                Vector3 n = Outward(i);
                float start = Vector3.Dot(a - Vertices[i], n) - margin;
                float delta = Vector3.Dot(b - a, n);
                if (Mathf.Abs(delta) < 0.00001f) { if (start > 0f) return false; continue; }
                float t = -start / delta;
                if (delta < 0f) enter = Mathf.Max(enter, t); else exit = Mathf.Min(exit, t);
                if (enter > exit) return false;
            }
            return true;
        }
    }
}
