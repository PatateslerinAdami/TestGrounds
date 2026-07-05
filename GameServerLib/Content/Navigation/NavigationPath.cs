using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;

namespace LeagueSandbox.GameServer.Content.Navigation
{
    /// <summary>
    /// Wrapper around an ordered sequence of waypoints. Mirrors the S4 client's
    /// <c>NavigationPath</c> class semantics. Read access goes through
    /// <see cref="IReadOnlyList{Vector2}"/> so existing indexer / Count / foreach / LINQ
    /// usage compiles unchanged. Mutation is funneled through explicit methods so the
    /// class can re-validate or re-publish on change. <see cref="IsPartial"/> replaces
    /// the previous <c>out bool isPartialPath</c> pattern on <see cref="NavigationGrid.GetPath"/>.
    /// </summary>
    public class NavigationPath : IReadOnlyList<Vector2>
    {
        private readonly List<Vector2> _waypoints;

        /// <summary>
        /// True when the path leads to the closest reachable cell rather than the originally
        /// requested destination (the destination was unreachable). Mirrors the client's
        /// partial-path flag set when bidirectional A* meets-in-the-middle without ever
        /// touching the goal cell.
        /// </summary>
        public bool IsPartial { get; set; }

        /// <summary>
        /// Force-movement (dash / leap / knock-arc) parameters bound to THIS path, or null for a
        /// normal walk path. Mirrors Riot's layout where the override speed / gravity / track-unit
        /// fields live directly on the NavigationPath (we keep them in a cohesive
        /// <see cref="ForceMovementParameters"/> object). Sharing the path's lifetime means replacing
        /// the path atomically clears any previous force-move state. Read/written through
        /// <see cref="AttackableUnit.MovementParameters"/>.
        /// </summary>
        public ForceMovementParameters ForceMovement { get; set; }

        public int Count => _waypoints.Count;
        public Vector2 this[int index] => _waypoints[index];

        public NavigationPath()
        {
            _waypoints = new List<Vector2>();
        }

        public NavigationPath(int capacity)
        {
            _waypoints = new List<Vector2>(capacity);
        }

        public NavigationPath(IEnumerable<Vector2> source)
        {
            _waypoints = new List<Vector2>(source);
        }

        /// <summary>
        /// Convenience for the common "path = single position" initialization (e.g. a fresh
        /// <c>AttackableUnit</c> whose path starts as just its spawn location).
        /// </summary>
        public static NavigationPath OfSingle(Vector2 position)
        {
            var path = new NavigationPath(1);
            path._waypoints.Add(position);
            return path;
        }

        /// <summary>
        /// Returns a fresh defensive copy of the underlying waypoints as <see cref="List{Vector2}"/>.
        /// Use this at any wire-format / network packet boundary — the live <see cref="NavigationPath"/>
        /// keeps mutating between packet construction and serialization, and shared references
        /// have caused replication bugs in the past (see <c>feedback_packet_collection_copy.md</c>).
        /// </summary>
        public List<Vector2> ToList() => new List<Vector2>(_waypoints);

        // === Mutation ===

        public void AddWaypoint(Vector2 waypoint) => _waypoints.Add(waypoint);
        public void AddWaypointFront(Vector2 waypoint) => _waypoints.Insert(0, waypoint);
        public void Insert(int index, Vector2 waypoint) => _waypoints.Insert(index, waypoint);

        /// <summary>
        /// Ensures the path begins at <paramref name="startPos"/>. Safe convenience for
        /// manual path construction:
        /// <list type="bullet">
        /// <item>Empty path → becomes <c>[startPos]</c>.</item>
        /// <item>Path's first waypoint is already at <paramref name="startPos"/> (within
        /// <paramref name="epsilon"/>) → no-op.</item>
        /// <item>Otherwise → prepends <paramref name="startPos"/> as <c>Waypoints[0]</c>.</item>
        /// </list>
        ///
        /// Mirrors S4 <c>NavigationPath::InsertStartPosition</c> (NavigationPath.cpp:1899).
        /// Use after manual path construction (HandleMove fallback, RefreshWaypoints, dash
        /// setup) to guarantee the path satisfies the Count &gt; 1 invariant <c>SetWaypoints</c>
        /// requires when the unit needs to walk somewhere — addresses the recurring
        /// "Count==1 SetWaypoints reject → unit can't move" bug class documented in the
        /// stuck-around-terrain memory entry.
        /// </summary>
        public void InsertStartPosition(Vector2 startPos, float epsilon = 1f)
        {
            if (_waypoints.Count == 0)
            {
                _waypoints.Add(startPos);
                return;
            }
            if (Vector2.DistanceSquared(_waypoints[0], startPos) < epsilon * epsilon)
            {
                return;
            }
            _waypoints.Insert(0, startPos);
        }

