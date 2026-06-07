using System;

namespace GameServerCore.Enums
{
    /// <summary>
    /// Flag bits of the Tips::Config wire struct (C2S_ClientReady / SynchVersionS2C
    /// TipConfig field). Verified against the S4 mac decomp:
    /// Tips::Config FLAG_* constants (UX/Tips/Tips.h) - exact Riot names and values.
    /// </summary>
    [Flags]
    public enum TipFlags : byte
    {
        None = 0,
        /// <summary>Riot FLAG_ENABLED - the tips system is enabled for this client.</summary>
        Enabled = 1,
        /// <summary>Riot FLAG_REPORT.</summary>
        Report = 2,
        /// <summary>Riot FLAG_LOADINGSCREEN - show the startup tip on the loading screen.</summary>
        LoadingScreen = 4,
        /// <summary>Riot FLAG_INGAME - show the startup tip in game.</summary>
        InGame = 8,
        /// <summary>Riot FLAG_DEBUG.</summary>
        Debug = 16
    }
}
