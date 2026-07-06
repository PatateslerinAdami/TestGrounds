using System.Collections.Generic;
using System.Numerics;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Path;
using Xunit;

namespace GameServerLib.Tests;

/// <summary>
/// Pins <see cref="LaneNavPointManager.GetNextNavTarget"/> — monotonic lane forward-nav. A straight
/// horizontal lane along +X keeps the math easy to follow (pullback per 1000u segment = 500).
/// </summary>
public class LaneNavPointManagerTests
{
    private const float THRESHOLD = 500f;
    private static readonly Vector2 Home = new Vector2(0f, 0f);

    private static readonly List<Vector2> Lane = new()
    {
        new Vector2(1000f, 0f),
        new Vector2(2000f, 0f),
        new Vector2(3000f, 0f),
        new Vector2(4000f, 0f),
    };

    private static Vector2? Next(Vector2 pos, int maxIndex, ref int idx)
        => LaneNavPointManager.GetNextNavTarget(Home, Lane, maxIndex, pos, THRESHOLD, ref idx);

    [Fact]
    public void FreshAtSpawn_TargetsFirstPoint()
    {
        int idx = 0;
        Assert.Equal(new Vector2(1000f, 0f), Next(new Vector2(0f, 0f), 3, ref idx));
        Assert.Equal(0, idx);
    }

    [Fact]
    public void WithinPullback_AdvancesToNext()
    {
        int idx = 0;
        // 300u from lane[0] (< 500 pullback) -> advance to lane[1].
        Assert.Equal(new Vector2(2000f, 0f), Next(new Vector2(700f, 0f), 3, ref idx));
        Assert.Equal(1, idx);
    }

    [Fact]
    public void PastPoint_AdvancesEvenIfOutsidePullback()
    {
        int idx = 0;
        // Overshot lane[0] to x=1400 without ever entering its pullback, and not yet within lane[1]'s
        // pullback (600 > 500) -> advanced exactly one point via the projection check.
        Assert.Equal(new Vector2(2000f, 0f), Next(new Vector2(1400f, 0f), 3, ref idx));
        Assert.Equal(1, idx);
    }

    [Fact]
    public void Monotonic_NeverTargetsBackwardWhenShovedBack()
    {
        int idx = 2; // already progressed to targeting lane[2]
        // Pushed back to near lane[0], but the index never decreases -> still targets lane[2] (no oscillation).
        Assert.Equal(new Vector2(3000f, 0f), Next(new Vector2(1200f, 0f), 3, ref idx));
        Assert.Equal(2, idx);
    }

    [Fact]
    public void TurretCap_WalksToCapPointAndNeverPast()
    {
        int idx = 0;
        // Cap at lane[1] (live turret). Standing past lane[0] -> walk up to the cap point lane[1]...
        Assert.Equal(new Vector2(2000f, 0f), Next(new Vector2(1900f, 0f), 1, ref idx));
        Assert.Equal(1, idx);
        // ...and even sitting on lane[1] it holds there, never advancing past the live turret.
        Assert.Equal(new Vector2(2000f, 0f), Next(new Vector2(2000f, 0f), 1, ref idx));
        Assert.Equal(1, idx);
    }

    [Fact]
    public void CapReleases_PushesOnAfterTurretDies()
    {
        int idx = 1; // held at the turret cap
        // Turret died -> cap is now the lane end; sitting on lane[1] advances toward lane[2].
        Assert.Equal(new Vector2(3000f, 0f), Next(new Vector2(2000f, 0f), 3, ref idx));
        Assert.Equal(2, idx);
    }

    [Fact]
    public void EmptyOrInvalidLane_ReturnsNull()
    {
        int idx = 0;
        Assert.Null(LaneNavPointManager.GetNextNavTarget(Home, new List<Vector2>(), 0, Home, THRESHOLD, ref idx));
        Assert.Null(LaneNavPointManager.GetNextNavTarget(Home, null, 0, Home, THRESHOLD, ref idx));
    }
}
