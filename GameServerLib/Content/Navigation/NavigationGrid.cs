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
        // Each synchronous search leases independent scratch, including nested
        // searches from traversal callbacks. No terrain result survives a lease.
        private readonly System.Collections.Concurrent.ConcurrentBag<PathScratch> _pathScratch = new();
        private sealed class PathScratch
        {
            public readonly Dictionary<int, float> Cost = new();
            public readonly Dictionary<int, int> Previous = new();
            public readonly PriorityQueue<(int Id, float Cost), (float Score, int Id)> Open = new();
            public readonly List<NavigationGridCell> Neighbors = new(9);
            public readonly List<int> Touched = new();
            public readonly byte[] Walkable;
            public PathScratch(int cells) => Walkable = new byte[cells];
            public void Reset()
            {
                foreach (int id in Touched) Walkable[id] = 0;
                Touched.Clear();
                Cost.Clear();
                Previous.Clear();
                Open.Clear();
                Neighbors.Clear();
            }
        }
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
        private int[] _runtimeBlockRefCount;
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
                _runtimeBlockRefCount = new int[Cells.Length];
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

        /// <summary>
        /// Finds a path of waypoints, which are aligned by the cells of the navgrid (A* method), that lead to a set destination.
        /// </summary>
        /// <param name="from">Point that the path starts at.</param>
        /// <param name="to">Point that the path ends at.</param>
        /// <param name="distanceThreshold">Amount of distance away from terrain that the path should be.</param>
        /// <returns>List of points forming a path in order: from -> to</returns>
        public List<Vector2> GetPath(Vector2 from, Vector2 to, float distanceThreshold = 0,
            Func<Vector2, Vector2, bool> canTraverse = null, int maxExpanded = int.MaxValue,
            bool allowPartial = false, float goalRadius = 0)
        {
            int expanded = 0;
            if (!IsWalkable(from, distanceThreshold)) return null;
            var goalCenter = to;
            if (goalRadius > 0 && Vector2.DistanceSquared(from, goalCenter) <= goalRadius * goalRadius)
                return new List<Vector2> { from, from };
            to = GetClosestTerrainExit(to, distanceThreshold);
            if (!IsWalkable(to, distanceThreshold)) return null;
            bool Clear(Vector2 a, Vector2 b) => !CastCircle(a, b, distanceThreshold)
                && (canTraverse == null || canTraverse(a, b));
            if (goalRadius > 0)
            {
                var direction = goalCenter - from;
                var endpoint = goalCenter - Vector2.Normalize(direction) * goalRadius;
                if (Clear(from, endpoint)) return new List<Vector2> { from, endpoint };
            }
            else if (Clear(from, to)) return new List<Vector2> { from, to };
            var first = GetCell(from);
            var last = GetCell(to);
            if (!_pathScratch.TryTake(out var scratch)) scratch = new PathScratch(Cells.Length);
            var cost = scratch.Cost;
            var previous = scratch.Previous;
            var walkable = scratch.Walkable;
            cost[first.ID] = 0;
            try
            {
                var open = scratch.Open;
                open.Enqueue((first.ID, 0), (Vector2.Distance(from, to), first.ID));
                Vector2 Point(NavigationGridCell cell) => cell.ID == first.ID ? from
                    : cell.ID == last.ID ? to : TranslateFromNavGrid(cell.Locator);
                List<Vector2> Build(int end)
                {
                    var path = new List<Vector2> { Point(Cells[end]) };
                    for (int id = end; id != first.ID;)
                    {
                        id = previous[id];
                        path.Add(Point(Cells[id]));
                    }
                    path.Reverse();
                    var smooth = new List<Vector2> { from };
                    int anchor = 0;
                    while (anchor < path.Count - 1)
                    {
                        int next = anchor + 1;
                        for (int candidate = next + 1; candidate < path.Count; candidate++)
                        {
                            if (!Clear(path[anchor], path[candidate])) break;
                            next = candidate;
                        }
                        smooth.Add(path[next]);
                        anchor = next;
                    }
                    return smooth;
                }
                int closest = first.ID;
                float bestDistance = Vector2.DistanceSquared(from, to);
                while (expanded < maxExpanded && open.TryDequeue(out var entry, out _))
                {
                    if (entry.Cost > cost[entry.Id]) continue;
                    expanded++;
                    if (goalRadius <= 0 && entry.Id == last.ID) return Build(last.ID);
                    var cell = Cells[entry.Id];
                    var point = Point(cell);
                    if (goalRadius > 0 && Vector2.DistanceSquared(point, goalCenter) <= goalRadius * goalRadius)
                        return Build(entry.Id);
                    float goalDistance = Vector2.DistanceSquared(point, to);
                    if (goalDistance < bestDistance) { closest = entry.Id; bestDistance = goalDistance; }
                    GetCellNeighbors(cell, scratch.Neighbors);
                    foreach (var neighbor in scratch.Neighbors)
                    {
                        if (neighbor.ID == cell.ID) continue;
                        var next = Point(neighbor);
                        float nextCost = entry.Cost + Vector2.Distance(point, next);
                        if (cost.TryGetValue(neighbor.ID, out float known) && known <= nextCost) continue;
                        if (walkable[neighbor.ID] == 0)
                        {
                            scratch.Touched.Add(neighbor.ID);
                            walkable[neighbor.ID] = (byte)(IsWalkable(next, distanceThreshold) ? 1 : 2);
                        }
                        // Both endpoint footprints are already validated in this
                        // search. Still check the full swept area and actor callback.
                        if (walkable[neighbor.ID] == 2
                            || CastCircleKnownEndpoints(point, next, distanceThreshold, true)
                            || (canTraverse != null && !canTraverse(point, next))) continue;
                        cost[neighbor.ID] = nextCost;
                        previous[neighbor.ID] = cell.ID;
                        open.Enqueue((neighbor.ID, nextCost), (nextCost + Vector2.Distance(next, to), neighbor.ID));
                    }
                }
                return allowPartial && closest != first.ID ? Build(closest) : null;
            }
            finally
            {
                scratch.Reset();
                _pathScratch.Add(scratch);
            }
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
            if (!float.IsFinite(coords.X) || !float.IsFinite(coords.Y)
                || coords.X < 0 || coords.Y < 0 || coords.X >= CellCountX || coords.Y >= CellCountY)
            {
                return null;
            }
            return GetCell((short)Math.Floor(coords.X), (short)Math.Floor(coords.Y));
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
            if (x < 0 || x >= CellCountX || y < 0 || y >= CellCountY || index >= Cells.Length)
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
        private void GetCellNeighbors(NavigationGridCell cell, List<NavigationGridCell> neighbors)
        {
            neighbors.Clear();
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
            if (translate)
            {
                coords = TranslateToNavGrid(coords);
            }

            return GetCell(
                new Vector2(
                    Math.Clamp(coords.X, 0, CellCountX - 1),
                    Math.Clamp(coords.Y, 0, CellCountY - 1)
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
            if (translate)
            {
                origin = TranslateToNavGrid(origin);
            }

            short fx = (short)Math.Max(0, Math.Floor(origin.X - radius));
            short lx = (short)Math.Min(CellCountX - 1, Math.Floor(origin.X + radius));
            short fy = (short)Math.Max(0, Math.Floor(origin.Y - radius));
            short ly = (short)Math.Min(CellCountY - 1, Math.Floor(origin.Y + radius));

            for (short x = fx; x <= lx; x++)
            {
                for (short y = fy; y <= ly; y++)
                {
                    float distSquared = Extensions.DistanceSquaredToRectangle(
                        new Vector2(x + 0.5f, y + 0.5f), 1f, 1f, origin
                    );
                    if (distSquared <= radius * radius)
                    {
                        var cell = GetCell(x, y);
                        if (cell != null)
                        {
                            yield return cell;
                        }
                    }
                }
            }
        }

        private bool IsWalkable(NavigationGridCell cell)
        {
            return cell != null
                && !cell.HasFlag(NavigationGridCellFlags.NOT_PASSABLE)
                && !cell.HasFlag(NavigationGridCellFlags.SEE_THROUGH)
                && !IsRuntimeBlocked(cell);
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
            var gridPosition = translate ? TranslateToNavGrid(coords) : coords;
            float radius = checkRadius / CellSize;
            if (!float.IsFinite(radius) || radius < 0 || GetCell(gridPosition, false) == null
                || gridPosition.X - radius < 0 || gridPosition.Y - radius < 0
                || gridPosition.X + radius >= CellCountX || gridPosition.Y + radius >= CellCountY)
            {
                return false;
            }
            if (checkRadius == 0)
            {
                NavigationGridCell cell = GetCell(coords, translate);
                return IsWalkable(cell);
            }

            short fx = (short)Math.Max(0, Math.Floor(gridPosition.X - radius));
            short lx = (short)Math.Min(CellCountX - 1, Math.Floor(gridPosition.X + radius));
            short fy = (short)Math.Max(0, Math.Floor(gridPosition.Y - radius));
            short ly = (short)Math.Min(CellCountY - 1, Math.Floor(gridPosition.Y + radius));
            for (short x = fx; x <= lx; x++)
                for (short y = fy; y <= ly; y++)
                {
                    float distance = Extensions.DistanceSquaredToRectangle(
                        new Vector2(x + 0.5f, y + 0.5f), 1f, 1f, gridPosition);
                    if (distance <= radius * radius)
                    {
                        var cell = GetCell(x, y);
                        if (cell != null && !IsWalkable(cell)) return false;
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

            bool originHasGrass = HasFlag(origin, NavigationGridCellFlags.HAS_GRASS, false);
            bool destinationHasGrass = HasFlag(destination, NavigationGridCellFlags.HAS_GRASS, false);

            bool Blocks(NavigationGridCell cell)
            {
                if (checkWalkable && !IsWalkable(cell)) return true;
                if (!checkVisible) return false;
                if (!IsVisible(cell)) return true;
                bool grass = cell.HasFlag(NavigationGridCellFlags.HAS_GRASS);
                return (!originHasGrass && grass)
                    || (originHasGrass && destinationHasGrass && !grass);
            }

            double dx = Math.Abs(destination.X - origin.X);
            double dy = Math.Abs(destination.Y - origin.Y);

            short x = (short)(Math.Floor(origin.X));
            short y = (short)(Math.Floor(origin.Y));

            int n = 1;
            short x_inc, y_inc;
            double error;

            if (dx == 0)
            {
                x_inc = 0;
                error = float.PositiveInfinity;
            }
            else if (destination.X > origin.X)
            {
                x_inc = 1;
                n += (int)(Math.Floor(destination.X)) - x;
                error = (Math.Floor(origin.X) + 1 - origin.X) * dy;
            }
            else
            {
                x_inc = -1;
                n += x - (int)(Math.Floor(destination.X));
                error = (origin.X - Math.Floor(origin.X)) * dy;
            }

            if (dy == 0)
            {
                y_inc = 0;
                error = float.NegativeInfinity;
            }
            else if (destination.Y > origin.Y)
            {
                y_inc = 1;
                n += (int)(Math.Floor(destination.Y)) - y;
                error -= (Math.Floor(origin.Y) + 1 - origin.Y) * dx;
            }
            else
            {
                y_inc = -1;
                n += y - (int)(Math.Floor(destination.Y));
                error -= (origin.Y - Math.Floor(origin.Y)) * dx;
            }

            for (; n > 0; --n)
            {
                if (Blocks(GetCell(x, y))) return true;

                if (error > 0)
                {
                    y += y_inc;
                    error -= dx;
                }
                else if (error < 0)
                {
                    x += x_inc;
                    error += dy;
                }
                else //if (error == 0)
                {
                    if (Blocks(GetCell((short)(x + x_inc), y))) return true;
                    if (Blocks(GetCell(x, (short)(y + y_inc)))) return true;

                    x += x_inc;
                    y += y_inc;
                    error += dy - dx;
                    n--;
                }
            }
            return false;
        }

        // https://playtechs.blogspot.com/2007/03/raytracing-on-grid.html
        // Direct traversal avoids allocating four iterator objects and the
        // concatenation chain for every candidate edge in a path search.
        private bool AddSweepCell(NavigationGridCell cell, int minY, short[] xRanges)
        {
            if (!IsWalkable(cell))
            {
                return true;
            }
            int y = cell.Locator.Y - minY;
            if (xRanges[y * 3 + 2] == 0)
            {
                xRanges[y * 3 + 0] = cell.Locator.X;
                xRanges[y * 3 + 1] = cell.Locator.X;
                xRanges[y * 3 + 2] = 1;
            }
            else
            {
                xRanges[y * 3 + 0] = Math.Min(xRanges[y * 3 + 0], cell.Locator.X);
                xRanges[y * 3 + 1] = Math.Max(xRanges[y * 3 + 1], cell.Locator.X);
            }
            return false;
        }

        private bool AddSweepRange(Vector2 origin, float radius, int minY, short[] xRanges)
        {
            radius /= CellSize;
            short fx = (short)Math.Max(0, Math.Floor(origin.X - radius));
            short lx = (short)Math.Min(CellCountX - 1, Math.Floor(origin.X + radius));
            short fy = (short)Math.Max(0, Math.Floor(origin.Y - radius));
            short ly = (short)Math.Min(CellCountY - 1, Math.Floor(origin.Y + radius));

            for (short x = fx; x <= lx; x++)
            {
                for (short y = fy; y <= ly; y++)
                {
                    float distSquared = Extensions.DistanceSquaredToRectangle(
                        new Vector2(x + 0.5f, y + 0.5f), 1f, 1f, origin
                    );
                    if (distSquared <= radius * radius)
                    {
                        var cell = GetCell(x, y);
                        if (cell != null)
                        {
                            if (AddSweepCell(cell, minY, xRanges)) return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool AddSweepLine(Vector2 v0, Vector2 v1, int minY, short[] xRanges)
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
                if (AddSweepCell(GetCell(x, y), minY, xRanges)) return true;

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
                    if (AddSweepCell(GetCell((short)(x + x_inc), y), minY, xRanges)) return true;
                    if (AddSweepCell(GetCell(x, (short)(y + y_inc)), minY, xRanges)) return true;

                    x += x_inc;
                    y += y_inc;
                    error += dy - dx;
                    n--;
                }
            }
            return false;
        }

        public bool CastCircle(Vector2 orig, Vector2 dest, float radius, bool translate = true)
        {
            if (!IsWalkable(orig, radius, translate) || !IsWalkable(dest, radius, translate))
            {
                return true;
            }
            return CastCircleKnownEndpoints(orig, dest, radius, translate);
        }

        private bool CastCircleKnownEndpoints(Vector2 orig, Vector2 dest, float radius, bool translate)
        {
            Vector2 direction = dest - orig;
            if (direction.LengthSquared() <= 0.0001f)
            {
                return false;
            }

            if (translate)
            {
                orig = TranslateToNavGrid(orig);
                dest = TranslateToNavGrid(dest);
            }

            float tradius = radius / CellSize;
            Vector2 p = direction.Normalized().Perpendicular() * tradius;

            int minY = (int)(Math.Min(orig.Y, dest.Y) - tradius) - 1;
            int maxY = (int)(Math.Max(orig.Y, dest.Y) + tradius) + 1;

            int countY = maxY - minY + 1;
            // A sweep is synchronous and owns its rented scratch until return.
            // Clear the used slice to preserve the old zero-initialized array.
            var xRanges = System.Buffers.ArrayPool<short>.Shared.Rent(countY * 3);
            Array.Clear(xRanges, 0, countY * 3);
            try
            {
                // Preserve the original sequence: start cap, end cap, then
                // the two sides. The row-fill pass below is unchanged.
                if (AddSweepRange(orig, radius, minY, xRanges)
                    || AddSweepRange(dest, radius, minY, xRanges)
                    || AddSweepLine(orig + p, dest + p, minY, xRanges)
                    || AddSweepLine(orig - p, dest - p, minY, xRanges)) return true;

                for(int y = 0; y < countY; y++)
                {
                    for(int x = xRanges[y * 3 + 0] + 1; x < xRanges[y * 3 + 1]; x++)
                    {
                        if (!IsWalkable(GetCell((short)x, (short)(minY + y))))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            finally { System.Buffers.ArrayPool<short>.Shared.Return(xRanges); }
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
        /// Gets the closest pathable position to the given position. *NOTE*: Computationally heavy, use sparingly.
        /// </summary>
        /// <param name="location">Vector2 position to start the check at.</param>
        /// <param name="distanceThreshold">Amount of distance away from terrain the exit should be.</param>
        /// <returns>Vector2 position which can be pathed on.</returns>
        public Vector2 GetClosestTerrainExit(Vector2 location, float distanceThreshold = 0)
        {
            if (IsWalkable(location, distanceThreshold))
            {
                return location;
            }

            Vector2 originalLocation = location;
            double angle = 0;
            float step = CellSize;

            for (float r = step; r < 2000f; r += step)
            {
                for (int i = 0; i < 8; i++)
                {
                    angle = i * (Math.PI / 4);
                    Vector2 testPoint = originalLocation + new Vector2(
                        r * (float)Math.Cos(angle),
                        r * (float)Math.Sin(angle)
                    );

                    if (IsWalkable(testPoint, distanceThreshold))
                    {
                        return testPoint;
                    }
                }
            }

            return originalLocation;
        }

        public NavigationGridCell GetClosestWalkableCell(Vector2 coords, float distanceThreshold = 0, bool translate = true)
        {
            if(translate)
            {
                coords = TranslateToNavGrid(coords);
            }

            short startX = (short)Math.Clamp(coords.X, 0f, CellCountX - 1);
            short startY = (short)Math.Clamp(coords.Y, 0f, CellCountY - 1);

            for (short r = 0; r < 50; r++)
            {
                for (short dx = (short)-r; dx <= r; dx++)
                {
                    for (short dy = (short)-r; dy <= r; dy++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                        {
                            continue;
                        }

                        short tx = (short)(startX + dx);
                        short ty = (short)(startY + dy);

                        var cell = GetCell(tx, ty);
                        if (cell != null && IsWalkable(cell, distanceThreshold))
                        {
                            return cell;
                        }
                    }
                }
            }

            return GetCell(startX, startY);
        }
        public void AddRuntimeBlocker(Vector2 position, float radius)
        {
            if (_runtimeBlockRefCount == null || radius <= 0.0f)
            {
                return;
            }

            foreach (var cell in GetAllCellsInRange(position, radius, true))
            {
                if (cell == null || Vector2.DistanceSquared(TranslateFromNavGrid(cell.Locator), position) > radius * radius)
                {
                    continue;
                }

                _runtimeBlockRefCount[cell.ID]++;
            }
        }

        public void RemoveRuntimeBlocker(Vector2 position, float radius)
        {
            if (_runtimeBlockRefCount == null || radius <= 0.0f)
            {
                return;
            }

            foreach (var cell in GetAllCellsInRange(position, radius, true))
            {
                if (cell == null || Vector2.DistanceSquared(TranslateFromNavGrid(cell.Locator), position) > radius * radius)
                {
                    continue;
                }

                if (_runtimeBlockRefCount[cell.ID] > 0)
                {
                    _runtimeBlockRefCount[cell.ID]--;
                }
            }
        }

        private bool IsRuntimeBlocked(NavigationGridCell cell)
        {
            if (cell == null || _runtimeBlockRefCount == null)
            {
                return false;
            }

            return _runtimeBlockRefCount[cell.ID] > 0;
        }
    }
}
