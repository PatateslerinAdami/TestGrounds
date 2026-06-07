namespace GameServerCore.Enums
{
    /// <summary>
    /// Why a channel ended, from the channel's own point of view.
    /// Verified against the S4 mac decomp: exact match with
    /// Spell::Channeling::StopCondition (ChannelingEnums.h: kNotCancelled=0,
    /// kSuccess=1, kCancel=2, kCount=3) - the enum is complete.
    /// Also part of Riot's server Lua API (BBStopChanneling StopCondition param,).
    /// </summary>
    public enum ChannelingStopCondition
    {
        /// <summary>
        /// The channel stopped without being cancelled - the spell transitions into its
        /// next phase. Riot Lua example: TF WildCards second press (card lock-in) stops
        /// the shuffle channel with (NotCancelled, NotCancelled).
        /// </summary>
        NotCancelled = 0,
        /// <summary>
        /// The channel completed successfully. Riot Lua example: MissFortuneBulletTime
        /// full-duration end uses (Success, TimeCompleted).
        /// </summary>
        Success = 1,
        /// <summary>
        /// The channel was aborted (death, movement, cc, new cast, lost target, ...).
        /// </summary>
        Cancel = 2
    }
}
