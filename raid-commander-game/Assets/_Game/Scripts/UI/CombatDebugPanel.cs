using System.Text;
using RaidCommander.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RaidCommander.UI
{
    /// <summary>Minimal screen-space debug controls and recent combat event view.</summary>
    public sealed class CombatDebugPanel : MonoBehaviour
    {
        [SerializeField] private CombatLogService log;
        [SerializeField] private CombatDebugActions actions;
        [SerializeField] private CombatUnit tank;
        [SerializeField] private CombatUnit healer;
        [SerializeField] private CombatUnit dps;
        [SerializeField] private CombatUnit boss;
        [SerializeField, Min(1)] private int visibleEntries = 12;
        private bool visible = true;
        private InputAction toggleAction;
        private Vector2 scroll;
        private readonly StringBuilder builder = new StringBuilder(1600);

        private void Awake() => toggleAction = new InputAction("Toggle Combat Debug", InputActionType.Button, "<Keyboard>/f8");
        private void OnEnable() => toggleAction?.Enable();
        private void OnDisable() => toggleAction?.Disable();
        private void OnDestroy() => toggleAction?.Dispose();

        private void Update()
        {
            if (toggleAction != null && toggleAction.WasPressedThisFrame())
                visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible)
            {
                if (GUI.Button(new Rect(12, 12, 150, 28), "Show Combat Log (F8)")) visible = true;
                return;
            }

            GUILayout.BeginArea(new Rect(12, 12, 370, Screen.height - 24), GUI.skin.box);
            GUILayout.Label("Combat Debug  (F8 hides panel)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Melee: Tank to Boss")) actions?.TankMelee();
            if (GUILayout.Button("Heal: Healer to Tank")) actions?.HealerHeal();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Melee: DPS to Boss")) actions?.DpsMelee();
            if (GUILayout.Button("Damage Tank (Boss)")) actions?.DamageTank();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Kill Boss")) actions?.KillBoss();
            if (GUILayout.Button("Reset Health + Encounter Log"))
                actions?.ResetEncounter();
            if (actions != null)
                GUILayout.Label(actions.LastStatus, GUI.skin.box);
            GUILayout.Space(5);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Max(100, Screen.height - 145)));
            if (log != null)
            {
                int start = Mathf.Max(0, log.History.Count - visibleEntries);
                for (int i = start; i < log.History.Count; i++)
                    GUILayout.Label(Format(log.History[i]));
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private string Format(CombatLogEntry entry)
        {
            builder.Length = 0;
            builder.Append(entry.Timestamp.ToString("F1")).Append("  ");
            builder.Append(NameFor(entry.SourceId)).Append(" -> ").Append(NameFor(entry.TargetId));
            builder.Append("  ").Append(entry.Type);
            if (entry.Damage.HasValue) builder.Append("  -").Append(entry.Damage.Value);
            if (entry.Healing.HasValue) builder.Append("  +").Append(entry.Healing.Value);
            return builder.ToString();
        }

        private string NameFor(int? id)
        {
            if (!id.HasValue) return "Environment";
            if (tank != null && tank.EncounterId == id.Value) return tank.name;
            if (healer != null && healer.EncounterId == id.Value) return healer.name;
            if (dps != null && dps.EncounterId == id.Value) return dps.name;
            if (boss != null && boss.EncounterId == id.Value) return boss.name;
            return "Unit " + id.Value;
        }
    }
}
