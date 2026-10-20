using System;
using RaidCommander.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RaidCommander.EditorTools
{
    /// <summary>Adds the fourth party member to the current arena without rebuilding its geometry.</summary>
    public static class RangedDpsSceneSetup
    {
        [MenuItem("Raid Commander/Configure Ranged DPS")]
        public static void ConfigureGeneratedArena()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ArenaMovementSetup.ScenePath)
                throw new InvalidOperationException("Open RaidArena in Edit mode before configuring Ranged DPS.");
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("RANGED DPS SETUP: saved four party units, selection, navigation, combat and tracer.");
        }

        public static void Apply(Scene scene)
        {
            EnsureUnit(scene);
            UnitSelectionSceneSetup.Apply(scene);
            ArenaMovementSetup.Apply(scene);
            CombatSceneSetup.Apply(scene);
        }

        public static GameObject EnsureUnit(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Ranged DPS") return child.gameObject;

            Transform party = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "Party - Friendly Units") party = root.transform;
            if (party == null) throw new InvalidOperationException("Missing party root in arena.");

            var unit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unit.name = "Ranged DPS";
            SceneManager.MoveGameObjectToScene(unit, scene);
            unit.transform.SetParent(party, false);
            unit.transform.position = new Vector3(-3f, 1f, -7f);
            unit.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
            unit.layer = LayerMask.NameToLayer("FriendlyUnit");
            unit.GetComponent<Renderer>().sharedMaterial = MaterialAsset("RangedDpsPlaceholder",
                "Universal Render Pipeline/Lit", new Color(0f, 0.9f, 1f, 1f));
            return unit;
        }

        public static void ConfigureTracer(CombatUnit unit, CombatSystem combat)
        {
            LineRenderer line = unit.GetComponent<LineRenderer>();
            if (line == null) line = unit.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = MaterialAsset("RangedAttackTracer", "Universal Render Pipeline/Unlit", Color.cyan);
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.09f;
            line.endWidth = 0.04f;
            line.startColor = Color.cyan;
            line.endColor = Color.cyan;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            var tracer = unit.GetComponent<RangedAttackTracer>();
            if (tracer == null) tracer = unit.gameObject.AddComponent<RangedAttackTracer>();
            var settings = new SerializedObject(tracer);
            settings.FindProperty("combat").objectReferenceValue = combat;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(line);
            EditorUtility.SetDirty(tracer);
        }

        private static Material MaterialAsset(string name, string shaderName, Color color)
        {
            string path = "Assets/_Game/Art/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing shader: " + shaderName);
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
