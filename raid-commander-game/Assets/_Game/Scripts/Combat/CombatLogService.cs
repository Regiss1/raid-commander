using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace RaidCommander.Combat
{
    /// <summary>Encounter-scoped recorder. It observes outcomes and never changes gameplay state.</summary>
    public sealed class CombatLogService : MonoBehaviour
    {
        [SerializeField, Min(1)] private int historyLimit = 1000;
        private readonly List<CombatLogEntry> history = new List<CombatLogEntry>(1000);
        private ReadOnlyCollection<CombatLogEntry> historyView;
        private double sessionStart;
        private long nextSequence = 1;

        public IReadOnlyList<CombatLogEntry> History => historyView;
        public double SessionElapsed => Time.realtimeSinceStartupAsDouble - sessionStart;
        public event Action<CombatLogEntry> EntryRecorded;
        public event Action SessionReset;

        private void Awake()
        {
            historyView = history.AsReadOnly();
            ResetSession();
        }

        public CombatLogEntry Record(ResolvedCombatEvent combatEvent)
        {
            int capacity = Mathf.Max(1, historyLimit);
            var entry = new CombatLogEntry(nextSequence++, Time.realtimeSinceStartupAsDouble - sessionStart,
                combatEvent.Type, combatEvent.Source != null ? combatEvent.Source.EncounterId : (int?)null,
                combatEvent.Target != null ? combatEvent.Target.EncounterId : (int?)null,
                combatEvent.SpellId, combatEvent.Damage, combatEvent.Healing, combatEvent.Flags);

            if (history.Count >= capacity)
                history.RemoveAt(0);
            history.Add(entry);
            EntryRecorded?.Invoke(entry);
            return entry;
        }

        public void ResetSession()
        {
            history.Clear();
            nextSequence = 1;
            sessionStart = Time.realtimeSinceStartupAsDouble;
            SessionReset?.Invoke();
        }

        private void OnValidate() => historyLimit = Mathf.Max(1, historyLimit);
    }
}
