namespace GameServerCore.Enums
{
    /// <summary>
    /// Death-type value carried in the DeathData wire block. NOT a Riot-named enum — on the wire it
    /// is a raw byte, and the receiving DoNPCDie never reads it. The names here describe the client's
    /// own local derivation in DoDieBroadcast (AIBase.cpp: minion/pet → 0, everything else → 1),
    /// which OVERWRITES whatever the broadcast packet carried. Riot's server sends 0 unconditionally
    /// (all 743 death packets in the 4.20 replay capture); only NPC_Hero_Die passes the field through
    /// untouched — where the server-sent 0 lands anyway. Dead wire field; kept for wire shape.
    /// </summary>
    public enum DieType : byte
    {
        MINION_DIE = 0x0,
        NEUTRAL_DIE = 0x1
    }
}