        /// <summary>
        /// Replaces a waypoint at the given index. Use this instead of an indexer setter so
        /// any future invariant checks (or re-publish hooks) live in one place.
        /// </summary>
        public void Replace(int index, Vector2 waypoint) => _waypoints[index] = waypoint;

        public void Clear() => _waypoints.Clear();
        public void RemoveAt(int index) => _waypoints.RemoveAt(index);
        public void RemoveRange(int index, int count) => _waypoints.RemoveRange(index, count);
        public void AddRange(IEnumerable<Vector2> items) => _waypoints.AddRange(items);
        public void Append(NavigationPath other) => _waypoints.AddRange(other._waypoints);

        public void Reset(IEnumerable<Vector2> source)
        {
            _waypoints.Clear();
            _waypoints.AddRange(source);
        }

        public void Reverse() => _waypoints.Reverse();

        /// <summary>
        /// Truncates the path to at most <paramref name="maxSize"/> waypoints, keeping the prefix.
        /// </summary>
        public void ShrinkPath(int maxSize)
        {
            if (maxSize < 0) maxSize = 0;
            if (_waypoints.Count <= maxSize) return;
            _waypoints.RemoveRange(maxSize, _waypoints.Count - maxSize);
        }

        /// <summary>
        /// Inserts intermediate points so no segment is longer than <paramref name="segmentLength"/>.
        /// <paramref name="startPos"/> is the implicit segment-start before the first waypoint.
        /// No-op when <paramref name="segmentLength"/> is non-positive or the path is empty.
        /// </summary>
        public void SubdividePath(Vector2 startPos, float segmentLength)
        {
            if (segmentLength <= 0f || _waypoints.Count == 0) return;
            var subdivided = new List<Vector2>(_waypoints.Count);
            Vector2 prev = startPos;
            foreach (var wp in _waypoints)
            {
                Vector2 delta = wp - prev;
                float dist = delta.Length();
                if (dist > segmentLength)
                {
                    int segments = (int)Math.Ceiling(dist / segmentLength);
                    Vector2 step = delta / segments;
                    for (int i = 1; i < segments; i++)
                    {
                        subdivided.Add(prev + step * i);
                    }
                }
                subdivided.Add(wp);
                prev = wp;
            }
            _waypoints.Clear();
            _waypoints.AddRange(subdivided);
        }

        // === Movement-trim ===

        /// <summary>
        /// Drops the prefix of waypoints up to (but not including) the one closest to
        /// <paramref name="currentPos"/>, then rewrites the new head to <paramref name="currentPos"/>
        /// so the path always starts at the unit. Mirrors the client's "I have already traversed
        /// the early waypoints" semantics.
        /// </summary>
        public void TrimUntilReached(Vector2 currentPos)
        {
            if (_waypoints.Count == 0)
            {
                _waypoints.Add(currentPos);
                return;
            }
            int closest = FindClosestIndex(currentPos);
            if (closest > 0)
            {
                _waypoints.RemoveRange(0, closest);
            }
            _waypoints[0] = currentPos;
        }

        /// <summary>
        /// Alias of <see cref="TrimUntilReached"/>. Kept for readability at callsites that
        /// emphasize "snap head to current position" rather than "drop traversed prefix".
        /// </summary>
        public void TrimToCurrentPosition(Vector2 currentPos) => TrimUntilReached(currentPos);

        /// <summary>
        /// Drops the first <paramref name="currentWaypointKey"/> waypoints. Used by the
        /// movement loop to compact a path once the unit has progressed past intermediate
        /// waypoints. No-op for invalid keys.
        /// </summary>
        public void TrimToCurrentWaypoint(int currentWaypointKey)
        {
            if (currentWaypointKey <= 0) return;
            if (currentWaypointKey >= _waypoints.Count)
            {
                _waypoints.Clear();
                return;
            }
            _waypoints.RemoveRange(0, currentWaypointKey);
        }

        // === Identity ===

