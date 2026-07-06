namespace GameServerCore.Enums
{
    /// <summary>
    /// CLIENT-side floating-text visibility filter (the player's "show floating text for" option),
    /// mirroring the 4.20 mac decomp FloatingTextManager::FloatTextFilters. The client decides which
    /// floating text to render from this setting plus the text's source/target relative to the local
    /// player. Reference only — the server does NOT consume this (display filtering is fully client-side).
    /// </summary>
    public enum FloatTextFilters
    {
        None = 0,
        Target = 1,
        Player = 2,
        Heroes = 3,
        All = 4,
        Max = 5,
    }
}
