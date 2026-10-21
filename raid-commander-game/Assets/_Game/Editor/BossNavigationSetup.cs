using System.Linq;
using RaidCommander.Combat;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace RaidCommander.EditorTools
{
    /// <summary>Separate static clearance for the larger boss; never changes the party bake/profile.</summary>
    public static class BossNavigationSetup
    {
        private const string Profile = "Raid Boss";
        private const string DataPath = "Assets/_Game/Scenes/RaidBossNavMesh.asset";
        public static void Apply(Scene scene, CombatUnit boss, bool rebake = false)
        {
            int type = -1;
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                int candidate = NavMesh.GetSettingsByIndex(i).agentTypeID;
                if (NavMesh.GetSettingsNameFromID(candidate) == Profile) { type = candidate; break; }
            }
            if (type == -1)
            {
                type = NavMesh.CreateSettings().agentTypeID;
                var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
                var profiles = settings.FindProperty("m_Settings");
                for (int i = 0; i < profiles.arraySize; i++)
                {
                    var profile = profiles.GetArrayElementAtIndex(i);
                    if (profile.FindPropertyRelative("agentTypeID").intValue != type) continue;
                    profile.FindPropertyRelative("agentRadius").floatValue = 0.95f;
                    profile.FindPropertyRelative("agentHeight").floatValue = 3f;
                    profile.FindPropertyRelative("agentClimb").floatValue = 0.4f;
                    settings.FindProperty("m_SettingNames").GetArrayElementAtIndex(i).stringValue = Profile;
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings.targetObject); AssetDatabase.SaveAssetIfDirty(settings.targetObject);
            }
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Boss Navigation");
            if (root == null) { root = new GameObject("Boss Navigation"); SceneManager.MoveGameObjectToScene(root, scene); }
            var surface = root.GetComponent<NavMeshSurface>();
            if (surface == null) surface = root.AddComponent<NavMeshSurface>();
            surface.agentTypeID = type; surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.layerMask = LayerMask.GetMask("Default", "Ground");
            surface.overrideVoxelSize = true; surface.voxelSize = 0.1f; surface.minRegionArea = 0.1f; surface.buildHeightMesh = true;
            NavMeshData saved = AssetDatabase.LoadAssetAtPath<NavMeshData>(DataPath);
            if (rebake || saved == null)
            {
                Physics.SyncTransforms(); surface.BuildNavMesh();
                NavMeshData baked = surface.navMeshData;
                if (baked == null) throw new System.InvalidOperationException("Boss NavMesh bake failed.");
                surface.RemoveData();
                if (saved == null) { baked.name = "RaidBossNavMesh"; AssetDatabase.CreateAsset(baked, DataPath); saved = baked; }
                else if (saved != baked) { EditorUtility.CopySerialized(baked, saved); Object.DestroyImmediate(baked); EditorUtility.SetDirty(saved); }
                AssetDatabase.SaveAssetIfDirty(saved);
            }
            surface.RemoveData(); surface.navMeshData = saved; surface.AddData();
            var agent = boss.GetComponent<NavMeshAgent>();
            if (agent.agentTypeID != type) { agent.agentTypeID = type; agent.radius = 0.9f; }
            agent.height = boss.GetComponent<Collider>().bounds.size.y / Mathf.Abs(boss.transform.lossyScale.y);
            EditorUtility.SetDirty(surface); EditorUtility.SetDirty(agent);
        }
    }
}
