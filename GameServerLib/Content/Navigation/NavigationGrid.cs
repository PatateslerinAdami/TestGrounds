using System;
using System.Collections.Generic;
using System.IO;
using GameServerCore;
using Vector2 = System.Numerics.Vector2;
using System.Numerics;
using GameServerLib.Extensions;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects;
using System.Linq;
using GameMaths;

namespace LeagueSandbox.GameServer.Content.Navigation
{
    public class NavigationGrid
    {
        /// <summary>
        /// The minimum position on the NavigationGrid in normal coordinate space (bottom left in 2D).
        /// NavigationGridCells are undefined below these minimums.
        /// </summary>
        public Vector3 MinGridPosition { get; private set; }
        /// <summary>
        /// The maximum position on the NavigationGrid in normal coordinate space (top right in 2D).
        /// NavigationGridCells are undefined beyond these maximums.
        /// </summary>
        public Vector3 MaxGridPosition { get; private set; }
        /// <summary>
        /// Calculated resolution of the Navigation Grid (percentage of a cell 1 normal unit takes up, not to be confused with 1/CellSize).
        /// Multiple used to convert cell-based coordinates back into normal coordinates (CellCountX/Z / TranslationMaxGridPosition).
        /// </summary>
        public Vector3 TranslationMaxGridPosition { get; private set; }
        /// <summary>
        /// Ideal number of normal units a cell takes up (not fully accurate, but mostly, refer to TranslationMaxGridPosition for true size).
        /// </summary>
        public float CellSize { get; private set; }
        /// <summary>
        /// Width of the Navigation Grid in cells.
        /// </summary>
        public uint CellCountX { get; private set; }
        /// <summary>
        /// Height of the Navigation Grid in cells.
        /// </summary>
        public uint CellCountY { get; private set; }
        /// <summary>
        /// Array of all cells contained in this Navigation Grid.
        /// </summary>
        public NavigationGridCell[] Cells { get; private set; }
        /// <summary>
        /// Array of region tags where each index represents a cell's index.
        /// </summary>
        public uint[] RegionTags { get; private set; }
        /// <summary>
        /// Table of regions possible in the current Navigation Grid.
        /// Regions are the areas representing key points on a map. In the case of OldSR, this could be lanes top, middle, or bot, and the last region being jungle.
        /// *NOTE*: Regions only exist in Navigation Grids with a version of 5 or higher. OldSR is version 3.
        /// </summary>
        public NavigationRegionTagTable RegionTagTable { get; private set; }
        /// <summary>
        /// Number of sampled heights in the X coordinate plane.
        /// </summary>
        public uint SampledHeightsCountX { get; private set; }
        /// <summary>
        /// Number of sampled heights in the Y coordinate plane (Z coordinate in 3D space).
        /// </summary>
        public uint SampledHeightsCountY { get; private set; }
        /// <summary>
        /// Multiple used to convert from normal coordinates to an index format used to get sampled heights from the Navigation Grid.
        /// </summary>
        /// TODO: Seems to be volatile. If there ever comes a time when Navigation Grid editing becomes easy, that'd be the perfect time to rework the methods for getting sampled heights.
        public Vector2 SampledHeightsDistance { get; private set; }
        /// <summary>
        /// Array of sampled heights where each index represents a cell's index (depends on SampledHeightsCountX/Y).
        /// </summary>
        public float[] SampledHeights { get; private set; }
        /// <summary>
        /// Grid of hints.
        /// Function likely related to pathfinding.
        /// Currently Unused.
        /// </summary>
        public NavigationHintGrid HintGrid { get; private set; }
        /// <summary>
        /// Width of the map in normal coordinate space, where the origin is at (0, 0).
        /// *NOTE*: Not to be confused with MaxGridPosition.X, whos origin is at MinGridPosition.
        /// </summary>
        public float MapWidth { get; private set; }
        /// <summary>
        /// Height of the map in normal coordinate space, where the origin is at (0, 0).
        /// *NOTE*: Not to be confused with MaxGridPosition.Z, whos origin is at MinGridPosition.
        /// </summary>
        public float MapHeight { get; private set; }
        /// <summary>
        /// Center of the map in normal coordinate space.
        /// </summary>
        public Vector2 MiddleOfMap { get; private set; }

        /// <summary>
        /// Brush group ID per cell (0 = not in any brush, 1..255 = group ID). Mirrors the client's
        /// <c>mCellGrassGroups</c> uint8 array (S4 NavGrid offset 0x3c). Populated at load time via
        /// 8 connected floodfill over <see cref="NavigationGridCellFlags.HAS_GRASS"/> cells. Two
        /// cells with the same non-zero group are part of the same connected brush; cells with
        /// different non-zero groups are in different brushes; group 0 means "not in a brush". Used
        /// by <see cref="GetNearestGrassGroup"/> for vision/visibility queries that need to
        /// distinguish "in same brush as observer" from "in different brush" e.g., line-of-sight
        /// rules at brush boundaries where two observers in the same brush can see each other but
        /// observers in different brushes cannot.
        /// </summary>
        public byte[] CellGrassGroups { get; private set; }

        public NavigationGrid(string fileLocation) : this(File.OpenRead(fileLocation)) { }
        public NavigationGrid(byte[] buffer) : this(new MemoryStream(buffer)) { }
        public NavigationGrid(Stream stream)
        {
            using (BinaryReader br = new BinaryReader(stream))
            {
                byte major = br.ReadByte();
                ushort minor = major != 2 ? br.ReadUInt16() : (ushort)0;
                if (major != 2 && major != 3 && major != 5 && major != 7)
                {
                    throw new Exception(string.Format("Unsupported Navigation Grid Version: {0}.{1}", major, minor));
                }

                MinGridPosition = br.ReadVector3();
                MaxGridPosition = br.ReadVector3();

                CellSize = br.ReadSingle();
                CellCountX = br.ReadUInt32();
                CellCountY = br.ReadUInt32();

                Cells = new NavigationGridCell[CellCountX * CellCountY];
                RegionTags = new uint[CellCountX * CellCountY];

                if (major == 2 || major == 3 || major == 5)
                {
                    for (int i = 0; i < Cells.Length; i++)
                    {
                        Cells[i] = NavigationGridCell.ReadVersion5(br, i);
                    }

                    if (major == 5)
                    {
                        for (int i = 0; i < RegionTags.Length; i++)
                        {
                            RegionTags[i] = br.ReadUInt16();
                        }
                    }
                }
                else if (major == 7)
                {
                    for (int i = 0; i < Cells.Length; i++)
                    {
                        Cells[i] = NavigationGridCell.ReadVersion7(br, i);
                    }
                    for (int i = 0; i < Cells.Length; i++)
                    {
                        Cells[i].SetFlags((NavigationGridCellFlags)br.ReadUInt16());
                    }

                    for (int i = 0; i < RegionTags.Length; i++)
                    {
                        RegionTags[i] = br.ReadUInt32();
                    }
                }

                if(major >= 5)
                {
                    uint groupCount = major == 5 ? 4u : 8u;
                    RegionTagTable = new NavigationRegionTagTable(br, groupCount);
                }

                SampledHeightsCountX = br.ReadUInt32();
                SampledHeightsCountY = br.ReadUInt32();
                SampledHeightsDistance = br.ReadVector2();
                SampledHeights = new float[SampledHeightsCountX * SampledHeightsCountY];
                for (int i = 0; i < SampledHeights.Length; i++)
                {
                    SampledHeights[i] = br.ReadSingle();
                }

                HintGrid = new NavigationHintGrid(br);

                MapWidth = MaxGridPosition.X + MinGridPosition.X;
                MapHeight = MaxGridPosition.Z + MinGridPosition.Z;
                MiddleOfMap = new Vector2(MapWidth / 2, MapHeight / 2);
            }

            ComputeBrushGroups();
        }

