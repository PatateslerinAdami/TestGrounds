namespace GameServerCore.Enums
{
    // Per-PAR-type visual bar states. The SetPARState packet (0x93) carries a "PARStateID" (int) =
    // an INDEX into the per-PAR-type state list defined in the client config DATA\Menu_SC4\PARStates.ini.
    // Each state swaps the resource bar's color + animated texture (VideoPrefix). The meaning is PER
    // PAR TYPE — and not even consistently ordered (see Aatrox below) — so there is intentionally NO
    // single global "PARState" enum. Use these champion/type-specific enums at the call sites and cast
    // to uint for the packet field. (Verified against PARStates.ini + the 4.17 mac decomp PARTypeHelper.)

    /// <summary>Rumble Heat bar — PARStates.ini [Heat], 3 states (grey → yellow → red).</summary>
    public enum RumbleHeatState : uint
    {
        Normal = 0,    // bar_par_heat_low (grey)
        Warning = 1,   // bar_par_heat_warning (yellow)
        Overheat = 2,  // bar_par_heat_overheat (red)
    }

    /// <summary>Gnar Rage bar — PARStates.ini [Gnarfury], 3 states (calm → building → full rage/transform).</summary>
    public enum GnarFuryState : uint
    {
        Calm = 0,      // bar_par_tantrum_calm (grey)
        Building = 1,  // bar_par_tantrum_warning (yellow)
        Raging = 2,    // bar_par_tantrum_active (red)
    }

    /// <summary>Aatrox Blood Well — PARStates.ini [BloodWell], 2 states. NOTE: index order is INVERTED (0 = active/full).</summary>
    public enum AatroxBloodWellState : uint
    {
        Active = 0,    // bar_par_bloodwell_active (dark red) — well filled
        Low = 1,       // bar_par_bloodwell_low (light)
    }

    /// <summary>Rengar Ferocity — PARStates.ini [Ferocity], 2 states.</summary>
    public enum RengarFerocityState : uint
    {
        Low = 0,        // bar_par_ferocity_low (grey)
        Empowered = 1,  // bar_par_ferocity_active (orange) — empowered ability ready
    }

    /// <summary>Yasuo Flow — PARStates.ini [Wind], 2 states.</summary>
    public enum YasuoFlowState : uint
    {
        Building = 0,     // bar_par_wind (blue) — building Flow
        ShieldReady = 1,  // bar_par_windShield (white) — Flow full, shield ready
    }
}
