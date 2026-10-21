using RaidCommander.Combat;
using RaidCommander.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RaidCommander.EditorTools
{
    public static class CombatSceneSetup
    {
        private const string ScenePath = "Assets/_Game/Scenes/RaidArena.unity";
        private const string StatsFolder = "Assets/_Game/Data/BaseGame/Units";

        [MenuItem("Raid Commander/Configure Combat and Logging")]
        public static void ConfigureGeneratedArena()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("COMBAT SETUP: configured unit health, health bars, combat logging, and debug controls.");
        }

        public static void Apply(Scene scene)
        {
            Camera camera = Require(scene, "Main Camera").GetComponent<Camera>();
            CombatUnit tank = ConfigureUnit(Require(scene, "Tank"), 1, CombatFaction.Friendly, camera,
                GetOrCreateStats("TankCombatStats", 120, 18, 20, 2.5f, 4f));
            CombatUnit healer = ConfigureUnit(Require(scene, "Healer"), 2, CombatFaction.Friendly, camera,
                GetOrCreateStats("HealerCombatStats", 90, 10, 25, 2.3f, 18f));
            CombatUnit dps = ConfigureUnit(Require(scene, "DPS"), 3, CombatFaction.Friendly, camera,
                GetOrCreateStats("DpsCombatStats", 100, 20, 20, 2.5f, 4f));
            CombatUnit boss = ConfigureUnit(Require(scene, "Raid Boss"), 4, CombatFaction.Hostile, camera,
                GetOrCreateStats("BossCombatStats", 500, 30, 0, 3.5f, 4f), attachWorldHealthBar: false);
            CombatUnit rangedDps = ConfigureUnit(Require(scene, "Ranged DPS"), 5, CombatFaction.Friendly, camera,
                GetOrCreateStats("RangedDpsCombatStats", 100, 20, 0, 2.5f, 18f));
            ConfigureBossHud(scene, boss);

            GameObject serviceObject = FindOrCreate(scene, "Combat Services");
            var log = GetOrAdd<CombatLogService>(serviceObject);
            var combat = GetOrAdd<CombatSystem>(serviceObject);
            var combatSettings = new SerializedObject(combat);
            combatSettings.FindProperty("logService").objectReferenceValue = log;
            combatSettings.FindProperty("obstacleRoot").objectReferenceValue = Require(scene, "Obstacles").transform;
            combatSettings.FindProperty("debugBoss").objectReferenceValue = boss;
            combatSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(combat);
            RangedDpsSceneSetup.ConfigureTracer(rangedDps, combat);

            GameObject panelObject = FindOrCreate(scene, "Combat Debug Panel");
            var panel = GetOrAdd<CombatDebugPanel>(panelObject);
            var actions = GetOrAdd<CombatDebugActions>(panelObject);
            var actionSettings = new SerializedObject(actions);
            actionSettings.FindProperty("combat").objectReferenceValue = combat;
            actionSettings.FindProperty("tank").objectReferenceValue = tank;
            actionSettings.FindProperty("healer").objectReferenceValue = healer;
            actionSettings.FindProperty("dps").objectReferenceValue = dps;
            actionSettings.FindProperty("rangedDps").objectReferenceValue = rangedDps;
            actionSettings.FindProperty("boss").objectReferenceValue = boss;
            actionSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(actions);

            var panelSettings = new SerializedObject(panel);
            panelSettings.FindProperty("log").objectReferenceValue = log;
            panelSettings.FindProperty("actions").objectReferenceValue = actions;
            panelSettings.FindProperty("tank").objectReferenceValue = tank;
            panelSettings.FindProperty("healer").objectReferenceValue = healer;
            panelSettings.FindProperty("dps").objectReferenceValue = dps;
            panelSettings.FindProperty("rangedDps").objectReferenceValue = rangedDps;
            panelSettings.FindProperty("boss").objectReferenceValue = boss;
            panelSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel);
            PartyAiSceneSetup.Apply(scene, combat, tank, healer, dps, rangedDps, boss);
        }

        private static CombatUnit ConfigureUnit(GameObject target, int id, CombatFaction faction, Camera camera,
            CombatStatsDefinition stats, bool attachWorldHealthBar = true)
        {
            var unit = GetOrAdd<CombatUnit>(target);
            var settings = new SerializedObject(unit);
            settings.FindProperty("encounterId").intValue = id;
            settings.FindProperty("faction").enumValueIndex = (int)faction;
            settings.FindProperty("stats").objectReferenceValue = stats;
            settings.FindProperty("fallbackMaximumHealth").intValue = target.name == "Raid Boss" ? 500 : 100;
            settings.FindProperty("fallbackBasicDamage").intValue = target.name == "Raid Boss" ? 30 : 15;
            settings.FindProperty("fallbackBasicHealing").intValue = target.name == "Healer" ? 25 : 20;
            settings.FindProperty("fallbackMeleeRange").floatValue = target.name == "Raid Boss" ? 3.5f : 2.5f;
            settings.FindProperty("fallbackRangedRange").floatValue = target.name == "Healer" ? 18f : 4f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(unit);

            if (attachWorldHealthBar)
                ConfigureHealthBar(target, camera, unit);
            else
            {
                Transform existingBar = target.transform.Find("World Health Bar");
                if (existingBar != null)
                    Object.DestroyImmediate(existingBar.gameObject);
            }
            return unit;
        }

        private static void ConfigureBossHud(Scene scene, CombatUnit boss)
        {
            GameObject hudObject = FindOrCreate(scene, "Boss Status HUD");
            Canvas canvas = GetOrAdd<Canvas>(hudObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = GetOrAdd<CanvasScaler>(hudObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image background = FindOrCreateImage(hudObject.transform, "Boss Health Bar",
                new Color(0.06f, 0.06f, 0.06f, 0.92f));
            RectTransform barRect = background.rectTransform;
            barRect.anchorMin = new Vector2(0.5f, 1f);
            barRect.anchorMax = new Vector2(0.5f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = new Vector2(0f, -20f);
            barRect.sizeDelta = new Vector2(760f, 32f);

            Image fill = FindOrCreateImage(background.transform, "Fill", new Color(0.75f, 0.12f, 0.16f, 1f));
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            HealthBarSceneAssets.ConfigureFill(fill);

            var percentage = HealthBarSceneAssets.ConfigureText(background.transform, "Health Percentage", "100%",
                TextAlignmentOptions.MidlineLeft);
            SetTextRect(percentage.rectTransform, Vector2.zero, new Vector2(0.25f, 1f));
            var healthValues = HealthBarSceneAssets.ConfigureText(background.transform, "Health Values",
                boss.MaximumHealth.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " / "
                + boss.MaximumHealth.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                TextAlignmentOptions.MidlineRight);
            SetTextRect(healthValues.rectTransform, new Vector2(0.25f, 0f), Vector2.one);

            var nameLabel = HealthBarSceneAssets.ConfigureText(hudObject.transform, "Boss Name", "Boss",
                TextAlignmentOptions.Center);
            RectTransform nameRect = nameLabel.rectTransform;
            nameRect.anchorMin = new Vector2(0.5f, 1f);
            nameRect.anchorMax = new Vector2(0.5f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -56f);
            nameRect.sizeDelta = new Vector2(760f, 28f);

            ConfigureBinding(hudObject, boss, fill, percentage, healthValues);

            ScreenBossHealthBar bossHealthBar = GetOrAdd<ScreenBossHealthBar>(hudObject);
            var hudSettings = new SerializedObject(bossHealthBar);
            hudSettings.FindProperty("barRect").objectReferenceValue = barRect;
            hudSettings.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            hudSettings.FindProperty("displayName").stringValue = "Boss";
            hudSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bossHealthBar);
        }

        private static void SetTextRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(8f, 0f);
            rect.offsetMax = new Vector2(-8f, 0f);
        }

        private static void ConfigureBinding(GameObject target, CombatUnit unit, Image fill,
            TMP_Text percentage = null, TMP_Text healthValues = null)
        {
            var binding = GetOrAdd<HealthBarBinding>(target);
            var settings = new SerializedObject(binding);
            settings.FindProperty("unit").objectReferenceValue = unit;
            settings.FindProperty("fill").objectReferenceValue = fill;
            settings.FindProperty("percentageLabel").objectReferenceValue = percentage;
            settings.FindProperty("healthValueLabel").objectReferenceValue = healthValues;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(binding);
        }

        private static CombatStatsDefinition GetOrCreateStats(string assetName, int health, int damage,
            int healing, float meleeRange, float rangedRange)
        {
            string path = StatsFolder + "/" + assetName + ".asset";
            CombatStatsDefinition stats = AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(path);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<CombatStatsDefinition>();
                AssetDatabase.CreateAsset(stats, path);
            }

            var settings = new SerializedObject(stats);
            settings.FindProperty("maximumHealth").intValue = health;
            settings.FindProperty("basicDamage").intValue = damage;
            settings.FindProperty("basicHealing").intValue = healing;
            settings.FindProperty("meleeRange").floatValue = meleeRange;
            settings.FindProperty("rangedRange").floatValue = rangedRange;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stats);
            return stats;
        }

        private static WorldHealthBar ConfigureHealthBar(GameObject unit, Camera camera, CombatUnit combatUnit)
        {
            Transform existing = unit.transform.Find("World Health Bar");
            GameObject root = existing != null ? existing.gameObject : new GameObject("World Health Bar");
            root.transform.SetParent(unit.transform, false);
            root.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * 0.01f;

            Canvas canvas = GetOrAdd<Canvas>(root);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5;
            RectTransform canvasRect = root.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(60f, 14f);

            Image background = FindOrCreateImage(root.transform, "Background", new Color(0.08f, 0.08f, 0.08f, 0.9f));
            Stretch(background.rectTransform);
            var selectable = unit.GetComponent<RaidCommander.Selection.SelectableUnit>();
            if (selectable != null)
                HealthBarSceneAssets.ConfigurePartyOutline(background, selectable.Role);
            Image fill = FindOrCreateImage(root.transform, "Health Fill", new Color(0.16f, 0.82f, 0.28f, 1f));
            Stretch(fill.rectTransform);
            HealthBarSceneAssets.ConfigureFill(fill);
            ConfigureBinding(root, combatUnit, fill);

            WorldHealthBar bar = GetOrAdd<WorldHealthBar>(root);
            var barSettings = new SerializedObject(bar);
            barSettings.FindProperty("followTarget").objectReferenceValue = unit.transform;
            barSettings.FindProperty("viewCamera").objectReferenceValue = camera;
            barSettings.FindProperty("worldOffset").vector3Value = new Vector3(0f, 1.8f, 0f);
            barSettings.FindProperty("screenHeight").floatValue = 10f;
            barSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bar);
            return bar;
        }

        private static Image FindOrCreateImage(Transform parent, string name, Color color)
        {
            Transform child = parent.Find(name);
            GameObject target = child != null ? child.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            target.transform.SetParent(parent, false);
            Image image = GetOrAdd<Image>(target);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static GameObject Require(Scene scene, string name) => Find(scene, name)
            ?? throw new System.InvalidOperationException("Missing arena object: " + name);

        private static GameObject FindOrCreate(Scene scene, string name)
        {
            GameObject found = Find(scene, name);
            if (found != null) return found;
            var created = new GameObject(name);
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child.gameObject;
            return null;
        }
    }
}
