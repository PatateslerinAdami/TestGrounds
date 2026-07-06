namespace LeagueSandbox.GameServer.Content
{
    public class GlobalCharacterDataConstants
    {
        /// <summary>
        /// Attack delay coefficient
        /// </summary>
        public float AttackDelay { get; set; } = 1.6f;
        /// <summary>
        /// Attack delay cast percent 0-1
        /// </summary>
        public float AttackDelayCastPercent { get; set; } = 0.3f;
        /// <summary>
        /// Attack min delay
        /// </summary>
        public float AttackMinDelay { get; set; } = 0.4f;
        /// <summary>
        /// The lowest Attack Speed Percent Mod penalty can go
        /// </summary>
        public float PercentAttackSpeedModMinimum { get; set; } = -0.95f;
        /// <summary>
        /// Attack max delay
        /// </summary>
        public float AttackMaxDelay { get; set; } = 5.0f;
        /// <summary>
        /// Minimum cooldown time for a spell.
        /// </summary>
        public float CooldownMinimum { get; set; } = 0.0f;
        /// <summary>
        /// The lowest RespawnTime Percent Mod bonus can go.
        /// </summary>
        public float PercentRespawnTimeModMinimum { get; set; } = -0.95f;
        /// <summary>
        /// The lowest GoldLostOnDeath Percent Mod bonus can go.
        /// NOT APPLIED: there is no gold-lost-on-death percent-mod stat server-side to clamp (the
        /// gold-loss-on-death system itself is unimplemented). Loaded for completeness; wire the clamp
        /// when that system exists. See docs/CONSTANTS_VAR_AUDIT.md.
        /// </summary>
        public float PercentGoldLostOnDeathModMinimum { get; set; } = -0.95f;
        /// <summary>
        /// The lowest EXPBonus Percent Mod penalty can go.
        /// NOT APPLIED: AddExperience grants raw experience with no percent EXP-bonus mod stat to clamp.
        /// Loaded for completeness; wire the clamp when an EXP-bonus modifier exists. See docs/CONSTANTS_VAR_AUDIT.md.
        /// </summary>
        public float PercentEXPBonusMinimum { get; set; } = -1.0f;
        /// <summary>
        /// The highest EXPBonus Percent Mod bonus can go.
        /// NOT APPLIED: see <see cref="PercentEXPBonusMinimum"/> — no EXP-bonus mod stat exists to cap.
        /// </summary>
        public float PercentEXPBonusMaximum { get; set; } = 5.0f;
        /// <summary>
        /// The lowest Cooldown Percent Mod bonus can go.
        /// </summary>
        public float PercentCooldownModMinimum { get; set; } = -0.4f;
    }
}
