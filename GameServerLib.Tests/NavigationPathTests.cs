using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LeagueSandbox.GameServer.Content.Navigation;
using Xunit;

namespace GameServerLib.Tests;

/// <summary>
/// Unit tests for the <see cref="NavigationPath"/> wrapper class. Covers the read-only
/// <see cref="IReadOnlyList{Vector2}"/> contract, mutation methods, identity / trim /
/// query helpers, and the <see cref="NavigationPath.ValidateLineOfSight"/> integration with
/// the real Map1 navgrid.
/// </summary>
public class NavigationPathTests
{
    private static readonly string Map1NavGridPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "Content", "LeagueSandbox-Default", "AIMesh", "Map1", "AIPath.aimesh_ngrid"
    ));

    private static NavigationGrid LoadMap1()
    {
        Assert.True(File.Exists(Map1NavGridPath));
        return new NavigationGrid(Map1NavGridPath);
    }

    // === Construction + IReadOnlyList ===

    [Fact]
    public void Empty_HasZeroCount()
    {
        var path = new NavigationPath();
        Assert.Empty(path);
        Assert.False(path.IsPartial);
    }

    [Fact]
    public void OfSingle_HasOneEntry()
    {
        var p = NavigationPath.OfSingle(new Vector2(100f, 200f));
        Assert.Single(p);
        Assert.Equal(new Vector2(100f, 200f), p[0]);
    }

    [Fact]
    public void Indexer_AndForeach_ReturnsSameOrder()
    {
        var src = new[] { new Vector2(0, 0), new Vector2(10, 10), new Vector2(20, 20) };
        var p = new NavigationPath(src);

        Assert.Equal(3, p.Count);
        for (int i = 0; i < src.Length; i++) Assert.Equal(src[i], p[i]);

        var collected = new List<Vector2>();
        foreach (var wp in p) collected.Add(wp);
        Assert.Equal(src, collected);
    }

    [Fact]
    public void LinqLastFirst_WorkUnchanged()
    {
        var p = new NavigationPath(new[] { new Vector2(1, 1), new Vector2(2, 2), new Vector2(3, 3) });
        Assert.Equal(new Vector2(1, 1), p.First());
        Assert.Equal(new Vector2(3, 3), p.Last());
    }

    [Fact]
    public void IsPartial_GetSet_RoundTrips()
    {
        var p = new NavigationPath();
        Assert.False(p.IsPartial);
        p.IsPartial = true;
        Assert.True(p.IsPartial);
        p.IsPartial = false;
        Assert.False(p.IsPartial);
    }

    // === Mutation ===

    [Fact]
    public void AddWaypoint_AppendsAtTail()
    {
        var p = new NavigationPath();
        p.AddWaypoint(new Vector2(1, 1));
        p.AddWaypoint(new Vector2(2, 2));
        Assert.Equal(2, p.Count);
        Assert.Equal(new Vector2(2, 2), p[1]);
    }

    [Fact]
    public void AddWaypointFront_PrependsAtHead()
    {
        var p = new NavigationPath(new[] { new Vector2(2, 2) });
        p.AddWaypointFront(new Vector2(1, 1));
        Assert.Equal(new Vector2(1, 1), p[0]);
        Assert.Equal(new Vector2(2, 2), p[1]);
    }

    [Fact]
    public void Replace_OverwritesAtIndex()
    {
        var p = new NavigationPath(new[] { new Vector2(1, 1), new Vector2(2, 2) });
        p.Replace(0, new Vector2(99, 99));
        Assert.Equal(new Vector2(99, 99), p[0]);
        Assert.Equal(new Vector2(2, 2), p[1]);
    }

    [Fact]
    public void RemoveRange_DropsContiguousSpan()
    {
        var p = new NavigationPath(new[]
        {
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(2, 2), new Vector2(3, 3)
        });
        p.RemoveRange(1, 2);
        Assert.Equal(2, p.Count);
        Assert.Equal(new Vector2(0, 0), p[0]);
        Assert.Equal(new Vector2(3, 3), p[1]);
    }

    [Fact]
    public void ShrinkPath_TrimsToMaxSize()
    {
        var p = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(1, 1), new Vector2(2, 2) });
        p.ShrinkPath(2);
        Assert.Equal(2, p.Count);
        Assert.Equal(new Vector2(1, 1), p[1]);
    }

    [Fact]
    public void ShrinkPath_LargerThanCount_NoOp()
    {
        var p = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(1, 1) });
        p.ShrinkPath(99);
        Assert.Equal(2, p.Count);
    }

    [Fact]
    public void Append_ConcatenatesOther()
    {
        var a = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(1, 1) });
        var b = new NavigationPath(new[] { new Vector2(2, 2), new Vector2(3, 3) });
        a.Append(b);
        Assert.Equal(4, a.Count);
        Assert.Equal(new Vector2(3, 3), a[3]);
        // b should be unaffected
        Assert.Equal(2, b.Count);
    }

    [Fact]
    public void Reset_ReplacesContents()
    {
        var p = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(1, 1) });
        p.Reset(new[] { new Vector2(9, 9) });
        Assert.Single(p);
        Assert.Equal(new Vector2(9, 9), p[0]);
    }

    [Fact]
    public void Reverse_FlipsOrder()
    {
        var p = new NavigationPath(new[] { new Vector2(1, 1), new Vector2(2, 2), new Vector2(3, 3) });
        p.Reverse();
        Assert.Equal(new Vector2(3, 3), p[0]);
        Assert.Equal(new Vector2(1, 1), p[2]);
    }

    [Fact]
    public void SubdividePath_InsertsIntermediatePoints()
    {
        var p = new NavigationPath(new[] { new Vector2(100, 0) });
        p.SubdividePath(new Vector2(0, 0), 25f);
        // 100u with 25u segment -> 4 segments, 3 inserts before the original waypoint
        Assert.Equal(4, p.Count);
        Assert.Equal(new Vector2(25, 0), p[0]);
        Assert.Equal(new Vector2(50, 0), p[1]);
        Assert.Equal(new Vector2(75, 0), p[2]);
        Assert.Equal(new Vector2(100, 0), p[3]);
    }

    [Fact]
    public void ToList_ReturnsDefensiveCopy()
    {
        var p = new NavigationPath(new[] { new Vector2(1, 1), new Vector2(2, 2) });
        var copy = p.ToList();
        copy.Add(new Vector2(99, 99));
        // Mutating the copy must not touch the wrapped path — required for safe wire-format use.
        Assert.Equal(2, p.Count);
        Assert.Equal(3, copy.Count);
    }

    // === Trim ===

    [Fact]
    public void TrimUntilReached_DropsConsumedPrefix()
    {
        var p = new NavigationPath(new[]
        {
            new Vector2(0, 0), new Vector2(100, 0), new Vector2(200, 0), new Vector2(300, 0)
        });
        // Unit is now near waypoint index 2 (at 200,0).
        p.TrimUntilReached(new Vector2(205, 0));
        Assert.Equal(2, p.Count);
        Assert.Equal(new Vector2(205, 0), p[0]); // head replaced with current pos
        Assert.Equal(new Vector2(300, 0), p[1]);
    }

    [Fact]
    public void TrimUntilReached_HeadAlreadyClosest_OnlyRewritesHead()
    {
        var p = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(100, 0), new Vector2(200, 0) });
        p.TrimUntilReached(new Vector2(5, 0));
        Assert.Equal(3, p.Count);
        Assert.Equal(new Vector2(5, 0), p[0]);
    }

    [Fact]
    public void TrimUntilReached_OnEmpty_AddsCurrentAsHead()
    {
        var p = new NavigationPath();
        p.TrimUntilReached(new Vector2(50, 50));
        Assert.Single(p);
        Assert.Equal(new Vector2(50, 50), p[0]);
    }

    [Fact]
    public void TrimToCurrentWaypoint_DropsFirstNEntries()
    {
        var p = new NavigationPath(new[]
        {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(2, 0), new Vector2(3, 0)
        });
        p.TrimToCurrentWaypoint(2);
        Assert.Equal(2, p.Count);
        Assert.Equal(new Vector2(2, 0), p[0]);
    }

    // === Identity ===

    [Fact]
    public void IsPathTheSame_Identical_True()
    {
        var a = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(10, 10) });
        var b = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(10, 10) });
        Assert.True(a.IsPathTheSame(b));
    }

    [Fact]
    public void IsPathTheSame_DifferentLength_False()
    {
        var a = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(10, 10) });
        var b = new NavigationPath(new[] { new Vector2(0, 0) });
        Assert.False(a.IsPathTheSame(b));
    }

    [Fact]
    public void IsPathTheSame_OneOff_False()
    {
        var a = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(10, 10) });
        var b = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(10, 11) });
        Assert.False(a.IsPathTheSame(b));
    }

    [Fact]
    public void IsPathTheSame_WithinEpsilon_True()
    {
        var a = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(10, 10) });
        var b = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(10.3f, 10.3f) });
        Assert.True(a.IsPathTheSame(b, epsilon: 1f));
        Assert.False(a.IsPathTheSame(b, epsilon: 0.1f));
    }

    [Fact]
    public void IsPathTheSame_Null_False()
    {
        var a = new NavigationPath();
        Assert.False(a.IsPathTheSame(null));
    }

    [Fact]
    public void HasDuplicateInARow_DetectsAdjacentDupes()
    {
        var clean = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(1, 1) });
        var dupes = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 1) });
        Assert.False(clean.HasDuplicateInARow());
        Assert.True(dupes.HasDuplicateInARow());
    }

    // === Queries ===

    [Fact]
    public void ComputePathDistance_SumsSegmentLengths()
    {
        var p = new NavigationPath(new[]
        {
            new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 100)
        });
        Assert.Equal(200f, p.ComputePathDistance(), 3);
    }

    [Fact]
    public void FindClosest_PicksNearestWaypoint()
    {
        var p = new NavigationPath(new[]
        {
            new Vector2(0, 0), new Vector2(100, 0), new Vector2(200, 0)
        });
        Assert.Equal(new Vector2(100, 0), p.FindClosest(new Vector2(110, 5)));
    }

    [Fact]
    public void GetPositionAtDistance_ClampsToEndpoints()
    {
        var p = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(100, 0) });
        Assert.Equal(new Vector2(0, 0), p.GetPositionAtDistance(-5f));
        Assert.Equal(new Vector2(50, 0), p.GetPositionAtDistance(50f));
        Assert.Equal(new Vector2(100, 0), p.GetPositionAtDistance(999f));
    }

    [Fact]
    public void GetLastEndPosition_ReturnsTail()
    {
        var p = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(50, 50) });
        Assert.Equal(new Vector2(50, 50), p.GetLastEndPosition());
    }

    // === ValidateLineOfSight (real navgrid) ===

    private static Vector2 FindWalkablePos(NavigationGrid grid, int approxCellX, int approxCellY)
    {
        for (int r = 0; r < 30; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                    var c = grid.GetCell((short)(approxCellX + dx), (short)(approxCellY + dy));
                    if (c == null) continue;
                    if (c.HasFlag(global::GameServerCore.Enums.NavigationGridCellFlags.NOT_PASSABLE)) continue;
                    if (c.HasFlag(global::GameServerCore.Enums.NavigationGridCellFlags.SEE_THROUGH)) continue;
                    return grid.TranslateFromNavGrid(c.Locator);
                }
            }
        }
        throw new InvalidOperationException("No walkable cell found near requested cell");
    }

    [Fact]
    public void ValidateLineOfSight_ClearCorridor_True()
    {
        var grid = LoadMap1();
        var a = FindWalkablePos(grid, 50, 50);
        var b = FindWalkablePos(grid, 55, 55);
        var path = grid.GetPath(a, b);

        Assert.NotNull(path);
        // Path returned by A* against the current navgrid is by construction line-of-sight clear.
        Assert.True(path.ValidateLineOfSight(grid, 0f));
    }

    [Fact]
    public void ValidateLineOfSight_TwoFarSidesOfMap_PathStillLosClear()
    {
        // Even a long A* path that routes around terrain remains LOS-clear when checked
        // pairwise — A* emits cells with mutual visibility (smoothed result).
        var grid = LoadMap1();
        var a = FindWalkablePos(grid, 30, 30);
        var b = FindWalkablePos(grid, 240, 240);
        var path = grid.GetPath(a, b);

        Assert.NotNull(path);
        Assert.True(path.ValidateLineOfSight(grid, 0f),
            "Smoothed A* path should validate line-of-sight against the same grid");
    }

    [Fact]
    public void ValidateLineOfSight_StraightThroughWall_False()
    {
        // Build a synthetic two-waypoint path that traverses far enough across the map that the
        // straight line is virtually guaranteed to cross terrain on Map1 (cross-lane river/walls).
        // CastCircle returns true (= blocked) on any unwalkable cell along the corridor, so
        // ValidateLineOfSight reports false.
        var grid = LoadMap1();
        var topLeft = FindWalkablePos(grid, 30, 240);
        var bottomRight = FindWalkablePos(grid, 240, 30);

        var synthetic = new NavigationPath(new[] { topLeft, bottomRight });
        Assert.False(synthetic.ValidateLineOfSight(grid, 0f),
            "Cross-map straight line should hit terrain at some point on Map1");
    }

    // === GetCollisionPosition ===

    [Fact]
    public void GetCollisionPosition_EmptyPath_ReturnsStart()
    {
        var path = new NavigationPath();
        var start = new Vector2(100f, 200f);
        var result = path.GetCollisionPosition(start, 500f, new Vector2(0, 0), 50f);
        Assert.Equal(start, result);
    }

    [Fact]
    public void GetCollisionPosition_ZeroDistance_ReturnsStart()
    {
        var path = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(1000, 0) });
        var start = new Vector2(100f, 0f);
        var result = path.GetCollisionPosition(start, 0f, new Vector2(500, 0), 50f);
        Assert.Equal(start, result);
    }

    [Fact]
    public void GetCollisionPosition_NoOtherUnit_WalksMaxDistance()
    {
        // Straight east-pointing path starting at origin. Walk 250 units → end at (250, 0).
        // No collision check (radius=0 disables it).
        var path = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(1000, 0) });
        var result = path.GetCollisionPosition(new Vector2(0, 0), 250f, Vector2.Zero, 0f);
        Assert.Equal(new Vector2(250f, 0f), result);
    }

    [Fact]
    public void GetCollisionPosition_BeyondPathEnd_ReturnsLastWaypoint()
    {
        // Path is 100 units long. Walk 500 → returns last waypoint, not start+500.
        var path = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(100, 0) });
        var result = path.GetCollisionPosition(new Vector2(0, 0), 500f, Vector2.Zero, 0f);
        Assert.Equal(new Vector2(100f, 0f), result);
    }

    [Fact]
    public void GetCollisionPosition_HitsOtherUnitOnSegment_ReturnsEntryPoint()
    {
        // Walker at (0,0) walks east. OtherUnit centered at (200, 0) with radius 50.
        // Entry point = (200 - 50, 0) = (150, 0) — first contact with the circle.
        var path = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(1000, 0) });
        var result = path.GetCollisionPosition(new Vector2(0, 0), 500f, new Vector2(200, 0), 50f);
        Assert.Equal(new Vector2(150f, 0f), result, new Vector2EqualityComparer(0.01f));
    }

    [Fact]
    public void GetCollisionPosition_OffsetOtherUnit_BackTrackByPerpendicular()
    {
        // OtherUnit at (200, 30) with radius 50. Closest-on-segment is (200, 0).
        // perpDist² = 30² = 900, radius² = 2500. backtrack = sqrt(2500 - 900) = sqrt(1600) = 40.
        // Entry at (200 - 40, 0) = (160, 0).
        var path = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(1000, 0) });
        var result = path.GetCollisionPosition(new Vector2(0, 0), 500f, new Vector2(200, 30), 50f);
        Assert.Equal(new Vector2(160f, 0f), result, new Vector2EqualityComparer(0.01f));
    }

    [Fact]
    public void GetCollisionPosition_OtherUnitTooFar_NoCollision()
    {
        // OtherUnit at (200, 100) with radius 50. perpDist=100 > radius=50 → no collision.
        // Walker walks the full 250 → ends at (250, 0).
        var path = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(1000, 0) });
        var result = path.GetCollisionPosition(new Vector2(0, 0), 250f, new Vector2(200, 100), 50f);
        Assert.Equal(new Vector2(250f, 0f), result, new Vector2EqualityComparer(0.01f));
    }

    [Fact]
    public void GetCollisionPosition_OtherUnitBehindWalker_NoCollision()
    {
        // OtherUnit at (-200, 0) — behind walker on the path. Walker only goes forward, no collision.
        var path = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(1000, 0) });
        var result = path.GetCollisionPosition(new Vector2(0, 0), 250f, new Vector2(-200, 0), 50f);
        Assert.Equal(new Vector2(250f, 0f), result, new Vector2EqualityComparer(0.01f));
    }

    [Fact]
    public void GetCollisionPosition_CollisionBeyondTravelBudget_NoEarlyReturn()
    {
        // OtherUnit at (500, 0) radius 50 → entry at (450, 0). Walker only has 250 budget → can't reach.
        // Should return walk-end (250, 0), not the would-be collision point.
        var path = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(1000, 0) });
        var result = path.GetCollisionPosition(new Vector2(0, 0), 250f, new Vector2(500, 0), 50f);
        Assert.Equal(new Vector2(250f, 0f), result, new Vector2EqualityComparer(0.01f));
    }

    [Fact]
    public void GetCollisionPosition_MultiSegment_CrossesCorner()
    {
        // L-shaped path: (0,0) → (100,0) → (100,100). Walker at (0,0), max 150.
        // After 100 units east, turns north. With 50 left, ends at (100, 50).
        // No otherUnit collision.
        var path = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 100) });
        var result = path.GetCollisionPosition(new Vector2(0, 0), 150f, Vector2.Zero, 0f);
        Assert.Equal(new Vector2(100f, 50f), result, new Vector2EqualityComparer(0.01f));
    }

    [Fact]
    public void GetCollisionPosition_MultiSegment_CollisionOnSecondSegment()
    {
        // Same L-shaped path. OtherUnit at (100, 75) radius 25. Entry on second segment at (100, 50).
        var path = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 100) });
        var result = path.GetCollisionPosition(new Vector2(0, 0), 200f, new Vector2(100, 75), 25f);
        Assert.Equal(new Vector2(100f, 50f), result, new Vector2EqualityComparer(0.01f));
    }

    [Fact]
    public void GetCollisionPosition_StartMidSegment_WalksFromStart()
    {
        // Path is (0,0)→(1000,0). Walker starts MID-segment at (500, 0), max 100.
        // The function uses startPoint as origin, walks toward _waypoints[0] = (0, 0) first.
        // That's BACKWARD on the X axis since _waypoints[0] is at (0,0).
        // So walker goes from (500, 0) toward (0, 0), 100 units → (400, 0).
        // (Caller responsibility to pass the right path direction.)
        var path = new NavigationPath(new[] { new Vector2(0, 0), new Vector2(1000, 0) });
        var result = path.GetCollisionPosition(new Vector2(500, 0), 100f, Vector2.Zero, 0f);
        Assert.Equal(new Vector2(400f, 0f), result, new Vector2EqualityComparer(0.01f));
    }

    // === TrimRedundantWaypoints ===

    [Fact]
    public void TrimRedundantWaypoints_EmptyPath_NoOp()
    {
        var path = new NavigationPath();
        path.TrimRedundantWaypoints();
        Assert.Empty(path);
    }

    [Fact]
    public void TrimRedundantWaypoints_SingleWaypoint_NoOp()
    {
        var path = NavigationPath.OfSingle(new Vector2(100, 200));
        path.TrimRedundantWaypoints();
        Assert.Single(path);
        Assert.Equal(new Vector2(100, 200), path[0]);
    }

    [Fact]
    public void TrimRedundantWaypoints_CoLocatedPair_DropsSecond()
    {
        // Two waypoints within epsilon → second dropped, leaving the first.
        var path = new NavigationPath(new[]
        {
            new Vector2(100, 200),
            new Vector2(100.1f, 200.1f),
        });
        path.TrimRedundantWaypoints();
        Assert.Single(path);
        Assert.Equal(new Vector2(100, 200), path[0]);
    }

    [Fact]
    public void TrimRedundantWaypoints_NoCoLocation_KeepsAll()
    {
        var path = new NavigationPath(new[]
        {
            new Vector2(0, 0),
            new Vector2(100, 0),
            new Vector2(200, 0),
        });
        path.TrimRedundantWaypoints();
        // Without grid, only co-location pass runs; these are 100 units apart → all kept.
        Assert.Equal(3, path.Count);
    }

    [Fact]
    public void TrimRedundantWaypoints_MultipleCoLocated_AllCollapseToOne()
    {
        var path = new NavigationPath(new[]
        {
            new Vector2(100, 200),
            new Vector2(100.1f, 200.1f),
            new Vector2(100.2f, 200.0f),
            new Vector2(100.0f, 200.3f),
        });
        path.TrimRedundantWaypoints();
        Assert.Single(path);
    }

    [Fact]
    public void TrimRedundantWaypoints_CoLocatedTail_KeepsFirstThenDedupesGoalArea()
    {
        // Real-world shape: long path with cell-snap stairsteps near the goal.
        var path = new NavigationPath(new[]
        {
            new Vector2(0, 0),
            new Vector2(100, 0),
            new Vector2(200, 0),
            new Vector2(199.9f, 0.1f),
            new Vector2(200.0f, 0.2f),
        });
        path.TrimRedundantWaypoints();
        // The 100→200 distance keeps both; the goal-side cluster collapses.
        Assert.Equal(3, path.Count);
        Assert.Equal(new Vector2(0, 0), path[0]);
        Assert.Equal(new Vector2(100, 0), path[1]);
        Assert.Equal(new Vector2(200, 0), path[2]);
    }

    [Fact]
    public void TrimRedundantWaypoints_StringPullingOnMap1_RemovesRedundantMidpoint()
    {
        // With Map1 grid: a path with a redundant midpoint on a clear corridor is shortened.
        var grid = LoadMap1();
        var a = FindWalkablePos(grid, 50, 50);
        var b = FindWalkablePos(grid, 55, 55);
        var path = grid.GetPath(a, b);
        Assert.NotNull(path);

        // Insert a redundant midpoint between two existing waypoints — should be removed.
        if (path.Count >= 2)
        {
            var mid = (path[0] + path[1]) * 0.5f;
            path.Insert(1, mid);
            int countBefore = path.Count;

            path.TrimRedundantWaypoints(grid, 0f);

            // The inserted midpoint had LOS to both neighbors → string-pulled out.
            Assert.True(path.Count < countBefore,
                $"Expected string-pulling to drop the redundant midpoint (before={countBefore}, after={path.Count})");
        }
    }

    // === InsertStartPosition ===

    [Fact]
    public void InsertStartPosition_Empty_AddsStartAsOnly()
    {
        var path = new NavigationPath();
        path.InsertStartPosition(new Vector2(100, 200));
        Assert.Single(path);
        Assert.Equal(new Vector2(100, 200), path[0]);
    }

    [Fact]
    public void InsertStartPosition_FirstAtStart_NoOp()
    {
        // Path already begins at startPos → no change.
        var path = new NavigationPath(new[]
        {
            new Vector2(100, 200),
            new Vector2(500, 600),
        });
        path.InsertStartPosition(new Vector2(100, 200));
        Assert.Equal(2, path.Count);
        Assert.Equal(new Vector2(100, 200), path[0]);
        Assert.Equal(new Vector2(500, 600), path[1]);
    }

    [Fact]
    public void InsertStartPosition_FirstWithinEpsilon_NoOp()
    {
        // Path begins at quasi-equal position (sub-unit drift) → no change.
        var path = new NavigationPath(new[]
        {
            new Vector2(100.3f, 200.1f),
            new Vector2(500, 600),
        });
        path.InsertStartPosition(new Vector2(100, 200), epsilon: 1f);
        Assert.Equal(2, path.Count);
        // First waypoint still the original — not replaced.
        Assert.Equal(new Vector2(100.3f, 200.1f), path[0]);
    }

    [Fact]
    public void InsertStartPosition_FirstDifferentEnoughToPrepend()
    {
        // Path's first waypoint is far from startPos → prepend.
        var path = new NavigationPath(new[]
        {
            new Vector2(500, 600),
            new Vector2(900, 800),
        });
        path.InsertStartPosition(new Vector2(100, 200));
        Assert.Equal(3, path.Count);
        Assert.Equal(new Vector2(100, 200), path[0]);
        Assert.Equal(new Vector2(500, 600), path[1]);
        Assert.Equal(new Vector2(900, 800), path[2]);
    }

    [Fact]
    public void InsertStartPosition_AddressesCount1SetWaypointsRejectBug()
    {
        // The bug class from stuck-around-terrain memory: a fallback path-builder ends with
        // only 1 waypoint, which SetWaypoints rejects. InsertStartPosition turns it into
        // a valid 2-waypoint path.
        var path = new NavigationPath(new[] { new Vector2(500, 600) });
        Assert.Single(path);
        path.InsertStartPosition(new Vector2(100, 200));
        Assert.Equal(2, path.Count);
        Assert.Equal(new Vector2(100, 200), path[0]);
        Assert.Equal(new Vector2(500, 600), path[1]);
    }

    [Fact]
    public void TrimRedundantWaypoints_NoGrid_SkipsStringPullingPass()
    {
        // Three co-linear waypoints with no co-location. Without grid, string-pull
        // is skipped → all kept.
        var path = new NavigationPath(new[]
        {
            new Vector2(0, 0),
            new Vector2(50, 0),
            new Vector2(100, 0),
        });
        path.TrimRedundantWaypoints(grid: null);
        Assert.Equal(3, path.Count);
    }
}

/// <summary>
/// Tolerance-based equality for <see cref="Vector2"/> in tests.
/// </summary>
internal sealed class Vector2EqualityComparer : IEqualityComparer<Vector2>
{
    private readonly float _tolerance;
    public Vector2EqualityComparer(float tolerance) { _tolerance = tolerance; }
    public bool Equals(Vector2 a, Vector2 b) => Vector2.DistanceSquared(a, b) <= _tolerance * _tolerance;
    public int GetHashCode(Vector2 v) => HashCode.Combine(v.X, v.Y);
}
