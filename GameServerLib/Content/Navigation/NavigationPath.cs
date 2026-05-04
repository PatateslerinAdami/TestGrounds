using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;

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
        /// True when both paths have the same length and every corresponding waypoint is
        /// within <paramref name="epsilon"/> world units of its peer. Default epsilon is
        /// 0.5 — sub-cell tolerance, since A* output is cell-snapped to <c>CellSize</c>=25.
        /// </summary>
        public bool IsPathTheSame(NavigationPath other, float epsilon = 0.5f)
        {
            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (_waypoints.Count != other._waypoints.Count) return false;
            float epsSq = epsilon * epsilon;
            for (int i = 0; i < _waypoints.Count; i++)
            {
                if (Vector2.DistanceSquared(_waypoints[i], other._waypoints[i]) > epsSq)
                {
                    return false;
                }
            }
            return true;
        }

        public bool ComparePathClose(NavigationPath other, float epsilon) => IsPathTheSame(other, epsilon);

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
                // CastCircle returns true when BLOCKED, false when clear.
                if (grid.CastCircle(_waypoints[i - 1], _waypoints[i], radius))
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

        // IEnumerable / IReadOnlyList

        public IEnumerator<Vector2> GetEnumerator() => _waypoints.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _waypoints.GetEnumerator();
    }
}
