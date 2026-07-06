using System;

namespace GameServerCore.Enums
{
    /// <summary>
    /// Unit classification tags read from a unit's character data <c>UnitTags</c> field (pipe-separated,
    /// e.g. "Minion | Minion_Lane", "Monster | Monster_Large", "Ward"). This is how Riot distinguishes a
    /// ward from a minion from a monster etc. — both spawn through the same SpawnMinion path; the TAG in
    /// the data decides what the unit is. Server-internal classification (NOT sent on the wire — the client
    /// reads its own UnitTags), so it drives targeting filters / AI acquisition / ward bookkeeping.
    ///
    /// Names, ORDER and bit values mirror the 4.20 building-block registry verbatim
    /// (luaobj-conv/DATA/BuildingBlocks/ObjectTags.lua `_BuildTags`: None = 0, Champion = 1, then doubling
    /// down the list). Members MUST stay name-identical to the data strings — CharData parses them by name
    /// (Enum.TryParse), and an unregistered tag (e.g. the data's bare "Special") simply fails to parse and
    /// is ignored, exactly as ParseUnitTagFlags ignores it in 4.20.
    ///
    /// NOTE: previously the members had no explicit values, so `[Flags]` numbered them 0,1,2,3,… instead of
    /// powers of two — bitwise OR and HasFlag produced false positives (e.g. Monster|Monster_Large == 15,
    /// which falsely HasFlag(Minion_Lane)). Fixed to proper 1&lt;&lt;N flags.
    /// </summary>
    [Flags]
    public enum UnitTag
    {
        None = 0,
        Champion = 1 << 0,
        Champion_Clone = 1 << 1,
        Minion = 1 << 2,
        Minion_Lane = 1 << 3,
        Minion_Lane_Siege = 1 << 4,
        Minion_Lane_Super = 1 << 5,
        Minion_Summon = 1 << 6,
        Monster = 1 << 7,
        Monster_Epic = 1 << 8,
        Monster_Large = 1 << 9,
        Special_EpicMonsterIgnores = 1 << 10,
        Special_SyndraSphere = 1 << 11,
        Special_TeleportTarget = 1 << 12,
        Special_Tunnel = 1 << 13,
        Structure = 1 << 14,
        Structure_Inhibitor = 1 << 15,
        Structure_Nexus = 1 << 16,
        Structure_Turret = 1 << 17,
        Structure_Turret_Outer = 1 << 18,
        Structure_Turret_Inner = 1 << 19,
        Structure_Turret_Inhib = 1 << 20,
        Structure_Turret_Nexus = 1 << 21,
        Structure_Turret_Shrine = 1 << 22,
        Ward = 1 << 23
    }

    /// <summary>
    /// Bitfield queries over <see cref="UnitTag"/>, mirroring Riot's tag tests on the UnitTagFlags
    /// bitmask (ObjectTags.lua builds the per-bit flags; the engine queries with numeric AND — same verbs
    /// as the generic Metadata tag API: HasTag / ContainsAny / ContainsAll). Use these instead of raw
    /// <c>&amp;</c> / <c>HasFlag</c> at call sites for clarity and to avoid HasFlag's boxing.
    /// </summary>
    public static class UnitTagExtensions
    {
        /// <summary>True if <paramref name="tags"/> carries every bit of <paramref name="tag"/> (a single
        /// tag, or all tags of a composite). Equivalent to Enum.HasFlag, without the boxing.</summary>
        public static bool HasTag(this UnitTag tags, UnitTag tag) => (tags & tag) == tag;

        /// <summary>True if <paramref name="tags"/> shares ANY bit with <paramref name="mask"/> — Riot's
        /// ContainsAny / the RequiredUnitTags OR-list semantics. Always false for an empty mask.</summary>
        public static bool ContainsAny(this UnitTag tags, UnitTag mask) => (tags & mask) != UnitTag.None;

        /// <summary>True if <paramref name="tags"/> carries ALL bits of <paramref name="mask"/> — Riot's
        /// ContainsAll. Vacuously true for an empty mask.</summary>
        public static bool ContainsAll(this UnitTag tags, UnitTag mask) => (tags & mask) == mask;
    }
}
