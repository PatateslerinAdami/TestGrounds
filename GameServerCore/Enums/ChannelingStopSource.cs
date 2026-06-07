namespace GameServerCore.Enums
{
    /// <summary>
    /// What caused a channel to end.
    /// Verified against the S4 mac decomp: exact match with
    /// Spell::Channeling::StopSource (ChannelingEnums.h, values 0-11, kCount=12).
    /// The previous extra member "PlayerCommand" did not exist at Riot and shifted
    /// our values past 10 - removed; player release/recast of charge channels maps
    /// to NotCancelled (Riot Lua: TF WildCards second press stops with
    /// (NotCancelled, NotCancelled).
    /// Decomp semantics (Channeling::ChannelingStopGeneric, Channeling.cpp:90):
    /// TimeCompleted / Animation / Unknown skip the spellbook cast-stop;
    /// Move first asks the spellbook whether movement is allowed mid-channel.
    /// </summary>
    public enum ChannelingStopSource
    {
        /// <summary>
        /// No external cause so the spell itself ends the channel to move on
        /// (e.g. player releases/recasts a charge channel to fire it).
        /// </summary>
        NotCancelled = 0,
        TimeCompleted = 1,
        Animation = 2,
        LostTarget = 3,
        StunnedOrSilencedOrTaunted = 4,
        ChannelingCondition = 5,
        Die = 6,
        HeroReincarnate = 7,
        Move = 8,
        Attack = 9,
        /// <summary>Another spell cast broke the channel.</summary>
        Casting = 10,
        Unknown = 11
    }
}
