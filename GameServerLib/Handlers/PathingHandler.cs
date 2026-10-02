using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Handlers
{
    /// <summary>
    /// Class which calls path based functions for GameObjects.
    /// </summary>
    public class PathingHandler
    {
        private MapScriptHandler _map;
        private readonly List<AttackableUnit> _pathfinders = new List<AttackableUnit>();
        private double _pathCheckCredit;
        private int _pathCheckCursor;

        public PathingHandler(MapScriptHandler map)
        {
            _map = map;
        }

        /// <summary>
        /// Adds the specified GameObject to the list of GameObjects to check for pathfinding. *NOTE*: Will fail to fully add the GameObject if it is out of the map's bounds.
        /// </summary>
        /// <param name="obj">GameObject to add.</param>
        public void AddPathfinder(AttackableUnit obj)
        {
            _pathfinders.Add(obj);
        }

        /// <summary>
        /// GameObject to remove from the list of GameObjects to check for pathfinding.
        /// </summary>
        /// <param name="obj">GameObject to remove.</param>
        /// <returns>true if item is successfully removed; false otherwise.</returns>
        public bool RemovePathfinder(AttackableUnit obj)
        {
            int index = _pathfinders.IndexOf(obj);
            if (index < 0) return false;
            _pathfinders.RemoveAt(index);
            if (index < _pathCheckCursor) _pathCheckCursor--;
            if (_pathCheckCursor >= _pathfinders.Count) _pathCheckCursor = 0;
            return true;
        }

        /// <summary>
        /// Function called every tick of the game by Map.cs.
        /// </summary>
        public void Update(float diff)
        {
            if (!float.IsFinite(diff) || diff <= 0) return;
            int count = _pathfinders.Count;
            if (count == 0) { _pathCheckCredit = 0; _pathCheckCursor = 0; return; }
            // Same average three second sweep, staggered across frames. Cap
            // catch up work after a stall, immediate movement checks still run.
            _pathCheckCredit = Math.Min(count, _pathCheckCredit + count * (double)diff / 3000.0);
            int checks = Math.Min(4, (int)_pathCheckCredit);
            if (checks == 0) return;
            _pathCheckCredit -= checks;
            var batch = System.Buffers.ArrayPool<AttackableUnit>.Shared.Rent(checks);
            try
            {
                for (int i = 0; i < checks; i++)
                {
                    if (_pathCheckCursor >= count) _pathCheckCursor = 0;
                    batch[i] = _pathfinders[_pathCheckCursor++];
                }
                for (int i = 0; i < checks; i++)
                    if (!batch[i].IsToRemove() && !batch[i].IsDead) UpdatePaths(batch[i]);
            }
            finally { System.Buffers.ArrayPool<AttackableUnit>.Shared.Return(batch, clearArray: true); }

        }

        /// <summary>
        /// Updates pathing for the specified object.
        /// </summary>
        /// <param name="obj">GameObject to check for incorrect paths.</param>
        public void UpdatePaths(AttackableUnit obj)
        {
            if (obj.MovementParameters != null || obj.IsPathEnded()) return;
            var nav = _map.NavigationGrid;
            var start = obj.Position;
            for (int i = obj.CurrentWaypointKey; i < obj.Waypoints.Count; i++)
            {
                var next = obj.Waypoints[i];
                if (nav.CastCircle(start, next, obj.PathfindingRadius))
                {
                    if (obj is ObjAIBase actor)
                    {
                        actor.RepairCrowdPath();
                        return;
                    }
                    var repaired = nav.GetPath(obj.Position,
                        obj.Waypoints[obj.Waypoints.Count - 1], obj.PathfindingRadius);
                    if (repaired != null) obj.SetWaypoints(repaired);
                    // AI movement retains its goal and retries through the budgeted queue.
                    else if (obj is not ObjAIBase) obj.StopMovement();
                    return;
                }
                start = next;
            }
        }

        /// <summary>
        /// Checks if the given position can be pathed on.
        /// </summary>
        public bool IsWalkable(Vector2 pos, float radius = 0, bool checkObjects = false)
        {
            bool walkable = true;

            if (!_map.NavigationGrid.IsWalkable(pos, radius))
            {
                walkable = false;
            }

            if (checkObjects && _map.CollisionHandler.GetNearestObjects(new System.Activities.Presentation.View.Circle(pos, radius)).Count > 0)
            {
                walkable = false;
            }

            return walkable;
        }

        /// <summary>
        /// Returns a path to the given target position from the given unit's position.
        /// </summary>
        public List<Vector2> GetPath(AttackableUnit obj, Vector2 target, bool usePathingRadius = true)
        {
            if (usePathingRadius)
            {
                return GetPath(obj.Position, target, obj.PathfindingRadius);
            }
            return GetPath(obj.Position, target, 0);
        }

        // Occupancy is query local, never stamped into permanent terrain flags.
        // Starting overlaps may be exited, stopped actors block future travel.
        public List<Vector2> GetActorPath(ObjAIBase actor, Vector2 target, AttackableUnit ignoredTarget = null,
            bool allowPartial = false, float goalRadius = 0)
        {
            var neighbors = new List<ObjAIBase>();
            var start = actor.Position;
            // Occupancy is local. Long distance terrain routing does not scan every actor on the map.
            _map.CollisionHandler.GetAIUnitsInRadius(start, 1200, neighbors);
            // Eligibility is constant during this synchronous search. Evaluate
            // it once, not once for every A* edge and smoothing candidate.
            var blockers = new List<(Vector2 Position, float RadiusSquared, bool Overlapping)>();
            float actorRadius = MinColl.GetActorRadius(actor);
            foreach (var other in neighbors)
            {
                if (other == actor || other == ignoredTarget || other is BaseTurret || !MinColl.CanCollide(actor, other)) continue;
                if (!other.IsPathEnded() && other.CanMove()
                    && other.NavigationState.BlockedSeconds < .5f && !other.NavigationState.Waiting) continue;
                float radius = actorRadius + MinColl.GetActorRadius(other);
                var position = other.Position;
                blockers.Add((position, radius * radius,
                    Vector2.DistanceSquared(start, position) < radius * radius));
            }
            bool Clear(Vector2 a, Vector2 b)
            {
                var ab = b - a;
                float lengthSquared = ab.LengthSquared();
                foreach (var blocker in blockers)
                {
                    var p = blocker.Position;
                    if (blocker.Overlapping && Vector2.DistanceSquared(b, p) >= Vector2.DistanceSquared(a, p)) continue;
                    float t = lengthSquared > 0 ? Math.Clamp(Vector2.Dot(p - a, ab) / lengthSquared, 0, 1) : 0;
                    if (Vector2.DistanceSquared(p, a + ab * t) < blocker.RadiusSquared) return false;
                }
                return true;
            }
            return _map.NavigationGrid.GetPath(start, target, actor.PathfindingRadius, Clear,
                actor is Champion || actor is Pet ? 10000 : 800, allowPartial, goalRadius);
        }

        public List<Vector2> GetAttackApproach(ObjAIBase actor, AttackableUnit target, float attackRange)
        {
            var path = GetActorPath(actor, target.Position, target, allowPartial: true,
                goalRadius: Math.Max(1, attackRange - 5));
            if (path == null) return null;
            // Stop at the first reachable entry into attack range, instead of
            // reserving a fixed angular slot on the far side of the target.
            float radius = Math.Max(1, attackRange - 5);
            for (int i = 1; i < path.Count; i++)
            {
                var offset = path[i - 1] - target.Position;
                if (offset.LengthSquared() <= radius * radius)
                    return path.GetRange(0, i);
                var direction = path[i] - path[i - 1];
                float a = direction.LengthSquared();
                float b = Vector2.Dot(offset, direction);
                float discriminant = b * b - a * (offset.LengthSquared() - radius * radius);
                if (a <= 0 || discriminant < 0) continue;
                float t = (-b - MathF.Sqrt(discriminant)) / a;
                if (t < 0 || t > 1) continue;
                var result = path.GetRange(0, i);
                result.Add(path[i - 1] + direction * t);
                return result;
            }
            return path;
        }

        /// <summary>
        /// Returns a path to the given target position from the given start position.
        /// </summary>
        public List<Vector2> GetPath(Vector2 start, Vector2 target, float checkRadius = 0)
        {
            return _map.NavigationGrid.GetPath(start, target, checkRadius);
        }
    }
}
