namespace GameServerCore.Enums
{
    /// <summary>
    /// Verified against the S4 mac decomp: exact match with `DamageTypes`
    /// (AI/Damage/DamageEnums.h: PHYSICAL=0, MAGIC=1, TRUE=2, MIXED=3) - complete.
    /// </summary>
    public enum DamageType : byte
    {
        DAMAGE_TYPE_PHYSICAL = 0x0,
        DAMAGE_TYPE_MAGICAL = 0x1,
        DAMAGE_TYPE_TRUE = 0x2,
        DAMAGE_TYPE_MIXED = 0x3
    }
}