using UnityEngine;

namespace RaidCommander.Abilities
{
    /// <summary>Presentation only. Disabling this component never changes hazard membership or damage.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GroundHazardVisual : MonoBehaviour
    {
        private Mesh mesh;
        private MeshRenderer view;
        private MaterialPropertyBlock properties;
        private float hideAt = float.PositiveInfinity;
        private void Awake()
        {
            mesh = new Mesh { name = "Runtime hazard footprint" };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            view = GetComponent<MeshRenderer>();
            properties = new MaterialPropertyBlock();
            Hide();
        }
        public void Show(HazardFootprint footprint, Color color, float duration = float.PositiveInfinity)
        {
            if (!isActiveAndEnabled || footprint == null || footprint.Vertices.Count < 3) return;
            var vertices = new Vector3[footprint.Vertices.Count];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = transform.InverseTransformPoint(footprint.Vertices[i] + Vector3.up * 0.04f);
            var triangles = new int[(vertices.Length - 2) * 3];
            for (int i = 0; i < vertices.Length - 2; i++) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 2; triangles[i * 3 + 2] = i + 1; }
            mesh.Clear(); mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateBounds(); mesh.RecalculateNormals();
            properties.SetColor("_BaseColor", color); view.SetPropertyBlock(properties); view.enabled = true;
            hideAt = Time.time + duration;
        }
        public void Hide() { if (view != null) view.enabled = false; hideAt = float.PositiveInfinity; }
        private void Update() { if (Time.time >= hideAt) Hide(); }
        private void OnDisable() => Hide();
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
