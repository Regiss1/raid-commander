using System.Linq;
using RaidCommander.Abilities;
using RaidCommander.Combat;
using RaidCommander.Encounters;
using RaidCommander.Movement;
using RaidCommander.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace RaidCommander.EditorTools
{
    public static class BossEncounterSceneSetup
    {
        public const string AbilityPath = "Assets/_Game/Data/BaseGame/Abilities/WallOfFlames.asset";
        [MenuItem("Raid Commander/Configure Boss Encounter and Wall of Flames")]
        public static void ConfigureGeneratedArena() => PartyAiSceneSetup.ConfigureGeneratedArena();
        public static void Apply(Scene scene, CombatSystem combat, CombatUnit[] party, CombatUnit boss)
        {
            GameObject Root(string name)
            {
                GameObject found = scene.GetRootGameObjects().FirstOrDefault(r => r.name == name);
                if (found != null) return found;
                var result = new GameObject(name); SceneManager.MoveGameObjectToScene(result, scene); return result;
            }
            var area = Root("Encounter Boundary");
            bool newBoundary = area.GetComponent<ArenaBoundary>() == null;
            var boundary = Get<ArenaBoundary>(area);
            if (newBoundary)
            {
                // Explicit volume matches the inner faces of the saved arena's perimeter walls.
                // Later applications preserve this authored boundary and all customized geometry.
                area.layer = 2;
                var volume = area.GetComponent<BoxCollider>(); volume.isTrigger = true;
                volume.size = new Vector3(33f, 0.1f, 33f); area.transform.position = Vector3.zero;
            }
            var hazards = Get<GroundHazards>(Root("Encounter Hazards"));
            Set(hazards, "boundary", boundary);
            var visual = Get<GroundHazardVisual>(Root("Boss Ground Telegraph"));
            const string materialPath = "Assets/_Game/Art/Materials/BossTelegraph.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "BossTelegraph" };
                material.SetFloat("_Surface", 1f); material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
                AssetDatabase.CreateAsset(material, materialPath);
            }
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            visual.GetComponent<MeshRenderer>().enabled = false;
            var ability = AssetDatabase.LoadAssetAtPath<BossAbilityDefinition>(AbilityPath);
            if (ability == null)
            {
                ability = ScriptableObject.CreateInstance<BossAbilityDefinition>(); ability.name = "WallOfFlames";
                AssetDatabase.CreateAsset(ability, AbilityPath);
            }
            bool newAgent = boss.GetComponent<NavMeshAgent>() == null;
            var agent = Get<NavMeshAgent>(boss.gameObject);
            if (newAgent)
            {
                agent.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID;
                agent.radius = 0.65f; agent.height = 3f;
                agent.baseOffset = (boss.transform.position.y - boss.GetComponent<Collider>().bounds.min.y) / Mathf.Abs(boss.transform.lossyScale.y);
                agent.speed = 3f; agent.acceleration = 18f; agent.stoppingDistance = 0.08f;
                agent.autoBraking = true; agent.autoRepath = true; agent.updateUpAxis = false;
            }
            agent.updateRotation = false; agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            // Agent base offset scales with this placeholder's 1.5 Y scale; collider bounds are already world-space.
            agent.baseOffset = (boss.transform.position.y - boss.GetComponent<Collider>().bounds.min.y) / Mathf.Abs(boss.transform.lossyScale.y);
            BossNavigationSetup.Apply(scene, boss);
            Get<UnitMovement>(boss.gameObject);
            var controller = Get<BossBasicAttack>(boss.gameObject);
            bool newRunner = boss.GetComponent<BossAbilityRunner>() == null;
            var runner = Get<BossAbilityRunner>(boss.gameObject);
            var controllerSettings = new SerializedObject(controller);
            if (newRunner) controllerSettings.FindProperty("attackInterval").floatValue = 3f;
            var members = controllerSettings.FindProperty("party"); members.arraySize = party.Length;
            for (int i = 0; i < party.Length; i++) members.GetArrayElementAtIndex(i).objectReferenceValue = party[i];
            controllerSettings.ApplyModifiedPropertiesWithoutUndo();
            Set(runner, "combat", combat); Set(runner, "boundary", boundary); Set(runner, "hazards", hazards); Set(runner, "presentation", visual);
            var settings = new SerializedObject(runner);
            var brains = settings.FindProperty("party"); brains.arraySize = party.Length;
            for (int i = 0; i < party.Length; i++)
            {
                var brain = party[i].GetComponent<PartyRoleBrain>(); brains.GetArrayElementAtIndex(i).objectReferenceValue = brain;
                Set(brain, "hazards", hazards);
            }
            if (newRunner) { var loadout = settings.FindProperty("loadout"); loadout.arraySize = 1; loadout.GetArrayElementAtIndex(0).objectReferenceValue = ability; }
            settings.ApplyModifiedPropertiesWithoutUndo();
            foreach (Object changed in new Object[] { area, area.GetComponent<BoxCollider>(), boundary, hazards, visual,
                visual.GetComponent<MeshRenderer>(), agent, controller, runner }) EditorUtility.SetDirty(changed);
            // CreateAsset persists new definitions/materials above. Existing assets are not changed here;
            // leave any requester-owned unsaved Inspector edits untouched.
        }
        private static T Get<T>(GameObject obj) where T : Component
        {
            T component = obj.GetComponent<T>();
            return component != null ? component : obj.AddComponent<T>();
        }
        private static void Set(Object owner, string field, Object value)
        {
            var settings = new SerializedObject(owner); settings.FindProperty(field).objectReferenceValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(owner);
        }
    }
}
