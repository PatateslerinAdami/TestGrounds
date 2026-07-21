using System;

namespace GameServerCore.Enums
{
    /// <summary>
    /// The wire ActionState bitfield (Riot CharacterState::CompressedStates). Cast directly to uint
    /// and replicated at ReplicationBucket.Local1 (hero index 0, turret index 2). StatusFlags is
    /// server-internal and mapped here via AttackableUnit.UpdateActionState.
    ///
    /// 4.20 LAYOUT (empirically reconstructed from replays — see memory
    /// "reference_4_20_actionstate_wire_bits"). It is the 4.17 decomp layout (CharacterState.h union
    /// CompressedStates) with a LOCAL +1 SHIFT over 4.17 bits 8-13:
    ///   4.17 {Fleeing8, Suppressed9, Sleep10, NearSight11, Ghosted12, GhostProofEnemies13}
    ///        ->  4.20 {9,10,11,12,13,14}.
    /// The shift STOPS at gpEnemies: 4.17 GhostProofAllies(14) has NO identified 4.20 bit (4.20 bit 15
    /// is a structural always-on bit, not gpAllies — allies pass through Azir's wall). Bits OUTSIDE the
    /// window are UNSHIFTED (Stealthed4, Taunted6, NoRender16, DisableAmbientGold19, DisableAmbientXP20,
    /// Selectable23 all match 4.17). Charm and Reveal follow neither rule (below).
    /// Tags: [V]=replay-verified, [I]=inferred (4.17+shift, not directly measured), [A]=anomalous
    /// (empirical but conflicts with 4.17), [D]=dead/never emitted.
    ///
    /// NOTE: "can't act" CC (silence/snare/stun/disarm/root/pacify) is conveyed by CLEARING the CAN_*
    /// capability bits, NOT by dedicated bits (replay-verified) — matching the M2 model. Suppression
    /// is the exception: it clears caps AND sets the dedicated SUPPRESSED bit.
    /// </summary>
    [Flags]
    public enum ActionState : uint
    {
        // --- mActionState (4.17 low nibble): capability bits ---
        CAN_ATTACK = 1 << 0,   // [V] Disarm clears it (Amumu R, rlp 7c2c52a8)
        CAN_CAST = 1 << 1,     // [V] Silence clears it (rlp f00c9592)
        CAN_MOVE = 1 << 2,     // [V] Snare clears it (Morgana Q, rlp b7c11f34)
        // [V] IMMOVABLE (4.17 mActionState bit 3). Baron/epic monsters set it (rlp 312026bc: Baron
        // ActionState 0x048C808B = bit3 set, CanMove(2) clear) — displacement-immune AND can't move.
        // Turrets do NOT use it (obj_Building; they keep CanMove set). We drive it from !CanMoveEver.
        CAN_NOT_MOVE = 1 << 3,

        STEALTHED = 1 << 4,    // [V] Teemo CamouflageStealth (rlp 15f6ef52); couples with IS_GHOSTED
        // [V] Taunt (rlp 83425536, Rengar/Blitz/Graves) AND Charm (Ahri Seduce, rlp 9c0533a1,
        // Jax/Lux/Talon) BOTH flip bit 6. 4.17 had Taunted=6 / Charmed=15; 4.20 deliberately UNIFIED
        // them into one "ForcedAction" bit (loss of control toward a source). The behaviour diff —
        // taunt walks to the taunter AND attacks, charm walks to the charmer and does NOT attack —
        // lives in the forced-action data, not a separate state bit. UpdateActionState drives bit 6
        // from (Charmed || Taunted) so they never overwrite.
        TAUNTED = 1 << 6,
        CHARMED = 1 << 6,      // shares bit 6 with TAUNTED (4.20 ForcedAction unification; 4.17 was 15)
        // [D] DEAD in 4.20 — fears are FLEE-type (IS_FLEEING). Never emitted. bit 7 is actually a
        // wire always-on structural bit; FEARED=1<<7 is a 4.17 leftover kept as a no-op.
        FEARED = 1 << 7,
        // [D] Vestigial — never emitted (no permanent can't-attack source). Parked at bit 8; inert.
        CAN_NOT_ATTACK = 1 << 8,

