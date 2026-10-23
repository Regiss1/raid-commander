using RaidCommander.Combat;
using RaidCommander.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RaidCommander.EditorTools
{
    /// <summary>Shared, repeatable wiring used by AI and HUD setup without replacing scene objects.</summary>
    public static class DecisionLogSceneSetup
    {
        public static DecisionLogService Ensure(Scene scene, CombatSystem combat)
        {
            if (combat == null) throw new System.InvalidOperationException("Missing combat system for decision log.");
            GameObject host = combat.gameObject;
            var decisions = host.GetComponent<DecisionLogService>();
            if (decisions == null) decisions = host.AddComponent<DecisionLogService>();
            var settings = new SerializedObject(decisions);
            settings.FindProperty("combat").objectReferenceValue = combat;
            settings.FindProperty("combatLog").objectReferenceValue = host.GetComponent<CombatLogService>();
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(decisions);
            return decisions;
        }
    }
}
