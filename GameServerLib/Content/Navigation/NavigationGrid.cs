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
        }

        // Reference counted dynamic blockers (cellId → number of static buildings blocking it).
        // Used by AddDynamicBlocker / RemoveDynamicBlocker so turrets, inhibitors and nexuses can
        // bake their footprints into the grid without fighting the map-loaded NOT_PASSABLE flags.
        private readonly Dictionary<int, int> _dynamicBlockers = new Dictionary<int, int>();

        /// <summary>
        /// Bakes a polygon footprint (in world XZ coordinates) into the navgrid. The polygon is
        /// reduced to its convex hull and rasterized via cell center in polygon tests. Returns the
        /// list of cell IDs that were newly blocked; pass it back to <see cref="RemoveDynamicBlocker"/>
        /// when the building dies. Multiple overlapping blockers ref-count correctly.
        /// </summary>
        public List<int> AddDynamicBlocker(Vector2[] polygonWorld)
        {
            var blocked = new List<int>();
            if (polygonWorld == null || polygonWorld.Length < 3)
            {
                return blocked;
            }

            var hull = ConvexHull(polygonWorld);
            if (hull.Length < 3)
            {
                return blocked;
            }

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in hull)
            {
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minZ) minZ = p.Y;
                if (p.Y > maxZ) maxZ = p.Y;
            }

            var minCell = TranslateToNavGrid(new Vector2(minX, minZ));
            var maxCell = TranslateToNavGrid(new Vector2(maxX, maxZ));
            short cx0 = (short)Math.Floor(minCell.X);
            short cx1 = (short)Math.Ceiling(maxCell.X);
            short cy0 = (short)Math.Floor(minCell.Y);
            short cy1 = (short)Math.Ceiling(maxCell.Y);

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
                    if (IsPointInConvexPolygon(centerWorld, hull))
                    {
                        IncrementBlocker(cell.ID);
                        blocked.Add(cell.ID);
                    }
                }
            }
            return blocked;
        }

        /// <summary>
        /// Bakes a circular footprint. Use as fallback when no mesh polygon is available.
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

        // Andrew's monotone chain. Produces a CCW convex hull. Output length may be lower than
        // input length (collinear / duplicate points are dropped).
        private static Vector2[] ConvexHull(Vector2[] points)
        {
            if (points.Length <= 1)
            {
                return points;
            }
            var pts = (Vector2[])points.Clone();
            Array.Sort(pts, (a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));

            var hull = new Vector2[2 * pts.Length];
            int k = 0;
            // Lower hull
            for (int i = 0; i < pts.Length; i++)
            {
                while (k >= 2 && Cross(hull[k - 2], hull[k - 1], pts[i]) <= 0)
                {
                    k--;
                }
                hull[k++] = pts[i];
            }
            // Upper hull
            int lower = k + 1;
            for (int i = pts.Length - 2; i >= 0; i--)
            {
                while (k >= lower && Cross(hull[k - 2], hull[k - 1], pts[i]) <= 0)
                {
                    k--;
                }
                hull[k++] = pts[i];
            }
            // Last point equals first; drop it.
            var result = new Vector2[Math.Max(0, k - 1)];
            Array.Copy(hull, result, result.Length);
            return result;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
        {
            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        }

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

        // Polygon must be CCW (which ConvexHull above produces). Returns true for points on the
        // boundary as well — that's fine for blocker rasterization (over-block at the edge by at
        // most one cell, which matches what the client does).
        private static bool IsPointInConvexPolygon(Vector2 p, Vector2[] poly)
        {
            for (int i = 0; i < poly.Length; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Length];
                if (Cross(a, b, p) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Finds a path of waypoints, which are aligned by the cells of the navgrid (A* method), that lead to a set destination.
        /// </summary>
        /// <param name="from">Point that the path starts at.</param>
        /// <param name="to">Point that the path ends at.</param>
        /// <param name="distanceThreshold">Amount of distance away from terrain that the path should be.</param>
        /// <returns>List of points forming a path in order: from -> to</returns>
        public List<Vector2> GetPath(Vector2 from, Vector2 to, float distanceThreshold = 0)
        {
            return GetPath(from, to, out _, distanceThreshold);
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
        public List<Vector2> GetPath(Vector2 from, Vector2 to, out bool isPartialPath, float distanceThreshold = 0)
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

            var openF = new PriorityQueue<NavigationGridCell, float>(1024);
            var gF = new Dictionary<int, float>();
            var cameFromF = new Dictionary<int, NavigationGridCell>();
            var closedF = new HashSet<int>();

            var openB = new PriorityQueue<NavigationGridCell, float>(1024);
            var gB = new Dictionary<int, float>();
            var cameFromB = new Dictionary<int, NavigationGridCell>();
            var closedB = new HashSet<int>();

            float startToGoal = ManhattanDistance(fromNav, toNav);
            openF.Enqueue(cellFrom, startToGoal);
            gF[cellFrom.ID] = 0;
            openB.Enqueue(cellTo, startToGoal);
            gB[cellTo.ID] = 0;

            // mu = the best-known total path cost through a meeting. Terminate if
            // topF + topB >= mu (no future path can be better).
            float mu = float.PositiveInfinity;
            NavigationGridCell meetingCell = null;

            // Closest-explored cell to the goal (forward direction). Fallback if cellTo is unreachable
            // When this happens units will then move toward the wall instead of staying still. Mirrors the
            // partialPath/destinationUnchanged behavior of the client.
            NavigationGridCell bestCellF = cellFrom;
            float bestHeuristicF = ManhattanDistance(cellFrom.GetCenter(), toNav);

            // Backward analogue. Used for the bidirectional convergence bias (client's
            // mOther.mLastKnownGoodLocation): each side's heuristic discounts cells that are
            // close to where the OTHER side has been reaching. Pulls the two frontiers together.
            NavigationGridCell bestCellB = cellTo;
            float bestHeuristicB = ManhattanDistance(cellTo.GetCenter(), fromNav);

            // Running mean of |neighbor to otherSide.bestCell| across both directions, used for the
            // 0.03-weight convergence bias term. Shared between forward and backward as in the client.
            float convergenceAccumulator = 0f;
            int convergenceCount = 0;

            while (openF.Count > 0 && openB.Count > 0)
            {
                openF.TryPeek(out _, out float topF);
                openB.TryPeek(out _, out float topB);
                if (topF + topB >= mu)
                {
                    break;
                }

                // Smaller-frontier-first reduces the worst-case scenario to the maximum of the two halves.
                if (openF.Count <= openB.Count)
                {
                    ExpandStep(
                        openF, gF, cameFromF, closedF, gB,
                        cellFrom, cellTo, fromNav, toNav, distanceThreshold,
                        forward: true, ref mu, ref meetingCell,
                        ref bestCellF, ref bestHeuristicF,
                        ref bestCellB, ref bestHeuristicB,
                        ref convergenceAccumulator, ref convergenceCount
                    );
                }
                else
                {
                    ExpandStep(
                        openB, gB, cameFromB, closedB, gF,
                        cellTo, cellFrom, toNav, fromNav, distanceThreshold,
                        forward: false, ref mu, ref meetingCell,
                        ref bestCellF, ref bestHeuristicF,
                        ref bestCellB, ref bestHeuristicB,
                        ref convergenceAccumulator, ref convergenceCount
                    );
                }
            }

            // Path reconstruction
            List<NavigationGridCell> pathCells;
            if (meetingCell != null)
            {
                // Full path: Forward chain (cellFrom → meetingCell) + backward chain (meetingCell → cellTo).
                pathCells = new List<NavigationGridCell>();

                // Walk backward through the forward half
                var current = meetingCell;
                while (current != null)
                {
                    pathCells.Add(current);
                    cameFromF.TryGetValue(current.ID, out current);
                }
                pathCells.Reverse();

                // Walk forward through the backward half (cameFromB[X] points to cellTo)
                cameFromB.TryGetValue(meetingCell.ID, out current);
                while (current != null)
                {
                    pathCells.Add(current);
                    cameFromB.TryGetValue(current.ID, out current);
                }
            }
            else
            {
                // No match — partial path via forward search to the best-explored cell.
                if (bestCellF.ID == cellFrom.ID)
                {
                    return null;
                }
                isPartialPath = true;

                pathCells = new List<NavigationGridCell>();
                var current = bestCellF;
                while (current != null)
                {
                    pathCells.Add(current);
                    cameFromF.TryGetValue(current.ID, out current);
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
        /// An expansion step for forward or backward search. Both directions are structurally
        /// identical; they differ only in source/target cells and coordinates.
        ///
        /// Updates <paramref name="mu"/> and <paramref name="meetingCell"/> if a cell update
        /// finds a better meeting point with the other search tree (gThis[c] + gOther[c] &lt; mu).
        /// </summary>
        private void ExpandStep(
            PriorityQueue<NavigationGridCell, float> open,
            Dictionary<int, float> gThis,
            Dictionary<int, NavigationGridCell> cameFromThis,
            HashSet<int> closedThis,
            Dictionary<int, float> gOther,
            NavigationGridCell sourceCell,
            NavigationGridCell targetCell,
            Vector2 sourceNav,
            Vector2 targetNav,
            float distanceThreshold,
            bool forward,
            ref float mu,
            ref NavigationGridCell meetingCell,
            ref NavigationGridCell bestCellF,
            ref float bestHeuristicF,
            ref NavigationGridCell bestCellB,
            ref float bestHeuristicB,
            ref float convergenceAccumulator,
            ref int convergenceCount)
        {
            if (!open.TryDequeue(out var cell, out _))
            {
                return;
            }

            // Close on dequeue (not enqueue) —> otherwise, better paths discovered later
            // will no longer be able to update a cell.
            if (closedThis.Contains(cell.ID))
            {
                return;
            }
            closedThis.Add(cell.ID);

            // Best explored tracking per direction. Forward side feeds the partial-path fallback
            // (bestCellF replaces unreachable cellTo); both feed the convergence bias heuristic.
            float hToTarget = ManhattanDistance(cell.GetCenter(), targetNav);
            if (forward)
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

            // Check for collisions on closing: if the other side has already reached this cell,
            // the path cellFrom -> .. -> cell -> .. -> cellTo is possible.
            if (gOther.TryGetValue(cell.ID, out float gOtherHere))
            {
                float candidate = gThis[cell.ID] + gOtherHere;
                if (candidate < mu)
                {
                    mu = candidate;
                    meetingCell = cell;
                }
            }

            foreach (NavigationGridCell neighborCell in GetCellNeighbors(cell))
            {
                if (closedThis.Contains(neighborCell.ID))
                {
                    continue;
                }

                Vector2 neighborCellCoord = targetNav;
                if (neighborCell.ID != targetCell.ID)
                {
                    neighborCellCoord = neighborCell.GetCenter();

                    Vector2 cellCoord = sourceNav;
                    if (cell.ID != sourceCell.ID)
                    {
                        cellCoord = cell.GetCenter();
                    }

                    if (CastCircle(cellCoord, neighborCellCoord, distanceThreshold, false))
                    {
                        continue;
                    }
                }

                bool diagonal = (cell.Locator.X != neighborCell.Locator.X)
                             && (cell.Locator.Y != neighborCell.Locator.Y);
                float stepCost = diagonal ? 1.41421356f : 1f;

                float tentativeG = gThis[cell.ID] + stepCost
                    + neighborCell.ArrivalCost
                    + neighborCell.AdditionalCost;

                if (gThis.TryGetValue(neighborCell.ID, out float existingG) && tentativeG >= existingG)
                {
                    continue;
                }

                gThis[neighborCell.ID] = tentativeG;
                cameFromThis[neighborCell.ID] = cell;

                float directionScale = forward ? 1.0f : BACKWARD_HEURISTIC_SCALE;

                // Bidirectional convergence bias -> discounts cells that are close to the OTHER
                // side's best known location relative to the running mean of the same.
                Vector2 otherBest = forward ? bestCellB.GetCenter() : bestCellF.GetCenter();
                float distToOtherBest = ManhattanDistance(neighborCellCoord, otherBest);
                convergenceAccumulator += distToOtherBest;
                convergenceCount++;
                float convergenceMean = convergenceAccumulator / convergenceCount;
                float convergenceBias = distToOtherBest / convergenceMean * 0.03f;

                open.Enqueue(
                    neighborCell,
                    tentativeG + neighborCell.Heuristic
                        + ManhattanDistance(neighborCellCoord, targetNav) * directionScale
                        + convergenceBias
                );

                // Check for cross-meetings even during edge relaxation — otherwise, some meetings
                // would not be detected until the next dequeue, which would delay the termination condition.
                if (gOther.TryGetValue(neighborCell.ID, out float gOtherNb))
                {
                    float candidate = tentativeG + gOtherNb;
                    if (candidate < mu)
                    {
                        mu = candidate;
                        meetingCell = neighborCell;
                    }
                }
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
            NavigationGridCell cell = GetCell(coords, translate);
            return IsVisible(cell); //TODO: implement bush logic here
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

            bool prevPosHadBush = HasFlag(origin, NavigationGridCellFlags.HAS_GRASS, false);
            bool destinationHasGrass = HasFlag(destination, NavigationGridCellFlags.HAS_GRASS, false);

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

                    bool isGrass = cell.HasFlag(NavigationGridCellFlags.HAS_GRASS);

                    // If you are outside of a bush
                    if (!prevPosHadBush && isGrass)
                    {
                        break;
                    }

                    // If you are in a different bush
                    if (prevPosHadBush && destinationHasGrass && !isGrass)
                    {
                        break;
                    }
                }
            }
            
            return hasNext;
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

            // Bush edge fallback -> if the observer is near/in brush, test LOS from nearby brush cells too.
            // This avoids false negatives when the observer origin is nudged outside of grass by radius offsets.
            if (!checkVision)
            {
                return true;
            }

            foreach (var cell in GetAllCellsInRange(a.Position, Math.Max(25.0f, a.PathfindingRadius)))
            {
                if (cell != null && cell.HasFlag(NavigationGridCellFlags.HAS_GRASS))
                {
                    if (!CastRay(TranslateFromNavGrid(cell.GetCenter()), destination, false, true))
                    {
                        return false;
                    }
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
            double angle = Math.PI / 4;

            // x = r * cos(angle)
            // y = r * sin(angle)
            // r = distance from center
            // Draws spirals until it finds a walkable spot
            for (int r = 1; !IsWalkable(location, distanceThreshold); r++)
            {
                location.X += r * (float)Math.Cos(angle);
                location.Y += r * (float)Math.Sin(angle);
                angle += Math.PI / 4;
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
    }
}
