using System.Collections.Generic;
using RaidCommander.CameraControl;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RaidCommander.EditorTools
{
    public static class PlayableArenaSetup
    {
        private const string ScenePath = "Assets/_Game/Scenes/RaidArena.unity";
        private const string MaterialFolder = "Assets/_Game/Art/Materials";
        private const int GroundLayer = 8;
        private const int FriendlyLayer = 9;
        private const int HostileLayer = 10;

        [MenuItem("Raid Commander/Generate Playable Arena")]
        public static void GeneratePlayableArena()
        {
            EnsureLayerNames();
            EnsureMaterialFolder();

            Material groundMaterial = GetOrCreateMaterial("ArenaGround", new Color(0.22f, 0.31f, 0.27f));
            Material obstacleMaterial = GetOrCreateMaterial("ArenaStone", new Color(0.34f, 0.36f, 0.39f));
            Material tankMaterial = GetOrCreateMaterial("TankPlaceholder", new Color(0.2f, 0.52f, 0.95f));
            Material healerMaterial = GetOrCreateMaterial("HealerPlaceholder", new Color(0.22f, 0.82f, 0.48f));
            Material dpsMaterial = GetOrCreateMaterial("DpsPlaceholder", new Color(0.98f, 0.65f, 0.18f));
            Material bossMaterial = GetOrCreateMaterial("BossPlaceholder", new Color(0.78f, 0.18f, 0.2f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject arena = new GameObject("Arena");
            GameObject party = new GameObject("Party - Friendly Units");
            GameObject hostiles = new GameObject("Hostile Units");
            GameObject obstacles = new GameObject("Obstacles");
            GameObject lighting = new GameObject("Lighting");

            GameObject floor = CreatePrimitive(
                PrimitiveType.Plane, "Arena Ground", arena.transform,
                new Vector3(0f, 0f, 0f), new Vector3(3.6f, 1f, 3.6f), GroundLayer, groundMaterial);
            floor.isStatic = true;

            CreatePrimitive(PrimitiveType.Cube, "West Wall", obstacles.transform,
                new Vector3(-17f, 1.5f, 0f), new Vector3(1f, 3f, 36f), 0, obstacleMaterial);
            CreatePrimitive(PrimitiveType.Cube, "East Wall", obstacles.transform,
                new Vector3(17f, 1.5f, 0f), new Vector3(1f, 3f, 36f), 0, obstacleMaterial);
            CreatePrimitive(PrimitiveType.Cube, "North Wall", obstacles.transform,
                new Vector3(0f, 1.5f, 17f), new Vector3(34f, 3f, 1f), 0, obstacleMaterial);
            CreatePrimitive(PrimitiveType.Cube, "South Wall", obstacles.transform,
                new Vector3(0f, 1.5f, -17f), new Vector3(34f, 3f, 1f), 0, obstacleMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Central Pillar", obstacles.transform,
                new Vector3(-5f, 1.4f, 3f), new Vector3(2.4f, 2.8f, 2.4f), 0, obstacleMaterial);
            CreatePrimitive(PrimitiveType.Cylinder, "East Column", obstacles.transform,
                new Vector3(6f, 1.2f, 5f), new Vector3(1.1f, 1.2f, 1.1f), 0, obstacleMaterial);
            CreatePrimitive(PrimitiveType.Cube, "South Cover", obstacles.transform,
                new Vector3(4f, 0.8f, -7f), new Vector3(5f, 1.6f, 1.5f), 0, obstacleMaterial);
            CreatePrimitive(PrimitiveType.Cube, "Northwest Cover", obstacles.transform,
                new Vector3(-10f, 0.9f, 10f), new Vector3(3f, 1.8f, 2f), 0, obstacleMaterial);

            CreatePrimitive(PrimitiveType.Capsule, "Tank", party.transform,
                new Vector3(-2f, 1f, -5f), new Vector3(1.15f, 1f, 1.15f), FriendlyLayer, tankMaterial);
            CreatePrimitive(PrimitiveType.Capsule, "Healer", party.transform,
                new Vector3(0f, 1f, -7f), new Vector3(0.9f, 1f, 0.9f), FriendlyLayer, healerMaterial);
            CreatePrimitive(PrimitiveType.Capsule, "DPS", party.transform,
                new Vector3(2f, 1f, -5f), new Vector3(0.9f, 1f, 0.9f), FriendlyLayer, dpsMaterial);
            CreatePrimitive(PrimitiveType.Capsule, "Raid Boss", hostiles.transform,
                new Vector3(0f, 1.5f, 8f), new Vector3(1.7f, 1.5f, 1.7f), HostileLayer, bossMaterial);

            CreateLighting(lighting.transform);
            CreateCamera(arena.transform);
            RangedDpsSceneSetup.EnsureUnit(scene);
            UnitSelectionSceneSetup.Apply(scene);
            ArenaMovementSetup.Apply(scene);
            CombatSceneSetup.Apply(scene);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.48f, 0.51f, 0.56f);
            RenderSettings.fog = false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildSceneFirst(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            UnityEditor.Selection.activeGameObject = floor;
            Debug.Log($"Playable 3D arena generated at {ScenePath}. Open it and press Play.");
        }

        private static void CreateCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.position = new Vector3(0f, 28f, -16.2f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 0f));

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 20f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 120f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.13f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<OrthographicArenaCamera>();
        }

        private static void CreateLighting(Transform parent)
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(52f, -28f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.94f, 0.84f);
            light.shadows = LightShadows.Soft;
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type, string objectName, Transform parent, Vector3 position,
            Vector3 scale, int layer, Material material)
        {
            GameObject instance = GameObject.CreatePrimitive(type);
            instance.name = objectName;
            instance.transform.SetParent(parent, true);
            instance.transform.position = position;
            instance.transform.localScale = scale;
            instance.layer = layer;

            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            Collider collider = instance.GetComponent<Collider>();
            if (collider == null)
                Debug.LogError($"Primitive {objectName} was created without a 3D collider.");

            return instance;
        }

        private static void EnsureLayerNames()
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(GroundLayer).stringValue = "Ground";
            layers.GetArrayElementAtIndex(FriendlyLayer).stringValue = "FriendlyUnit";
            layers.GetArrayElementAtIndex(HostileLayer).stringValue = "HostileUnit";
            tagManager.ApplyModifiedProperties();
        }

        private static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/_Game/Art", "Materials");
        }

        private static Material GetOrCreateMaterial(string materialName, Color color)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");

                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetBuildSceneFirst(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(scenePath, true) };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != scenePath)
                    scenes.Add(existing);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
