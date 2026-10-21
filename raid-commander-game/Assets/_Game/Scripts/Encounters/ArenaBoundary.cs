using UnityEngine;

namespace RaidCommander.Encounters
{
    /// <summary>An explicit horizontal playable volume, independent of wall names and camera framing.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ArenaBoundary : MonoBehaviour
    {
        // Collider.bounds can be empty during another object's early OnEnable, before
        // physics registers this collider. The authored volume is already available.
        public Bounds Bounds
        {
            get
            {
                BoxCollider box = GetComponent<BoxCollider>();
                if (box == null) return default;
                Vector3 half = box.size * 0.5f;
                Vector3 x = transform.TransformVector(Vector3.right * half.x);
                Vector3 y = transform.TransformVector(Vector3.up * half.y);
                Vector3 z = transform.TransformVector(Vector3.forward * half.z);
                Vector3 extents = Abs(x) + Abs(y) + Abs(z);
                return new Bounds(transform.TransformPoint(box.center), extents * 2f);
            }
        }
        public float GroundY => Bounds.center.y;
        public bool Contains(Vector3 point, float clearance = 0f)
        {
            Bounds b = Bounds;
            return point.x >= b.min.x + clearance && point.x <= b.max.x - clearance
                && point.z >= b.min.z + clearance && point.z <= b.max.z - clearance;
        }
        public bool IsValid
        {
            get
            {
                BoxCollider box = GetComponent<BoxCollider>();
                if (!isActiveAndEnabled || box == null || !box.enabled) return false;
                Bounds bounds = Bounds;
                return Finite(bounds.center) && Finite(bounds.size) && bounds.size.x > 0.01f && bounds.size.z > 0.01f;
            }
        }
        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
