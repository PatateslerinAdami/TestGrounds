using System;

namespace GameServerCore.Enums
{
    /// <summary>
    /// The flags byte of AddRegion (0x23) / AddConeRegion (0x12E).
    /// Verified 2026-06-08 against the S4 mac decomp
    /// (RegionManager::CreateRegionFromAddRegionPkt) + 17,532 4.20 AddRegion packets:
    /// the client reads ONLY bits 0-2; bits 3-7 are uninitialized Riot junk (they vary
    /// 36-64% across packets, same pattern as the NPC_Die bitfield - mask before
    /// byte-diffing, don't send meaning in them).
    ///
    /// Replay corroboration: bit1 (GrantVision) is set on 100% of regions (they ARE
    /// vision bubbles); bit0 (RequiresLOS) 53% (wards need line-of-sight, fountain
    /// regions don't); bit2 (RevealStealth) 20%.
    ///
    /// bit0 (RESOLVED 2026-06-08): now wire-true RequiresLOS (region->mRequiresLOS =
    /// flags &amp; 1), sent as !Region.IgnoresLineOfSight. The server's separate HasCollision
    /// concept (a LeagueSandbox extension, set when PathfindingRadius > 0) was previously
    /// overloaded onto this same bit; it is now server-internal only (drives
    /// CollisionHandler) and deliberately off-wire. Don't re-map HasCollision onto bit0.
    /// </summary>
    [Flags]
    public enum RegionFlags : byte
    {
        None = 0,
        /// <summary>
        /// Riot: region vision requires line-of-sight (blocked by terrain) - true for
        /// ward/unit perception bubbles, false for fountain regions that reveal an area
        /// unconditionally. (Server currently reuses this bit as HasCollision.)
        /// </summary>
        RequiresLOS = 1 << 0,
        /// <summary>The region grants vision. Set on 100% of replay regions.</summary>
        GrantVision = 1 << 1,
        /// <summary>
        /// The region's granted vision also reveals stealthed units. Only meaningful when
        /// <see cref="GrantVision"/> is set (the client reads it inside the bit1 branch).
        /// </summary>
        RevealStealth = 1 << 2
        // bits 3-7: not read by the client - uninitialized Riot junk, never assign meaning.
    }
}