        /// <summary>
        /// Faithful port of S4 <c>NavigationPath::IsPathTheSame(unitPos, otherPath)</c>
        /// (mac decomp NavigationPath.cpp:327; Ghidra s4 ai/NavigationPath.cpp:1954 DWARF proto).
        /// Returns true when, after skipping each path's already-reached prefix near
        /// <paramref name="unitPos"/>, the remainder of THIS path matches <paramref name="other"/>
        /// closely enough that <paramref name="other"/> is fully consumed.
        ///
        /// Three phases (this = the freshly-built path, other = the current path):
        ///   1. Advance past THIS waypoints within a 50-unit box of <paramref name="unitPos"/>.
        ///   2. Advance past OTHER waypoints within a 50-unit box of <paramref name="unitPos"/>.
        ///   3. Compare the remainders pairwise with a 10-unit box tolerance; the paths are "the
        ///      same" iff OTHER is exhausted by the match.
        ///
        /// <paramref name="thisNext"/> / <paramref name="otherNext"/> are the per-path
        /// <c>m_NextWaypoint</c> traversal cursors — Riot stores them on the path; we keep them on
        /// the unit (<c>CurrentWaypointKey</c>), so callers thread them in. Box (Chebyshev) distance
        /// and the 50/10 thresholds are the client literals; the leniency is deliberate (dedups
        /// near-identical re-paths so the server doesn't re-broadcast a WaypointGroup every recompute).
        /// </summary>
        public bool IsPathTheSame(NavigationPath other, Vector2 unitPos, int thisNext = 0, int otherNext = 0)
        {
            if (other == null) return false;

            int thisSize = _waypoints.Count;
            int otherSize = other._waypoints.Count;

            // Both cursors past their ends -> both paths fully traversed -> trivially "the same".
            bool bothDone = otherNext >= otherSize && thisNext >= thisSize;
            if (thisNext >= thisSize) return bothDone;

            int iter = thisNext;
            int iterOther = otherNext;
            if (iterOther >= otherSize) return bothDone;

            // Phase 1: skip THIS waypoints already reached (within 50-unit box of the unit).
            while (iter < thisSize)
            {
                Vector2 p = _waypoints[iter];
                if (Math.Abs(p.X - unitPos.X) > 50f || Math.Abs(p.Y - unitPos.Y) > 50f) break;
                iter++;
            }

            // Phase 2: skip OTHER waypoints already reached.
            while (iterOther < otherSize)
            {
                Vector2 p = other._waypoints[iterOther];
                if (Math.Abs(p.X - unitPos.X) > 50f || Math.Abs(p.Y - unitPos.Y) > 50f) break;
                iterOther++;
            }

            // Phase 3: pairwise compare remainders with a 10-unit box tolerance.
            if (iter < thisSize)
            {
                int i = 0;
                while (true)
                {
                    int otherIdx = iterOther + i;
                    if (otherIdx >= otherSize) break;
                    Vector2 p1 = _waypoints[iter + i];
                    Vector2 p2 = other._waypoints[otherIdx];
                    if (Math.Abs(p1.X - p2.X) > 10f || Math.Abs(p1.Y - p2.Y) > 10f) break;
                    if (iter + i + 1 >= thisSize)
                    {
                        iterOther = iterOther + i + 1;
                        break;
                    }
                    i++;
                }
            }

            return iterOther == otherSize;
        }

        public bool HasDuplicateInARow()
        {
            for (int i = 1; i < _waypoints.Count; i++)
            {
                if (_waypoints[i] == _waypoints[i - 1]) return true;
            }
            return false;
        }

        // === Validation ===

        /// <summary>
        /// Returns true when every consecutive waypoint pair has a clear corridor of width
        /// <paramref name="radius"/> through the navgrid i.e., the path is still walkable
        /// against the current terrain. Returns false when any segment is blocked (e.g., a
        /// turret was built between two previously-clear waypoints, or a dynamic blocker
        /// has been added since the path was computed). Caller should A*-repath when false.
        /// </summary>
        public bool ValidateLineOfSight(NavigationGrid grid, float radius)
        {
            if (_waypoints.Count < 2) return true;
            for (int i = 1; i < _waypoints.Count; i++)
            {
                if (radius == 0f)
                {
                    // Same thin-line walk the SmoothPath trim built this path with. Using the
                    // supercover CastCircle here would re-flag the trim's legitimate corner-touch
                    // legs as blocked — PathingHandler.UpdatePaths runs this every 3s per unit,
                    // so a validator/builder disagreement reroutes every corner-hugging path per
                    // batch. New blockers (turret bakes, dynamic blockers) span whole footprints
                    // and are caught by the thin line just as reliably.
                    if (!grid.IsGridLineOfSightClear(_waypoints[i - 1], _waypoints[i]))
                    {
                        return false;
                    }
                }
                // CastCircle returns true when BLOCKED, false when clear.
                else if (grid.CastCircle(_waypoints[i - 1], _waypoints[i], radius))
                {
                    return false;
                }
            }
            return true;
        }

