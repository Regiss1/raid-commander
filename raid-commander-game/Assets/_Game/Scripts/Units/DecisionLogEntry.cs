namespace RaidCommander.Units
{
    public enum DecisionKind { AwarenessCheck, EscapeStarted, NoSafeExit, ManualSuppression }

    /// <summary>Immutable snapshot: later casts and renamed/destroyed objects cannot rewrite history.</summary>
    public sealed class DecisionLogEntry
    {
        public long Sequence { get; }
        public double Timestamp { get; }
        public DecisionKind Kind { get; }
        public int MemberId { get; }
        public string MemberName { get; }
        public string Hazards { get; }
        public int? Awareness { get; }
        public int? Roll { get; }
        public bool? Success { get; }

        public DecisionLogEntry(long sequence, double timestamp, DecisionKind kind, int memberId,
            string memberName, string hazards, int? awareness, int? roll, bool? success)
        {
            Sequence = sequence; Timestamp = timestamp; Kind = kind; MemberId = memberId;
            MemberName = memberName; Hazards = hazards; Awareness = awareness; Roll = roll; Success = success;
        }
    }
}
