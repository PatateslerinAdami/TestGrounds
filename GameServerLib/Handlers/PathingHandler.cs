using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.Content.Navigation;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Logging;

namespace LeagueSandbox.GameServer.Handlers
{
    /// <summary>
    /// Class which calls path based functions for GameObjects.
    /// </summary>
    public class PathingHandler
    {
        private MapScriptHandler _map;
        private readonly List<AttackableUnit> _pathfinders = new List<AttackableUnit>();
        private float pathUpdateTimer;

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
            return _pathfinders.Remove(obj);
        }

        /// <summary>
        /// Function called every tick of the game by Map.cs.
        /// </summary>
        public void Update(float diff)
        {
            // TODO: Verify if this is the proper time between path updates.
            if (pathUpdateTimer >= 3000.0f)
            {
                // we iterate over a copy of _pathfinders because the original gets modified
                var objectsCopy = new List<AttackableUnit>(_pathfinders);
                foreach (var obj in objectsCopy)
                {
                    UpdatePaths(obj);
                }

                pathUpdateTimer = 0;
            }

            pathUpdateTimer += diff;
        }

        /// <summary>
        /// Updates pathing for the specified object.
        /// </summary>
        /// <param name="obj">GameObject to check for incorrect paths.</param>
        public void UpdatePaths(AttackableUnit obj)
        {
            // Runs for every pathfinder in the 3s batch (see Update). The ValidateLineOfSight fast
            // path usually returns without an A*, but a corridor blockage triggers an IsWalkable
            // sweep + waypoint rebuild. Scoped to confirm the periodic batch isn't itself the
            // "lag spike every few seconds" the dev observed.
            using var _scope = Profiler.Scope("PathingHandler.UpdatePaths", "pathing");

            var path = obj.Waypoints;
            if (path.Count == 0)
            {
                return;
            }

            var lastWaypoint = path[path.Count - 1];
            if (obj.CurrentWaypoint.Equals(lastWaypoint) && lastWaypoint.Equals(obj.Position))
            {
                return;
            }

            // Fast path: when the corridor between every consecutive waypoint pair is still
            // clear at the unit's pathfinding radius, no rebuild is needed. This catches the
            // case the per-waypoint loop below misses so a new blocker (e.g., a freshly built
            // turret, a respawned inhibitor) sitting between two still-walkable waypoints.
            // Big win in the common case: no allocation, no A*.
            if (path.ValidateLineOfSight(_map.NavigationGrid, obj.PathfindingRadius))
            {
                return;
            }

            var newPath = new NavigationPath(path.Count);
            newPath.AddWaypoint(obj.Position);

            foreach (Vector2 waypoint in path)
            {
                if (IsWalkable(waypoint, obj.PathfindingRadius))
                {
                    newPath.AddWaypoint(waypoint);
                }
                else
                {
                    break;
                }
            }

            obj.SetWaypoints(newPath);
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
        /// Returns a path to the given target position from the given unit's position. Uses the
        /// unit's <see cref="ObjAIBase.UsesFastPath"/> setting to pick the client A* mode
        /// (champions = fast, minions = slow-accurate). Also threads an actor-aware path filter
        /// so the search routes around other units that would block this attacker. The returned
        /// <see cref="NavigationPath.IsPartial"/> flag signals that the goal was unreachable
        /// and the path lands at the closest reachable cell.
        /// </summary>
        public NavigationPath GetPath(AttackableUnit obj, Vector2 target, bool usePathingRadius = true)
        {
            bool useFastPath = obj is ObjAIBase ai && ai.UsesFastPath;
            float radius = usePathingRadius ? obj.PathfindingRadius : 0f;
            var actorBlocked = BuildActorBlockedPredicate(obj);
            return _map.NavigationGrid.GetPath(obj.Position, target, radius, useFastPath, actorBlocked);
        }

        /// <summary>
        /// Returns a path to the given target position from the given start position. No attacker
        /// is supplied, so actor-aware blocking is disabled (terrain-only pathing).
        /// </summary>
        public NavigationPath GetPath(Vector2 start, Vector2 target, float checkRadius = 0, bool useFastPath = false)
        {
            return _map.NavigationGrid.GetPath(start, target, checkRadius, useFastPath);
        }

        /// <summary>
        /// Builds the actor-blocked predicate (A1) for the supplied attacker. The closure captures
        /// the live <see cref="CollisionHandler"/> and the attacker's collision state so the A*
        /// expansion can short-circuit cells occupied by units this attacker would collide with.
        ///
        /// Mirrors the client's HasStuckActor (S1:6533-6588): a fixed search radius of 2*pathRadius
        /// is used to fetch QuadTree candidates, and each candidate is tested against an effective
        /// radius of <c>max(15, pathRadius - 0.9*cellSize)</c> matching S4:7989-7991 / S1:6510-6513.
        /// The 15-unit floor is the client's <c>minRadius</c> constant — keeps small actors honest.
        ///
        /// Filter rules:
        ///   - Self is excluded (no self-blocking).
        ///   - Removed objects skipped (they're about to vanish).
        ///   - Ghosted units pass through everything (Wukong stealth, etc.) —> both as
        ///     the attacker (whole predicate becomes false) and as a candidate (skipped).
        ///   - Same-team units don't block path expansion: the client's <c>Actor_Common::shouldCollide</c>
        ///     filters allies out of the pathfinding query (post-process push handles overlap),
        ///     otherwise a champion would route around its own minion wave instead of walking
        ///     through it. Cross-team units block.
        /// </summary>
        /// <summary>
        /// Computes a stand-position for <paramref name="attacker"/> from which it can hit
        /// <paramref name="target"/> within <paramref name="effectiveRange"/>. The result is on
        /// the line between attacker and target, at <c>effectiveRange * 0.95</c> from the target
        /// (mirrors the client's 0.95 multiplier at S4:4275 -> small inset so the unit lands
        /// definitively in range rather than on the boundary, which would race against target
        /// movement). F2 Phase 1.
        ///
        /// This is the geometric core of the client's `Actor_Common::GetClosestAttackPoint` (S4
        /// NavGrid.cpp:3706) which is the C++ backend of the Lua API `SetStateAndCloseToTarget`
        /// used by every AI script (Minion.lua, Hero.lua, BaronMinionAI.lua, Aggro.lua, Pet AIs)
        /// to "approach a target to attack range".
        ///
        /// <paramref name="effectiveRange"/> should already include both units' collision radii
        /// for auto-attacks (= attacker.Range + attacker.CollisionRadius + target.CollisionRadius
        /// for edge-to-edge engagement) — caller's <c>idealRange</c> variable typically has this.
        /// For spell casts where range is center-to-center, pass the spell's cast range without
        /// the radius sum.
        ///
        /// Caller should still snap to a walkable cell via <see cref="NavigationGrid.GetClosestTerrainExit"/>
        /// before pathing —> the geometric position may land inside terrain.
        /// </summary>
        public Vector2 GetAttackStandPosition(AttackableUnit attacker, AttackableUnit target, float effectiveRange)
        {
            return GetAttackStandPosition(attacker.Position, target.Position, effectiveRange);
        }

        /// <summary>
        /// Pure-geometry overload an exposed static so unit tests can exercise the math without
        /// constructing AttackableUnit instances.
        /// </summary>
        public static Vector2 GetAttackStandPosition(Vector2 attackerPos, Vector2 targetPos, float effectiveRange)
        {
            Vector2 toAttacker = attackerPos - targetPos;
            float distSq = toAttacker.LengthSquared();
            if (distSq < 1e-4f)
            {
                // Standing on top of target — direction undefined. Caller's per-tick range check
                // will already trigger the attack from this position; just return attacker's pos.
                return attackerPos;
            }
            Vector2 dir = toAttacker / MathF.Sqrt(distSq);
            const float STAND_INSET = 0.95f;
            return targetPos + dir * (effectiveRange * STAND_INSET);
        }

        /// <summary>
        /// Local-window reachability check (A2 — S1:9069 / S4:1694). Returns true if <paramref name="to"/>
        /// is reachable from <paramref name="unit"/>'s position within ~4 cells, considering both
        /// terrain and actor blocking via the same predicate as <see cref="GetPath"/>. Ghosted
        /// attackers short-circuit to true (mirrors S1:9131 mIgnoreCollisions). Use as a cheap
        /// pre-check before committing to a full <see cref="GetPath"/> when the target is
        /// known-close (auto-attack viability, melee dash validation).
        /// </summary>
        public bool CheckIsGetToAble(AttackableUnit unit, Vector2 to,
            float radius = 0,
            float ignoreTargetRadius = 0)
        {
            if (unit == null)
            {
                return _map.NavigationGrid.CheckIsGetToAble(Vector2.Zero, to, null, radius, ignoreTargetRadius);
            }
            // S1:9131 short-circuit -> ghosted unit ignores all collision and is trivially "able to get there".
            if (unit.Status.HasFlag(StatusFlags.Ghosted))
            {
                return true;
            }
            var pred = BuildActorBlockedPredicate(unit);
            return _map.NavigationGrid.CheckIsGetToAble(unit.Position, to, pred, radius, ignoreTargetRadius);
        }

        /// <summary>
        /// Spiral-snap helper (A2 — S1:9686). Returns the requested <paramref name="target"/> if
        /// the unit can locally reach it; otherwise spirals outward and returns the nearest cell
        /// that's locally reachable. Useful for "snap movement target to a nearby walkable+reachable
        /// cell" when the user-requested destination is occluded. Falls back to the original
        /// <paramref name="target"/> if no reachable cell is found within the spiral cap.
        /// </summary>
        public Vector2 SetToNearestGetToAbleCell(AttackableUnit unit, Vector2 target,
            float radius = 0,
            float ignoreTargetRadius = 0,
            float targetRadius = 0)
        {
            if (unit == null)
            {
                return _map.NavigationGrid.SetToNearestGetToAbleCell(target, Vector2.Zero, null, radius, ignoreTargetRadius, targetRadius);
            }
            if (unit.Status.HasFlag(StatusFlags.Ghosted))
            {
                return target;
            }
            var pred = BuildActorBlockedPredicate(unit);
            return _map.NavigationGrid.SetToNearestGetToAbleCell(target, unit.Position, pred, radius, ignoreTargetRadius, targetRadius);
        }

        private NavigationGrid.ActorBlockedPredicate BuildActorBlockedPredicate(AttackableUnit attacker)
        {
            if (attacker == null)
            {
                return null;
            }
            // A ghosted attacker mirrors the client's mIgnoreCollisions short-circuit at S1:6556
            // the entire HasStuckActor is no-op'd. Cheap to detect once at build time.
            if (attacker.Status.HasFlag(StatusFlags.Ghosted))
            {
                return null;
            }

            var collision = _map.CollisionHandler;
            float pathRadius = attacker.PathfindingRadius;
            float halfCell = _map.NavigationGrid.CellSize * 0.5f;
            float effRadius = Math.Max(15f, pathRadius - halfCell * 1.8f);
            float searchRadius = pathRadius * 2f;
            var attackerTeam = attacker.Team;

            return (cellCenterWorld, _) =>
            {
                var nearby = collision.GetNearestObjects(
                    new System.Activities.Presentation.View.Circle(cellCenterWorld, searchRadius));
                for (int i = 0; i < nearby.Count; i++)
                {
                    var other = nearby[i];
                    if (other == attacker) continue;
                    if (other.IsToRemove()) continue;
                    if (other is AttackableUnit otherUnit)
                    {
                        if (otherUnit.Status.HasFlag(StatusFlags.Ghosted)) continue;
                    }
                    if (other.Team == attackerTeam) continue;

                    float combined = other.CollisionRadius + effRadius;
                    if (Vector2.DistanceSquared(other.Position, cellCenterWorld) < combined * combined)
                    {
                        return true;
                    }
                }
                return false;
            };
        }
    }
}
