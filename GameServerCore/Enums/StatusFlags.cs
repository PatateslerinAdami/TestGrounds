using System;

namespace GameServerCore.Enums
{
    /// <summary>
    /// Enum of all statuses that can be applied to a unit.
    ///
    /// M2 Phase 3 (2026-06-27): the invented CC flags Stunned (1&lt;&lt;26), Rooted (1&lt;&lt;22), Silenced
    /// (1&lt;&lt;23), Disarmed (1&lt;&lt;7), Netted (1&lt;&lt;17) and Pacified (1&lt;&lt;19) were REMOVED — Riot
    /// has no such states. Those CCs are now pure capability disables (a STUN clears CanMove+CanAttack+CanCast,
    /// a SNARE clears CanMove, etc. — see BuffTypeExtensions.ToCapabilityDisable), matching the replay-verified
    /// wire (ActionState clears the positive CAN_MOVE/CAN_ATTACK/CAN_CAST bits, never sets CAN_NOT_*). Charmed/
    /// Feared/Taunted/Sleep/Suppressed/NearSighted remain — they ARE real Riot CharacterState bits. The freed
    /// bit positions are left unused (not renumbered) to avoid churn.
    /// </summary>
    [Flags]
    public enum StatusFlags
    {
        None,
        CallForHelpSuppressor = 1 << 0,
        CanAttack = 1 << 1,
        CanCast = 1 << 2,
        CanMove = 1 << 3,
        CanMoveEver = 1 << 4,
        Charmed = 1 << 5,
        DisableAmbientGold = 1 << 6,
        Feared = 1 << 8,
        ForceRenderParticles = 1 << 9,
        GhostProof = 1 << 10,
        Ghosted = 1 << 11,
        IgnoreCallForHelp = 1 << 12,
        Immovable = 1 << 13,
        Invulnerable = 1 << 14,
        MagicImmune = 1 << 15,
        NearSighted = 1 << 16,
        NoRender = 1 << 18,
        PhysicalImmune = 1 << 20,
        RevealSpecificUnit = 1 << 21,
        Sleep = 1 << 24,
        Stealthed = 1 << 25,
        SuppressCallForHelp = 1 << 27,
        Suppressed = 1 << 28,
        Targetable = 1 << 29,
        Taunted = 1 << 30
    }
}