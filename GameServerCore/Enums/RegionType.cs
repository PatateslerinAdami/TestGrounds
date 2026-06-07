namespace GameServerCore.Enums
{
    /// <summary>
    /// Region type for AddRegion (0x23) / AddConeRegion packets.
    /// Verified via replays (17,532 AddRegion packets across 26 replays):
    /// Riot 4.20 sends ONLY -2 (every ward/unit perception bubble, 17,530x) and
    /// -1 (exactly the two permanent team fountain regions). The value 0 is NEVER
    /// on the 4.20 wire. The 4.17 mac-decomp handler is unreliable here (mangled
    /// lift with reordered assert lines), and the packet layout itself changed
    /// between 4.17 (variable data[] + DWORD flags) and 4.20 (byte flags + fixed
    /// BaseRadius float), so the replays are the authority.
    /// </summary>
    public enum RegionType
    {
        /// <summary>
        /// Never observed on the 4.20 wire - do not send. (Pre-4.18 clients gated a
        /// variable-payload region path on 0; semantics in 4.20 unknown.)
        /// </summary>
        Default = 0,
        /// <summary>
        /// Observed only for the two permanent team fountain regions (one per team,
        /// radius 1600, GrantVision + RevealStealth, no bound unit, infinite TTL).
        /// </summary>
        Fountain = -1,
        /// <summary>
        /// Riot's standard circle region: all ward/unit/effect perception bubbles
        /// (radii 50-400, bound or unbound). Use this as the default.
        /// </summary>
        Circle = -2
    }
}
