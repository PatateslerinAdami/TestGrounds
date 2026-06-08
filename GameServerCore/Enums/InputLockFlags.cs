using System;

namespace GameServerCore.Enums
{
    /// <summary>
    /// Input lock bits for S2C_SetInputLockFlag (0x84) / S2C_ToggleInputLockFlag (0x9B).
    /// Verified against the S4 mac decomp: exact match with `InputLockType`
    /// (UX/Hud/InputLockTypeEnums.h) - names and values are Riot's own.
    /// Client keeps a uint32 bitmask (InputLock singleton): Set = OR/AND-NOT,
    /// Toggle = XOR, so multiple bits may be combined in one packet.
    /// </summary>
    [Flags]
    public enum InputLockFlags : uint
    {
        None = 0x0,
        CameraLocking = 0x1,
        Movement = 0x2,
        Abilities = 0x4,
        SummonerSpells = 0x8,
        Shop = 0x10,
        Chat = 0x20,
        MinimapMovement = 0x40,
        CameraMovement = 0x80,
        /// <summary>
        /// Not in the 4.17 client header it was added with the Sion rework (patch 4.18).
        /// Replay-verified (4.20): sent as a triple (Movement | this | CameraLocking),
        /// ON at Sion R channel start and OFF exactly at the Spell4_Hit/Spell4_STOP
        /// end animation. Specific to forced-movement steering (Sion R marches forward,
        /// movement clicks are redirected to steering) - NOT sent for Vel'Koz R even
        /// though that is also a steerable channel (verified: Velkoz-perspective replays
        /// with ~18 R casts contain zero input-lock packets). Exact UI gate unknown.
        /// </summary>
        ForcedMovementSteering = 0x100
    }
}
