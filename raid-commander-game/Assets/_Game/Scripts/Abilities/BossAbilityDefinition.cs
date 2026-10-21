using System;
using RaidCommander.Units;
using UnityEngine;

namespace RaidCommander.Abilities
{
    public enum AbilityTrigger { Interval, HealthThreshold }
    public enum AbilityRange { Unlimited, Limited }
    public enum AbilityDelivery { GroundArea, DirectTarget }
    public enum AbilityShape { Rectangle }
    public enum AbilityLength { Fixed, ToArenaBoundary }
    [Flags] public enum TargetRoles { Tank = 1, Healer = 2, MeleeDps = 4, RangedDps = 8, All = 15 }

    /// <summary>Shared configuration only. Casts, targets and readiness belong to the runner instance.</summary>
    [CreateAssetMenu(menuName = "Raid Commander/Boss Ability")]
    public sealed class BossAbilityDefinition : ScriptableObject
    {
        public int AbilityId = 1;
        public string DisplayName = "Wall of Flames";
        public AbilityTrigger TriggerMode;
        public float InitialDelaySeconds = 90f;
        public float IntervalSeconds = 90f;
        public float CooldownSeconds;
        public float HealthThresholdPercent = 50f;
        public int Damage = 95;
        public float CastTimeSeconds = 3f;
        public TargetRoles AllowedTargetRoles = TargetRoles.All;
        public AbilityRange TargetRangeMode;
        public float TargetRange = 20f;
        public AbilityDelivery DeliveryMode;
        public AbilityShape Shape;
        public float Width = 4f;
        public float Length = 10f;
        public AbilityLength LengthMode = AbilityLength.ToArenaBoundary;
        public bool LockFacingOnCastStart = true;
        public bool StopMovementDuringCast = true;
        public bool SuppressAutoAttacksDuringCast = true;
        public bool ShowTelegraph = true;
        public Color TelegraphColor = new Color(1f, 0.8f, 0.05f, 0.65f);
        public Color ImpactColor = new Color(1f, 0.08f, 0.03f, 0.8f);
        public float ImpactDuration = 0.35f;

        public bool Allows(CombatRole role) => (AllowedTargetRoles & RoleMask(role)) != 0;
        private static TargetRoles RoleMask(CombatRole role) => role == CombatRole.Tank ? TargetRoles.Tank
            : role == CombatRole.Healer ? TargetRoles.Healer : role == CombatRole.MeleeDps ? TargetRoles.MeleeDps : TargetRoles.RangedDps;
        public string ValidationError(bool hasBoundary)
        {
            if (AbilityId <= 0 || string.IsNullOrWhiteSpace(DisplayName)) return "Ability needs a positive stable ID and display name.";
            if (!Enum.IsDefined(typeof(AbilityTrigger), TriggerMode) || !Enum.IsDefined(typeof(AbilityRange), TargetRangeMode)
                || !Enum.IsDefined(typeof(AbilityDelivery), DeliveryMode) || !Enum.IsDefined(typeof(AbilityLength), LengthMode)
                || Shape != AbilityShape.Rectangle) return "Unsupported ability mode or shape (only Rectangle is supported).";
            if (!Nonnegative(InitialDelaySeconds) || !Nonnegative(CooldownSeconds) || !Nonnegative(CastTimeSeconds)
                || !Nonnegative(ImpactDuration) || Damage < 0) return "Damage and durations must be finite and nonnegative.";
            if (!Finite(HealthThresholdPercent) || HealthThresholdPercent < 0f || HealthThresholdPercent > 100f)
                return "Health threshold must be between 0 and 100 percent.";
            if (TriggerMode == AbilityTrigger.Interval && (!Finite(IntervalSeconds) || IntervalSeconds <= 0f)) return "Interval must be finite and positive.";
            // Unity's flags Inspector writes -1 for its built-in Everything option.
            if (AllowedTargetRoles == 0 || (AllowedTargetRoles != (TargetRoles)(-1) && (AllowedTargetRoles & ~TargetRoles.All) != 0))
                return "Choose at least one supported target role.";
            if (TargetRangeMode == AbilityRange.Limited && (!Finite(TargetRange) || TargetRange <= 0f)) return "Limited target range must be finite and positive.";
            if (DeliveryMode == AbilityDelivery.GroundArea && !hasBoundary) return "Ground rectangles need a valid arena boundary.";
            if (DeliveryMode == AbilityDelivery.GroundArea && (!Finite(Width) || Width <= 0f
                || !Finite(Length) || Length <= 0f)) return "Ground rectangle width and length must be positive finite values.";
            return null;
        }
        private static bool Nonnegative(float x) => Finite(x) && x >= 0f;
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }
}
