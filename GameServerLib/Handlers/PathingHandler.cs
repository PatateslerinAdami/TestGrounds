using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.Content.Navigation;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Handlers
{
    /// <summary>
    /// Class which calls path based functions for GameObjects.
    /// </summary>
    public class PathingHandler
    {
        // Polling interval — safety net for path invalidations not covered by the
        // OnDynamicBlockerChanged event (e.g. PathfindingRadius changes from buffs).
        // Can be aggressive because UpdatePaths early-exits on a clean ValidateLineOfSight,
        // so a poll over an unblocked path is just N cheap CastCircle checks.
        private const float PathUpdateIntervalMs = 500f;

        private MapScriptHandler _map;
        private readonly List<AttackableUnit> _pathfinders = new List<AttackableUnit>();
        private float pathUpdateTimer;

        // Cells whose walkability changed since the last tick. Filled by the
        // OnDynamicBlockerChanged event handler; on the next tick the loop only revalidates
        // pathfinders whose path bounding box overlaps these cells (instead of every
        // pathfinder), which keeps the cost proportional to the change rather than to the
        // total pathfinder count.
        private readonly HashSet<int> _dirtyCells = new HashSet<int>();

        public PathingHandler(MapScriptHandler map)
        {
            _map = map;
            // Subscribe so dynamic blocker add/remove events drive immediate re-validation
            // instead of waiting for the next 500ms poll. Anivia walls / Trundle pillars /
            // turret respawns invalidate paths within one tick of their world change.
            _map.NavigationGrid.OnDynamicBlockerChanged += OnBlockerCellsChanged;
        }

        private void OnBlockerCellsChanged(IReadOnlyList<int> changedCells)
        {
            for (int i = 0; i < changedCells.Count; i++)
            {
                _dirtyCells.Add(changedCells[i]);
            }
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
            pathUpdateTimer += diff;
            bool intervalElapsed = pathUpdateTimer >= PathUpdateIntervalMs;
            bool hasDirty = _dirtyCells.Count > 0;

            if (!intervalElapsed && !hasDirty)
            {
                return;
            }

            // Defensive copy — UpdatePaths can mutate _pathfinders (e.g. via OnRemoved
            // reactions that deregister a unit).
            var objectsCopy = new List<AttackableUnit>(_pathfinders);

            if (intervalElapsed)
            {
                // Poll tick: walk every pathfinder. Catches invalidations not surfaced by
                // OnDynamicBlockerChanged (PathfindingRadius changes etc.). UpdatePaths
                // early-exits cheaply when ValidateLineOfSight is clean, so this is fine.
                foreach (var obj in objectsCopy)
                {
                    UpdatePaths(obj);
                }
                pathUpdateTimer = 0;
                _dirtyCells.Clear();
            }
            else
            {
                // Event tick: only revalidate pathfinders whose path bounding box overlaps
                // the dirty area. The vast majority of paths are not crossing the affected
                // cells and are skipped without even touching their waypoints.
                ComputeDirtyCellsBoundingBox(out short cxMin, out short cyMin, out short cxMax, out short cyMax);
                foreach (var obj in objectsCopy)
                {
                    if (PathBoundingBoxIntersectsDirtyArea(obj, cxMin, cyMin, cxMax, cyMax))
                    {
                        UpdatePaths(obj);
                    }
                }
                _dirtyCells.Clear();
            }
        }

        // Bounding box of all dirty cells in grid coordinates. Caller must ensure
        // _dirtyCells.Count > 0 before calling.
        private void ComputeDirtyCellsBoundingBox(out short cxMin, out short cyMin, out short cxMax, out short cyMax)
        {
            cxMin = short.MaxValue;
            cyMin = short.MaxValue;
            cxMax = short.MinValue;
            cyMax = short.MinValue;

            var cells = _map.NavigationGrid.Cells;
            foreach (var id in _dirtyCells)
            {
                if ((uint)id >= (uint)cells.Length)
                {
                    continue;
                }
                var loc = cells[id].Locator;
                if (loc.X < cxMin) cxMin = loc.X;
                if (loc.X > cxMax) cxMax = loc.X;
                if (loc.Y < cyMin) cyMin = loc.Y;
                if (loc.Y > cyMax) cyMax = loc.Y;
            }
        }

        // Conservative pre-filter: does the unit's remaining path bounding box overlap
        // the dirty area (expanded by PathfindingRadius)? False positives are harmless
        // (UpdatePaths just runs an extra cheap walkability pass and returns); false
        // negatives would miss a needed re-route, so we err on the permissive side.
        private bool PathBoundingBoxIntersectsDirtyArea(AttackableUnit obj, short cxMin, short cyMin, short cxMax, short cyMax)
        {
            if (obj.IsPathEnded())
            {
                return false;
            }

            var nav = _map.NavigationGrid;
            float radiusInCells = obj.PathfindingRadius / nav.CellSize;
            int rExpand = (int)Math.Ceiling(radiusInCells);

            short cxMinExp = (short)(cxMin - rExpand);
            short cxMaxExp = (short)(cxMax + rExpand);
            short cyMinExp = (short)(cyMin - rExpand);
            short cyMaxExp = (short)(cyMax + rExpand);

            // Build the path bounding box from current position + remaining waypoints,
            // all in cell coordinates.
            var posNav = nav.TranslateToNavGrid(obj.Position);
            short pcxMin = (short)posNav.X, pcxMax = pcxMin;
            short pcyMin = (short)posNav.Y, pcyMax = pcyMin;

            var path = obj.Waypoints;
            for (int i = obj.CurrentWaypointKey; i < path.Count; i++)
            {
                var wpNav = nav.TranslateToNavGrid(path[i]);
                short wcx = (short)wpNav.X;
                short wcy = (short)wpNav.Y;
                if (wcx < pcxMin) pcxMin = wcx;
                if (wcx > pcxMax) pcxMax = wcx;
                if (wcy < pcyMin) pcyMin = wcy;
                if (wcy > pcyMax) pcyMax = wcy;
            }

            return pcxMax >= cxMinExp && pcxMin <= cxMaxExp
                && pcyMax >= cyMinExp && pcyMin <= cyMaxExp;
        }

        /// <summary>
        /// Updates pathing for the specified object.
        /// </summary>
        /// <param name="obj">GameObject to check for incorrect paths.</param>
        public void UpdatePaths(AttackableUnit obj)
        {
            // Bug fix: without this early return, the CurrentWaypoint access further down
            // can crash with IndexOutOfRange once a unit has finished its path
            // (CurrentWaypointKey >= Waypoints.Count). IsPathEnded() covers both the
            // path.Count==0 case and the consumed-path case.
            if (obj.IsPathEnded())
            {
                return;
            }

            var path = obj.Waypoints;
            var lastWaypoint = path[path.Count - 1];
            if (lastWaypoint.Equals(obj.Position))
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

            // Bug fix: previously the foreach iterated over *all* waypoints, including ones
            // the unit had already consumed. SetWaypoints resets CurrentWaypointKey to 1,
            // which would have made the unit walk back to already-traversed waypoints.
            // Now we only iterate from the current index forward, so the new path picks up
            // exactly where the old one left off.
            for (int i = obj.CurrentWaypointKey; i < path.Count; i++)
            {
                if (!IsWalkable(path[i], obj.PathfindingRadius))
                {
                    break;
                }
                newPath.AddWaypoint(path[i]);
            }

            // If nothing walkable remains (the very next waypoint is blocked), don't apply
            // the new path — that would just leave the unit standing still. OnCollision push
            // handles the local dodging; the next UpdatePaths tick will retry.
            if (newPath.Count > 1)
            {
                obj.SetWaypoints(newPath);
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
            // Per-champion engagement multiplier from CharData (4.20 client per-character
            // ChasingAttackRangePercent). ADCs typically 0.8 (lands at 80% of range), bruisers
            // 0.5, melee carries 0.3 (close to the target). Falls back to the legacy 0.95
            // global inset when no CharData is available (non-ObjAIBase units like turrets).
            float inset = (attacker as ObjAIBase)?.CharData?.ChasingAttackRangePercent ?? DefaultStandInset;
            return GetAttackStandPosition(attacker.Position, target.Position, effectiveRange, inset);
        }

        /// <summary>
        /// Pure-geometry overload exposed as a static so unit tests can exercise the math
        /// without constructing AttackableUnit instances. <paramref name="insetMultiplier"/>
        /// defaults to <see cref="DefaultStandInset"/> (the global 0.95 buffer); pass a smaller
        /// value to push the stand-position deeper inside attack range.
        /// </summary>
        public static Vector2 GetAttackStandPosition(Vector2 attackerPos, Vector2 targetPos, float effectiveRange, float insetMultiplier = DefaultStandInset)
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
            return targetPos + dir * (effectiveRange * insetMultiplier);
        }

        /// <summary>
        /// Default fraction of effective range used by <see cref="GetAttackStandPosition"/>
        /// when no per-character override (CharData.ChasingAttackRangePercent) is available
        /// — non-ObjAIBase units like turrets, or unit tests. Mirrors the client's S4:4275
        /// 0.95 multiplier.
        ///
        /// Tracking (per-frame chase smoothing in <see cref="AttackableUnit.UpdateTracking"/>)
        /// MUST use the same effective inset (per-champion or this default) when overriding
        /// the path's last waypoint — otherwise the orbit point would sit at a different
        /// distance than the path destination, producing an attack/chase jitter loop:
        /// AI tick sees in-range -> tries to AA -> StopMovement -> ClearTracking -> target
        /// drifts a hair -> AI tick sees out-of-range -> repath -> tracking pulls back onto
        /// boundary -> repeat. Two-tier hysteresis (TargetInAttackRange vs
        /// TargetInCancelAttackRange in 4.20 Hero.lua) is the proper structural fix.
        /// </summary>
        public const float DefaultStandInset = 0.95f;

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
