namespace GameServerCore.Enums
{
    /// <summary>
    /// Unit AI order types.
    /// Verified against the S4 mac decomp: exact 1:1 match with `orders_e`
    /// (AI/AIEnums.h, AI_* names, ORDERS_END_OF_LIST = 16 - the enum is complete).
    /// Values 0-9 are also part of Riot's server Lua API (S1 luaspellscripthelper.cpp
    /// exports AI_ORDER_NONE..AI_PETHARDRETURN as Lua globals), so keep values exact.
    /// </summary>
    public enum OrderType : byte
    {
        /// <summary>
        /// No order (Riot AI_ORDER_NONE). Initial state and the value orders reset to.
        /// </summary>
        OrderNone = 0x0,
        /// <summary>
        /// Hold position: postpone further movement, targeting still allowed (Riot AI_HOLD).
        /// </summary>
        Hold = 0x1,
        /// <summary>
        /// Move to a location (Riot AI_MOVETO).
        /// </summary>
        MoveTo = 0x2,
        /// <summary>
        /// Attack a targeted unit, moving toward it as needed (Riot AI_ATTACKTO).
        /// </summary>
        AttackTo = 0x3,
        /// <summary>
        /// Postponed spell cast (Riot AI_TEMP_CASTSPELL): set by obj_AI_Base::PostponeSpell
        /// together with ORDER_STATUS_POSTPONED when a cast cannot execute immediately
        /// (out of range / busy); the queued cast retries later and
        /// ClearPostponedSpells resets the order to OrderNone.
        /// </summary>
        TempCastSpell = 0x4,
        /// <summary>
        /// Player-issued pet command: attack this unit (Riot AI_PETHARDATTACK).
        /// </summary>
        PetHardAttack = 0x5,
        /// <summary>
        /// Player-issued pet command: move to this location (Riot AI_PETHARDMOVE).
        /// </summary>
        PetHardMove = 0x6,
        /// <summary>
        /// Attack-move: move to a location, engaging targets found along the way
        /// (Riot AI_ATTACKMOVE).
        /// </summary>
        AttackMove = 0x7,
        /// <summary>
        /// Order forced on a unit while taunted (Riot AI_TAUNT).
        /// </summary>
        Taunt = 0x8,
        /// <summary>
        /// Player-issued pet command: return to owner (Riot AI_PETHARDRETURN; client
        /// HudCursorTargetLogic issues it from the pet-control UI; uses the Move
        /// confirmation voice-over like PetHardMove).
        /// </summary>
        PetHardReturn = 0x9,
        /// <summary>
        /// Stop movement (Riot AI_STOP).
        /// </summary>
        Stop = 0xA,
        /// <summary>
        /// Pet stop command (Riot AI_PETHARDSTOP).
        /// </summary>
        PetHardStop = 0xB,
        /// <summary>
        /// Use an object (Riot AI_USE). No caller found in the visible 4.17 client code
        /// or S1 source - likely vestigial (Dominion capture points use other paths).
        /// </summary>
        Use = 0xC,
        /// <summary>
        /// Continuously attack terrain/attackable map geometry (Riot
        /// AI_ATTACKTERRAIN_SUSTAINED; client gates it on CharState.CanAttack and
        /// plays the Attack confirmation VO like AttackTo).
        /// </summary>
        AttackTerrainSustained = 0xD,
        /// <summary>
        /// Attack terrain once (Riot AI_ATTACKTERRAIN_ONCE; same client handling as
        /// AttackTerrainSustained).
        /// </summary>
        AttackTerrainOnce = 0xE,
        /// <summary>
        /// Cast a spell (Riot AI_CASTSPELL).
        /// </summary>
        CastSpell = 0xF,
    }
}