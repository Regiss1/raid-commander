using System;

namespace RaidCommander.Combat
{
    public enum CombatEventType
    {
        SwingDamage,
        SwingMissed,
        SpellCastStart,
        SpellCastSuccess,
        SpellCastFailed,
        SpellDamage,
        SpellMissed,
        SpellHeal,
        SpellPeriodicDamage,
        SpellPeriodicHeal,
        AuraApplied,
        AuraRemoved,
        UnitDied
    }

    [Flags]
    public enum CombatLogFlags
    {
        None = 0,
        Critical = 1 << 0,
        Blocked = 1 << 1,
        Absorbed = 1 << 2
    }

    /// <summary>Immutable, encounter-local record of a resolved combat outcome.</summary>
    public readonly struct CombatLogEntry
    {
        public long Sequence { get; }
        public double Timestamp { get; }
        public CombatEventType Type { get; }
        public int? SourceId { get; }
        public int? TargetId { get; }
        public int? SpellId { get; }
        public int? Damage { get; }
        public int? Healing { get; }
        public CombatLogFlags Flags { get; }

        public CombatLogEntry(long sequence, double timestamp, CombatEventType type,
            int? sourceId, int? targetId, int? spellId, int? damage, int? healing, CombatLogFlags flags)
        {
            Sequence = sequence;
            Timestamp = timestamp;
            Type = type;
            SourceId = sourceId;
            TargetId = targetId;
            SpellId = spellId;
            Damage = damage;
            Healing = healing;
            Flags = flags;
        }
    }

    /// <summary>Resolved outcome sent from combat calculations to independent observers.</summary>
    public readonly struct ResolvedCombatEvent
    {
        public CombatEventType Type { get; }
        public CombatUnit Source { get; }
        public CombatUnit Target { get; }
        public int? SpellId { get; }
        public int? Damage { get; }
        public int? Healing { get; }
        public CombatLogFlags Flags { get; }

        public ResolvedCombatEvent(CombatEventType type, CombatUnit source, CombatUnit target,
            int? spellId = null, int? damage = null, int? healing = null, CombatLogFlags flags = CombatLogFlags.None)
        {
            Type = type;
            Source = source;
            Target = target;
            SpellId = spellId;
            Damage = damage;
            Healing = healing;
            Flags = flags;
        }
    }
}
