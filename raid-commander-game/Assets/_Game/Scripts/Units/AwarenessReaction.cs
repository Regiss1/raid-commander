using System;
using RaidCommander.Combat;

namespace RaidCommander.Units
{
    public enum AwarenessResultKind { None, Failed, Succeeded, ManualSuppression }

    public readonly struct AwarenessResult
    {
        public AwarenessResultKind Kind { get; }
        public int Awareness { get; }
        public int Roll { get; }
        public AwarenessResult(AwarenessResultKind kind, int awareness = 0, int roll = 0)
        { Kind = kind; Awareness = awareness; Roll = roll; }
    }

    /// <summary>One scaled-time check stream per member, independent of hazard overlap count.</summary>
    public sealed class AwarenessReaction
    {
        private readonly Func<int> roll;
        private bool exposed, suppressed;
        private float nextCheck;
        public bool Committed { get; private set; }

        public AwarenessReaction(Func<int> roll) { this.roll = roll ?? throw new ArgumentNullException(nameof(roll)); }

        public AwarenessResult Evaluate(bool insideCenter, bool insideClearance, bool manual, float now, int stat)
        {
            if (manual)
            {
                bool report = (insideCenter || Committed) && !suppressed;
                Reset();
                suppressed = insideCenter || report;
                return report ? new AwarenessResult(AwarenessResultKind.ManualSuppression) : default;
            }
            suppressed = false;
            if (!insideCenter && (!Committed || !insideClearance))
            {
                Reset();
                return default;
            }
            if (Committed) return default;
            if (!insideCenter) return default;
            if (!exposed) { exposed = true; nextCheck = now; }
            if (now < nextCheck) return default;
            int awareness = CombatStatsDefinition.ValidAwareness(stat);
            int value = roll();
            if (value < 1 || value > 10) throw new InvalidOperationException("Awareness roll must be in 1..10.");
            if (value <= awareness)
            {
                Committed = true;
                return new AwarenessResult(AwarenessResultKind.Succeeded, awareness, value);
            }
            nextCheck = now + 0.5f;
            return new AwarenessResult(AwarenessResultKind.Failed, awareness, value);
        }

        public void Reset() { exposed = false; Committed = false; nextCheck = 0f; suppressed = false; }
    }
}
