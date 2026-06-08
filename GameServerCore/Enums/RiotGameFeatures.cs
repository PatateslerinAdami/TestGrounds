using System;

namespace GameServerCore.Enums
{
    /// <summary>
    /// Riot's client FeaturesConfig bitfield, sent as the uint64 GameFeatures field of
    /// SynchVersionS2C (0x54). Bit N = FeatureType N; the client does
    /// FeaturesConfig::InitFromBitField(value) and gates client behavior on
    /// IsEnabled(feature) == (flags &amp; (1 &lt;&lt; feature)).
    /// Verified against the S4 mac decomp
    /// (Core/RiotStandard/DesignPatterns/FeaturesConfig.h, enum class FeatureType,
    /// kNumFeatures = 30). These are CLIENT features (cursors, indicators, metrics, ...),
    /// distinct from the server-internal <see cref="FeatureFlags"/>.
    /// </summary>
    [Flags]
    public enum RiotGameFeatures : ulong
    {
        None = 0,
        Equalize = 1UL << 0,
        FoundryOptions = 1UL << 1,
        OldOptions = 1UL << 2,
        FoundryQuickChat = 1UL << 3,
        EarlyWarningForFOWMissiles = 1UL << 4,
        AnimatedCursors = 1UL << 5,
        ItemUndo = 1UL << 6,
        NewPlayerRecommendedPages = 1UL << 7,
        HighlightLineMissileTargets = 1UL << 8,
        ControlledChampionIndicator = 1UL << 9,
        AlternateBountySystem = 1UL << 10,
        NewMinionSpawnOrder = 1UL << 11,
        TurretRangeIndicators = 1UL << 12,
        GoldSourceInfoLogDump = 1UL << 13,
        ParticleSkinNameTech = 1UL << 14,
        NetworkMetrics = 1UL << 15,
        HardwareMetrics = 1UL << 16,
        TruLagMetrics = 1UL << 17,
        DradisSDK = 1UL << 18,
        ServerIPLogging = 1UL << 19,
        JungleTimers = 1UL << 20,
        TraceRouteMetrics = 1UL << 21,
        IsLolbug19805LoggingEnabled = 1UL << 22,
        IsLolbug19805HackyTourniquetEnabled = 1UL << 23,
        TurretMemory = 1UL << 24,
        TimerSyncForReplay = 1UL << 25,
        RegisterWithLocalServiceDiscovery = 1UL << 26,
        MinionFarmingBounty = 1UL << 27,
        TeleportToDestroyedTowers = 1UL << 28,
        NonRefCountedCharacterStates = 1UL << 29,

        /// <summary>
        /// The exact bitfield captured from a real 4.20 game (= 662166610). Sent verbatim
        /// in SynchVersionS2C so the client enables the same feature set Riot's 4.20
        /// servers did. Decomposed into named bits for readability; the numeric value is
        /// unchanged.
        /// </summary>
        Default420 = FoundryOptions | EarlyWarningForFOWMissiles | ItemUndo
            | AlternateBountySystem | NewMinionSpawnOrder | TurretRangeIndicators
            | ParticleSkinNameTech | NetworkMetrics | HardwareMetrics | TruLagMetrics
            | DradisSDK | JungleTimers | TraceRouteMetrics | IsLolbug19805LoggingEnabled
            | TurretMemory | TimerSyncForReplay | RegisterWithLocalServiceDiscovery
            | NonRefCountedCharacterStates
    }
}
