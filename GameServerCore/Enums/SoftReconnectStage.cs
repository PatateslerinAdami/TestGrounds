namespace GameServerCore.Enums
{
    /// <summary>
    /// Stage of the soft-reconnect GC mark-and-sweep state resync, carried by
    /// S2C_MarkOrSweepForSoftReconnect (0xEF). Verified vs the 4.17 mac decomp
    /// (PKT_S2C_MarkOrSweepForSoftReconnect_s::Stage).
    /// </summary>
    public enum SoftReconnectStage : byte
    {
        /// <summary>Mark ALL of the reconnecting client's objects — sent BEFORE re-replicating the world.</summary>
        MarkAllUnits = 0,
        /// <summary>Sweep — destroy objects still marked (= stale/ghost) after the re-replication refreshed the live ones.</summary>
        DestroyAllUnits = 1,
    }
}
