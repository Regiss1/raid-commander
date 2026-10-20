using RaidCommander.Selection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RaidCommander.EditorTools
{
    public static class UnitSelectionSceneSetup
    {
        private const string ScenePath = "Assets/_Game/Scenes/RaidArena.unity";
        private const string RingMaterialPath = "Assets/_Game/Art/Materials/SelectionRing.mat";

        [MenuItem("Raid Commander/Configure Unit Selection")]
        public static void ConfigureGeneratedArena()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Unit selection configured in {ScenePath}.");
        }

        public static void Apply(Scene scene)
        {
            GameObject tankObject = FindByName(scene, "Tank");
            GameObject healerObject = FindByName(scene, "Healer");
            GameObject dpsObject = FindByName(scene, "DPS");
            GameObject rangedDpsObject = FindByName(scene, "Ranged DPS");
            GameObject cameraObject = FindByName(scene, "Main Camera");

            if (tankObject == null || healerObject == null || dpsObject == null || rangedDpsObject == null || cameraObject == null)
                throw new System.InvalidOperationException("Arena must contain Tank, Healer, DPS, Ranged DPS, and Main Camera objects before selection is configured.");

            SelectableUnit tank = ConfigureUnit(tankObject, PartyRole.Tank);
            SelectableUnit healer = ConfigureUnit(healerObject, PartyRole.Healer);
            SelectableUnit dps = ConfigureUnit(dpsObject, PartyRole.Dps);
            SelectableUnit rangedDps = ConfigureUnit(rangedDpsObject, PartyRole.RangedDps);
            Material ringMaterial = GetOrCreateRingMaterial();
            SelectionRing ring = ConfigureRing(scene, ringMaterial);

            UnitSelectionController controller = cameraObject.GetComponent<UnitSelectionController>();
            if (controller == null)
                controller = cameraObject.AddComponent<UnitSelectionController>();

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("selectionCamera").objectReferenceValue = cameraObject.GetComponent<Camera>();
            serializedController.FindProperty("raycastMask").intValue = ~0;
            serializedController.FindProperty("tank").objectReferenceValue = tank;
            serializedController.FindProperty("healer").objectReferenceValue = healer;
            serializedController.FindProperty("dps").objectReferenceValue = dps;
            serializedController.FindProperty("rangedDps").objectReferenceValue = rangedDps;
            serializedController.FindProperty("selectionRing").objectReferenceValue = ring;
            serializedController.FindProperty("raycastDistance").floatValue = 200f;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static SelectableUnit ConfigureUnit(GameObject unitObject, PartyRole role)
        {
            SelectableUnit unit = unitObject.GetComponent<SelectableUnit>();
            if (unit == null)
                unit = unitObject.AddComponent<SelectableUnit>();

            var serializedUnit = new SerializedObject(unit);
            serializedUnit.FindProperty("role").enumValueIndex = (int)role;
            serializedUnit.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(unit);
            return unit;
        }

        private static SelectionRing ConfigureRing(Scene scene, Material material)
        {
            GameObject ringObject = FindByName(scene, "Selection Ring");
            if (ringObject == null)
                ringObject = new GameObject("Selection Ring");

            if (ringObject.scene != scene)
                SceneManager.MoveGameObjectToScene(ringObject, scene);

            MeshFilter meshFilter = ringObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = ringObject.AddComponent<MeshFilter>();

            MeshRenderer meshRenderer = ringObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = ringObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;

            SelectionRing ring = ringObject.GetComponent<SelectionRing>();
            if (ring == null)
                ring = ringObject.AddComponent<SelectionRing>();

            var serializedRing = new SerializedObject(ring);
            serializedRing.FindProperty("ringMaterial").objectReferenceValue = material;
            serializedRing.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ring);
            return ring;
        }

        private static Material GetOrCreateRingMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(RingMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                material = new Material(shader) { name = "SelectionRing" };
                AssetDatabase.CreateAsset(material, RingMaterialPath);
            }

            Color color = new Color(1f, 0.82f, 0.12f, 1f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.4f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject FindByName(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                    return root;

                Transform[] children = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in children)
                {
                    if (child.name == objectName)
                        return child.gameObject;
                }
            }

            return null;
        }
    }
}
