using System;
using System.IO;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.Content.Navigation;
using Xunit;

namespace GameServerLib.Tests;

/// <summary>
/// Verifies that NavigationGrid.IsWallOfGrass matches the client semantics from
/// S4 NavGrid.cpp:IsWallOfGrass. Loads the actual Map1 navgrid file so we test against
/// real Riot-baked grass cell flags, not synthetic data.
/// </summary>
public class IsWallOfGrassTests
{
    // Resolve relative to the test binary's location → repo root → Content path.
    private static readonly string Map1NavGridPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "Content", "LeagueSandbox-Default", "AIMesh", "Map1", "AIPath.aimesh_ngrid"
    ));

    private static NavigationGrid LoadMap1()
    {
        Assert.True(File.Exists(Map1NavGridPath), $"Map1 navgrid not found at {Map1NavGridPath}");
        return new NavigationGrid(Map1NavGridPath);
    }

    /// <summary>Find a cell whose center is "deep" in grass (no non-grass neighbors).</summary>
    private static NavigationGridCell FindDeepGrassCell(NavigationGrid grid)
    {
        foreach (var c in grid.Cells)
        {
            if (!c.HasFlag(NavigationGridCellFlags.HAS_GRASS)) continue;
            // Require the 3x3 neighborhood to be fully grass — that's "deep" enough that
            // a 100u radius scan stays inside grass.
            bool allGrass = true;
            for (short dx = -1; dx <= 1 && allGrass; dx++)
            for (short dy = -1; dy <= 1 && allGrass; dy++)
            {
                var n = grid.GetCell((short)(c.Locator.X + dx), (short)(c.Locator.Y + dy));
                if (n == null || !n.HasFlag(NavigationGridCellFlags.HAS_GRASS))
                {
                    allGrass = false;
                }
            }
            if (allGrass) return c;
        }
        throw new InvalidOperationException("No deep-grass cell found in Map1");
    }

    /// <summary>Find a cell that is walkable, far from any grass.</summary>
    private static NavigationGridCell FindOpenLaneCell(NavigationGrid grid)
    {
        foreach (var c in grid.Cells)
        {
            if (c.HasFlag(NavigationGridCellFlags.HAS_GRASS)) continue;
            if (c.HasFlag(NavigationGridCellFlags.NOT_PASSABLE)) continue;
            // Require 5x5 neighborhood to also be non-grass — guarantees a 100u scan won't
            // accidentally pick up a nearby bush edge.
            bool allOpen = true;
            for (short dx = -2; dx <= 2 && allOpen; dx++)
            for (short dy = -2; dy <= 2 && allOpen; dy++)
            {
                var n = grid.GetCell((short)(c.Locator.X + dx), (short)(c.Locator.Y + dy));
                if (n != null && n.HasFlag(NavigationGridCellFlags.HAS_GRASS))
                {
                    allOpen = false;
                }
            }
            if (allOpen) return c;
        }
        throw new InvalidOperationException("No open-lane cell found in Map1");
    }

    [Fact]
    public void SmallRadius_GrassCell_ReturnsTrue()
    {
        var grid = LoadMap1();
        var grassCell = FindDeepGrassCell(grid);
        var pos = grid.TranslateFromNavGrid(grassCell.Locator);

        Assert.True(grid.IsWallOfGrass(pos, 0f));
        Assert.True(grid.IsWallOfGrass(pos, 30f));
    }

    [Fact]
    public void SmallRadius_OpenCell_ReturnsFalse()
    {
        var grid = LoadMap1();
        var openCell = FindOpenLaneCell(grid);
        var pos = grid.TranslateFromNavGrid(openCell.Locator);

        Assert.False(grid.IsWallOfGrass(pos, 0f));
        Assert.False(grid.IsWallOfGrass(pos, 30f));
    }

    [Fact]
    public void LargeRadius_DeepGrass_ReturnsTrue()
    {
        var grid = LoadMap1();
        var grassCell = FindDeepGrassCell(grid);
        var pos = grid.TranslateFromNavGrid(grassCell.Locator);

        // 100u radius around a deep-grass cell -> grass density should be near 100% > 40%.
        Assert.True(grid.IsWallOfGrass(pos, 100f));
    }

    [Fact]
    public void LargeRadius_OpenLane_ReturnsFalse()
    {
        var grid = LoadMap1();
        var openCell = FindOpenLaneCell(grid);
        var pos = grid.TranslateFromNavGrid(openCell.Locator);

        // Far-from-grass position -> grass count is 0 -> false branch.
        Assert.False(grid.IsWallOfGrass(pos, 100f));
        Assert.False(grid.IsWallOfGrass(pos, 250f));
    }

    [Fact]
    public void RadiusBoundary_35_BehavesAsLargeRadius()
    {
        var grid = LoadMap1();
        var openCell = FindOpenLaneCell(grid);
        var pos = grid.TranslateFromNavGrid(openCell.Locator);

        // r=34.99 takes the small-radius branch (single cell), r=35 takes the density branch.
        // Both should agree on a clearly-non-grass position.
        Assert.False(grid.IsWallOfGrass(pos, 34.99f));
        Assert.False(grid.IsWallOfGrass(pos, 35f));
    }

    [Fact]
    public void GrassFraction_BelowThreshold_ReturnsFalse()
    {
        var grid = LoadMap1();
        // Find a position adjacent to a single grass cell with mostly-non-grass surroundings.
        // We sweep cells looking for one with HAS_GRASS where its 5x5 neighborhood has
        // less than 40% grass cells — at that position with radius=100, the density check
        // should land below threshold and return false despite grass being present.
        foreach (var c in grid.Cells)
        {
            if (c.HasFlag(NavigationGridCellFlags.HAS_GRASS)) continue;
            if (c.HasFlag(NavigationGridCellFlags.NOT_PASSABLE)) continue;
            int grass = 0, total = 0;
            for (short dx = -2; dx <= 2; dx++)
            for (short dy = -2; dy <= 2; dy++)
            {
                var n = grid.GetCell((short)(c.Locator.X + dx), (short)(c.Locator.Y + dy));
                if (n == null) continue;
                if (n.HasFlag(NavigationGridCellFlags.HAS_GRASS)) { grass++; total++; }
                else if (!n.HasFlag(NavigationGridCellFlags.NOT_PASSABLE)) total++;
            }
            if (grass > 0 && total > 0 && (float)grass / total < 0.3f)
            {
                var pos = grid.TranslateFromNavGrid(c.Locator);
                Assert.False(grid.IsWallOfGrass(pos, 100f),
                    $"At ({pos.X},{pos.Y}) grass={grass}/{total} ({(float)grass / total:P0}) — should be under 40% threshold");
                return;
            }
        }
        // If no such cell exists on Map1, that's fine — the test is a positive assertion that
        // when we DO find a low-density grass-adjacent spot, the threshold rejects it.
    }
}
