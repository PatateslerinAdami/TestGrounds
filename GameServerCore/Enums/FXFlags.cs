using System;

namespace GameServerCore.Enums
{
    /// <summary>
    /// Flag word (ushort on the wire) of FXCreateGroupData in FX_Create_Group (0x87).
    /// Verified against S1 source (spelleffects.cpp Client_spellEffectCreate,
    /// full flag branches) + S4 mac decomp (SpellEffectClient.cpp CreateClientEffect -
    /// lifted body is abbreviated but its DWARF locals confirm the same branches) +
    /// 313,700 FX groups across 26 replays (4.20 wire usage).
    /// Riot 4.x only ever SENDS bits 4-8; bits 0-3 have client-side handling but never
    /// appear on the wire in our replays.
    /// </summary>
    [Flags]
    public enum FXFlags : ushort
    {
        None = 0,
        /// <summary>
        /// Particle persists (client sets EffectEmitter.bKeepAlive = 1).
        /// Client-supported, never sent by Riot in 4.x replays.
        /// </summary>
        KeepAlive = 1 << 0,
        // 1 << 1: no client consumer found (S1 or S4), never sent - presumed dead.
        /// <summary>
        /// Particle is scaled to the bound object's bounding box
        /// (client SetScale(bbox) + SetGlobalScale). Never sent by Riot in 4.x replays.
        /// </summary>
        ScaleToBoundObject = 1 << 2,
        /// <summary>
        /// Position-only: skip creating bone/AI attachments for bind and target objects.
        /// (S1 attachment branches are gated on this bit being CLEAR; 4.17 DisplayParticle
        /// checks it for the attachment cache.) Never sent by Riot in 4.x replays.
        /// </summary>
        PositionOnly = 1 << 3,
        /// <summary>
        /// Particle faces the direction given in FXCreateData.OrientationVector.
        /// Riot's own name: EFFCREATE_UPDATE_ORIENTATION (S1 Lua export, value 16).
        /// Replay-verified: 100% of flagged entries (17,662) carry a non-zero
        /// orientation vector. Send this whenever a direction is supplied.
        /// </summary>
        UpdateOrientation = 1 << 4,
        /// <summary>
        /// Particle keeps simulating while off screen (client EffectEmitter
        /// FLAG_SIMULATE_WHILE_OFF_SCREEN). Riot's near-universal default:
        /// set on 98% of all FX groups - server-replicated particles must keep
        /// simulating so their state is correct when scrolled into view.
        /// (Previously misnamed "BindDirection".)
        /// </summary>
        SimulateWhileOffScreen = 1 << 5,
        /// <summary>
        /// Particle is oriented to the terrain slope: client samples the nav-grid
        /// ground normal at the position and builds the rotation from it
        /// (S1 GetNormalForPosition branch). Used for ground decals on slopes (~0.2%).
        /// </summary>
        OrientToGroundNormal = 1 << 6,
        /// <summary>
        /// Particle faces its target (client EffectEmitter FLAG_DIRECTION_FACE_TARGET).
        /// Replay: 95% of flagged groups carry a TargetNetID. Riot always combines
        /// this with <see cref="SimulateWhileOffScreen"/> (0xa0), never sends it alone.
        /// </summary>
        TargetDirection = 1 << 7,
        /// <summary>
        /// A particle parameter is driven by the bound unit's primary ability resource
        /// (client binds a PARFlexValue via AddFlexFloatValue) - e.g. fill-state effects
        /// that follow mana/energy. ~1.8% of groups. (Previously "Unknown7".)
        /// </summary>
        PARDriven = 1 << 8
    }
}
