namespace GameServerCore.Enums
{
    /// <summary>
    /// The six replication buckets, mirroring Riot's <c>ReplicationType</c> (decomp
    /// AI/Replication/ReplicationManager.h). The integer value here is the WIRE bucket INDEX (the bit
    /// position in the <c>OnReplication</c> setMaps byte: 0..5), NOT Riot's enum bit-VALUE (1/2/4/8/16/32).
    ///
    /// Each bucket reaches a different observer tier. Replay-verified (4.20 SR, 3 POVs): only
    /// <see cref="ClientOnly"/> is owner-restricted — exactly one unit per game ever receives it (the
    /// recorder's own champion). <see cref="Local1"/>/<see cref="Local2"/>/<see cref="Map"/>/
    /// <see cref="Global"/> all reach any client with vision of the unit (allies AND enemies alike,
    /// consistent with enemies seeing health bars). See docs/REPLICATION_VISIBILITY_SCOPING_PLAN.md.
    ///
    /// NOTE: our per-field placement is replay-verified against the 4.20 wire — e.g. CurrentHealth/Mana sit
    /// in <see cref="Map"/> (a 4.20 champion's bit-3 var0 is HP=MaxHP at spawn, then regens) and ActionState
    /// in <see cref="Local1"/> (bit-1 var0 is the status bitfield). The 4.17 mac decomp (AIHero.cpp) has the
    /// OPPOSITE placement (HP in LocalRepData1, ActionState in MapRepData) — Riot reorganized the buckets
    /// between 4.17 and 4.20, so the decomp is NOT authoritative for 4.20 field→bucket placement; the replay
    /// is. The names here label the wire bucket POSITION (matching the 4.20 ReplicationType bit order).
    /// </summary>
    public enum ReplicationBucket
    {
        /// <summary>CLIENT_ONLY_REP_DATA — owner only: gold, spell can-cast bits, per-slot mana costs, evolve.</summary>
        ClientOnly = 0,
        /// <summary>LOCAL_REP_DATA1 — health, mana, exp, base stats. Reaches any in-vision observer.</summary>
        Local1 = 1,
        /// <summary>LOCAL_REP_DATA2 — bonus penetration (hero-only). Reaches any in-vision observer.</summary>
        Local2 = 2,
        /// <summary>MAP_REP_DATA — movespeed, level, action state, immunities, targetability. Any in-vision observer.</summary>
        Map = 3,
        /// <summary>ONVISIBLE_REP_DATA — empty in patch 4.x (reserved).</summary>
        OnVisible = 4,
        /// <summary>GLOBAL_REP_DATA — structure targetability flags. Sent broadly.</summary>
        Global = 5,
    }
}
