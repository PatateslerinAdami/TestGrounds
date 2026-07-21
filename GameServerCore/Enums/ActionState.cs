using System;

namespace GameServerCore.Enums
{
    [Flags]
    public enum ActionState : uint
    {
        CAN_ATTACK = 1 << 0,
        CAN_CAST = 1 << 1,
        CAN_MOVE = 1 << 2,
        CAN_NOT_MOVE = 1 << 3,
        STEALTHED = 1 << 4,
        // Wire bit REPLAY-VERIFIED = 25 (Morgana R SoulShackles reveal, rlp b7c11f34: bit25 spans
        // the whole buff on Riven/Darius/FiddleSticks — independent of the end-stun (clears caps
        // 0/1/2) and the slow (a StatMod, no bit). Also the "reveal-on-attack" transient seen on
        // stealthed units. Was 1<<5.
        REVEAL_SPECIFIC_UNIT = 1 << 25,
        // Wire bit REPLAY-VERIFIED = 6, the SAME bit as CHARMED (rlp 83425536: "TAUNT" buff on
        // Rengar/Blitzcrank/Graves flips bit6, identical to Ahri Seduce). 4.20 uses ONE
        // "controlled / forced-action" bit for both charm and taunt — the direction/target comes
        // from the forced-movement data, not a separate state bit. UpdateActionState drives bit6
        // from (Charmed || Taunted) so the two never overwrite each other.
        TAUNTED = 1 << 6,
        // DEAD in 4.20 — no source sets fear as an ActionState bit (fears are FLEE-type → IS_FLEEING
        // bit 9). Never emitted (UpdateActionState hardcodes false). bit 7 is actually a wire
        // always-on structural bit; this FEARED=1<<7 label is a 4.17 leftover, kept only as a no-op.
        FEARED = 1 << 7,
        // Vestigial: never set by the server (no permanent can't-attack source, see UpdateActionState).
        // Parked at the now-free bit 8 to avoid colliding with IS_FLEEING; its true 4.20 wire position
        // is unverified, but since it is never emitted the position is inert.
        CAN_NOT_ATTACK = 1 << 8,
        // Wire bit REPLAY-VERIFIED = 9 (Shaco JackInTheBox "Flee" buff on feared champs, rlp
        // 7dea59f9: bit9 flips with every Flee add/expire across MasterYi/Caitlyn/Morgana/Jax, with
        // CanMove cleared alongside). Was 1<<8. This REFUTES the "IS_FLEEING never set" note in
        // UpdateActionState — that measured the wrong (4.17) bit 8; the real flee bit (9) flips on
        // every fear.
        IS_FLEEING = 1 << 9,
        IS_ASLEEP = 1 << 10,
        // Wire bit REPLAY-VERIFIED = 12 (Nocturne R NearSight on enemies, rlp 312026bc: bit12
        // flips with every NearSight add/expire). Was 1<<11.
        IS_NEAR_SIGHTED = 1 << 12,
        // Wire bit REPLAY-VERIFIED = 13 (Nocturne DuskbringerHaste ghost, rlp 312026bc: bit13
        // flips with every buff add/expire). Was 1<<12 — that bit is not the client's ghost bit.
        IS_GHOSTED = 1 << 13,
        // Wire bit REPLAY-VERIFIED = 6 (Ahri AhriSeduce charm, rlp 9c0533a1: bit6 flips with every
        // Seduce add/expire across Jax/Lux/Talon, CanAttack cleared alongside). Was 1<<15 (bit 15 is
        // a wire always-on bit, never the charm state).
        CHARMED = 1 << 6,
        NO_RENDER = 1 << 16,
        FORCE_RENDER_PARTICLES = 1 << 17,
        TARGETABLE = 1 << 23
    }
}