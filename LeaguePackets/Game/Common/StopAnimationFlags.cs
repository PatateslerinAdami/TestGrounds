using System;

namespace LeaguePackets.Game.Common
{
    /// <summary>
    /// Flag bits for the <see cref="LeaguePackets.Game.S2C_StopAnimation"/> packet (cmd 0x29).
    /// Stored as a single byte on the wire.
    ///
    /// <para>S4 client (`obj_AI_Base::StopAnimation` at obj_AI_Base.cpp:27523) reads only
    /// <see cref="Fade"/>, <see cref="IgnoreLock"/>, <see cref="StopAll"/>. The dispatch in
    /// `obj_AI_Base_PImpl_Int::OnNetworkPacket(PKT_S2C_StopAnimation_s)` masks bits 0x01/0x02/0x04
    /// from the flags byte and forwards them as individual bools.</para>
    ///
    /// <para>Bits <see cref="Unknown_0x10"/> and <see cref="Unknown_0x20"/> are observed set in the
    /// replay (flags=0x33) on natural channel-end StopAnimation, but no S4 client code reads them
    /// directly. Empirically required for unlocking the spell-driven looping animation that
    /// `PlaySpellAnimation` installs (KatarinaR fix: without these the override layer
    /// stays sticky after channel-end). Likely consumed in a code path not visible in the visible
    /// decomp output, or vestigial from an older patch. Leave as Unknown_* until verified.</para>
    /// </summary>
    [Flags]
    public enum StopAnimationFlags : byte
    {
        None         = 0,
        Fade         = 1 << 0,  // 0x01
        IgnoreLock   = 1 << 1,  // 0x02
        StopAll      = 1 << 2,  // 0x04 — only applied when AnimationName is empty
        // bit 0x08 unobserved on the wire
        Unknown_0x10 = 1 << 4,  // 0x10 — empirically required pair with Unknown_0x20 for channel-end unlock
        Unknown_0x20 = 1 << 5,  // 0x20 — see above
    }
}