        /// <summary>
        /// 8 connected flood fill over <see cref="NavigationGridCellFlags.HAS_GRASS"/> cells to
        /// assign each connected brush region a unique group ID in <see cref="CellGrassGroups"/>.
        /// Mirrors the client's <c>NavGrid::ComputeBrushGroups</c> logic the function exists as a
        /// standalone definition at S4:2546 but in the shipped binary the body is inlined directly
        /// into <c>NavGrid::Finalize</c> at S4:3098-3131 (= load time post init), meaning the array
        /// is populated at (jfyi if you wanna look in the decomp urself)
        /// runtime. We use iterative BFS instead of the client's recursive approach to avoid stack
        /// overflow on maps with large connected brush regions. (basically never happens but just in case)
        /// </summary>
        private void ComputeBrushGroups()
        {
            int total = (int)(CellCountX * CellCountY);
            CellGrassGroups = new byte[total];
            byte currentGroupID = 0;
            int width = (int)CellCountX;
            int height = (int)CellCountY;
            var queue = new Queue<int>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int seedIdx = y * width + x;
                    var seed = Cells[seedIdx];
                    if (!seed.HasFlag(NavigationGridCellFlags.HAS_GRASS)) continue;
                    if (CellGrassGroups[seedIdx] != 0) continue;

                    if (currentGroupID == 255)
                    {
                        // uint8 group ID overflow would wrap to 0 (= "ungrouped"). OldSR has about ~12
                        // brushes, so realistic maps stay way under this limit. Bail with a stable
                        // marker rather than corrupting the assignment.
                        break;
                    }
                    currentGroupID++;

                    queue.Enqueue(seedIdx);
                    CellGrassGroups[seedIdx] = currentGroupID;
                    while (queue.Count > 0)
                    {
                        int idx = queue.Dequeue();
                        int cx = idx % width;
                        int cy = idx / width;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int ny = cy + dy;
                            if (ny < 0 || ny >= height) continue;
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = cx + dx;
                                if (nx < 0 || nx >= width) continue;
                                int nIdx = ny * width + nx;
                                if (CellGrassGroups[nIdx] != 0) continue;
                                if (!Cells[nIdx].HasFlag(NavigationGridCellFlags.HAS_GRASS)) continue;
                                CellGrassGroups[nIdx] = currentGroupID;
                                queue.Enqueue(nIdx);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the brush-group ID at <paramref name="worldPos"/> within
        /// <paramref name="checkRadius"/>, or 0 if the position isn't in any brush. Mirrors the
        /// client's <c>NavGrid::GetNearestGrassGroupGridSpace</c> (S4:5379 the world space
        /// wrapper at 5353 just translates coords).
        ///
        /// Two mode query same threshold as <see cref="IsWallOfGrass"/> at 35 world units:
        ///   * radius &lt; 35: single cell lookup at the position's cell.
        ///   * radius &gt;= 35: scan cells within radius (clamped to 500 max), tally non-zero group
        ///     IDs into a 256 bucket vote, return the group with the highest count. This handles
        ///     "champion straddling a brush boundary" by picking the brush they're most associated
        ///     with rather than an arbitrary cell.
        ///
        /// Use case is vision logic that needs "are these two units in the same brush?" (makes sense) call this
        /// for both observer and target, then compare the returned group IDs (mirrors the client's
        /// internal <c>TestGrassGroupMismatch</c> at S4:13748 which is just <c>group(target) !=
        /// group(src)</c>).
        /// </summary>
        public byte GetNearestGrassGroup(Vector2 worldPos, float checkRadius)
        {
            if (checkRadius < 35f)
            {
                var navPos = TranslateToNavGrid(worldPos);
                short cx = (short)navPos.X;
                short cy = (short)navPos.Y;
                if (cx < 0 || cy < 0 || cx >= CellCountX || cy >= CellCountY) return 0;
                int idx = cy * (int)CellCountX + cx;
                return CellGrassGroups[idx];
            }

            // Large radius vote. Cap at 500 world units (matches client S4:5446-5448).
            float r = Math.Min(checkRadius, 500f);
            float r2 = r * r;

            var navMin = TranslateToNavGrid(new Vector2(worldPos.X - r, worldPos.Y - r));
            var navMax = TranslateToNavGrid(new Vector2(worldPos.X + r, worldPos.Y + r));
            short cx0 = (short)Math.Floor(navMin.X);
            short cx1 = (short)Math.Floor(navMax.X);
            short cy0 = (short)Math.Floor(navMin.Y);
            short cy1 = (short)Math.Floor(navMax.Y);

            Span<int> groupCounts = stackalloc int[256];
            for (short y = cy0; y <= cy1; y++)
            {
                if (y < 0 || y >= CellCountY) continue;
                for (short x = cx0; x <= cx1; x++)
                {
                    if (x < 0 || x >= CellCountX) continue;
                    int idx = y * (int)CellCountX + x;
                    byte g = CellGrassGroups[idx];
                    if (g == 0) continue;
                    var cellCenter = TranslateFromNavGrid(Cells[idx].Locator);
                    if (Vector2.DistanceSquared(cellCenter, worldPos) > r2) continue;
                    groupCounts[g]++;
                }
            }

            byte bestGroup = 0;
            int bestCount = 0;
            for (int i = 1; i < 256; i++)
            {
                if (groupCounts[i] > bestCount)
                {
                    bestCount = groupCounts[i];
                    bestGroup = (byte)i;
                }
            }
            return bestGroup;
        }

        // Reference counted dynamic blockers (cellId -> number of static buildings blocking it).
        // Used by AddDynamicBlocker / RemoveDynamicBlocker so turrets, inhibitors and nexuses can
        // bake their footprints into the grid without fighting the map-loaded NOT_PASSABLE flags.
        private readonly Dictionary<int, int> _dynamicBlockers = new Dictionary<int, int>();

        /// <summary>
        /// Bakes a circular footprint into the navgrid. Returns the list of cell IDs that were
        /// newly blocked; pass it back to <see cref="RemoveDynamicBlocker"/> when the building dies.
        /// Multiple overlapping blockers ref-count correctly.
        /// </summary>
        public List<int> AddDynamicBlocker(Vector2 worldCenter, float worldRadius)
        {
            var blocked = new List<int>();
            if (worldRadius <= 0)
            {
                return blocked;
            }

            var minCell = TranslateToNavGrid(new Vector2(worldCenter.X - worldRadius, worldCenter.Y - worldRadius));
            var maxCell = TranslateToNavGrid(new Vector2(worldCenter.X + worldRadius, worldCenter.Y + worldRadius));
            short cx0 = (short)Math.Floor(minCell.X);
            short cx1 = (short)Math.Ceiling(maxCell.X);
            short cy0 = (short)Math.Floor(minCell.Y);
            short cy1 = (short)Math.Ceiling(maxCell.Y);

            float r2 = worldRadius * worldRadius;
            for (short cx = cx0; cx <= cx1; cx++)
            {
                for (short cy = cy0; cy <= cy1; cy++)
                {
                    var cell = GetCell(cx, cy);
                    if (cell == null)
                    {
                        continue;
                    }
                    var centerWorld = TranslateFromNavGrid(cell.Locator);
                    if (Vector2.DistanceSquared(centerWorld, worldCenter) <= r2)
                    {
                        IncrementBlocker(cell.ID);
                        blocked.Add(cell.ID);
                    }
                }
            }
            return blocked;
        }

        /// <summary>
        /// Releases the cells previously blocked by <see cref="AddDynamicBlocker"/>. The list
        /// passed in must come from the matching Add call (cell ID set is ref counted, not raw).
        /// </summary>
        public void RemoveDynamicBlocker(List<int> blockedCellIds)
        {
            if (blockedCellIds == null)
            {
                return;
            }
            foreach (var id in blockedCellIds)
            {
                if (_dynamicBlockers.TryGetValue(id, out int count))
                {
                    if (count <= 1)
                    {
                        _dynamicBlockers.Remove(id);
                    }
                    else
                    {
                        _dynamicBlockers[id] = count - 1;
                    }
                }
            }
        }

        private void IncrementBlocker(int cellId)
        {
            if (_dynamicBlockers.TryGetValue(cellId, out int count))
            {
                _dynamicBlockers[cellId] = count + 1;
            }
            else
            {
                _dynamicBlockers[cellId] = 1;
            }
        }

        // Per search counter — each GetPath bumps this. Cells with stale SearchSession are
        // treated as untouched, eliminating the need to allocate HashSet/Dictionary per search.
        private int _searchSession;

        // Manhattan distance, used as the A* heuristic to mirror the client's BuildNavGridPath
        // source is this: NavGrid.cpp::ComputeCellDistHeuristic. Inadmissible with 8-neighbor + 1.0/√2 step
        // costs, so paths can be slightly suboptimal in real distance but biases toward
        // axis-aligned shapes that match what the client produces.
        private static float ManhattanDistance(Vector2 a, Vector2 b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }

        // Backward search heuristic scale. The client gives the backward bidirectional search
        // a 4× discount on its heuristic so it expands more aggressively when both heaps share
        // a global priority pop — verified against S1 source where it's `mFlags & 0x80 ? 0.25 : 1.0`
        // on the PathInformation struct, not on cells.
        private const float BACKWARD_HEURISTIC_SCALE = 0.25f;

        // Diagonal terrain corner cut prevention. The S4 client's A* expansion at S1:10157-10178
        // / S4:437-451 unconditionally pushes all 8 neighbors via TryPushToHeap; the only terrain
        // gate inside (S1:8950-8951, S4:14059-14070) is a per-cell NOT_PASSABLE check that
        // applies equally to orthogonal and diagonal directions. The named function `DiagonalCheck`
        // (S4:2952-2979) is just `!HasStuckActor(...)` — actor blocking, not terrain and has
        // zero callers (dead/inlined). So the client allows diagonal moves through 1-cell terrain
        // pinches.
        //
        // Setting this to true makes the server stricter than the client: a diagonal step is
        // skipped when either orthogonal neighbor is unwalkable, preventing units from clipping
        // through wall corners. CastCircle further down the expansion already provides implicit
        // corner cut prevention for non-target neighbors via line rasterization (the perfect 
        // diagonal `error == 0` branch in `GetAllCellsInLine` emits both ortho cells), so this
        // flag's effect is mostly limited to the target-cell case where CastCircle is skipped
        // and to `distanceThreshold == 0` paths.
        //
        // Flip to false for 1:1 client parity at the cost of allowing visible terrain-corner
        // glitching on tight pinches.
        private const bool ENABLE_DIAGONAL_CORNER_CUT_PREVENTION = true;

        // Hint-grid weight `mAmountOfHintToUse` for the slow-accurate A* branch.
        //
        // S1 vs S4 divergence (verified 2026-05-04):
        //  * S1 (`gameengine/aipath/navigationgrid.cpp:9956`): the dynamic formula
        //    `(distScaled - 100) / 140 * 0.5 + 3.0` writes to `this->mAmountOfHintToUse`
        //    (NavGrid+0x304), the field the heuristic later reads. So the per-search value
        //    propagates: short paths use 3.0, long paths climb above.
        //  * S4 (`ai/NavGrid.cpp:228-234`): the same formula computes a value but writes it to
        //    `outNavPath[9].m_PathOverrideSpeed` — a NavigationPath field, NOT NavGrid+0x304.
        //    The heuristic reads NavGrid+0x304 which is set ONCE in the constructor
        //    (`mov [navgrid+0x304], 0x40400000` at S4:0x00779931 = 3.0f) and never overwritten.
        //    So S4 effectively uses static 3.0 for the heuristic regardless of path length.
        //
        // S4 is our primary target patch
        // 4.17 client behavior is what we mirror. So we use the static 3.0, matching what the
        // S4 heuristic actually consumes. The dynamic formula in S4 looks like dead code (writes
        // to a NavigationPath field that's not read by the path-cost computation we ported).
        //
        // Fast branch keeps the hardcoded 6.0 fallback per S4:237 / S1:9960.
        private const float HINT_MULTIPLIER_SLOW = 3.0f;
        private const float HINT_MULTIPLIER_FAST = 6.0f;

        /// <summary>
        /// Returns the hint grid network cost between two cells via bilinear interpolation over
        /// each cell's two RefHintNodes and RefHintWeight, mirroring the client's
        /// NavHintGrid&lt;30&gt;::GetCost. Returns 0 if either cell has invalid hint references.
        /// </summary>
        private float GetHintCost(NavigationGridCell from, NavigationGridCell to)
        {
            short fromA = from.RefHintNode[0];
            short fromB = from.RefHintNode[1];
            short toA   = to.RefHintNode[0];
            short toB   = to.RefHintNode[1];

            int hintCount = HintGrid?.HintNodes?.Length ?? 0;
            if (hintCount == 0) return 0f;
            if (fromA < 0 || fromA >= hintCount || fromB < 0 || fromB >= hintCount) return 0f;
            if (toA   < 0 || toA   >= hintCount || toB   < 0 || toB   >= hintCount) return 0f;

            float wF = from.RefHintWeight;
            float wT = to.RefHintWeight;
            var nodes = HintGrid.HintNodes;

            // Bilinear blend over (fromA, fromB) × (toA, toB) using the two weights.
            float dAA = nodes[fromA].Distances[toA];
            float dAB = nodes[fromA].Distances[toB];
            float dBA = nodes[fromB].Distances[toA];
            float dBB = nodes[fromB].Distances[toB];

            float costAtFromA = wT * dAA + (1f - wT) * dAB;
            float costAtFromB = wT * dBA + (1f - wT) * dBB;
            return wF * costAtFromA + (1f - wF) * costAtFromB;
        }

        /// <summary>
        /// Predicate consulted during A* expansion to test whether a candidate neighbor cell is
        /// blocked by another actor (mirrors the client's HasStuckActor at S1:9016 / S4 callsite
        /// inside QueryForPath). When supplied, the search treats positive cells as impassable
        /// for this attacker and routes around them; null preserves the pre-A1 terrain-only
        /// behavior. The closure typically captures the attacker's collision state and the
        /// CollisionHandler.
        /// </summary>
        public delegate bool ActorBlockedPredicate(Vector2 cellCenterWorld, NavigationGridCell cell);

        /// <summary>
        /// Finds a path of waypoints, which are aligned by the cells of the navgrid (A* method), that lead to a set destination.
        /// </summary>
        /// <param name="from">Point that the path starts at.</param>
        /// <param name="to">Point that the path ends at.</param>
        /// <param name="distanceThreshold">Amount of distance away from terrain that the path should be.</param>
        /// <param name="useFastPath">Use the client's fast (less accurate) A* mode — mTravelFactor=2.5
        /// and a fixed hint multiplier of 6.0, mirroring the `!collisionState->m_UseSlowerButMoreAccurateSearch`
        /// branch at S4:237 / S1:9960. Default is false (slow-accurate, mTravelFactor=5.0, dynamic hint
        /// multiplier) which matches the client's default when no CollisionState is supplied.</param>
        /// <param name="actorBlocked">Optional actor-aware path filter (A1). When supplied, cells
        /// for which it returns true are skipped during expansion just like statically-blocked
        /// cells. Null = terrain-only pathing (pre-A1 behavior).</param>
        /// <returns>List of points forming a path in order: from -> to</returns>
        public List<Vector2> GetPath(Vector2 from, Vector2 to, float distanceThreshold = 0, bool useFastPath = false, ActorBlockedPredicate actorBlocked = null)
        {
            return GetPath(from, to, out _, distanceThreshold, useFastPath, actorBlocked);
        }

        /// <summary>
        /// Builds a path from `from` to `to` using bidirectional A*. Two parallel A* searches
        /// run from start and goal, meeting in the middle. Matches the client's BuildNavigationPath
        /// behavior so server and client agree on routes (avoids visible divergence-based snaps).
        ///
        /// If the goal cell is unreachable, returns the path to the closest-explored reachable
        /// cell instead (forward direction) and sets <paramref name="isPartialPath"/>. Returns
        /// null only when no progress at all could be made (start cell isolated, same position,
        /// or out-of-grid coordinates).
        /// </summary>
        public List<Vector2> GetPath(Vector2 from, Vector2 to, out bool isPartialPath, float distanceThreshold = 0, bool useFastPath = false, ActorBlockedPredicate actorBlocked = null)
        {
            isPartialPath = false;

            if(from == to)
            {
                return null;
            }

            var fromNav = TranslateToNavGrid(from);
            var cellFrom = GetCell(fromNav, false);
            to = GetClosestTerrainExit(to, distanceThreshold);
            var toNav = TranslateToNavGrid(to);
            var cellTo = GetCell(toNav, false);

            if (cellFrom == null || cellTo == null)
            {
                return null;
            }
            if(cellFrom.ID == cellTo.ID)
            {
                return new List<Vector2>(2){ from, to };
            }

            // Straight-line LOS first. if the corridor is clear, skip A* entirely.
            // This matches the client's BuildNavigationPath which always does GridLineOfSightTest2
            // before falling through to the grid pathfinder.
            if (!CastCircle(from, to, distanceThreshold))
            {
                return new List<Vector2>(2) { from, to };
            }

            // === Bidirectional A* ===
            // Forward search expands from cellFrom toward cellTo; backward search expands from cellTo toward cellFrom.
            // Both run in parallel; at each step, we expand the smaller frontier. The searches
            // meet in the middle typically resulting in about half the number of cells compared to unidirectional A*.

            // Bump session —> cells with stale SearchSession*F/B fields are now treated as
            // "untouched by current search", so we don't have to clear anything per call.
            int session = ++_searchSession;

            var openF = new PriorityQueue<NavigationGridCell, float>(1024);
            var openB = new PriorityQueue<NavigationGridCell, float>(1024);

            float startToGoal = ManhattanDistance(fromNav, toNav);
            openF.Enqueue(cellFrom, startToGoal);
            cellFrom.SearchSession = session;
            cellFrom.SearchClosed = false;
            cellFrom.SearchG = 0f;
            cellFrom.SearchCameFrom = null;
            cellFrom.LastTouchByBackward = false;
            cellFrom.CellInBackwardHeap = false;
            openB.Enqueue(cellTo, startToGoal);
            cellTo.SearchSession = session;
            cellTo.SearchClosed = false;
            cellTo.SearchG = 0f;
            cellTo.SearchCameFrom = null;
            cellTo.LastTouchByBackward = true;
            cellTo.CellInBackwardHeap = true;

            // Hint multiplier and travel factor the client picks one of two presets at the start of
            // BuildNavGridPath based on `collisionState->m_UseSlowerButMoreAccurateSearch`
            // (S4:222-238 / S1:9946-9962). Both pairs verified literally in S4 (S1 mislabels the
            // floats `Karma::kMaxKarma`/`StunYourselfDuration` as RTTI-shared symbols).
            //
            //   Slow accurate (default; client when collisionState is null OR opts in):
            //     mTravelFactor      = 5.0
            //     mAmountOfHintToUse = (max(0, distWorld/50 - 100) / 140) * 0.5 + 3.0
            //
            //   Fast (client when collisionState explicitly opts out of accuracy):
            //     mTravelFactor      = 2.5 -> less G-weighting, more greedy, fewer expansions
            //     mAmountOfHintToUse = 6.0  -> fixed high hint pull, biases hard along lane splines
            //
            // The fast branch trades path optimality for fewer heap operations. Server's caller
            // can opt in via `useFastPath`; default stays at slow-accurate for path quality.
            float TRAVEL_FACTOR;
            float hintMultiplier;
            if (useFastPath)
            {
                TRAVEL_FACTOR = 2.5f;
                hintMultiplier = HINT_MULTIPLIER_FAST;
            }
            else
            {
                TRAVEL_FACTOR = 5f;
                hintMultiplier = HINT_MULTIPLIER_SLOW;
            }
            float effectiveGMultiplier = TRAVEL_FACTOR * CellSize;

            // Readding hysteresis: only re-open a closed/queued cell if the new G is at least
            // READD_HYSTERESIS_WORLD better. Mirrors the client's `mArrivalCost > newCost + 100.0`
            // gate on the AdjustCell branch (S4 NavGrid.cpp:11664, S1 navigationgrid.cpp:7948).
            // The literal 100 is in WORLD units in the client (mArrivalCost is world unit distance,
            // confirmed by S1:10051 dividing it by mCellSize to convert to cell units). Server's G
            // is in cell step units, so we convert the threshold to cell-step units once here.
            const float READD_HYSTERESIS_WORLD = 100f;
            float readdHysteresisCells = READD_HYSTERESIS_WORLD / CellSize;

            // Closest-explored cell to the goal (forward direction). Used only for the partial-path
            // fallback when the goal cell is unreachable.
            NavigationGridCell bestCellF = cellFrom;
            float bestHeuristicF = ManhattanDistance(cellFrom.GetCenter(), toNav);

            // Backward analogue: closest explored cell toward fromNav. Partial-path fallback for the
            // backward direction (currently informational; backward never produces the final path).
            NavigationGridCell bestCellB = cellTo;
            float bestHeuristicB = ManhattanDistance(cellTo.GetCenter(), fromNav);

            // Each side's last dequeued cell -> this is what the client calls
            // mCurrentPathInformation->mLastKnownGoodLocation (S1 navigationgrid.cpp:10111, assigned
            // on every pop). The convergence bias uses the OTHER side's value, so the heuristic
            // tracks where the opposite frontier most recently was, not its all-time best.
            NavigationGridCell lastDequeuedF = cellFrom;
            NavigationGridCell lastDequeuedB = cellTo;

            // Running mean of |neighbor to otherSide.lastDequeued| across both directions, used for
            // the 0.03-weight convergence bias. Shared between forward and backward the client stores
            // these on the NavGrid itself (mAccumaltiveDistanceBetweenBestNodes / mNumberNodesAdded,
            // S1 line 5993-6000) and resets both at the start of each path search.
            float convergenceAccumulator = 0f;
            int convergenceCount = 0;

            // Client style  first meeting termination (S1 navigationgrid.cpp:10112-10142): two break conditions —>
            // (1) forward dequeues its own goal cell (standard A* end), or (2) forward expansion
            // sees a neighbor that backward has already closed (first meeting via 0x80 flag in the
            // client; here via closedB membership). The backward search itself never breaks the
            // loop; it only feeds the heuristic and the meeting set.
            bool goalReached = false;
            NavigationGridCell meetingCell = null;
            // The forward popped cell at meeting time (Phase 4 reconstruction anchor). When meeting
            // fires from a cross direction case where forward never previously touched meetingCell,
            // walking meetingCell.SearchCameFrom would jump into the backward chain. Walking from
            // this anchor instead gives the clean forward chain to cellFrom.
            NavigationGridCell meetingPoppedCell = null;

            // Backward half of the path, populated INSIDE the expansion step when meeting is
            // detected (mirrors client placement: TraverseToEnd is called inline at S1:10144 from
            // within the popped cell handling, not as post-loop reconstruction).
            // List order: closest-to-meeting first, ending at cellTo.
            var backwardHalf = new List<NavigationGridCell>();

            // Per direction pop counters for the GetTopNode-style scheduler below. Mirrors the
            // client's `m_CountGets` field on each NavigationHeap (S1:8789-8791, S4:6405).
            int popCountF = 0;
            int popCountB = 0;
            const int POP_BALANCE_TOLERANCE = 50;
            const float COST_HYSTERESIS = 50f;

            while (openF.Count > 0 && openB.Count > 0 && !goalReached && meetingCell == null)
            {
                // Scheduling this is a port of client GetTopNode (S1:8772-8833, S4:6366-6470). Both heaps
                // active, so the `mPathFromBothDirections` guard is implicit. Three regimes:
                //   * One side is 50+ pops ahead → pop the other (catch-up).
                //   * Both within 50 pops → peek both tops, pick globally cheapest with
                //     +COST_HYSTERESIS bias toward forward (S1:8801, S4:6445).
                bool expandForward;
                if (popCountB > popCountF + POP_BALANCE_TOLERANCE)
                {
                    expandForward = true;
                }
                else if (popCountF > popCountB + POP_BALANCE_TOLERANCE)
                {
                    expandForward = false;
                }
                else
                {
                    // Balanced regime we peek both heap tops. C#'s PriorityQueue.TryPeek gives the
                    // current smallest priority without popping; equivalent to client's PokeTop.
                    openF.TryPeek(out _, out float fTopCost);
                    openB.TryPeek(out _, out float bTopCost);
                    expandForward = !(bTopCost + COST_HYSTERESIS < fTopCost);
                }

                if (expandForward)
                {
                    ExpandStep(
                        openF, openB, session,
                        cellFrom, cellTo, fromNav, toNav, distanceThreshold,
                        forward: true,
                        ref bestCellF, ref bestHeuristicF,
                        ref bestCellB, ref bestHeuristicB,
                        ref lastDequeuedF, ref lastDequeuedB,
                        ref convergenceAccumulator, ref convergenceCount,
                        hintMultiplier, readdHysteresisCells, effectiveGMultiplier,
                        backwardHalf,
                        ref goalReached, ref meetingCell, ref meetingPoppedCell,
                        actorBlocked, cellFrom, cellTo
                    );
                    popCountF++;
                }
                else
                {
                    ExpandStep(
                        openF, openB, session,
                        cellTo, cellFrom, toNav, fromNav, distanceThreshold,
                        forward: false,
                        ref bestCellF, ref bestHeuristicF,
                        ref bestCellB, ref bestHeuristicB,
                        ref lastDequeuedF, ref lastDequeuedB,
                        ref convergenceAccumulator, ref convergenceCount,
                        hintMultiplier, readdHysteresisCells, effectiveGMultiplier,
                        backwardHalf,
                        ref goalReached, ref meetingCell, ref meetingPoppedCell,
                        actorBlocked, cellFrom, cellTo
                    );
                    popCountB++;
                }
            }

            // Path reconstruction. Three cases:
            //   1. meetingCell set -> walk shared chain from `meetingPoppedCell` (the F-popped
            //      cell at meeting time, captured as anchor). Prepend meetingCell on top, append
            //      pre-built backwardHalf. Without this anchor, walking from meetingCell directly
            //      would drop the forward prefix when forward never previously touched meetingCell.
            //   2. goalReached    -> walk shared chain from cellTo.
            //   3. neither        -> partial path from bestCellF.
            //
            // The shared chain is reliable here because Phase 5's tag-driven expansion guarantees
            // that the entry point (meetingPoppedCell, cellTo, or bestCellF) is forward-tagged.
            // Cells along its chain back to cellFrom were forward-popped earlier and thus
            // forward closed; the closed in either direction skip in ExpandStep prevents backward
            // from cross direction AdjustCelling them post close, so their shared.SearchCameFrom
            // is never overwritten with a backward direction predecessor.
            var pathCells = new List<NavigationGridCell>();
            if (meetingCell != null)
            {
                pathCells.Add(meetingCell);
                var current = meetingPoppedCell;
                while (current != null)
                {
                    pathCells.Add(current);
                    current = current.SearchSession == session ? current.SearchCameFrom : null;
                }
                pathCells.Reverse();
                pathCells.AddRange(backwardHalf);
            }
            else
            {
                NavigationGridCell pathTail;
                if (goalReached)
                {
                    pathTail = cellTo;
                }
                else
                {
                    if (bestCellF.ID == cellFrom.ID)
                    {
                        return null;
                    }
                    isPartialPath = true;
                    pathTail = bestCellF;
                }

                var current = pathTail;
                while (current != null)
                {
                    pathCells.Add(current);
                    current = current.SearchSession == session ? current.SearchCameFrom : null;
                }
                pathCells.Reverse();
            }

            SmoothPath(pathCells, distanceThreshold);

            var returnList = new List<Vector2>(pathCells.Count);
            returnList.Add(from);
            for (int i = 1; i < pathCells.Count - 1; i++)
            {
                returnList.Add(TranslateFromNavGrid(pathCells[i].Locator));
            }
            // A full path lands exactly on the request target; a partial path lands at the center
            // of the closest reachable cell.
            if (isPartialPath)
            {
                returnList.Add(TranslateFromNavGrid(pathCells[pathCells.Count - 1].Locator));
            }
            else
            {
                returnList.Add(to);
            }

            return returnList;
        }

        /// <summary>
        /// Walks the backward chain starting at <paramref name="meetingCell"/>, appending each
        /// successor via <see cref="NavigationGridCell.SearchCameFrom"/> until the chain ends at
        /// <c>cellTo</c>. Mirrors the client's <c>TraverseToEnd</c> (S1 navigationgrid.cpp:6220-6296):
        /// the same semantic step (walk opposite of arrival toward the backward search's start),
        /// using shared SearchCameFrom which on backward tagged cells equals the backward predecessor.
        /// Reliable because all cells on the backward chain back from meetingCell were B-popped
        /// during expansion (B-closed), and the closed in either direction skip prevents forward
        /// from overwriting their shared.SearchCameFrom post close. 200-step cap matches S1:6256.
        /// </summary>
        private static void TraverseToEnd(
            NavigationGridCell meetingCell,
            int session,
            List<NavigationGridCell> outBackwardHalf)
        {
            var current = meetingCell.SearchSession == session ? meetingCell.SearchCameFrom : null;
            for (int j = 0; j < 200 && current != null; j++)
            {
                outBackwardHalf.Add(current);
                current = current.SearchSession == session ? current.SearchCameFrom : null;
            }
        }

        /// <summary>
        /// An expansion step for forward or backward search. Both directions are structurally
        /// identical; they differ only in source/target cells and coordinates.
        ///
        /// Sets <paramref name="goalReached"/> when the forward search dequeues its target cell.
        /// Sets <paramref name="meetingCell"/> when forward expansion encounters a neighbor that
        /// backward has already closed (first-meeting termination, mirrors client 0x80 detection)
        /// and populates <paramref name="backwardHalf"/> via <see cref="TraverseToEnd"/>.
        /// Backward expansion never terminates the loop because it only feeds the heuristic.
        /// </summary>
        private void ExpandStep(
            PriorityQueue<NavigationGridCell, float> openF,
            PriorityQueue<NavigationGridCell, float> openB,
            int session,
            NavigationGridCell sourceCell,
            NavigationGridCell targetCell,
            Vector2 sourceNav,
            Vector2 targetNav,
            float distanceThreshold,
            bool forward,
            ref NavigationGridCell bestCellF,
            ref float bestHeuristicF,
            ref NavigationGridCell bestCellB,
            ref float bestHeuristicB,
            ref NavigationGridCell lastDequeuedF,
            ref NavigationGridCell lastDequeuedB,
            ref float convergenceAccumulator,
            ref int convergenceCount,
            float hintMultiplier,
            float readdHysteresisCells,
            float effectiveGMultiplier,
            List<NavigationGridCell> backwardHalf,
            ref bool goalReached,
            ref NavigationGridCell meetingCell,
            ref NavigationGridCell meetingPoppedCell,
            ActorBlockedPredicate actorBlocked,
            NavigationGridCell actorRefOrigin,
            NavigationGridCell actorRefGoal)
        {
            // Dequeue from the popped direction's heap (scheduler picked one in GetPath).
            var open = forward ? openF : openB;
            if (!open.TryDequeue(out var cell, out _))
            {
                return;
            }

            // Close-on-dequeue uses the shared mIsOpen equivalent (S4 NavGrid offset 0xc) to
            // mirror the client's single-bit close. A cell can be in both heaps simultaneously
            // (cross direction first touch enqueues into the toucher's heap; subsequent re adds
            // go to the cell's owning heap via CellInBackwardHeap). Without the shared close check
            // the cell would be processed twice so once per heap.
            if (cell.SearchSession == session && cell.SearchClosed) return;
            cell.SearchClosed = true;

            // Phase 5: expansion direction follows the popped cell's tag (S1:10044-10048,
            // S4:296-371), not which heap was popped. A cell that backward most recently
            // AdjustCelled (LastTouchByBackward=true) belongs to the backward chain even if the
            // scheduler popped it from openF via a stale entry. `forward` (heap popped direction)
            // is still used for own side mirror close above and lastDequeued tracking below; all
            // other per direction logic uses `expandForward`.
            bool expandForward = !cell.LastTouchByBackward;

            // Track this side's last-dequeued cell so the convergence-bias heuristic on the other
            // side reads this every time it expands (mirrors client's mLastKnownGoodLocation,
            // S1 navigationgrid.cpp:10111: assigned every pop, not filtered for "best"). Tracked
            // by heap popped direction (the scheduler's `m_CountGets`-driven pop), not tag.
            if (forward) lastDequeuedF = cell;
            else         lastDequeuedB = cell;

            // Best explored tracking per expansion direction this is used for the partial-path fallback
            // when the goal cell is unreachable.
            float hToTarget = ManhattanDistance(cell.GetCenter(), targetNav);
            if (expandForward)
            {
                if (hToTarget < bestHeuristicF)
                {
                    bestHeuristicF = hToTarget;
                    bestCellF = cell;
                }
            }
            else
            {
                if (hToTarget < bestHeuristicB)
                {
                    bestHeuristicB = hToTarget;
                    bestCellB = cell;
                }
            }

            // Forward expansion dequeued its goal: done. Note `expandForward` (tag), not
            // `forward` (heap-popped). With Phase 5's tag driven expansion, only a cell tagged
            // forward AND whose ID matches the forward target counts as goal reached; popping
            // cellTo via a stale openF entry while it's now backward tagged is just a no-op pop.
            if (expandForward && cell.ID == targetCell.ID)
            {
                goalReached = true;
                return;
            }

            float thisG = cell.SearchG;

            // Escape mode: when the cell currently being expanded is itself unwalkable (unit
            // started inside a building footprint, was pushed in, etc.), we relax both the corner 
            // cut check and the CastCircle corridor check so A* can find a way out of the blocker.
            // Once the search reaches walkable terrain, normal restrictions apply for that cell's
            // own expansion. This is what fixes "stuck inside turret/inhibitor/nexus" without the
            // unstable spiral-snap from earlier attempts.
            bool sourceWalkable = IsWalkable(cell);

            // Popped cell direction gate (S1:10119, S4:387) the meeting detection only runs if the
            // popped cell is currently forward tagged. With Phase 5 the gate is just `expandForward`
            // (= !cell.LastTouchByBackward), since the tag IS the expansion direction now.
            bool checkMeeting = expandForward;

            foreach (NavigationGridCell neighborCell in GetCellNeighbors(cell))
            {
                // First-meeting check this runs BEFORE any other neighbor processing, mirroring
                // client S1:10125-10142 (a separate pre-pass over neighbors before LOOP 2 /
                // TryPushToHeap). Four conditions, all matching client S4:415-418:
                //   (1) backward has touched this cell in current session
                //   (2) backward has popped (finalized) it
                //   (3) backward was the LAST direction to mutate it (= client mFlags & 0x80)
                //   (4) cell has a real backward predecessor (= client arrivalDirection != 9 —
                //       refuses meeting on cellTo, the backward search start, where chain ends)
                // Outer gate `checkMeeting` adds the S1:10119 popped-cell-tag requirement.
                if (checkMeeting
                    && neighborCell.SearchSession == session
                    && neighborCell.SearchClosed
                    && neighborCell.LastTouchByBackward
                    && neighborCell.SearchCameFrom != null)
                {
                    meetingCell = neighborCell;
                    // Phase 4: capture the forward popped cell as anchor for path reconstruction.
                    // meetingCell itself was last touched by backward (its SearchCameFrom points
                    // up the backward chain in shared state, which would jump chains during a
                    // forward walk). Walking from `cell` (F-popped, F-closed, never overwritten
                    // by cross-direction adjust) gives a clean forward chain to cellFrom.
                    meetingPoppedCell = cell;
                    TraverseToEnd(neighborCell, session, backwardHalf);
                    return;
                }

                // Closed in either direction skip — mirrors client's `mIsOpen=false -> return 0`
                // gate at S1:7958-7960. Once a cell is finalized by ANY direction, no further
                // state mutation is allowed; the cell is "claimed" for path purposes. This is
                // the structural equivalent of the client's shared mIsOpen flag. Without it,
                // server's per direction storage would let forward overwrite a backward-closed
                // cell's state (and vice versa), breaking the chain consistency that the client
                // relies on. Meeting-detection above intentionally uses backward-closed cells, so
                // it must run BEFORE this skip.
                if (neighborCell.SearchSession == session && neighborCell.SearchClosed)
                {
                    continue;
                }

                bool diagonal = (cell.Locator.X != neighborCell.Locator.X)
                             && (cell.Locator.Y != neighborCell.Locator.Y);

                Vector2 neighborCellCoord = neighborCell.ID == targetCell.ID
                    ? targetNav
                    : neighborCell.GetCenter();

                if (sourceWalkable)
                {
                    // Server stricter than client corner cut prevention. Gated by
                    // `ENABLE_DIAGONAL_CORNER_CUT_PREVENTION` (see flag block above for client
                    // verification + tradeoffs). When enabled, skip diagonal moves where either
                    // orthogonal neighbor is unwalkable this prevents wall corner clipping that the
                    // client tolerates.
                    if (ENABLE_DIAGONAL_CORNER_CUT_PREVENTION && diagonal)
                    {
                        var orthA = GetCell(neighborCell.Locator.X, cell.Locator.Y);
                        var orthB = GetCell(cell.Locator.X, neighborCell.Locator.Y);
                        if (!IsWalkable(orthA) || !IsWalkable(orthB))
                        {
                            continue;
                        }
                    }

                    if (neighborCell.ID != targetCell.ID)
                    {
                        Vector2 cellCoord = cell.ID == sourceCell.ID ? sourceNav : cell.GetCenter();
                        if (CastCircle(cellCoord, neighborCellCoord, distanceThreshold, false))
                        {
                            continue;
                        }
                    }

                    // Actor-aware path filter (A1). Mirrors the client's HasStuckActor gate at
                    // S1:9016 / S4 callsite inside QueryForPath: after terrain walkability passes,
                    // a per-cell actor query rejects neighbors blocked by other units. Held inside
                    // the `sourceWalkable` branch on purpose: when the popped cell is already
                    // inside a blocker (escape mode), we relax both terrain AND actor checks so A*
                    // can route the unit out without compounding restrictions.
                    //
                    // Two exemptions mirror the client's pre-HasStuckActor gates at S1:8985-9001.
                    // Both reference the un-rotated path endpoints (actorRefOrigin = cellFrom,
                    // actorRefGoal = cellTo), regardless of expansion direction. Matching the
                    // client where each direction's PathInformation labels them differently but
                    // both gates ultimately key off the same two world points (cellFrom for #1,
                    // cellTo for #2). Direction rotating these would mean backward expansion
                    // (sourceCell=cellTo) wouldn't apply the near goal exemption near cellTo at
                    // all, breaking the case where backward starts at cellTo and its immediate
                    // neighbors must remain pathable.
                    //
                    // 1st. FAR-FROM-ORIGIN skip (S1:8985-8989, `v49 >= 1500.0 → goto LABEL_34`):
                    //     when the neighbor is ≥1500 max-axis world-units from cellFrom, the
                    //     actor check is bypassed. Long-path perf opt and semantic match: actor
                    //     blocking matters near where the unit actually starts traversing.
                    //
                    // 2nd. NEAR-GOAL skip (S1:9000-9001, `(neighbor - mGoalLocator)² <= centerCell`):
                    //     when the neighbor is within a 2-cell squared-distance of cellTo, the
                    //     actor check is bypassed so the unit can path INTO an enemy at/near the
                    //     goal (attack-target reachability). Client's exact `centerCell` threshold
                    //     isn't decoded; 2-cell radius² covers the immediate neighbors of the
                    //     goal, matching the spirit of "let the path land on the target's
                    //     collision footprint". Replaces the prior ID-equality exemption.
                    if (actorBlocked != null)
                    {
                        int dxOrig = neighborCell.Locator.X - actorRefOrigin.Locator.X;
                        if (dxOrig < 0) dxOrig = -dxOrig;
                        int dyOrig = neighborCell.Locator.Y - actorRefOrigin.Locator.Y;
                        if (dyOrig < 0) dyOrig = -dyOrig;
                        int maxAxisOrig = dxOrig > dyOrig ? dxOrig : dyOrig;
                        bool farFromOrigin = maxAxisOrig * CellSize >= 1500f;

                        int dxGoal = neighborCell.Locator.X - actorRefGoal.Locator.X;
                        int dyGoal = neighborCell.Locator.Y - actorRefGoal.Locator.Y;
                        bool nearGoal = dxGoal * dxGoal + dyGoal * dyGoal <= 4;

                        if (!farFromOrigin
                            && !nearGoal
                            && actorBlocked(TranslateFromNavGrid(neighborCell.Locator), neighborCell))
                        {
                            continue;
                        }
                    }
                }
                // else: escape mode, skip both checks — let A* expand freely out of the blocker.

                float stepCost = diagonal ? 1.41421356f : 1f;

                float tentativeG = thisG + stepCost
                    + neighborCell.ArrivalCost
                    + neighborCell.AdditionalCost;

                // Detect first-touch BEFORE updating the session marker. First-touch is the only
                // path that re-computes H and bumps the convergence accumulator, mirroring the
                // client's split between AddCell (computes mHeuristic, S1:7975-7998) and
                // AdjustCell (replaces cost only, S1:7950).
                //
                // Re-add hysteresis: on the AdjustCell branch (sessionID matches) the client only
                // accepts a re-add if the new G is at least 100 world-units better than existing
                // (S4:11664 / S1:7948). Translated to server's cell-unit G, the threshold is
                // readdHysteresisCells. First-touches always proceed (no hysteresis).
                //
                // LastTouchByBackward is set to !forward on every successful state mutation, mirroring
                // the client's `mFlags |= 0x80` / `&= ~0x80` at S1:7966-7972 (AddCell) and S1:7952-7955
                // (AdjustCell). Skipped paths (closed in our direction continue, hysteresis reject)
                // intentionally don't update it basically same as client returning 0 without state change.
                // Phase 5: state writes use `expandForward` (cell tag driven) instead of `forward`
                // (heap-popped). A cross-direction-tagged popped cell stamps neighbors with its
                // tag's direction, not its heap's direction.
                // Phase 6: AddCell vs AdjustCell branch on shared state. AdjustCell hysteresis
                // mirrors client S1:7948 / S4:11664: skip if new G isn't at least 100 world-units
                // better than the cell's existing accumulated cost. AddCell (sharedFirstTouch)
                // always proceeds. LastTouchByBackward = !expandForward stamps the cell with the
                // expansion direction (S1:7966-7972 / S4:11671-11675).
                if (neighborCell.SearchSession == session
                    && tentativeG >= neighborCell.SearchG - readdHysteresisCells)
                    continue;
                bool sharedFirstTouch = neighborCell.SearchSession != session;
                neighborCell.SearchSession = session;
                neighborCell.SearchClosed = false;
                neighborCell.SearchG = tentativeG;
                neighborCell.SearchCameFrom = cell;
                neighborCell.LastTouchByBackward = !expandForward;
                if (sharedFirstTouch)
                {
                    // Heap ownership decided at first AddCell (mirrors client's
                    // mCurrentPathInformation->mNavHeap selection at AddCell time, fixed for the
                    // life of the cell in this search). AdjustCell never re-routes.
                    neighborCell.CellInBackwardHeap = !expandForward;
                }
                float h;
                if (sharedFirstTouch)
                {
                    float directionScale = expandForward ? 1.0f : BACKWARD_HEURISTIC_SCALE;

                    // Convergence bias — discounts cells close to the OTHER side's last-dequeued
                    // cell, normalized by a running mean of the same distance across all
                    // expansions. Mirrors client's `(manhattan(cell, otherPath.lastKnownGoodLocation)
                    // / mean) * 0.03` (S1 navigationgrid.cpp:5996-6002). The mean state is
                    // captured here at first-touch time and frozen into the cached H this re-adds
                    // never re-enter this branch. "Other side" is in expansion-direction terms.
                    Vector2 otherLastDequeued = (expandForward ? lastDequeuedB : lastDequeuedF).GetCenter();
                    float distToOtherLast = ManhattanDistance(neighborCellCoord, otherLastDequeued);
                    convergenceAccumulator += distToOtherLast;
                    convergenceCount++;
                    float convergenceMean = convergenceAccumulator / convergenceCount;
                    float convergenceBias = distToOtherLast / convergenceMean * 0.03f;

                    // Hint-grid network distance -> each cell carries refs into a 30x30 hint mesh
                    // (RefHintNode + RefHintWeight) with precomputed all-pairs shortest paths in
                    // world units. Client multiplies by mAmountOfHintToUse (S1 line 7983).
                    float hintCost = GetHintCost(neighborCell, targetCell) * hintMultiplier;

                    // Unit consistency with the client: manhattan + convergence terms are scaled
                    // to world units via CellSize so they balance correctly against the
                    // world-unit hint cost. Client does this via
                    // `ComputeCellDistHeuristic(...) * mCellSize` at S1 line 7975. G is kept in
                    // cell-step units in storage but multiplied by effectiveGMultiplier
                    // (= mTravelFactor * CellSize) when forming F so the contribution matches the
                    // client's `mTravelFactor * G_world + H_world` (S4:11783, S1:8018).
                    // NOTE: neighborCell.Heuristic is intentionally NOT added because that field is a
                    // serialized scratch value from when Riot dumped the navgrid file (per-search
                    // state, not a baked property), so reading it back as part of F is meaningless.
                    float manhattanWorld = ManhattanDistance(neighborCellCoord, targetNav)
                        * directionScale * CellSize;
                    float convergenceWorld = convergenceBias * CellSize;

                    h = manhattanWorld + convergenceWorld + hintCost;
                    neighborCell.SearchHeuristic = h;

                    // NOTE: client S1:7986-7994 / S4:11741-11762 has a goal-cell-sentinel +
                    // heap-reset branch here (sets mArrivalCost=-100000, mHeuristic=0, clears the
                    // heap, re-adds goal). Verified as DEAD CODE in client: it sits inside the
                    // AddCell branch (sessionID mismatch) but cellTo's sessionID is set by
                    // backward's initial AddCell, and forward's later cross-direction AddCell
                    // attempts hit the AdjustCell branch (sessionID matches) where cellTo's
                    // mIsOpen=0 (set at init S1:8013) blocks any state mutation via the S1:7948
                    // `mIsOpen && etc...` gate → return 0. So the sentinel never executes in client.
                    // Server reaches cellTo via direct forward expansion just fine so `goalReached`
                    // fires on its eventual pop. Mirroring the sentinel literally would create
                    // server-specific early-commit behavior (heap reset cuts off pending cells),
                    // diverging from the client which actually completes via meeting+TraverseToEnd
                    // in this scenario, so we deliberately don't port it.
                }
                else
                {
                    // Re-add with a better G we reuse cached H. Matches client's AdjustCell branch
                    // which only swaps mArrivalCost in the heap, never touching mHeuristic.
                    h = neighborCell.SearchHeuristic;
                }

                // Phase 5: re-adds go to the cell's owning heap (immutable post-first-AddCell),
                // not the popped heap and not the expansion direction. Mirrors client's behavior
                // where mCurrentPathInformation owns the heap at AddCell time and AdjustCell only
                // swaps cost, leaving the cell on its original heap.
                var pushHeap = neighborCell.CellInBackwardHeap ? openB : openF;
                pushHeap.Enqueue(neighborCell, tentativeG * effectiveGMultiplier + h);
            }
        }

        /// <summary>
        /// Remove waypoints (cells) that have LOS from one to the other from path.
        /// </summary>
        /// <param name="path"></param>
        private void SmoothPath(List<NavigationGridCell> path, float checkDistance = 0f)
        {
            if(path.Count < 3)
            {
                return;
            }
            int j = 0;
            // The first point remains untouched.
            for(int i = 2; i < path.Count; i++)
            {
                // If there is something between the last added point and the current one
                if(CastCircle(path[j].GetCenter(), path[i].GetCenter(), checkDistance, false))
                {
                    // add previous.
                    path[++j] = path[i - 1];
                }
            }
            // Add last.
            path[++j] = path[path.Count - 1];
            j++; // Remove everything after.
            path.RemoveRange(j, path.Count - j);
        }

        /// <summary>
        /// Translates the given Vector2 into cell format where each unit is a cell.
        /// This is to simplify the calculations required to get cells.
        /// </summary>
        /// <param name="vector">Vector2 to translate.</param>
        /// <returns>Cell formatted Vector2.</returns>
        public Vector2 TranslateToNavGrid(Vector2 vector)
        {
            return new Vector2
            (
                (vector.X - MinGridPosition.X) / CellSize,
                (vector.Y - MinGridPosition.Z) / CellSize
            );
        }

        /// <summary>
        /// Translates the given cell locator position back into normal coordinate space as a Vector2.
        /// *NOTE*: Returns the coordinates of the center of the cell.
        /// </summary>
        /// <param name="locator">Cell locator.</param>
        /// <returns>Normal coordinate space Vector2.</returns>
        public Vector2 TranslateFromNavGrid(NavigationGridLocator locator)
        {
            return TranslateFromNavGrid(new Vector2(locator.X, locator.Y)) + Vector2.One * 0.5f * CellSize;
        }

        /// <summary>
        /// Translates the given cell formatted Vector2 back into normal coordinate space.
        /// </summary>
        /// <param name="vector">Vector2 to translate.</param>
        /// <returns>Normal coordinate space Vector2.</returns>
        public Vector2 TranslateFromNavGrid(Vector2 vector)
        {
            return new Vector2
            (
                vector.X * CellSize + MinGridPosition.X,
                vector.Y * CellSize + MinGridPosition.Z
            );
        }

        public NavigationGridCell GetCell(Vector2 coords, bool translate = true)
        {
            if(translate)
            {
                coords = TranslateToNavGrid(coords);
            }
            return GetCell((short)coords.X, (short)coords.Y);
        }

        /// <summary>
        /// Gets the cell at the given cell based coordinates.
        /// </summary>
        /// <param name="x">cell based X coordinate</param>
        /// <param name="y">cell based Y coordinate.</param>
        /// <returns>Cell instance.</returns>
        public NavigationGridCell GetCell(short x, short y)
        {
            long index = y * CellCountX + x;
            if (x < 0 || x > CellCountX || y < 0 || y > CellCountY || index >= Cells.Length)
            {
                return null;
            }
            return Cells[index];
        }

        /// <summary>
        /// Gets a list of all cells within 8 cardinal directions of the given cell.
        /// </summary>
        /// <param name="cell">Cell to start the check at.</param>
        /// <returns>List of neighboring cells.</returns>
        private List<NavigationGridCell> GetCellNeighbors(NavigationGridCell cell)
        {
            List<NavigationGridCell> neighbors = new List<NavigationGridCell>(9);
            for (short dirY = -1; dirY <= 1; dirY++)
            {
                for (short dirX = -1; dirX <= 1; dirX++)
                {
                    short nx = (short)(cell.Locator.X + dirX);
                    short ny = (short)(cell.Locator.Y + dirY);
                    NavigationGridCell neighborCell = GetCell(nx, ny);
                    if (neighborCell != null)
                    {
                        neighbors.Add(neighborCell);
                    }
                }
            }
            return neighbors;
        }

        /// <summary>
        /// Gets the index of a cell that is closest to the given 2D point.
        /// Usually used when the given point is outside the boundaries of the Navigation Grid.
        /// </summary>
        /// <param name="x">X coordinate to check.</param>
        /// <param name="y">Y coordinate to check.</param>
        /// <param name="translate">Whether or not the given coordinates are in LS form.</param>
        /// <returns>Index of a valid cell.</returns>
        public NavigationGridCell GetClosestValidCell(Vector2 coords, bool translate = true)
        {
            Vector2 minGridPos = Vector2.Zero;
            Vector2 maxGridPos = TranslateToNavGrid(
                new Vector2(MaxGridPosition.X, MaxGridPosition.Z)
            );

            if (translate)
            {
                coords = TranslateToNavGrid(coords);
            }

            return GetCell(
                new Vector2(
                    Math.Clamp(coords.X, minGridPos.X, maxGridPos.X),
                    Math.Clamp(coords.Y, minGridPos.Y, maxGridPos.Y)
                ),
                false
            );
        }

        /// <summary>
        /// Gets a list of cells within the specified range of a specified point.
        /// </summary>
        /// <param name="origin">Vector2 with normal coordinates to start the check.</param>
        /// <param name="radius">Range to check around the origin.</param>
        /// <returns>List of all cells in range. Null if range extends outside of NavigationGrid boundaries.</returns>
        private IEnumerable<NavigationGridCell> GetAllCellsInRange(Vector2 origin, float radius, bool translate = true)
        {
            radius /= CellSize;
            if(translate)
            {
                origin = TranslateToNavGrid(origin);
            }

            short fx = (short)(origin.X - radius);
            short lx = (short)(origin.X + radius);
            short fy = (short)(origin.Y - radius);
            short ly = (short)(origin.Y + radius);

            for(short x = fx; x <= lx; x++)
            {
                for(short y = fy; y <= ly; y++)
                {
                    float distSquared = Extensions.DistanceSquaredToRectangle(
                        new Vector2(x + 0.5f, y + 0.5f), 1f, 1f, origin
                    );
                    if(distSquared <= radius*radius)
                    {
                        var cell = GetCell(x, y);
                        if(cell != null)
                        {
                            yield return cell;
                        }
                    }
                }
            }
        }

        bool IsWalkable(NavigationGridCell cell)
        {
            return cell != null
                && !cell.HasFlag(NavigationGridCellFlags.NOT_PASSABLE)
                && !cell.HasFlag(NavigationGridCellFlags.SEE_THROUGH)
                && !_dynamicBlockers.ContainsKey(cell.ID);
        }

        bool IsWalkable(NavigationGridCell cell, float checkRadius)
        {
            Vector2 cellCenter = cell.GetCenter();
            return IsWalkable(cellCenter, checkRadius, false);
        }

        /// <summary>
        /// Whether or not the cell at the given position can be pathed on.
        /// </summary>
        /// <param name="coords">Vector2 position to check.</param>
        /// <param name="checkRadius">Radius around the given point to check for walkability.</param>
        /// <param name="translate">Whether or not to translate the given position to cell-based format.</param>
        /// <returns>True/False.</returns>
        public bool IsWalkable(Vector2 coords, float checkRadius = 0, bool translate = true)
        {
            if (checkRadius == 0)
            {
                NavigationGridCell cell = GetCell(coords, translate);
                return IsWalkable(cell);
            }

            var cells = GetAllCellsInRange(coords, checkRadius, translate);            
            foreach (NavigationGridCell c in cells)
            {
                if (!IsWalkable(c))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Whether or not the given position is see-through. In other words, if it does not block vision.
        /// </summary>
        /// <param name="coords">Vector2 position to check.</param>
        /// <param name="translate">Whether or not to translate the given position to cell-based format.</param>
        /// <returns>True/False.</returns>
        public bool IsVisible(Vector2 coords, bool translate = true)
        {
            // Single-cell visibility check (NOT_PASSABLE-without-SEE_THROUGH detection only).
            // Bush vision rules are observer-relative and live in CastRay/IsAnythingBetween,
            // not on a per-cell flag. Same brush group is visible regardless of HAS_GRASS,
            // different/no brush group is occluded. There is therefore no per-cell bush
            // logic to add here.
            NavigationGridCell cell = GetCell(coords, translate);
            return IsVisible(cell);
        }

        /// <summary>
        /// Whether the given position lies on a "wall of grass" — port of the client's
        /// NavGrid::IsWallOfGrass (S4 NavGrid.cpp:8881). Two-mode query:
        ///   * radius &lt; 35: single-cell HAS_GRASS check.
        ///   * radius &gt;= 35: scan cells within (clamped) radius around pos and return true if
        ///     at least 40% of non-wall cells are flagged HAS_GRASS. Walls (NOT_PASSABLE without
        ///     HAS_GRASS) are excluded from the denominator.
        /// Used by the client for spell-missile + vision logic at bush edges.
        /// </summary>
        public bool IsWallOfGrass(Vector2 pos, float radius)
        {
            if (radius < 35f)
            {
                NavigationGridCell single = GetCell(pos);
                return single != null && single.HasFlag(NavigationGridCellFlags.HAS_GRASS);
            }

            float r = Math.Min(radius, 500f);
            float r2 = r * r;

            var navMin = TranslateToNavGrid(new Vector2(pos.X - r, pos.Y - r));
            var navMax = TranslateToNavGrid(new Vector2(pos.X + r, pos.Y + r));
            short cx0 = (short)Math.Floor(navMin.X);
            short cx1 = (short)Math.Floor(navMax.X);
            short cy0 = (short)Math.Floor(navMin.Y);
            short cy1 = (short)Math.Floor(navMax.Y);

            int grassCount = 0;
            int totalCount = 0;
            for (short cy = cy0; cy <= cy1; cy++)
            {
                for (short cx = cx0; cx <= cx1; cx++)
                {
                    var c = GetCell(cx, cy);
                    if (c == null) continue;
                    var center = TranslateFromNavGrid(c.Locator);
                    if (Vector2.DistanceSquared(center, pos) > r2) continue;

                    if (c.HasFlag(NavigationGridCellFlags.HAS_GRASS))
                    {
                        grassCount++;
                        totalCount++;
                    }
                    else if (!c.HasFlag(NavigationGridCellFlags.NOT_PASSABLE))
                    {
                        totalCount++;
                    }
                }
            }

            if (grassCount == 0) return false;
            return totalCount * 0.4f <= grassCount;
        }

        bool IsVisible(NavigationGridCell cell)
        {
            return cell != null
                && (!cell.HasFlag(NavigationGridCellFlags.NOT_PASSABLE)
                || cell.HasFlag(NavigationGridCellFlags.SEE_THROUGH)
                || cell.HasFlag(NavigationGridCellFlags.HAS_GLOBAL_VISION));
        }

        /// <summary>
        /// Whether or not the given position has the specified flags.
        /// </summary>
        /// <param name="coords">Vector2 position to check.</param>
        /// <param name="translate">Whether or not to translate the given position to cell-based format.</param>
        /// <returns>True/False.</returns>
        public bool HasFlag(Vector2 coords, NavigationGridCellFlags flag, bool translate = true)
        {
            NavigationGridCell cell = GetCell(coords, translate);
            return cell != null && cell.HasFlag(flag);
        }

        /// <summary>
        /// Gets the height of the ground at the given position. Used purely for packets.
        /// </summary>
        /// <param name="location">Vector2 position to check.</param>
        /// <returns>Height (3D Y coordinate) at the given position.</returns>
        public float GetHeightAtLocation(Vector2 location)
        {
            // Uses SampledHeights to get the height of a given location on the Navigation Grid
            // This is the method the game uses to get height data

            if (location.X >= MinGridPosition.X && location.Y >= MinGridPosition.Z &&
                location.X <= MaxGridPosition.X && location.Y <= MaxGridPosition.Z)
            {
                float reguestedHeightX = (location.X - MinGridPosition.X) / SampledHeightsDistance.X;
                float requestedHeightY = (location.Y - MinGridPosition.Z) / SampledHeightsDistance.Y;

                int sampledHeight1IndexX = (int)reguestedHeightX;
                int sampledHeight1IndexY = (int)requestedHeightY;
                int sampledHeight2IndexX;
                int sampledHeight2IndexY;

                float v13;
                float v15;

                if (reguestedHeightX >= SampledHeightsCountX - 1)
                {
                    v13 = 1.0f;
                    sampledHeight2IndexX = sampledHeight1IndexX--;
                }
                else
                {
                    v13 = 0.0f;
                    sampledHeight2IndexX = sampledHeight1IndexX + 1;
                }
                if (requestedHeightY >= SampledHeightsCountY - 1)
                {
                    v15 = 1.0f;
                    sampledHeight2IndexY = sampledHeight1IndexY--;
                }
                else
                {
                    v15 = 0.0f;
                    sampledHeight2IndexY = sampledHeight1IndexY + 1;
                }

                uint sampledHeightsCount = SampledHeightsCountX * SampledHeightsCountY;
                int v1 = (int)SampledHeightsCountX * sampledHeight1IndexY;
                int x0y0 = v1 + sampledHeight1IndexX;

                if (v1 + sampledHeight1IndexX < sampledHeightsCount)
                {
                    int v19 = sampledHeight2IndexX + v1;
                    if (v19 < sampledHeightsCount)
                    {
                        int v20 = sampledHeight2IndexY * (int)SampledHeightsCountX;
                        int v21 = v20 + sampledHeight1IndexX;

                        if (v21 < sampledHeightsCount)
                        {
                            int v22 = sampledHeight2IndexX + v20;
                            if (v22 < sampledHeightsCount)
                            {
                                float height = ((1.0f - v13) * SampledHeights[x0y0])
                                          + (v13 * SampledHeights[v19])
                                          + (((SampledHeights[v21] * (1.0f - v13))
                                          + (SampledHeights[v22] * v13)) * v15);

                                return (1.0f - v15) * height;
                            }
                        }
                    }
                }

            }

            return 0.0f;
        }

        /// <summary>
        /// Casts a ray and returns false when failed, with a stopping position, or true on success with the given destination.
        /// </summary>
        /// <param name="origin">Vector position to start the ray cast from.</param>
        /// <param name="destination">Vector2 position to end the ray cast at.</param>
        /// <param name="checkWalkable">Whether or not the ray stops when hitting a position which blocks pathing.</param>
        /// <param name="checkVisible">Whether or not the ray stops when hitting a position which blocks vision.</param>
        /// <returns>True = Reached destination. True = Failed.</returns>
        public bool CastRay(Vector2 origin, Vector2 destination, bool checkWalkable = false, bool checkVisible = false, bool translate = true)
        {
            // Out of bounds
            if (origin.X < MinGridPosition.X || origin.X >= MaxGridPosition.X || origin.Y < MinGridPosition.Z || origin.Y >= MaxGridPosition.Z)
            {
                return true;
            }

            if(translate)
            {
                origin = TranslateToNavGrid(origin);
                destination = TranslateToNavGrid(destination);
            }

            var cells = GetAllCellsInLine(origin, destination).GetEnumerator();

            // Group-aware bush vision: which brush (if any) is the observer in?
            // Origin group 0 = outside any brush. The ray then blocks at any cell whose group
            // differs from origin's: outside→inBrush blocked, brushA->brushB (B != A) blocked.
            // Mirrors the client's per-cell `groupOf(originCell) != groupOf(rayCell)`-style
            // gate (vision into a different brush is occluded). Same-group rays propagate
            // freely; rays leaving a brush into open ground (group 0) propagate freely.
            byte originGroup = GetGrassGroupAtNavCell(origin);

            bool hasNext;
            while (hasNext = cells.MoveNext())
            {
                var cell = cells.Current;

                //TODO: Implement methods for maps whose NavGrids don't use SEE_THROUGH flags for buildings
                if (checkWalkable)
                {
                    if(!IsWalkable(cell))
                    {
                        break;
                    }
                }

                if (checkVisible)
                {
                    if (!IsVisible(cell))
                    {
                        break;
                    }

                    byte cellGroup = (cell != null && CellGrassGroups != null
                        && cell.ID >= 0 && cell.ID < CellGrassGroups.Length)
                        ? CellGrassGroups[cell.ID]
                        : (byte)0;

                    // Block when the ray enters a brush whose group differs from the observer's:
                    // - origin outside (group 0), cell in brush X → blocked (looking into a brush)
                    // - origin in brush A, cell in brush B (B != A) → blocked (cross-brush vision)
                    // Same group OR cell in open ground (group 0) propagates.
                    if (cellGroup != 0 && cellGroup != originGroup)
                    {
                        break;
                    }
                }
            }

            return hasNext;
        }

        // Lookup the brush group of a cell at navgrid-space coordinates. 0 = outside any brush.
        private byte GetGrassGroupAtNavCell(Vector2 navPos)
        {
            if (CellGrassGroups == null) return 0;
            var cell = GetCell(navPos, false);
            if (cell == null || cell.ID < 0 || cell.ID >= CellGrassGroups.Length) return 0;
            return CellGrassGroups[cell.ID];
        }

        // https://playtechs.blogspot.com/2007/03/raytracing-on-grid.html
        private IEnumerable<NavigationGridCell> GetAllCellsInLine(Vector2 v0, Vector2 v1)
        {
            double dx = Math.Abs(v1.X - v0.X);
            double dy = Math.Abs(v1.Y - v0.Y);

            short x = (short)(Math.Floor(v0.X));
            short y = (short)(Math.Floor(v0.Y));

            int n = 1;
            short x_inc, y_inc;
            double error;

            if (dx == 0)
            {
                x_inc = 0;
                error = float.PositiveInfinity;
            }
            else if (v1.X > v0.X)
            {
                x_inc = 1;
                n += (int)(Math.Floor(v1.X)) - x;
                error = (Math.Floor(v0.X) + 1 - v0.X) * dy;
            }
            else
            {
                x_inc = -1;
                n += x - (int)(Math.Floor(v1.X));
                error = (v0.X - Math.Floor(v0.X)) * dy;
            }

            if (dy == 0)
            {
                y_inc = 0;
                error = float.NegativeInfinity;
            }
            else if (v1.Y > v0.Y)
            {
                y_inc = 1;
                n += (int)(Math.Floor(v1.Y)) - y;
                error -= (Math.Floor(v0.Y) + 1 - v0.Y) * dx;
            }
            else
            {
                y_inc = -1;
                n += y - (int)(Math.Floor(v1.Y));
                error -= (v0.Y - Math.Floor(v0.Y)) * dx;
            }

            for (; n > 0; --n)
            {
                yield return GetCell(x, y);

                if (error > 0)
                {
                    y += y_inc;
                    error -= dx;
                }
                else if(error < 0)
                {
                    x += x_inc;
                    error += dy;
                }
                else //if (error == 0)
                {
                    yield return GetCell((short)(x + x_inc), y);
                    yield return GetCell(x, (short)(y + y_inc));

                    x += x_inc;
                    y += y_inc;
                    error += dy - dx;
                    n--;
                }
            }
        }

        public bool CastCircle(Vector2 orig, Vector2 dest, float radius, bool translate = true)
        {
            if(translate)
            {
                orig = TranslateToNavGrid(orig);
                dest = TranslateToNavGrid(dest);
            }
            
            float tradius = radius / CellSize;
            Vector2 p = (dest - orig).Normalized().Perpendicular() * tradius;

            var cells = GetAllCellsInRange(orig, radius, false)
            .Concat(GetAllCellsInRange(dest, radius, false))
            .Concat(GetAllCellsInLine(orig + p, dest + p))
            .Concat(GetAllCellsInLine(orig - p, dest - p));

            int minY = (int)(Math.Min(orig.Y, dest.Y) - tradius) - 1;
            int maxY = (int)(Math.Max(orig.Y, dest.Y) + tradius) + 1;

            int countY = maxY - minY + 1;
            var xRanges = new short[countY, 3];
            foreach(var cell in cells)
            {
                if(!IsWalkable(cell))
                {
                    return true;
                }
                int y = cell.Locator.Y - minY;
                if(xRanges[y, 2] == 0)
                {
                    xRanges[y, 0] = cell.Locator.X;
                    xRanges[y, 1] = cell.Locator.X;
                    xRanges[y, 2] = 1;
                }
                else
                {
                    xRanges[y, 0] = Math.Min(xRanges[y, 0], cell.Locator.X);
                    xRanges[y, 1] = Math.Max(xRanges[y, 1], cell.Locator.X);
                }
            }

            for(int y = 0; y < countY; y++)
            {
                for(int x = xRanges[y, 0] + 1; x < xRanges[y, 1]; x++)
                {
                    if(!IsWalkable(GetCell((short)x, (short)(minY + y))))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        /// <summary>
        /// Casts a ray in the given direction and returns false when failed, with a stopping position, or true on success with the given destination.
        /// *NOTE*: Is not actually infinite, just travels (direction * 1024) units ahead of the given origin.
        /// </summary>
        /// <param name="origin">Vector position to start the ray cast from.</param>
        /// <param name="direction">Ray cast direction.</param>
        /// <param name="checkWalkable">Whether or not the ray stops when hitting a position which blocks pathing.</param>
        /// <param name="checkVisible">Whether or not the ray stops when hitting a position which blocks vision. *NOTE*: Does not apply if checkWalkable is also true.</param>
        /// <returns>False = Reached destination. True = Failed.</returns>
        public bool CastInfiniteRay(Vector2 origin, Vector2 direction, bool checkWalkable = true, bool checkVisible = false)
        {
            return CastRay(origin, origin + direction * 1024, checkWalkable, checkVisible);
        }

        /// <summary>
        /// Whether or not there is anything blocking the two given GameObjects from either seeing eachother or pathing straight towards eachother (depending on checkVision).
        /// </summary>
        /// <param name="a">GameObject to start the check from.</param>
        /// <param name="b">GameObject to end the check at.</param>
        /// <param name="checkVision">True = Check for positions that block vision. False = Check for positions that block pathing.</param>
        /// <returns>True/False.</returns>
        public bool IsAnythingBetween(GameObject a, GameObject b, bool checkVision = false)
        {
            var d = Vector2.Normalize(b.Position - a.Position);

            Vector2 origin = a.Position + d * a.PathfindingRadius;
            Vector2 destination = b.Position - d * b.PathfindingRadius;

            if (!CastRay(origin, destination, !checkVision, checkVision))
            {
                return false;
            }

            // Brush-edge fallback when the observer is in a brush, the radius-offset on origin
            // (line 1830) can nudge the ray-start out of the brush by a few units. Re-test LOS
            // from nearby brush cells of the SAME group so the observer doesn't lose vision just
            // because their pushed-forward origin landed in open ground.
            //
            // Group filter: only consider cells of the observer's own brush group. Without it,
            // an observer in brush A would "borrow" vision from a touching brush B and erroneously
            // see into brush B from outside it.
            if (!checkVision)
            {
                return true;
            }

            byte aGroup = (CellGrassGroups != null)
                ? GetGrassGroupAtNavCell(TranslateToNavGrid(a.Position))
                : (byte)0;
            if (aGroup == 0)
            {
                // Observer not in any brush -> no edge-snap problem to fix.
                return true;
            }

            foreach (var cell in GetAllCellsInRange(a.Position, Math.Max(25.0f, a.PathfindingRadius)))
            {
                if (cell == null || cell.ID < 0 || cell.ID >= CellGrassGroups.Length) continue;
                if (CellGrassGroups[cell.ID] != aGroup) continue;
                if (!CastRay(TranslateFromNavGrid(cell.GetCenter()), destination, false, true))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Walks the ray from origin to destination cell-by-cell and returns the world-space center
        /// of the last walkable cell before the ray hits unwalkable terrain. If the whole ray is
        /// clear, returns destination unchanged.
        /// </summary>
        public Vector2 GetFirstWallHitPoint(Vector2 origin, Vector2 destination)
        {
            // Out of bounds → nothing to clamp against.
            if (origin.X < MinGridPosition.X || origin.X >= MaxGridPosition.X
                || origin.Y < MinGridPosition.Z || origin.Y >= MaxGridPosition.Z)
            {
                return destination;
            }

            var navOrigin = TranslateToNavGrid(origin);
            var navDest = TranslateToNavGrid(destination);

            NavigationGridCell lastWalkable = null;
            foreach (var cell in GetAllCellsInLine(navOrigin, navDest))
            {
                if (!IsWalkable(cell))
                {
                    if (lastWalkable == null)
                    {
                        // Origin already inside terrain — nothing useful to clamp to.
                        return origin;
                    }
                    return TranslateFromNavGrid(lastWalkable.GetCenter());
                }
                lastWalkable = cell;
            }

            return destination;
        }

        /// <summary>
        /// Gets the closest pathable position to the given position. *NOTE*: Computationally heavy, use sparingly.
        /// </summary>
        /// <param name="location">Vector2 position to start the check at.</param>
        /// <param name="distanceThreshold">Amount of distance away from terrain the exit should be.</param>
        /// <returns>Vector2 position which can be pathed on.</returns>
        public Vector2 GetClosestTerrainExit(Vector2 location, float distanceThreshold = 0)
        {
            // Trivial: already walkable.
            if (IsWalkable(location, distanceThreshold))
            {
                return location;
            }

            // Concentric ring scan in cell space around the input. Returns the world-space center
            // of the nearest walkable cell. The previous spiral implementation accumulated offsets
            // with a step that scaled with iteration count, drifting hundreds of units away from
            // the input on the first few unwalkable iterations. That instability caused the
            // CollisionHandler-driven teleport cycle when a unit stood on a building blocker:
            // the unit got snapped far, RefreshWaypoints re-pathed back, repeat.
            var navInput = TranslateToNavGrid(location);
            short cx = (short)navInput.X;
            short cy = (short)navInput.Y;

            int maxRing = (int)Math.Max(CellCountX, CellCountY);
            for (int r = 1; r < maxRing; r++)
            {
                NavigationGridCell best = null;
                float bestDistSq = float.MaxValue;

                // Walk the ring perimeter at Chebyshev distance r.
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        var cell = GetCell((short)(cx + dx), (short)(cy + dy));
                        if (cell == null) continue;
                        if (!IsWalkable(cell, distanceThreshold)) continue;

                        var center = cell.GetCenter();
                        float dSq = Vector2.DistanceSquared(center, navInput);
                        if (dSq < bestDistSq)
                        {
                            bestDistSq = dSq;
                            best = cell;
                        }
                    }
                }

                if (best != null)
                {
                    return TranslateFromNavGrid(best.Locator);
                }
            }

            return location;
        }

        public NavigationGridCell GetClosestWalkableCell(Vector2 coords, float distanceThreshold = 0, bool translate = true)
        {
            if(translate)
            {
                coords = TranslateToNavGrid(coords);
            }
            float closestDist = 0;
            NavigationGridCell closestCell = null;
            foreach(var cell in Cells)
            {
                if(IsWalkable(cell, distanceThreshold))
                {
                    float dist = Vector2.DistanceSquared(cell.GetCenter(), coords);
                    if(closestCell == null || dist < closestDist)
                    {
                        closestCell = cell;
                        closestDist = dist;
                    }
                }
            }
            return closestCell;
        }

        // --- Local-window reachability (CheckIsGetToAble family, ported from S1:7404-9909) ---
        //
        // Three small helpers that answer "can I get from A to B without colliding, within a
        // 4-cell chebyshev window?" via a bounded BFS. Cheaper than full GetPath when the answer
        // is needed only for nearby targets (auto-attack viability checks, melee dash validation,
        // target snapping). For long-distance reachability use GetPath and check for null/partial
        // path instead.
        //
        // Architecture (mirrors client S1:9069):
        //   * Quick bounds + ghosted shortcut.
        //   * HasBlockedActor on target cell -> if the target itself is occupied by a blocker, no
        //     local window can save us; bail.
        //   * Two ortho ring-walks (+X then +Y) up to MAX_RINGS_CHECK steps, calling IsNotBlocked
        //     each step. Either ring fully clear → reachable.
        //   * If both rings hit obstacles, BFS within the same window via FloodFillGetToAble until
        //     either the BFS escapes the window (reachable) or the queue exhausts (not reachable).

        private const int MAX_RINGS_CHECK = 4;

        /// <summary>
        /// Per-cell pass/fail check used by both the ring walks and the BFS expander. Returns true
        /// if the cell at <paramref name="locator"/> is reachable in this search context which
        /// is either: distance-from-start is within the squared ignore radius (free pass), or the
        /// cell is statically walkable AND the actor predicate doesn't block it. Mirrors S1:7407
        /// IsNotBlocked exactly: the start-vs-target distance comparison is constant across the
        /// BFS but allows the caller to set <paramref name="ignoreTargetRadiusSq"/> to skip checks
        /// for targets within trivial range.
        /// </summary>
        private bool IsNotBlocked(
            short locX, short locY,
            float worldStartX, float worldStartZ,
            float worldTargetX, float worldTargetZ,
            float ignoreTargetRadiusSq,
            ActorBlockedPredicate actorBlocked,
            float radius)
        {
            // Squared distance start->target in world units. Constant across the BFS —> the
            // ignoreTargetRadiusSq early-out lets close-range queries succeed without per-cell
            // checks. Only when start and target are far enough apart do the static + actor
            // checks actually fire (S1:7456).
            float dx = worldTargetX - worldStartX;
            float dz = worldTargetZ - worldStartZ;
            if (dx * dx + dz * dz <= ignoreTargetRadiusSq)
            {
                return true;
            }

            var cell = GetCell(locX, locY);
            if (cell == null || cell.HasFlag(NavigationGridCellFlags.NOT_PASSABLE))
            {
                return false;
            }
            if (actorBlocked != null)
            {
                Vector2 cellWorld = TranslateFromNavGrid(cell.Locator);
                if (actorBlocked(cellWorld, cell))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Per-step BFS expander used by <see cref="CheckIsGetToAble"/>. Mirrors S1:7817.
        /// On each call, processes the cell at offset (<paramref name="dx"/>, <paramref name="dy"/>)
        /// from the search start. Returns:
        ///   <c>true</c> search should terminate as REACHABLE (either out of window bounds, or
        ///   cell is unblocked and we've expanded its neighbors).
        ///   <c>false</c> — keep BFSing; cell was either out-of-grid, already visited, or blocked
        ///   in a way that doesn't let neighbors be expanded.
        ///
        /// The asymmetric return semantics match client (S1:7837 returns 1 on out-of-bounds, which
        /// CheckIsGetToAble's outer loop reads as "we got far enough, count it as reachable").
        /// </summary>
        private bool FloodFillGetToAble(
            Queue<(int dx, int dy)> queue,
            bool[,] visited,
            int dx, int dy, int safeCell,
            short startX, short startY,
            float worldStartX, float worldStartZ,
            float worldTargetX, float worldTargetZ,
            float ignoreTargetRadiusSq,
            ActorBlockedPredicate actorBlocked,
            float radius)
        {
            // Out-of-window —> count as reachable (BFS escaped without finding a wall enclosure).
            if (Math.Abs(dx) > safeCell || Math.Abs(dy) > safeCell)
            {
                return true;
            }

            short cellX = (short)(startX + dx);
            short cellY = (short)(startY + dy);

            // Out-of-grid → no expansion, but keep BFSing other branches.
            if (cellX < 0 || cellY < 0 || cellX >= CellCountX || cellY >= CellCountY)
            {
                return false;
            }

            int vx = dx + MAX_RINGS_CHECK;
            int vy = dy + MAX_RINGS_CHECK;
            if (visited[vx, vy])
            {
                return false;
            }
            visited[vx, vy] = true;

            // Skip the start cell itself if its blocked-ness shouldn't decide reachability of the
            // target. Mirrors S1:7849.
            if (dx == 0 && dy == 0)
            {
                return false;
            }

            if (!IsNotBlocked(cellX, cellY,
                              worldStartX, worldStartZ,
                              worldTargetX, worldTargetZ,
                              ignoreTargetRadiusSq, actorBlocked, radius))
            {
                // Cell is blocked but we still mark it visited so other BFS branches don't retest.
                return true; // signal "stop here" — matches client's S1:7892 return 1 on
                             // IsNotBlocked-fail. Outer loop reads this as "found a clean stopping
                             // point, target counts as reachable from this side". The BFS exits
                             // success on first clean stopping cell.
            }

            // Cell is reachable; push 4 ortho neighbors. Bounds prefilter (skip x>safeCell etc.)
            // is delegated to recursive FloodFill calls.
            queue.Enqueue((dx - 1, dy));
            queue.Enqueue((dx + 1, dy));
            queue.Enqueue((dx, dy - 1));
            queue.Enqueue((dx, dy + 1));
            return false;
        }

        /// <summary>
        /// Local-window reachability check (S1:9069 / S4:1694). Returns true when <paramref name="to"/>
        /// is reachable from <paramref name="from"/> within a chebyshev-distance window of
        /// <see cref="MAX_RINGS_CHECK"/> cells, applying static-terrain checks plus the optional
        /// actor-blocking predicate. Use as a cheap pre-check before <see cref="GetPath"/> when
        /// the target is known-close (auto-attack viability, melee dash, etc). For long-range
        /// reachability use <see cref="GetPath"/> and check for null/partial path.
        ///
        /// A null <paramref name="actorBlocked"/> means "skip actor checks" (= terrain-only). The
        /// PathingHandler wrapper translates ghosted attackers to a null predicate, which is the
        /// equivalent of S1:9131's <c>mIgnoreCollisions -> return 1</c> short-circuit for actors
        /// only — terrain still applies.
        /// </summary>
        public bool CheckIsGetToAble(Vector2 from, Vector2 to,
            ActorBlockedPredicate actorBlocked = null,
            float radius = 0,
            float ignoreTargetRadius = 0)
        {
            var fromNav = TranslateToNavGrid(from);
            var toNav = TranslateToNavGrid(to);
            short fromX = (short)fromNav.X, fromY = (short)fromNav.Y;
            short toX = (short)toNav.X, toY = (short)toNav.Y;

            var fromCell = GetCell(fromX, fromY);
            if (fromCell == null) return false;

            // Start cell on static-non-passable -> no local recovery, bail. S1:9129.
            if (fromCell.HasFlag(NavigationGridCellFlags.NOT_PASSABLE)) return false;

            // Target cell static-blocked or actor-blocked -> no point BFSing. S1:9146 HasBlockedActor.
            var toCell = GetCell(toX, toY);
            if (toCell == null || toCell.HasFlag(NavigationGridCellFlags.NOT_PASSABLE)) return false;
            if (actorBlocked != null)
            {
                Vector2 toWorld = TranslateFromNavGrid(toCell.Locator);
                if (actorBlocked(toWorld, toCell)) return false;
            }

            // Chebyshev distance, capped at MAX_RINGS_CHECK. If start IS target, trivially reachable.
            int chebyshev = Math.Max(Math.Abs(fromX - toX), Math.Abs(fromY - toY));
            int safeCell = Math.Min(chebyshev, MAX_RINGS_CHECK);
            if (safeCell == 0) return true;

            // Ring-walk +X and +Y. Each step calls IsNotBlocked —> if the entire walk is clear, we
            // count the target as reachable. Mirrors S1:9176-9194.
            float ignoreSq = ignoreTargetRadius * ignoreTargetRadius;
            Vector2 startWorld = TranslateFromNavGrid(fromCell.Locator);
            Vector2 targetWorld = TranslateFromNavGrid(toCell.Locator);

            int xStep = 0;
            for (; xStep < safeCell; xStep++)
            {
                short cx = (short)(fromX + xStep + 1);
                if (cx >= CellCountX) break;
                if (!IsNotBlocked(cx, fromY, startWorld.X, startWorld.Y,
                                  targetWorld.X, targetWorld.Y, ignoreSq,
                                  actorBlocked, radius)) break;
            }
            if (xStep == safeCell) return true;

            int yStep = 0;
            for (; yStep < safeCell; yStep++)
            {
                short cy = (short)(fromY + yStep + 1);
                if (cy >= CellCountY) break;
                if (!IsNotBlocked(fromX, cy, startWorld.X, startWorld.Y,
                                  targetWorld.X, targetWorld.Y, ignoreSq,
                                  actorBlocked, radius)) break;
            }
            if (yStep == safeCell) return true;

            // Both rings blocked —> fall through to BFS within the same window. Visited array sized
            // 9x9 (= 2*MAX_RINGS_CHECK+1 each axis) and indexed via (dx+4, dy+4). Cleaner than the
            // client's 1D Array[4*y+20+x] which has aliasing collisions at boundary offsets.
            int axisSize = 2 * MAX_RINGS_CHECK + 1;
            var visited = new bool[axisSize, axisSize];
            visited[MAX_RINGS_CHECK, MAX_RINGS_CHECK] = true; // start cell

            var queue = new Queue<(int dx, int dy)>();
            // Seed with the cell where the +Y ring stopped (matches client semantics -> pick up
            // BFS where the linear walk failed, S1:9197). We pass yStep+1 so the offset points to
            // the first blocking cell, which FloodFillGetToAble will then evaluate and expand
            // around.
            queue.Enqueue((0, yStep));

            while (queue.Count > 0)
            {
                var (dx, dy) = queue.Dequeue();
                if (FloodFillGetToAble(queue, visited, dx, dy, safeCell,
                                       fromX, fromY,
                                       startWorld.X, startWorld.Y,
                                       targetWorld.X, targetWorld.Y,
                                       ignoreSq, actorBlocked, radius))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Spiral search for the nearest cell that is locally reachable from <paramref name="from"/>,
        /// starting at <paramref name="target"/> and walking outward. Mirrors S1:9686
        /// SetToNearestGetToAbleCell. Caller's typical use is "snap a navigation target to a
        /// nearby reachable cell" when the requested target isn't directly reachable (occluded by
        /// terrain or actors). Returns the original <paramref name="target"/> if the target itself
        /// is reachable, the closest spiral hit otherwise, or <c>target</c> unchanged when the
        /// 4000-iteration cap runs out (matches client's hard cap S1:9725).
        /// </summary>
        public Vector2 SetToNearestGetToAbleCell(Vector2 target, Vector2 from,
            ActorBlockedPredicate actorBlocked = null,
            float radius = 0,
            float ignoreTargetRadius = 0,
            float targetRadius = 0)
        {
            // Trivial: target already reachable.
            if (CheckIsGetToAble(from, target, actorBlocked, radius, ignoreTargetRadius))
            {
                return target;
            }

            // Spiral around target. The 8-direction sArrivalOffsets pattern from the client is
            // (-1,0),(-1,-1),(0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1). The client steps once
            // per offset and grows the leg length every two complete cycles (every other "side").
            // We mirror that loop structure with iterCap = 4000 to keep parity.
            ReadOnlySpan<(int dx, int dy)> offsets = stackalloc (int, int)[8]
            {
                (-1, 0), (-1, -1), (0, -1), (1, -1),
                (1, 0), (1, 1), (0, 1), (-1, 1)
            };

            var cur = TranslateToNavGrid(target);
            short cx = (short)cur.X, cy = (short)cur.Y;
            int legLength = 1;
            int legProgress = 0;
            int dirIdx = 2;             // start with (0,-1) like client S1:9736 v26 = 2
            bool grewLegOnPriorTurn = true;
            int iterCap = 4000;

            float bestDistSq = float.MaxValue;
            float bestRingPenalty = float.MaxValue;
            short bestX = 0, bestY = 0;
            bool foundAny = false;

            while (--iterCap > 0)
            {
                if (cx > 1 && cy > 1 && cx < CellCountX - 2 && cy < CellCountY - 2)
                {
                    Vector2 candidate = TranslateFromNavGrid(new NavigationGridLocator(cx, cy));
                    if (CheckIsGetToAble(from, candidate, actorBlocked, radius, ignoreTargetRadius))
                    {
                        var fromNav = TranslateToNavGrid(from);
                        float ddx = cx - fromNav.X;
                        float ddy = cy - fromNav.Y;
                        float distSq = ddx * ddx + ddy * ddy;
                        if (bestDistSq >= distSq)
                        {
                            // Also weight by distance from original target (penalty for far rings)
                            // — mirrors client's v27 = targetRadius² * 0.75 reference at S1:9739.
                            float originDx = cx - cur.X;
                            float originDy = cy - cur.Y;
                            float ringPenalty = originDx * originDx + originDy * originDy
                                              - targetRadius * targetRadius * 0.75f;
                            if (ringPenalty < 0) ringPenalty = 0;
                            if (bestRingPenalty >= ringPenalty)
                            {
                                bestX = cx;
                                bestY = cy;
                                bestDistSq = distSq;
                                bestRingPenalty = ringPenalty;
                                foundAny = true;
                            }
                        }
                    }
                }

                cx = (short)(cx + offsets[dirIdx].dx);
                cy = (short)(cy + offsets[dirIdx].dy);
                if (++legProgress == legLength)
                {
                    legProgress = 0;
                    dirIdx = (dirIdx + 2) & 7;
                    if (grewLegOnPriorTurn)
                    {
                        legLength++;
                        grewLegOnPriorTurn = false;
                    }
                    else
                    {
                        grewLegOnPriorTurn = true;
                    }
                    // Bail-out: if we've spiraled out further than the original target's
                    // collision radius without finding anything, give up (S1:9791 ≈ 4 ring growth
                    // attempts before bailing). The 4000-iter cap is the hard ceiling.
                    if (foundAny && legLength > 4) break;
                }
            }

            if (foundAny)
            {
                return TranslateFromNavGrid(new NavigationGridLocator(bestX, bestY));
            }
            return target;
        }
    }
}