        // --- +1-shifted window (4.17 bits 8-14 -> 4.20 bits 9-15) ---
        IS_FLEEING = 1 << 9,           // [V] Shaco "Flee" (rlp 7dea59f9, x4 champs); 4.17 bit 8
        SUPPRESSED = 1 << 10,          // [V] Warwick R "Suppression" (rlp ba5459a2): clears caps AND sets this; 4.17 bit 9
        IS_ASLEEP = 1 << 11,           // [I] 4.17 Sleep(10)+1; not directly measured
        IS_NEAR_SIGHTED = 1 << 12,     // [V] Nocturne R NearSight (rlp 312026bc); 4.17 bit 11
        IS_GHOSTED = 1 << 13,          // [V] Nocturne Q Duskbringer + Teemo-stealth coupling; 4.17 bit 12
        // [V] Azir Emperor's Divide wall soldiers set it TOGETHER with Ghosted(13) — rlp fc985140,
        // unit 0x40002990 ActionState 0x0488E087 (bits 13+14). Ghosted = allies pass through;
        // GhostProofEnemies = enemies still collide (blocked). Matches ShouldIgnoreCollisionDueToGhost.
        // 4.17 GhostProofEnemies(13) +1.
        GHOST_PROOF_ENEMIES = 1 << 14,
        // Wire ALWAYS-ON structural bit — set on EVERY champion (all 120 in the 12-replay audit, 0
        // toggles) AND on minions (Azir wall, Trundle pillar). MEANING UNKNOWN; it never toggles, so
        // it is not an anchorable state. It is NOT GhostProofForAllies: the 4.17 +1 shift would put
        // gpAllies here (4.17 bit 14 -> 15), but allies walk through Azir's *ghosted* wall — impossible
        // if a bit set on everyone were gpAllies. So the +1 shift STOPS at gpEnemies (13->14); gpAllies
        // has no identified 4.20 bit (unused/unanchored). One of three always-on structural bits (7,
        // 15, 26). Modelled only so the position is documented; the server never sets it.
        STRUCTURAL_ALWAYS_ON_15 = 1 << 15,

        // --- unshifted region (4.17 bits >=16 == 4.20) ---
        NO_RENDER = 1 << 16,             // [V] live-confirmed (client stops rendering)
        FORCE_RENDER_PARTICLES = 1 << 17,// [V] live-confirmed: a NoRender'd unit's particle renders again once this bit is set (Ryze DesperatePower test). 4.17 = 17.
        // [A] bit 18 is NOT DodgePiercing (audit: toggles on ~all heroes as one early-game window,
        // not per-dodge). Best hypothesis = Map11 early spawn-lock (S4SpawnLockSpeed, absent in 4.17).
        // A live SetStatus test showed NO standalone client-visible effect -> likely a backend/info
        // flag (like DisableAmbientGold/XP). Not driven by anything; meaning UNCONFIRMED, name is a
        // placeholder.
        SPAWN_LOCK = 1 << 18,
        DISABLE_AMBIENT_GOLD = 1 << 19,  // [V-ish] 4.17 = 19; matches bit19 on Vlad pool / Sion combat-cycle
        DISABLE_AMBIENT_XP = 1 << 20,    // [V] 4.17 = 20; constant on all turrets/structures (rlp x3)
        // Bit 21 DECOMP-CONFIRMED (CharacterState::SetBrushVisibilityFake writes <<21; unshifted region
        // -> same in 4.20). Semantics: treat the unit as if standing in a brush/bush for the vision
        // system (hidden from enemies without vision at its position, without physically being in grass)
        // — a vision-fake flag. No consumer/caller in the decomp slice and never seen in replays, so the
        // trigger/gating is unknown; position is solid, behaviour unanchored.
        BRUSH_VISIBILITY_FAKE = 1 << 21,
        // [D] UNUSED in 4.20 — slows are pure MoveSpeed StatMods and set NO ActionState bit
        // (replay-verified: AkaliWDebuff/Slow don't touch bit 22, no cap cleared; audit: bit 22 never
        // set in any of 12 replays). Kept at the 4.17 position as a no-op.
        SLOWED = 1 << 22,
        // [V] = Riot mSelectable (MOUSE-PICK, NOT attack-targetability!): stays set during Fizz E /
        // Vlad pool untargetability (rlp ba2b1e23 / d89363c7). Attack-targetability is the separate
        // Global-bucket IsTargetable bool.
        SELECTABLE = 1 << 23,
        // [V] bit 25 (Morgana R SoulShackles, rlp b7c11f34, x3 targets; also reveal-on-attack). 4.17
        // had RevealSpecificUnit at bit 5, but bit 5 is EMPTY in 4.20 (scanned 433 replays, never set)
        // — 4.20 moved reveal to bit 25 (the 4.17 mRemain region). No longer an anomaly.
        REVEAL_SPECIFIC_UNIT = 1 << 25,
    }
}
