using RaidCommander.Combat;
using RaidCommander.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RaidCommander.EditorTools
{
    public static class PartyAiSceneSetup
    {
        [MenuItem("Raid Commander/Configure Autonomous Party AI")]
        public static void ConfigureGeneratedArena()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ArenaMovementSetup.ScenePath)
                throw new System.InvalidOperationException("Open RaidArena in Edit mode before configuring party AI.");
            // Reuse existing owners/geometry; encounter setup creates only missing boss navigation data.
            CombatUnit Unit(string name)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (CombatUnit candidate in root.GetComponentsInChildren<CombatUnit>(true))
                        if (candidate.name == name) return candidate;
                throw new System.InvalidOperationException("Missing combat unit: " + name);
            }
            CombatSystem combat = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                combat = root.GetComponentInChildren<CombatSystem>(true);
                if (combat != null) break;
            }
            if (combat == null) throw new System.InvalidOperationException("Missing combat system.");
            CombatUnit boss = Unit("Raid Boss");
            var settings = new SerializedObject(combat);
            settings.FindProperty("debugBoss").objectReferenceValue = boss;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(combat);
            Apply(scene, combat, Unit("Tank"), Unit("Healer"), Unit("DPS"), Unit("Ranged DPS"), boss);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PARTY AI SETUP: saved four role brains, mobile boss, ability loadout and debug wiring.");
        }

        public static void Apply(Scene scene, CombatSystem combat, CombatUnit tank, CombatUnit healer,
            CombatUnit dps, CombatUnit rangedDps, CombatUnit boss)
        {
            CombatUnit[] party = { tank, healer, dps, rangedDps };
            foreach (CombatUnit unit in party)
            {
                var unitSettings = new SerializedObject(unit);
                var stats = unitSettings.FindProperty("stats").objectReferenceValue as CombatStatsDefinition;
                if (stats == null) continue;
                var statsSettings = new SerializedObject(stats);
                var speed = statsSettings.FindProperty("attackSpeed");
                speed.floatValue = CombatStatsDefinition.ValidAttackSpeed(speed.floatValue);
                statsSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(stats);
                // Save only these stats assets, preserving unrelated unsaved Editor assets.
                AssetDatabase.SaveAssetIfDirty(stats);
            }
            Configure(tank, CombatRole.Tank, 0f);
            Configure(healer, CombatRole.Healer, 12f);
            Configure(dps, CombatRole.MeleeDps, 0f);
            Configure(rangedDps, CombatRole.RangedDps, 12f);
            var attack = boss.GetComponent<BossBasicAttack>();
            if (attack == null) attack = boss.gameObject.AddComponent<BossBasicAttack>();
            var settings = new SerializedObject(attack);
            settings.FindProperty("combat").objectReferenceValue = combat;
            settings.FindProperty("tank").objectReferenceValue = tank;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(attack);
            BossEncounterSceneSetup.Apply(scene, combat, party, boss);

            void Configure(CombatUnit unit, CombatRole role, float distance)
            {
                var brain = unit.GetComponent<PartyRoleBrain>();
                bool created = brain == null;
                if (created) brain = unit.gameObject.AddComponent<PartyRoleBrain>();
                var serialized = new SerializedObject(brain);
                serialized.FindProperty("role").enumValueIndex = (int)role;
                serialized.FindProperty("combat").objectReferenceValue = combat;
                serialized.FindProperty("boss").objectReferenceValue = boss;
                serialized.FindProperty("tank").objectReferenceValue = tank;
                var allies = serialized.FindProperty("party");
                allies.arraySize = party.Length;
                for (int i = 0; i < party.Length; i++) allies.GetArrayElementAtIndex(i).objectReferenceValue = party[i];
                if (created) serialized.FindProperty("preferredDistance").floatValue = distance;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(brain);
            }
        }
    }
}
