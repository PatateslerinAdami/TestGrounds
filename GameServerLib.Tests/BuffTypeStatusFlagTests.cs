using GameServerCore.Enums;
using Xunit;

namespace GameServerLib.Tests;

/// <summary>
/// Pins the BuffType -> StatusFlags projection (<see cref="BuffTypeExtensions.ToStatusFlag"/>) used by
/// AttackableUnit.RecomputeBuffEffects to derive CC states from active buffs' types. Guards against
/// enum drift and accidental remapping.
/// </summary>
public class BuffTypeStatusFlagTests
{
    [Theory]
    [InlineData(BuffType.STUN, StatusFlags.Stunned)]
    [InlineData(BuffType.SNARE, StatusFlags.Rooted)]
    [InlineData(BuffType.CHARM, StatusFlags.Charmed)]
    [InlineData(BuffType.FEAR, StatusFlags.Feared)]
    [InlineData(BuffType.FLEE, StatusFlags.Feared)]
    [InlineData(BuffType.TAUNT, StatusFlags.Taunted)]
    [InlineData(BuffType.SILENCE, StatusFlags.Silenced)]
    [InlineData(BuffType.SUPPRESSION, StatusFlags.Suppressed)]
    [InlineData(BuffType.SLEEP, StatusFlags.Sleep)]
    [InlineData(BuffType.DISARM, StatusFlags.Disarmed)]
    [InlineData(BuffType.NEAR_SIGHT, StatusFlags.NearSighted)]
    public void MapsCrowdControlBuffTypesToTheirStatusFlag(BuffType type, StatusFlags expected)
    {
        Assert.Equal(expected, type.ToStatusFlag());
    }

    [Theory]
    // Not status-flag CC: stat debuff, forced movement, no-flag-exists, and non-CC types.
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
    public void ReturnsNoneForUnmappedBuffTypes(BuffType type)
    {
        Assert.Equal(StatusFlags.None, type.ToStatusFlag());
    }
}
