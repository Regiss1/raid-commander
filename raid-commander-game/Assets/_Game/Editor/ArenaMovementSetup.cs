using System;
using RaidCommander.Movement;
using RaidCommander.Selection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace RaidCommander.EditorTools
{
    public static class ArenaMovementSetup
    {
        public const string ScenePath = "Assets/_Game/Scenes/RaidArena.unity";
        private const string DataPath = "Assets/_Game/Scenes/RaidArenaNavMesh.asset";
        private const string ArtPath = "Assets/_Game/Art/Materials/";

        [MenuItem("Raid Commander/Configure Movement and Bake Navigation")]
        public static void ConfigureGeneratedArena()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save the arena movement setup.");
            AssetDatabase.SaveAssets();
            Debug.Log("MOVEMENT SETUP: baked NavMesh and saved agents, player input, and destination marker.");
        }

        public static void Apply(Scene scene)
        {
            // The project uses one Humanoid agent profile. Clearance covers the widest party capsule.
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            var humanoid = settings.FindProperty("m_Settings").GetArrayElementAtIndex(0);
            humanoid.FindPropertyRelative("agentRadius").floatValue = 0.7f;
            humanoid.FindPropertyRelative("agentHeight").floatValue = 2f;
            humanoid.FindPropertyRelative("agentClimb").floatValue = 0.4f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            int agentType = NavMesh.GetSettingsByIndex(0).agentTypeID;

            MarkNonWalkable(Require(scene, "Obstacles"));
            var bossModifier = Require(scene, "Raid Boss").GetComponent<NavMeshModifier>();
            if (bossModifier != null)
                UnityEngine.Object.DestroyImmediate(bossModifier);
            int friendlyLayer = LayerMask.NameToLayer("FriendlyUnit");
            int hostileLayer = LayerMask.NameToLayer("HostileUnit");
            Physics.IgnoreLayerCollision(friendlyLayer, friendlyLayer, true);
            Physics.IgnoreLayerCollision(friendlyLayer, hostileLayer, true);
            Physics.IgnoreLayerCollision(hostileLayer, hostileLayer, true);
            EditorUtility.SetDirty(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]);
            var surface = GetOrAdd<NavMeshSurface>(FindOrCreateRoot(scene, "Arena Navigation"));
            surface.agentTypeID = agentType;
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Default", "Ground");
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;
            surface.minRegionArea = 0.1f;
            surface.buildHeightMesh = true;
            Physics.SyncTransforms();
            BakeAndSave(surface);

            string[] names = { "Tank", "Healer", "DPS" };
            for (int i = 0; i < names.Length; i++)
            {
                GameObject unit = Require(scene, names[i]);
                var agent = GetOrAdd<NavMeshAgent>(unit);
                agent.agentTypeID = agentType;
                agent.radius = 0.65f;
                agent.height = 2f;
                agent.baseOffset = unit.transform.position.y - unit.GetComponent<Collider>().bounds.min.y;
                agent.speed = 4.5f;
                agent.acceleration = 18f;
                agent.angularSpeed = 540f;
                agent.stoppingDistance = 0.15f;
                agent.autoBraking = true;
                agent.autoRepath = true;
                agent.updateUpAxis = false;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                agent.avoidancePriority = 40 + i;
                GetOrAdd<UnitMovement>(unit);
                EditorUtility.SetDirty(agent);
            }

            DestinationMarker marker = ConfigureMarker(scene);
            GameObject camera = Require(scene, "Main Camera");
            var input = GetOrAdd<PlayerMovementInput>(camera);
            var inputSettings = new SerializedObject(input);
            inputSettings.FindProperty("worldCamera").objectReferenceValue = camera.GetComponent<Camera>();
            inputSettings.FindProperty("selection").objectReferenceValue = camera.GetComponent<UnitSelectionController>();
            inputSettings.FindProperty("destinationMarker").objectReferenceValue = marker;
            inputSettings.FindProperty("groundMask").intValue = LayerMask.GetMask("Ground");
            inputSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(surface);
        }

        private static void BakeAndSave(NavMeshSurface surface)
        {
            NavMeshData saved = AssetDatabase.LoadAssetAtPath<NavMeshData>(DataPath);
            surface.BuildNavMesh();
            NavMeshData baked = surface.navMeshData;
            if (baked == null || NavMesh.CalculateTriangulation().vertices.Length == 0)
                throw new InvalidOperationException("Arena NavMesh bake produced no walkable geometry.");
            surface.RemoveData();
            if (saved == null)
            {
                baked.name = "RaidArenaNavMesh";
                AssetDatabase.CreateAsset(baked, DataPath);
                saved = baked;
            }
            else if (saved != baked)
            {
                EditorUtility.CopySerialized(baked, saved);
                UnityEngine.Object.DestroyImmediate(baked);
                EditorUtility.SetDirty(saved);
            }
            surface.navMeshData = saved;
            surface.AddData();
        }

        private static void MarkNonWalkable(GameObject target)
        {
            var modifier = GetOrAdd<NavMeshModifier>(target);
            modifier.overrideArea = true;
            modifier.area = NavMesh.GetAreaFromName("Not Walkable");
            modifier.applyToChildren = true;
            EditorUtility.SetDirty(modifier);
        }

        private static DestinationMarker ConfigureMarker(Scene scene)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "DestinationMarker.mat");
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    throw new InvalidOperationException("URP Unlit shader is required for the marker.");
                material = new Material(shader) { name = "DestinationMarker" };
                AssetDatabase.CreateAsset(material, ArtPath + "DestinationMarker.mat");
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ArtPath + "DestinationMarkerMesh.asset");
            if (mesh == null)
            {
                const int count = 48;
                var vertices = new Vector3[count * 2];
                var triangles = new int[count * 6];
                for (int i = 0; i < count; i++)
                {
                    float angle = i * 2f * Mathf.PI / count;
                    Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    vertices[i * 2] = radial * 0.5f;
                    vertices[i * 2 + 1] = radial * 0.36f;
                    int outer = i * 2;
                    int next = (i + 1) % count * 2;
                    int offset = i * 6;
                    triangles[offset] = outer;
                    triangles[offset + 1] = outer + 1;
                    triangles[offset + 2] = next + 1;
                    triangles[offset + 3] = outer;
                    triangles[offset + 4] = next + 1;
                    triangles[offset + 5] = next;
                }
                mesh = new Mesh { name = "DestinationMarkerMesh", vertices = vertices, triangles = triangles };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, ArtPath + "DestinationMarkerMesh.asset");
            }

            GameObject marker = FindOrCreateRoot(scene, "Destination Marker");
            GetOrAdd<MeshFilter>(marker).sharedMesh = mesh;
            GetOrAdd<MeshRenderer>(marker).sharedMaterial = material;
            return GetOrAdd<DestinationMarker>(marker);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static GameObject FindOrCreateRoot(Scene scene, string name)
        {
            GameObject target = Find(scene, name);
            if (target != null)
                return target;
            target = new GameObject(name);
            SceneManager.MoveGameObjectToScene(target, scene);
            return target;
        }

        private static GameObject Require(Scene scene, string name) =>
            Find(scene, name) ?? throw new InvalidOperationException("Missing arena object: " + name);

        private static GameObject Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name)
                        return child.gameObject;
            return null;
        }
    }
}
