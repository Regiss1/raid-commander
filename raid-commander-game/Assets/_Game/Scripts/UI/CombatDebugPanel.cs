using System.Text;
using RaidCommander.Combat;
using RaidCommander.Units;
using RaidCommander.Abilities;
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
        [SerializeField] private CombatUnit rangedDps;
        [SerializeField] private CombatUnit boss;
        [SerializeField, Min(1)] private int visibleEntries = 12;
        private bool visible = true;
        private InputAction toggleAction;
        private Vector2 scroll;
        private readonly StringBuilder builder = new StringBuilder(1600);
        private PartyRoleBrain[] brains;
        private BossBasicAttack bossAttack;
        private BossAbilityRunner bossAbilities;

        private void Awake()
        {
            toggleAction = new InputAction("Toggle Combat Debug", InputActionType.Button, "<Keyboard>/f8");
            CombatUnit[] party = { tank, healer, dps, rangedDps };
            brains = new PartyRoleBrain[party.Length];
            for (int i = 0; i < party.Length; i++) brains[i] = party[i] != null ? party[i].GetComponent<PartyRoleBrain>() : null;
            bossAttack = boss != null ? boss.GetComponent<BossBasicAttack>() : null;
            bossAbilities = boss != null ? boss.GetComponent<BossAbilityRunner>() : null;
        }
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
            scroll = GUILayout.BeginScrollView(scroll);
            if (actions != null)
                actions.KeepBossAlive = GUILayout.Toggle(actions.KeepBossAlive, "Keep Boss Alive (minimum 1 HP)");
            if (brains != null)
                foreach (PartyRoleBrain brain in brains)
                    if (brain != null) GUILayout.Label(brain.name + ": " + brain.State
                        + (brain.Target != null ? " -> " + brain.Target.name : ""));
            if (bossAttack != null) GUILayout.Label("Boss: " + bossAttack.State + (bossAttack.CombatTarget != null ? " -> " + bossAttack.CombatTarget.name : ""));
            if (bossAbilities != null && bossAbilities.IsCasting) GUILayout.Label(bossAbilities.ActiveDefinition.DisplayName
                + "  " + Mathf.Max(0f, bossAbilities.CastEndsAt - Time.time).ToString("F1") + "s");
            if (bossAbilities != null && !string.IsNullOrEmpty(bossAbilities.SetupError)) GUILayout.Label(bossAbilities.SetupError);
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Melee: Tank to Boss")) actions?.TankMelee();
            if (GUILayout.Button("Heal: Healer to Tank")) actions?.HealerHeal();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Melee: Melee DPS to Boss")) actions?.DpsMelee();
            if (GUILayout.Button("Damage Tank (Boss)")) actions?.DamageTank();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Ranged: Ranged DPS to Boss")) actions?.RangedDpsAttack();
            if (GUILayout.Button("Kill Boss")) actions?.KillBoss();
            if (GUILayout.Button("Reset Health + Encounter Log"))
                actions?.ResetEncounter();
            if (actions != null)
                GUILayout.Label(actions.LastStatus, GUI.skin.box);
            GUILayout.Space(5);
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
            if (entry.SpellId.HasValue) builder.Append("  spell:").Append(entry.SpellId.Value);
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
            if (rangedDps != null && rangedDps.EncounterId == id.Value) return rangedDps.name;
            if (boss != null && boss.EncounterId == id.Value) return boss.name;
            return "Unit " + id.Value;
        }
    }
}
