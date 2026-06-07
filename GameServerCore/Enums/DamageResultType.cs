namespace GameServerCore.Enums
{
    /// <summary>
    /// Result of a damage-flow evaluation.
    /// Verified against the S4 mac decomp: matches `DamageResultType`
    /// (AI/Damage/DamageEnums.h, Riot names DAMAGEFLOW_*) - complete with 7 values.
    /// </summary>
    public enum DamageResultType : byte
    {
        RESULT_INVULNERABLE = 0x0,
        RESULT_INVULNERABLENOMESSAGE = 0x1,
        RESULT_DODGE = 0x2,
        RESULT_CRITICAL = 0x3,
        RESULT_NORMAL = 0x4,
        RESULT_MISS = 0x5,
        /// <summary>
        /// Riot DAMAGEFLOW_UNASSIGNED - sentinel for a not-yet-evaluated damage flow.
        /// Was missing from this enum.
        /// </summary>
        RESULT_UNASSIGNED = 0x6,
    }
}
