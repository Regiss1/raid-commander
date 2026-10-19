using UnityEngine;

namespace RaidCommander.Selection
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SelectionRing : MonoBehaviour
    {
        private const int SegmentCount = 64;
        private const float InnerRadius = 0.68f;
        private const float OuterRadius = 0.82f;
        private const float GroundOffset = 0.035f;

        [SerializeField] private Material ringMaterial;

        private MeshRenderer ringRenderer;
        private SelectableUnit target;

        private void Awake()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            ringRenderer = GetComponent<MeshRenderer>();
            meshFilter.sharedMesh = CreateRingMesh();
            if (ringMaterial != null)
                ringRenderer.sharedMaterial = ringMaterial;
            ringRenderer.enabled = false;
            transform.rotation = Quaternion.identity;
        }

        public void SetTarget(SelectableUnit newTarget)
        {
            target = newTarget;
            if (ringRenderer != null)
                ringRenderer.enabled = target != null;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Collider targetCollider = target.SelectionCollider;
            if (targetCollider == null)
                targetCollider = target.GetComponent<Collider>();

            float groundY = targetCollider != null
                ? targetCollider.bounds.min.y + GroundOffset
                : target.transform.position.y + GroundOffset;

            Vector3 targetPosition = target.transform.position;
            transform.SetPositionAndRotation(
                new Vector3(targetPosition.x, groundY, targetPosition.z),
                Quaternion.identity);
        }

        private static Mesh CreateRingMesh()
        {
            var vertices = new Vector3[SegmentCount * 2];
            var triangles = new int[SegmentCount * 6];

            for (int i = 0; i < SegmentCount; i++)
            {
                float angle = i * Mathf.PI * 2f / SegmentCount;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                vertices[i * 2] = new Vector3(x * OuterRadius, 0f, z * OuterRadius);
                vertices[i * 2 + 1] = new Vector3(x * InnerRadius, 0f, z * InnerRadius);

                int next = (i + 1) % SegmentCount;
                int triangle = i * 6;
                int outer = i * 2;
                int inner = outer + 1;
                int nextOuter = next * 2;
                int nextInner = nextOuter + 1;

                // Winding faces upward so the ring lies visibly on the XZ plane.
                triangles[triangle] = outer;
                triangles[triangle + 1] = inner;
                triangles[triangle + 2] = nextInner;
                triangles[triangle + 3] = outer;
                triangles[triangle + 4] = nextInner;
                triangles[triangle + 5] = nextOuter;
            }

            var mesh = new Mesh { name = "Unit Selection Ring" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.name == "Unit Selection Ring")
            {
                if (Application.isPlaying)
                    Destroy(meshFilter.sharedMesh);
                else
                    DestroyImmediate(meshFilter.sharedMesh);
            }
        }
    }
}
