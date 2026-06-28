using GameServerCore.Enums;
using Xunit;

namespace GameServerLib.Tests;

/// <summary>
/// Pins the BuffType -> StatusFlags / capability-disable projections used by
/// AttackableUnit.RecomputeBuffEffects. M2 Phase 3: pure-capability CCs (STUN/SNARE/SILENCE/DISARM) no longer
/// map to an invented flag — they map to <see cref="BuffTypeExtensions.ToCapabilityDisable"/> (CanMove/
/// CanAttack/CanCast). Only the real Riot CharacterState bits remain in <see cref="BuffTypeExtensions.ToStatusFlag"/>.
/// Guards against enum drift and accidental remapping.
/// </summary>
public class BuffTypeStatusFlagTests
{
    // ToStatusFlag: only the REAL Riot state bits (no Stunned/Rooted/Silenced/Disarmed — those are gone).
    [Theory]
    [InlineData(BuffType.CHARM, StatusFlags.Charmed)]
    [InlineData(BuffType.FEAR, StatusFlags.Feared)]
    [InlineData(BuffType.FLEE, StatusFlags.Feared)]
    [InlineData(BuffType.TAUNT, StatusFlags.Taunted)]
    [InlineData(BuffType.SUPPRESSION, StatusFlags.Suppressed)]
    [InlineData(BuffType.SLEEP, StatusFlags.Sleep)]
    [InlineData(BuffType.NEAR_SIGHT, StatusFlags.NearSighted)]
    public void MapsStateBuffTypesToTheirStatusFlag(BuffType type, StatusFlags expected)
    {
        Assert.Equal(expected, type.ToStatusFlag());
    }

    [Theory]
    // Pure-capability CCs no longer carry a status flag (they disable capabilities instead), plus the
    // non-status types (stat debuff, forced movement, no-flag, non-CC).
    [InlineData(BuffType.STUN)]
    [InlineData(BuffType.SNARE)]
    [InlineData(BuffType.SILENCE)]
    [InlineData(BuffType.DISARM)]
    [InlineData(BuffType.SLOW)]
    [InlineData(BuffType.KNOCKUP)]
    [InlineData(BuffType.KNOCKBACK)]
    [InlineData(BuffType.POLYMORPH)]
    [InlineData(BuffType.BLIND)]
    [InlineData(BuffType.INTERNAL)]
    [InlineData(BuffType.AURA)]
    [InlineData(BuffType.COMBAT_ENCHANCER)]
    [InlineData(BuffType.COMBAT_DEHANCER)]
    [InlineData(BuffType.DAMAGE)]
    [InlineData(BuffType.HEAL)]
    public void ReturnsNoneForNonStateBuffTypes(BuffType type)
    {
        Assert.Equal(StatusFlags.None, type.ToStatusFlag());
    }

    // ToCapabilityDisable: the faithful 4.20 CC model — which CAN_MOVE/CAN_ATTACK/CAN_CAST bits each CC clears.
    [Theory]
    [InlineData(BuffType.STUN, StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast)]
    [InlineData(BuffType.SLEEP, StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast)]
    [InlineData(BuffType.SUPPRESSION, StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast)]
    [InlineData(BuffType.SNARE, StatusFlags.CanMove)]
    [InlineData(BuffType.SILENCE, StatusFlags.CanCast)]
    [InlineData(BuffType.DISARM, StatusFlags.CanAttack)]
    // Charm/Fear/Taunt deliberately do NOT disable CanMove (the AI drives their movement).
    [InlineData(BuffType.CHARM, StatusFlags.CanAttack | StatusFlags.CanCast)]
    [InlineData(BuffType.FEAR, StatusFlags.CanAttack | StatusFlags.CanCast)]
    [InlineData(BuffType.FLEE, StatusFlags.CanAttack | StatusFlags.CanCast)]
    [InlineData(BuffType.TAUNT, StatusFlags.CanCast)]
    // Non-CC / stat / movement types disable no capability.
    [InlineData(BuffType.SLOW, StatusFlags.None)]
    [InlineData(BuffType.KNOCKUP, StatusFlags.None)]
    [InlineData(BuffType.DAMAGE, StatusFlags.None)]
    [InlineData(BuffType.AURA, StatusFlags.None)]
    public void MapsCrowdControlToCapabilityDisable(BuffType type, StatusFlags expected)
    {
        Assert.Equal(expected, type.ToCapabilityDisable());
    }
}
