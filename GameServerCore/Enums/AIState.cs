using System;
namespace GameServerCore.Enums
{
    // Server-internal AI state machine state (Riot AIState). NOTE: this value is NEVER serialized
    // — it lives only in SetAIState/GetAIState and is compared by EQUALITY (never cast to int,
    // never range-checked, never put on the wire). So the numeric ordinals are non-binding and the
    // ordering below is NOT the verified engine order.
    //
    // What IS verified (2026-06-12): the member NAMES (from the client lua symbol dump
    // _all_uppercase_symbols.txt + active use in Scripts/Aggro.lua, Hero.lua, Leashed.lua,
    // UncontrollablePet.lua), and that AI_LAST_NONPET_AI_STATE is a sentinel separating the
    // non-pet states from the AI_PET_* block. The exact intra-group order is NOT recoverable from
    // the decomp (the symbol dump is alphabetical; the enum has no text definition in the
    // mac/ghidra decomp), so within each group this follows the alphabetical symbol-dump order.
    // AI_TARGET_* are a DIFFERENT enum (target-type) and intentionally excluded.
    public enum AIState : uint
    {
        // ---- Non-pet states ----
        AI_ATTACK,
        AI_ATTACKMOVESTATE,
        AI_ATTACKMOVE_ATTACKING,
        AI_ATTACK_GOING_TO_LAST_KNOWN_LOCATION,
        AI_ATTACK_HERO,
        AI_CHARMED,
        AI_FEARED,
        AI_FLEEING,
        AI_FOLLOW,
        AI_FOLLOW_HERO,
        AI_GUARD,
        AI_HALTED,
        AI_HARDATTACK,
        AI_HARDIDLE,
        AI_HARDIDLE_ATTACKING,
        AI_IDLE,
        AI_MOVE,
        AI_RETREAT,
        AI_SHOP,
        AI_SIEGEATTACK,
        AI_SOFTATTACK,
        AI_STANDING,
        AI_STOP,
        AI_TAUNTED,

        // Sentinel: boundary between non-pet and pet states. Riot uses it for range checks
        // ("is this a pet state?"); we don't range-check on it, so its value is only a marker.
        AI_LAST_NONPET_AI_STATE,

        // ---- Pet states ----
        AI_PET_ATTACK,
        AI_PET_ATTACKMOVE,
        AI_PET_ATTACKMOVE_ATTACKING,
        AI_PET_HARDATTACK,
        AI_PET_HARDIDLE,
        AI_PET_HARDIDLE_ATTACKING,
        AI_PET_HARDMOVE,
        AI_PET_HARDRETURN,
        AI_PET_HARDSTOP,
        AI_PET_HOLDPOSITION,
        AI_PET_HOLDPOSITION_ATTACKING,
        AI_PET_IDLE,
        AI_PET_MOVE,
        AI_PET_RETURN,
        AI_PET_RETURN_ATTACKING,
        AI_PET_SPAWNING
    }
}
