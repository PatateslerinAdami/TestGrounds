namespace AIScripts.Common
{
    /// <summary>
    /// Enum representing different types of pings a bot can send.
    /// Used to categorize ping intent (serious vs toxic).
    /// </summary>
    public enum BotPingType
    {
        /// <summary>
        /// Standard communication ping (Attack, OnMyWay, RequestHelp)
        /// Used when coordinating with allies.
        /// </summary>
        Serious,

        /// <summary>
        /// Negative/unsportsmanlike ping (Missing ping spam, etc.)
        /// Used when allies die or make mistakes.
        /// </summary>
        Toxic
    }

    /// <summary>
    /// Enum representing different categories of chat messages a bot can send.
    /// </summary>
    public enum BotChatType
    {
        /// <summary>
        /// Normal gameplay communication (strategy, calls, etc.)
        /// </summary>
        SeriousTalk,

        /// <summary>
        /// Unsportsmanlike/rude messages (taunts, insults, etc.)
        /// </summary>
        TrashTalk
    }
}