        // Queries

        public float ComputePathDistance()
        {
            float total = 0f;
            for (int i = 1; i < _waypoints.Count; i++)
            {
                total += Vector2.Distance(_waypoints[i - 1], _waypoints[i]);
            }
            return total;
        }

        public Vector2 FindClosest(Vector2 pos)
        {
            int idx = FindClosestIndex(pos);
            return idx < 0 ? pos : _waypoints[idx];
        }

        public int FindClosestIndex(Vector2 pos)
        {
            if (_waypoints.Count == 0) return -1;
            int bestIdx = 0;
            float bestDistSq = Vector2.DistanceSquared(_waypoints[0], pos);
            for (int i = 1; i < _waypoints.Count; i++)
            {
                float dSq = Vector2.DistanceSquared(_waypoints[i], pos);
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        public Vector2 FindFirstWithinRangeOrClosest(Vector2 pos, float range)
        {
            if (_waypoints.Count == 0) return pos;
            float rangeSq = range * range;
            for (int i = 0; i < _waypoints.Count; i++)
            {
                if (Vector2.DistanceSquared(_waypoints[i], pos) <= rangeSq)
                {
                    return _waypoints[i];
                }
            }
            return FindClosest(pos);
        }

        /// <summary>
        /// Returns the position along the path at the given cumulative <paramref name="distance"/>
        /// from the path origin. Clamps to first/last waypoint outside [0, total-length].
        /// </summary>
        public Vector2 GetPositionAtDistance(float distance)
        {
            if (_waypoints.Count == 0) return Vector2.Zero;
            if (distance <= 0f) return _waypoints[0];
            float remaining = distance;
            for (int i = 1; i < _waypoints.Count; i++)
            {
                Vector2 segment = _waypoints[i] - _waypoints[i - 1];
                float segLen = segment.Length();
                if (segLen >= remaining)
                {
                    return _waypoints[i - 1] + segment / segLen * remaining;
                }
                remaining -= segLen;
            }
            return _waypoints[_waypoints.Count - 1];
        }

        public Vector2 GetNextPointFromLocation(Vector2 pos)
        {
            int closest = FindClosestIndex(pos);
            if (closest < 0) return pos;
            int next = closest + 1;
            return next < _waypoints.Count ? _waypoints[next] : _waypoints[closest];
        }

        public Vector2 GetLastEndPosition()
        {
            return _waypoints.Count == 0 ? Vector2.Zero : _waypoints[_waypoints.Count - 1];
        }

        /// <summary>
        /// Goal-side / general path-trim post-processing. Mirrors S4
        /// <c>NavigationMesh::TrimNavigationPath</c> (NavigationMesh.cpp:2683): drops
        /// waypoints that don't add meaningful path length, in two passes:
        ///
        /// <list type="bullet">
        /// <item>Co-location dedup: removes waypoints within <paramref name="coLocationEpsilon"/>
        /// distance of their immediate neighbor (cell-snap stairsteps).</item>
        /// <item>String-pulling (only when <paramref name="grid"/> supplied): removes intermediate
        /// waypoint <c>i+1</c> when <c>i</c> has direct line-of-sight to <c>i+2</c>. Iterates
        /// until stable or <paramref name="maxIterations"/> reached.</item>
        /// </list>
        ///
        /// Useful as post-process after A* produces extra cell-snap waypoints, after manual
        /// path mutations (Replace, Insert), or whenever a path needs cleanup before being
        /// shipped over the wire.
        /// </summary>
        public void TrimRedundantWaypoints(
            NavigationGrid grid = null,
            float radius = 0f,
            float coLocationEpsilon = 0.5f,
            int maxIterations = 100)
        {
            // Pass 1: co-location dedup. Walk forward, drop waypoints within epsilon of their
            // predecessor.
            float epsilonSq = coLocationEpsilon * coLocationEpsilon;
            int i = 0;
            while (i + 1 < _waypoints.Count)
            {
                if (Vector2.DistanceSquared(_waypoints[i], _waypoints[i + 1]) < epsilonSq)
                {
                    _waypoints.RemoveAt(i + 1);
                }
                else
                {
                    i++;
                }
            }

            if (grid == null) return;

            // Pass 2: string-pulling. Iterate until no more shortcuts found or cap hit.
            // Each inner pass walks the path forward once and drops any redundant middle.
            for (int iter = 0; iter < maxIterations; iter++)
            {
                bool changed = false;
                int j = 0;
                while (j + 2 < _waypoints.Count)
                {
                    // CastCircle returns TRUE on blocked (= LOS broken). Falsey = LOS clear.
                    // `translate` defaults to true — waypoints are world-coords; CastCircle
                    // does the world-to-grid translation for us.
                    if (!grid.CastCircle(_waypoints[j], _waypoints[j + 2], radius))
                    {
                        _waypoints.RemoveAt(j + 1);
                        changed = true;
                    }
                    else
                    {
                        j++;
                    }
                }
                if (!changed) break;
            }
        }

        /// <summary>
        /// Walks the path from <paramref name="startPoint"/> for up to
        /// <paramref name="maxDistance"/> world-units. Returns the position the unit ends
        /// up at — either where it collides with <paramref name="otherUnitPos"/>'s
        /// hit-circle (radius <paramref name="otherUnitCollisionRadius"/>), or the
        /// projected end-of-walk position if no collision occurs within travel budget.
        ///
        /// Mirrors S4 NavigationPath::GetCollisionPosition (NavigationPath.cpp:876).
        /// Use case: predict the landing of a forced movement (knockback, dash) when the
        /// unit may collide with another unit mid-flight.
        ///
        /// If <paramref name="otherUnitCollisionRadius"/> is &lt;= 0 the collision check is
        /// skipped — the function returns purely the projected end of walk.
        /// </summary>
        /// <param name="startPoint">Position the walker starts from. Need not be on a waypoint.</param>
        /// <param name="maxDistance">Maximum travel distance.</param>
        /// <param name="otherUnitPos">Position of the unit to test collision against.</param>
        /// <param name="otherUnitCollisionRadius">Hit-circle radius of <paramref name="otherUnitPos"/>.</param>
        /// <returns>Collision-entry point if collision, else the position after walking <paramref name="maxDistance"/> along the path, else the path's last waypoint if path is shorter than <paramref name="maxDistance"/>.</returns>
        public Vector2 GetCollisionPosition(
            Vector2 startPoint,
            float maxDistance,
            Vector2 otherUnitPos,
            float otherUnitCollisionRadius)
        {
            if (_waypoints.Count == 0 || maxDistance <= 0f)
            {
                return startPoint;
            }

            float radiusSq = otherUnitCollisionRadius > 0f
                ? otherUnitCollisionRadius * otherUnitCollisionRadius
                : 0f;
            bool checkCollision = radiusSq > 0f;

            float remaining = maxDistance;
            Vector2 segStart = startPoint;

            for (int i = 0; i < _waypoints.Count; i++)
            {
                Vector2 segEnd = _waypoints[i];
                Vector2 seg = segEnd - segStart;
                float segLen = seg.Length();
                if (segLen < 1e-5f)
                {
                    segStart = segEnd;
                    continue;
                }
                Vector2 segDir = seg / segLen;
                float walkOnSeg = MathF.Min(segLen, remaining);

                // Collision check (only if the walked portion of this segment passes near
                // otherUnit). Project otherUnit onto the segment, clamp to walked range,
                // then test if the resulting closest point is within the unit's radius.
                if (checkCollision)
                {
                    float t = Vector2.Dot(otherUnitPos - segStart, segDir);
                    if (t >= 0f && t <= walkOnSeg)
                    {
                        Vector2 closestOnSeg = segStart + segDir * t;
                        float perpDistSq = Vector2.DistanceSquared(otherUnitPos, closestOnSeg);
                        if (perpDistSq <= radiusSq)
                        {
                            // Walker enters the circle. Entry point is the closest-on-seg
                            // position back-tracked by sqrt(radius² - perp²) along segDir.
                            float backtrack = MathF.Sqrt(radiusSq - perpDistSq);
                            float entryT = MathF.Max(0f, t - backtrack);
                            return segStart + segDir * entryT;
                        }
                    }
                }

                if (segLen >= remaining)
                {
                    return segStart + segDir * remaining;
                }
                remaining -= segLen;
                segStart = segEnd;
            }

            // Walked entire path without exhausting distance budget — return path end
            return _waypoints[_waypoints.Count - 1];
        }

        // IEnumerable / IReadOnlyList

        public IEnumerator<Vector2> GetEnumerator() => _waypoints.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _waypoints.GetEnumerator();
    }
}
