using System;

namespace LeaguePackets.Game.Common
{
    /// <summary>
    /// Flag bits for the <see cref="LeaguePackets.Game.S2C_StopAnimation"/> packet (cmd 0x29).
    /// Stored as a single byte on the wire.
    ///
    /// <para>S4 client (`obj_AI_Base::StopAnimation` at obj_AI_Base.cpp:27523) reads only
    /// <see cref="FadeOut"/>, <see cref="IgnoreLock"/>, <see cref="StopAll"/>. The dispatch in
    /// `obj_AI_Base_PImpl_Int::OnNetworkPacket(PKT_S2C_StopAnimation_s)` masks bits 0x01/0x02/0x04
    /// from the flags byte and forwards them as individual bools.</para>
    ///
    /// <para>The S4 client reads ONLY the low 3 bits. Every other bit (0x08/0x10/0x20/0x40/0x80)
    /// is present on the wire but ignored by this handler — the dispatch masks nothing above 0x04.
    /// The 4.20 replay shows the whole high nibble varying: bit 0x02 (IgnoreLock) is set on
    /// essentially every StopAnimation packet, 0x04 (StopAll) is never observed (only used with an
    /// empty AnimationName), and 0x08/0x10/0x20/0x40/0x80 all appear in various combinations
    /// (e.g. flags 0x1a, 0x3a, 0xba, 0xfa). These upper bits are likely a server-side field packed
    /// into the same byte, or consumed by a code path not in the visible decomp. Their meaning is
    /// unverified; the client-relevant semantics are fully captured by the low 3 bits below.</para>
    ///
    /// <para><see cref="Unknown_0x10"/> + <see cref="Unknown_0x20"/> (flags=0x33) are seen on
    /// natural channel-end StopAnimation and were empirically required to unlock the spell-driven
    /// looping animation `PlaySpellAnimation` installs (KatarinaR fix). Kept as named bits for that
    /// use even though the client handler does not read them.</para>
    /// </summary>
    [Flags]
    public enum StopAnimationFlags : byte
    {
        None         = 0,
        FadeOut         = 1 << 0,  // 0x01 — verified: dispatch masks (flags & 1); parser bool `fade`
        IgnoreLock   = 1 << 1,  // 0x02 — verified: dispatch masks (flags >> 1 & 1); set on ~every wire packet
        StopAll      = 1 << 2,  // 0x04 — verified: dispatch masks (flags & 4) >> 2; only applied when AnimationName is empty; never seen on wire
        // bits 0x08 / 0x40 / 0x80 ARE observed on the wire (4.20) but are not read by the S4 client handler
        //Unknown_0x10 = 1 << 4,  // 0x10 — on wire, not client-read; pairs with 0x20 for channel-end unlock (KatarinaR)
        //Unknown_0x20 = 1 << 5,  // 0x20 — see above
    }
}
