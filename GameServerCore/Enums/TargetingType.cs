namespace GameServerCore.Enums
{
    /// <summary>
    /// Spell targeting types.
    /// Verified against the S4 mac decomp: exact match with
    /// Spell::Enums::TargetingType (Spell/Resource/SpellDataEnums.h, values 0-9,
    /// kTargetingTypeCount = 10 - the enum is complete). Riot's kTargetingTypeInvalid
    /// is -1 in an int enum; our byte-typed Invalid = 0xFF is the equivalent sentinel.
    /// Also matches the S1 Lua exports (TTYPE_* globals, values 0-7 script-visible).
    /// </summary>
    public enum TargetingType : byte
    {
        Self = 0x0,
        Target = 0x1,
        Area = 0x2,
        Cone = 0x3,
        SelfAOE = 0x4,
        TargetOrLocation = 0x5,
        Location = 0x6,
        Direction = 0x7,
        /// <summary>
        /// Riot kTargetingTypeDragDirection: direction chosen by click-dragging
        /// (e.g. Viktor E / Rumble R style cast input).
        /// </summary>
        DragDirection = 0x8,
        /// <summary>
        /// Riot kTargetingTypeLineTargetToCaster: a line anchored from the target
        /// back toward the caster.
        /// </summary>
        LineTargetToCaster = 0x9,
        Invalid = 0xFF,
    }
}
