using RaidCommander.Combat;
using RaidCommander.Selection;
using RaidCommander.UI;
using RaidCommander.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RaidCommander.EditorTools
{
    /// <summary>Idempotent scene wiring for the combat HUD, independent of balance setup.</summary>
    public static class CombatHudSceneSetup
    {
        private const string ScenePath = "Assets/_Game/Scenes/RaidArena.unity";
        private const string ActionsPath = "Assets/_Game/Input/CombatHudControls.asset";

        [MenuItem("Raid Commander/Configure Combat HUD")]
        public static void ConfigureOpenArena()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new System.InvalidOperationException("Open RaidArena in Edit mode before configuring the HUD.");
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssetIfDirty(Controls());
            Debug.Log("COMBAT HUD SETUP: saved screen HUD, Input System binding and EventSystem.");
        }

        public static void Apply(Scene scene)
        {
            GameObject root = Find(scene, "Combat HUD");
            if (root == null)
            {
                root = new GameObject("Combat HUD", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, scene);
            }
            var hud = root.GetComponent<CombatHud>();
            if (hud == null) hud = root.AddComponent<CombatHud>();
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 15;
            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();
            GameObject bossHud = Need(scene, "Boss Status HUD");
            if (bossHud.GetComponent<GraphicRaycaster>() == null) bossHud.AddComponent<GraphicRaycaster>();
            Transform bossBar = bossHud.transform.Find("Boss Health Bar");
            if (bossBar != null && bossBar.TryGetComponent(out UnityEngine.UI.Image bossBackground))
                bossBackground.raycastTarget = true;
            var settings = new SerializedObject(hud);
            settings.FindProperty("selection").objectReferenceValue = Need(scene, "Main Camera").GetComponent<UnitSelectionController>();
            string[] names = { "Tank", "Healer", "DPS", "Ranged DPS" };
            var party = settings.FindProperty("party");
            party.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
                party.GetArrayElementAtIndex(i).objectReferenceValue = Need(scene, names[i]).GetComponent<CombatUnit>();
            settings.FindProperty("boss").objectReferenceValue = Need(scene, "Raid Boss").GetComponent<CombatUnit>();
            settings.FindProperty("log").objectReferenceValue = Need(scene, "Combat Services").GetComponent<CombatLogService>();
            CombatSystem combat = Need(scene, "Combat Services").GetComponent<CombatSystem>();
            settings.FindProperty("decisions").objectReferenceValue = DecisionLogSceneSetup.Ensure(scene, combat);
            settings.FindProperty("actions").objectReferenceValue = Need(scene, "Combat Debug Panel").GetComponent<CombatDebugActions>();
            settings.FindProperty("controls").objectReferenceValue = Controls();
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);

            var oldPanel = Need(scene, "Combat Debug Panel").GetComponent<CombatDebugPanel>();
            if (oldPanel != null) Object.DestroyImmediate(oldPanel);

            EventSystem existingSystem = null;
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                existingSystem = sceneRoot.GetComponentInChildren<EventSystem>(true);
                if (existingSystem != null) break;
            }
            GameObject input = existingSystem != null ? existingSystem.gameObject : Find(scene, "UI EventSystem");
            if (input == null)
            {
                input = new GameObject("UI EventSystem");
                SceneManager.MoveGameObjectToScene(input, scene);
            }
            if (input.GetComponent<EventSystem>() == null) input.AddComponent<EventSystem>();
            if (input.GetComponent<InputSystemUIInputModule>() == null)
                input.AddComponent<InputSystemUIInputModule>();
            var legacy = input.GetComponent<StandaloneInputModule>();
            if (legacy != null) Object.DestroyImmediate(legacy);
        }

        private static InputActionAsset Controls()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "CombatHudControls";
            var map = new InputActionMap("Combat HUD");
            map.AddAction("Toggle Party World Health Bars", InputActionType.Button, "<Keyboard>/v");
            asset.AddActionMap(map);
            AssetDatabase.CreateAsset(asset, ActionsPath);
            return asset;
        }

        private static GameObject Need(Scene scene, string name) => Find(scene, name)
            ?? throw new System.InvalidOperationException("Missing arena object: " + name);

        private static GameObject Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child.gameObject;
            return null;
        }
    }
}
