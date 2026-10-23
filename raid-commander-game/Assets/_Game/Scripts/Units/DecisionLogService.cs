using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RaidCommander.Combat;
using UnityEngine;

namespace RaidCommander.Units
{
    /// <summary>Optional encounter-local observer of Awareness checks and escape outcomes.</summary>
    [DisallowMultipleComponent]
    public sealed class DecisionLogService : MonoBehaviour
    {
        [SerializeField] private CombatSystem combat;
        [SerializeField] private CombatLogService combatLog;
        [SerializeField, Min(1)] private int historyLimit = 1000;
        private readonly List<DecisionLogEntry> history = new List<DecisionLogEntry>(1000);
        private ReadOnlyCollection<DecisionLogEntry> historyView;
        private double fallbackStart;
        private long sequence = 1;
        public IReadOnlyList<DecisionLogEntry> History => historyView;
        public event Action<DecisionLogEntry> EntryRecorded;
        public event Action SessionReset;

        private void Awake() { historyView = history.AsReadOnly(); ResetSession(); }
        private void OnEnable() { if (combat != null) combat.SessionReset += ResetSession; }
        private void OnDisable() { if (combat != null) combat.SessionReset -= ResetSession; }

        public DecisionLogEntry Record(DecisionKind kind, CombatUnit member, string[] hazardNames,
            int? awareness = null, int? roll = null, bool? success = null)
        {
            if (!isActiveAndEnabled || member == null) return null;
            string identities = hazardNames == null || hazardNames.Length == 0
                ? "Pending hazard" : string.Join(", ", hazardNames);
            var entry = new DecisionLogEntry(sequence++, combatLog != null ? combatLog.SessionElapsed
                : Time.realtimeSinceStartupAsDouble - fallbackStart, kind, member.EncounterId,
                member.name, identities, awareness, roll, success);
            if (history.Count >= Mathf.Max(1, historyLimit)) history.RemoveAt(0);
            history.Add(entry);
            EntryRecorded?.Invoke(entry);
            return entry;
        }

        public void ResetSession()
        {
            history.Clear(); sequence = 1; fallbackStart = Time.realtimeSinceStartupAsDouble;
            SessionReset?.Invoke();
        }
        private void OnValidate() => historyLimit = Mathf.Max(1, historyLimit);
    }
}
