using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.Content.Navigation;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Logging;
using Buildings = LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;

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

        // Proactive path-revalidation cadence. Was a single 3000ms timer that revalidated (and
        // potentially A*-rerouted) EVERY pathfinder in one frame → coarse (3s to notice a path
        // blocked by a new wall/turret) and a periodic lag spike. Now spread round-robin so each
        // pathfinder is revalidated ~once per window with the per-frame cost bounded. ~1s keeps the
        // proactive backstop responsive; the reactive collision/stuck repath still handles contact
        // within ~200ms.
        private const float RevalidateWindowMs = 1000f;
        private int _revalidateCursor;

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
            int count = _pathfinders.Count;
            if (count == 0)
            {
                return;
            }

            // Round-robin: process enough pathfinders this frame to cover the whole set every
            // RevalidateWindowMs, instead of all-at-once every 3s. Bounds are re-checked each
            // iteration so a mid-loop add/remove (UpdatePaths -> SetWaypoints) can't index out of
            // range; an occasional skip/repeat across the cursor wrap is harmless for revalidation.
            int budget = (int)Math.Ceiling(count * diff / RevalidateWindowMs);
            if (budget < 1) budget = 1;
            if (budget > count) budget = count;

            for (int n = 0; n < budget; n++)
            {
                if (_pathfinders.Count == 0)
                {
                    break;
                }
                if (_revalidateCursor >= _pathfinders.Count)
                {
                    _revalidateCursor = 0;
                }
                var obj = _pathfinders[_revalidateCursor];
                _revalidateCursor++;
                UpdatePaths(obj);
            }
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
            // Cell-based (radius 0) for consistency with GetPath's cell-based corridors —
            // validating cell-built paths with the unit radius would re-flag every wall-hugging
            // path as blocked and churn rebuilds each 3s batch.
            if (path.ValidateLineOfSight(_map.NavigationGrid, 0f))
            {
                return;
            }

            // The corridor is blocked (new turret / dynamic blocker / wall placed across the path
            // since it was computed). REROUTE to the original goal — full actor-aware A* around the
            // obstacle (matches Riot, which rebuilds the path) — instead of truncating at the first
            // blocked waypoint and stopping short of where the unit was ordered to go.
            var goal = path[path.Count - 1];
            var rerouted = GetPath(obj, goal);
            if (rerouted != null && rerouted.Count >= 2)
            {
                obj.SetWaypoints(rerouted, pathReason: "reroute");
                return;
            }

            // Fallback: no route to the goal exists (fully walled off). Truncate at the first blocked
            // waypoint so the unit at least advances cleanly to the last reachable point rather than
            // marching into the blocker; the reactive stuck/collision layer takes over from there.
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

            obj.SetWaypoints(newPath, pathReason: "reroute-trunc");
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
        /// (minions = fast, champions/pets = slower-accurate). Also threads an actor-aware path filter
        /// so the search routes around other units that would block this attacker. The returned
        /// <see cref="NavigationPath.IsPartial"/> flag signals that the goal was unreachable
        /// and the path lands at the closest reachable cell.
        /// </summary>
        public NavigationPath GetPath(AttackableUnit obj, Vector2 target, bool usePathingRadius = true,
            bool skipLineOfSight = false, float ignoreTargetRadius = -1f)
        {
            // Non-AI actors keep the Actor ctor default (flag false = fast, Actor.cpp:1465).
            bool useFastPath = obj is not ObjAIBase ai || ai.UsesFastPath;
            float radius = usePathingRadius ? obj.PathfindingRadius : 0f;
            var actorBlocked = BuildActorBlockedPredicate(obj);
            return _map.NavigationGrid.GetPath(obj.Position, target, radius, useFastPath, actorBlocked,
                ENABLE_MOVEMENT_HINT_PENALTY ? ComputeMovementHint(obj) : null, skipLineOfSight, ignoreTargetRadius);
        }

        // DISABLED 2026-06-07 (in-game regression): penalizing the cell directly ahead by +100
        // world units (~2 cell-steps) makes a straight walk and a one-cell sidestep nearly
        // equal-cost — successive repaths (stuck recovery, RefreshWaypoints) flip-flop between
        // them, producing zigzag/slow-walk/repath feedback loops after unit contact (user-
        // observed: champion + minion both crawling and re-pathing after a head-on bump).
        // The port's client-fidelity is also questionable: the client's GetNextTargetLocator
        // has an `x != 0 AND z != 0` gate, so for AXIS-ALIGNED movement it adds the raw
        // (tiny, per-frame) movement vector instead of a full cell offset — i.e. it usually
        // marks the unit's OWN cell (harmless, start is closed) and only hits the true
        // ahead-cell on diagonal movement. Re-enable only after verifying the real client
        // behavior end-to-end (what m_Movement contains at hint time + measured path shapes).
        private const bool ENABLE_MOVEMENT_HINT_PENALTY = false;

        /// <summary>
        /// Server equivalent of the client's <c>Actor_Common::GetNextTargetLocator</c>
        /// (S4 Actor.cpp:2569): the position one cell-size ahead of the unit along its current
        /// movement direction, or the unit's own position when idle. Feeds the A* hint-cell
        /// arrival penalty (see NavigationGrid.GetPath). The client offsets by
        /// normalize(m_Movement) * (cellSize + 1); our movement direction is the vector toward
        /// the current waypoint. Currently UNUSED — see <see cref="ENABLE_MOVEMENT_HINT_PENALTY"/>.
        /// </summary>
        private Vector2? ComputeMovementHint(AttackableUnit obj)
        {
            if (obj.IsPathEnded())
            {
                return obj.Position;
            }

            Vector2 dir = obj.CurrentWaypoint - obj.Position;
            float lenSq = dir.LengthSquared();
            if (lenSq < 0.0001f)
            {
                return obj.Position;
            }

            float scale = (_map.NavigationGrid.CellSize + 1f) / MathF.Sqrt(lenSq);
            return obj.Position + dir * scale;
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
        ///   - NO team filter (corrected 2026-06-18, new mac decomp): Riot's server A* probes
        ///     actor-blocking with a ZEROED collisionState (mTeamID=0), so a non-ghosted actor
        ///     blocks regardless of team — a unit routes AROUND allied bodies too. Lane-wave
        ///     clumping is preserved instead by the start-proximity exemption (below) + the
        ///     farFromOrigin / near-goal exemptions in <see cref="NavigationGrid.GetPath"/>.
        ///     (The old "same-team units don't block" note described a removed team filter.)
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
            // 0.95 inset matches S4:4275 generic-inset mirror. (The JSON field
            // `ChasingAttackRangePercent` is NOT a substitute for this constant: verified
            // vestigial — no consumer in any decomp/Lua corpus — and a trial wiring into the
            // engage range was falsified in-game 2026-07-15 (Yi-vs-turret standoff). See memory
            // `project_chardata_chasing_postattack_loaded.md`.)
            const float STAND_INSET = 0.95f;
            return targetPos + dir * (effectiveRange * STAND_INSET);
        }

        /// <summary>
        /// F2 Phase 2: full path-based attack-approach recommendation — the client's
        /// <c>Actor_Common::GetClosestAttackPoint</c> (S4 Actor.cpp:2812-2852 prediction wrapper)
        /// + <c>NavGrid::GetClosestAttackPoint</c> (NavigationGrid.cpp:2397-2554), the C++
        /// backend of the Lua AI API <c>SetStateAndCloseToTarget</c>. Flow:
        ///   1. Project the target forward along ITS path (differential-speed prediction, capped
        ///      at 300u; sanity fallback to the raw position when the projection lands closer
        ///      than 50% of the current gap to the attacker).
        ///   2. Snap the projected position to the nearest get-to-able cell, path to it
        ///      (actor-aware A*), and scan the waypoints for the FIRST one within
        ///      <paramref name="effectiveRange"/> of the target.
        ///   3. Recommend the waypoint TWO past that entry point (capped at the first waypoint
        ///      within the close radius max(radii sum, 15)) — the unit pushes slightly into
        ///      range instead of stopping on the boundary. Beeline shortcut: when walking the
        ///      path costs much more than the crow distance to the snapped end
        ///      ((0.6*travel)² > beeline²), recommend the snapped end directly.
        ///
        /// <paramref name="effectiveRange"/> is center-to-center (include both collision radii
        /// for AAs — same convention as <see cref="GetAttackStandPosition"/>).
        /// `closingAttackRangeModifier` = ar_ClosingAttackRangeModifier, a CVar defaulting to 0
        /// in code (Actor.cpp:155) but set to 300 by Map1's Constants.var:16 — found 2026-06-07
        /// in the playable-client level data. With 300, the close-walk radius
        /// max(radiiSum − 300, 15) collapses to the 15u floor, i.e. the close scan runs to the
        /// waypoint that practically touches the target.
        ///
        /// Residual divergence (documented): the client threads ignoreTargetRadius
        /// (= effectiveRange − radii − halfCell) into the A* so actor-blocking is bypassed
        /// ANYWHERE within attack range of the goal; our GetPath's near-goal exemption is a
        /// fixed 2-cell radius. Ranged attackers may route around crowds the client would
        /// path straight through — thread the parameter if that shows up in testing.
        /// </summary>
        /// <returns>true when a recommendation was produced; false when no path exists.</returns>
        public bool GetClosestAttackPoint(AttackableUnit attacker, AttackableUnit target, float effectiveRange,
            out Vector2 recommendedAttackPos, out bool canReachCloseSpot, out float travelDistanceNeededBeforeTargetable)
        {
            recommendedAttackPos = target.Position;
            canReachCloseSpot = false;
            travelDistanceNeededBeforeTargetable = float.MaxValue;

            // --- 1. Target-movement prediction (Actor.cpp:2816-2850) ---
            Vector2 projected = target.Position;
            float attackerSpeed = attacker.GetMoveSpeed();
            if (!target.IsPathEnded() && attackerSpeed > 0f)
            {
                float targetSpeed = target.GetMoveSpeed();
                float distFromUnit = Vector2.Distance(attacker.Position, target.Position)
                    - attacker.CollisionRadius - target.CollisionRadius;
                if (targetSpeed > 0f && distFromUnit > 0f)
                {
                    float differential = targetSpeed / (attackerSpeed * 0.95f);
                    float distToTrack = Math.Min(differential * distFromUnit * 1.5f, 300f);
                    projected = GetPathCollisionPosition(target, distToTrack, attacker.Position);
                    // Projection landing closer than half the current gap means the target is
                    // pathing TOWARD us — chase its real position instead (Actor.cpp:2843-2847).
                    if (Vector2.Distance(attacker.Position, projected) / distFromUnit < 0.5f)
                    {
                        projected = target.Position;
                    }
                }
            }

            // --- 2. Snap + path (NavigationGrid.cpp:2417-2447) ---
            float halfCell = _map.NavigationGrid.CellSize * 0.5f;
            float closeRadius = attacker.CollisionRadius + target.CollisionRadius;
            float ignoreTargetRadius = effectiveRange - closeRadius - halfCell;

            Vector2 end = SetToNearestGetToAbleCell(attacker, projected,
                attacker.PathfindingRadius, ignoreTargetRadius, effectiveRange);

            // Decomp ignoreTargetRadiusForBuild (NavGrid::GetClosestAttackPoint): if the get-to-able
            // snap RELOCATED the target cell (it was occupied — which is the always-case when attacking
            // a unit, since the enemy sits on it), the path build runs with FULL actor-blocking near the
            // goal (ignoreTargetRadius = 0). That makes co-attacking minions route AROUND each other to
            // DISTINCT stand cells instead of all pathing onto the same in-range spot (the real Riot
            // attacker-spread mechanism — measured: our casters spent 7.1% of time stacked <20u vs Riot
            // 2.8%, because we previously always used the fixed near-goal exemption here). Only when the
            // target cell was NOT relocated do we keep the in-range exemption (= ignoreTargetRadius).
            var endCell = _map.NavigationGrid.TranslateToNavGrid(end);
            var projCell = _map.NavigationGrid.TranslateToNavGrid(projected);
            bool endRelocated = (short)endCell.X != (short)projCell.X || (short)endCell.Y != (short)projCell.Y;
            float ignoreTargetRadiusForBuild = endRelocated ? 0f : ignoreTargetRadius;

            var path = GetPath(attacker, end, ignoreTargetRadius: ignoreTargetRadiusForBuild);
            if (path == null || path.Count < 2)
            {
                return false;
            }

            // --- 3. Waypoint scans (NavigationGrid.cpp:2453-2545) ---
            float rangeSq = effectiveRange * effectiveRange;
            int firstInRange = -1;
            for (int i = 1; i < path.Count; i++)
            {
                if (Vector2.DistanceSquared(projected, path[i]) < rangeSq)
                {
                    firstInRange = i;
                    break;
                }
            }

            if (firstInRange < 0)
            {
                // No waypoint enters range (far / partial path): walk to the path end.
                // travelDistance = 0 is the client literal here (NavigationGrid.cpp:2468-2472).
                // CommitStand: this exit previously bypassed the stand-cell reservation — two
                // far attackers of the same target got the same path end and fused on arrival.
                recommendedAttackPos = CommitStand(attacker, path[path.Count - 1]);
                travelDistanceNeededBeforeTargetable = 0f;
                canReachCloseSpot = true;
                return true;
            }

            float travel = 0f;
            for (int i = 1; i <= firstInRange; i++)
            {
                travel += Vector2.Distance(path[i - 1], path[i]);
            }
            travelDistanceNeededBeforeTargetable = travel;

            // Beeline shortcut (NavGrid::GetClosestAttackPoint, NavigationGrid.cpp:2395-2447): when
            // even 60% of the walk to the first in-range waypoint exceeds the crow distance from the
            // attacker to the snapped end cell, the path is a long detour — recommend the snapped
            // end directly and skip the stand-point commit (the client returns the end-cell center
            // and sets canReachCloseSpot there).
            float beelineSq = Vector2.DistanceSquared(attacker.Position, end);
            if (travel * 0.6f * (travel * 0.6f) > beelineSq)
            {
                // LaneMinion: the shortcut fires PREFERENTIALLY in clumps (the relocated end forces
                // full actor-blocking → the path curves around the bodies → travel ≫ beeline), and
                // it would return here WITHOUT the stand-cell reservation handshake below — two
                // simultaneous deciders both got the same snapped `end` and fused (wire104
                // incidents, 2026-07-03). If another unit has reserved within body distance of
                // `end`, fall through to the stand-point walk + clearance spiral instead (which
                // resolves against reservations); otherwise take the shortcut and reserve it.
                bool reservedConflict = attacker is LaneMinion
                    && HasReservationConflict(attacker, end, attacker.PathfindingRadius * 2f);
                if (!reservedConflict)
                {
                    recommendedAttackPos = end;
                    canReachCloseSpot = true;
                    if (attacker is LaneMinion)
                    {
                        _standReservations[attacker] = end;
                    }
                    return true;
                }
            }

            // Stand point = commit ~2 cells INTO attack range: the first path waypoint within
            // (effectiveRange − ~2 cells) of the target, floored at the close radius. DISTANCE-based,
            // NOT "firstInRange + 2 waypoints": our A* path is sparse (corner waypoints), so +2 indices
            // overshoots toward the target and pulled RANGED units into melee. The decomp does
            // firstInRange+2 at PER-CELL granularity (NavigationGrid.cpp:2531) — i.e. the same ~2-cell
            // commit; this reproduces it independent of our waypoint spacing.
            float commit = 2f * _map.NavigationGrid.CellSize;
            float standDist = Math.Max(effectiveRange - commit, closeRadius);
            // Walk the path and find where it first crosses INTO standDist of the target, INTERPOLATING
            // within the segment. The A* path is sparse (corner waypoints) — a straight [start,target]
            // beeline has no vertex at the stand distance, so picking a vertex collapsed ranged units
            // onto the target (melee). The interpolated crossing puts the unit exactly standDist away.
            recommendedAttackPos = path[path.Count - 1];
            for (int i = 0; i < path.Count - 1; i++)
            {
                float da = Vector2.Distance(projected, path[i]);
                if (da <= standDist)
                {
                    // Already at/inside the stand distance at this vertex — don't advance further in.
                    recommendedAttackPos = path[i];
                    break;
                }
                float db = Vector2.Distance(projected, path[i + 1]);
                if (db <= standDist)
                {
                    float t = (da - standDist) / Math.Max(da - db, 1e-3f);
                    recommendedAttackPos = Vector2.Lerp(path[i], path[i + 1], Math.Clamp(t, 0f, 1f));
                    break;
                }
            }

            // --- 4. Near-side stand-cell divergence ---
            // clash8 proved the faithful HasStuckActor path port does NOT un-stack a tight clash:
            // Riot's own start-proximity + near-goal pathing exemptions deliberately clear actor-
            // blocking in the clash zone, so A* paths stay straight there (reroute=0). The wave
            // spread therefore comes from distinct attack-stand CELLS — each attacker stands on a
            // different free cell around the enemy. If the natural stand point above is free we keep
            // it; otherwise spiral to the nearest free (actor-unblocked) cell — but CONSTRAINED to
            // the attacker's HEMISPHERE of the target so we never relocate a unit through or behind
            // the wave (the failure of the earlier unconstrained spiral, clash7: melee teleported
            // across the wave + dropped their target). The spiral already prefers the cell closest
            // to THIS attacker, so co-attacking units fan onto distinct near-side cells.
            //
            // NOTE 2026-06-28: tried REMOVING this whole block (it is an invention vs the decomp's
            // single pre-path end-cell spiral) to kill a small "snap toward an ally on arrival" — but
            // it REGRESSED HARD in-game (attacking minions converge on one stand point → stack → the
            // actor-aware forward push then routes the stacked group around each other → paths cross,
            // glitch, teleport, + A* cost spike → lag). So this spiral is LOAD-BEARING anti-stacking,
            // not pure decoration. Reverted. The proper fix for the arrival snap is to compute the
            // stand cell ONCE per (re)acquire and CACHE it (not per chase needRepath), so it stops
            // shifting with live ally motion WITHOUT losing the spread — a bigger change, deferred.
            Vector2 fromTargetToAttacker = attacker.Position - projected;
            // Band max sits HALF A CELL inside the attack range, not AT it: the chase→settle handoff
            // in RefreshWaypoints settles only when the arrived unit is within idealRange (== this
            // effectiveRange). A cell exactly at the range edge fails that check after grid
            // quantization, so IsPathEnded() keeps re-triggering needRepath → a re-path flood that
            // the client renders as teleporting. Keeping the cell inside the range guarantees the
            // unit settles on arrival and goes quiet (matches Minion.lua: attack in place, no re-path).
            float standBandMax = Math.Max(effectiveRange - halfCell, closeRadius + halfCell);
            float standBandMaxSq = standBandMax * standBandMax;
            float standBandMinSq = closeRadius * closeRadius;

            // CLEARANCE snapshot (lane minions only, 2026-06-21): capture nearby allied minions so the
            // stand-cell spiral keeps a body-gap from them. Without this the spiral accepts any FREE
            // cell — including one right next to an already-occupied cell — so the wave stands shoulder
            // to shoulder ("still paths to the side where a minion already is" / "fan out more"). With
            // it, each attacker's cell must clear the others, so they settle on the OPEN side with
            // spacing. One query; the per-candidate test below is cheap distance checks. Falls back
            // gracefully (natural stand point) if the band is too dense to find a clear cell. Scoped to
            // lane minions so champions/pets don't avoid standing near friendly minions.
            bool spreadFromAllies = attacker is LaneMinion;
            var allyClear = new List<Vector2>();
            if (spreadFromAllies)
            {
                float snapR = effectiveRange + _map.NavigationGrid.CellSize;
                foreach (var o in _map.CollisionHandler.GetNearestObjects(
                             new System.Activities.Presentation.View.Circle(projected, snapR)))
                {
                    if (o is AttackableUnit au && au != attacker && au.Team == attacker.Team
                        && au is Minion && !au.IsDead
                        && !au.Status.HasFlag(StatusFlags.Ghosted) && !au.IsTemporarilyGhosted)
                    {
                        allyClear.Add(au.Position);
                    }
                }

                // STAND-CELL RESERVATIONS (2026-07-03, the "two wizards fuse" fix): the spiral's
                // clearance test above only sees allies' CURRENT positions — when a shared target
                // dies, co-attacking minions re-acquire the same next target in the SAME tick and
                // each spiral run sees the other still standing at its OLD spot, so both commit
                // the SAME stand cell (captured pair: identical attack paths chord=232/clr=41 at
                // t=190.5s, then d=0.0 lockstep for 16s — once fused, every later decision is
                // identical and NO separation mechanism fires on their n=2 paths). Treating other
                // units' freshly COMMITTED cells as occupied closes the race: the second decider
                // sees the first one's reservation and spirals to a distinct cell. Reservations
                // self-expire (dead / removed / wandered >600u from the reserved cell).
                foreach (var kv in _standReservations)
                {
                    var owner = kv.Key;
                    if (owner == attacker) continue;
                    if (IsReservationStale(owner, kv.Value))
                    {
                        _staleReservations.Add(owner);
                        continue;
                    }
                    if (owner.Team == attacker.Team
                        && Vector2.DistanceSquared(kv.Value, projected) <= snapR * snapR)
                    {
                        allyClear.Add(kv.Value);
                    }
                }
                foreach (var stale in _staleReservations)
                {
                    _standReservations.Remove(stale);
                }
                _staleReservations.Clear();
            }
            // Body diameter + one cell gap → a visible "bit more" fan beyond just non-overlap. Tunable.
            float fullClearance = attacker.PathfindingRadius * 4f + _map.NavigationGrid.CellSize;

            // GRACEFUL-DEGRADE (2026-06-29): try progressively smaller ally-clearance levels and keep
            // the cell with the MOST clearance found. The single-level spiral fell back to the
            // CONVERGING natural point whenever no cell achieved the full body-gap — exactly the dense
            // co-located case (a whole wave re-acquiring the same target while stacked) → clr≈3 hard
            // stacks. Relaxing the target gap (140→84→49u) lets the spiral find a less-overlapping
            // reachable cell instead of giving up onto the pile. No faithful Riot mechanism exists for
            // the co-located case (Riot avoids co-location upstream via continuous client-side
            // separation, which we can't reproduce), so this is the pragmatic mitigation.
            Vector2 naturalStand = recommendedAttackPos;
            Vector2 bestStand = naturalStand;
            // Seed BELOW any real clearance (not naturalStand's own clearance): naturalStand is the
            // RAW, un-relocated cell — it must never win the comparison against a get-to-able-snapped
            // candidate. With no nearby allies (every champion, and lane minions in the open) allyClear
            // is empty so MinAllyDistSq == MaxValue for all cells; seeding bestClearSq at MaxValue made
            // the relocated candidate fail `> bestClearSq` and we returned the un-snapped raw cell —
            // i.e. the get-to-able relocation was silently skipped, so the approach path aimed at a cell
            // that wasn't necessarily directly reachable (the "from some angles not the shortest path"
            // champion regression). Seeding at -1 lets the first relocated candidate always take over,
            // restoring the committed single-call behavior in the no-ally case.
            float bestClearSq = -1f;
            foreach (float frac in StandClearanceFracs)
            {
                float curClearSq = (fullClearance * frac) * (fullClearance * frac);
                Vector2 cand = SetToNearestGetToAbleCell(
                    attacker, naturalStand, attacker.PathfindingRadius,
                    cellAccept: c =>
                    {
                        // Reject the far hemisphere (behind the target relative to the attacker).
                        if (Vector2.Dot(c - projected, fromTargetToAttacker) < 0f)
                        {
                            return false;
                        }
                        // Keep the cell inside the usable attack band (in range, off the footprint).
                        float dT = Vector2.DistanceSquared(c, projected);
                        if (dT < standBandMinSq || dT > standBandMaxSq)
                        {
                            return false;
                        }
                        return MinAllyDistSq(c, allyClear) >= curClearSq;
                    },
                    // Prefer the free cell ALONG the attacker's approach line, not merely the nearest:
                    // the last/most-constrained minion then sidesteps minimally onto its OWN side past
                    // its leaders, instead of crossing the formation to a closer sideways cell.
                    preferOnAxis: true);
                float candClearSq = MinAllyDistSq(cand, allyClear);
                if (candClearSq > bestClearSq)
                {
                    bestClearSq = candClearSq;
                    bestStand = cand;
                }
                // Satisfied this clearance level → good enough, stop relaxing.
                if (candClearSq >= curClearSq)
                {
                    break;
                }
            }
            recommendedAttackPos = bestStand;
            canReachCloseSpot = true;
            if (spreadFromAllies)
            {
                // Commit = reserve (see the reservation block above). Overwritten on every
                // re-acquire/chase-repath, so it always reflects the unit's live intent.
                _standReservations[attacker] = bestStand;
            }
            return true;
        }

        // Live attack-stand commitments of lane minions (unit -> committed stand cell), consulted
        // by the clearance spiral so SIMULTANEOUS deciders pick distinct cells (see the
        // reservation block in GetClosestAttackPoint). Pruned lazily during consultation.
        private readonly Dictionary<AttackableUnit, Vector2> _standReservations = new Dictionary<AttackableUnit, Vector2>();
        private readonly List<AttackableUnit> _staleReservations = new List<AttackableUnit>();

        /// <summary>
        /// True when another live reservation sits within <paramref name="dist"/> of
        /// <paramref name="pos"/>. Prunes stale entries as it scans — see
        /// <see cref="IsReservationStale"/> for the abandonment rule.
        /// </summary>
        private bool HasReservationConflict(AttackableUnit attacker, Vector2 pos, float dist)
        {
            bool conflict = false;
            foreach (var kv in _standReservations)
            {
                var owner = kv.Key;
                if (owner == attacker) continue;
                if (IsReservationStale(owner, kv.Value))
                {
                    _staleReservations.Add(owner);
                    continue;
                }
                if (!conflict && Vector2.DistanceSquared(kv.Value, pos) < dist * dist)
                {
                    conflict = true;
                }
            }
            foreach (var stale in _staleReservations)
            {
                _standReservations.Remove(stale);
            }
            _staleReservations.Clear();
            return conflict;
        }

        /// <summary>
        /// Abandonment rule for a stand/approach reservation. Dead / removed owners always prune.
        /// The distance leg REQUIRES the owner's path to be over: the original ">600u from the
        /// cell" prune alone silently nullified every FAR commitment — the turret-cap approach
        /// target is committed while the minion is still 1000u+ down the lane (tt121: the cap fix
        /// did nothing, wave siblings fused pairwise on the raw tower center again, because every
        /// consult pruned the walker's reservation as "wandered off"). A unit still WALKING
        /// (path not ended) keeps its claim however far away it is; only a unit that finished its
        /// path somewhere else (idle / re-decided) has genuinely abandoned the cell.
        /// </summary>
        private static bool IsReservationStale(AttackableUnit owner, Vector2 cell)
        {
            if (owner.IsDead || owner.IsToRemove())
            {
                return true;
            }
            return owner.IsPathEnded()
                && Vector2.DistanceSquared(owner.Position, cell) > 600f * 600f;
        }

        /// <summary>
        /// Distinct-cell guarantee for EVERY lane-minion stand decision (2026-07-04): all exits
        /// that produce a stand position — beeline shortcut, firstInRange&lt;0 path-end, the
        /// clearance spiral, AND the geometric GetAttackStandPosition fallback in ObjAIBase —
        /// must end up body-distinct, or two simultaneous deciders fuse (identical inputs →
        /// identical decisions forever; nothing separates an n=2 fused pair). If
        /// <paramref name="pos"/> collides with another unit's live reservation, relocate to the
        /// nearest get-to-able cell clear of ALL reservations; then reserve. Champions and
        /// non-lane units pass through untouched.
        /// </summary>
        public Vector2 CommitStand(AttackableUnit attacker, Vector2 pos,
            Func<Vector2, bool> cellAccept = null)
        {
            if (attacker is not LaneMinion)
            {
                return pos;
            }
            float bodyDist = attacker.PathfindingRadius * 2f;
            // F9 (2026-07-19): callers may constrain the relocation (e.g. the turret-cap
            // approach requires the committed cell to stay within the minion's acquisition
            // range of the tower). The constraint participates in the spiral itself — before,
            // an out-of-range ring cell was clamped POST-HOC to the raw shared center, which
            // (a) let several crowded minions fall back onto the identical coordinate (the
            // exact tt120 fusion this handshake exists to prevent) and (b) left the reservation
            // pointing at the unused ring cell, blocking siblings for nothing.
            bool needRelocate = HasReservationConflict(attacker, pos, bodyDist)
                || (cellAccept != null && !cellAccept(pos));
            if (needRelocate)
            {
                pos = SetToNearestGetToAbleCell(attacker, pos, attacker.PathfindingRadius,
                    cellAccept: c => (cellAccept == null || cellAccept(c))
                        && !HasReservationConflict(attacker, c, bodyDist),
                    preferOnAxis: true);
            }
            _standReservations[attacker] = pos;
            return pos;
        }

        /// <summary>
        /// True while <paramref name="attacker"/>'s own live stand reservation still backs
        /// <paramref name="pos"/> (same committed cell). Lets a caller that CACHES a committed
        /// stand target (the turret-cap approach) detect that intervening decisions — a combat
        /// stand commit overwrites the single per-unit slot — invalidated the claim, so it
        /// re-runs the handshake instead of resuming toward an unreserved cell that siblings
        /// may since have claimed (F9, 2026-07-19).
        /// </summary>
        public bool HasOwnStandReservation(AttackableUnit attacker, Vector2 pos)
        {
            return _standReservations.TryGetValue(attacker, out var reserved)
                && Vector2.DistanceSquared(reserved, pos) < 1f;
        }

        // Graceful-degrade clearance levels (fractions of the full body-gap) for the attack-stand
        // spiral: try the full gap first, then relax so a dense co-located cluster still gets the
        // least-overlapping reachable cell instead of falling back to the converging natural point.
        private static readonly float[] StandClearanceFracs = { 1.0f, 0.6f, 0.35f };

        /// <summary>Min squared distance from <paramref name="p"/> to any position in
        /// <paramref name="others"/>; <see cref="float.MaxValue"/> if the list is empty.</summary>
        private static float MinAllyDistSq(Vector2 p, List<Vector2> others)
        {
            float best = float.MaxValue;
            for (int i = 0; i < others.Count; i++)
            {
                float d = Vector2.DistanceSquared(p, others[i]);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// Port of <c>NavigationPath::GetCollisionPosition</c> (S4 NavigationPath.cpp:~570):
        /// walks <paramref name="unit"/>'s remaining path up to <paramref name="distanceToTravel"/>
        /// and returns where it will be — cut short at the point of closest approach to
        /// <paramref name="otherUnitPos"/> when the path passes near it (the client's literal
        /// heuristic: traveled-distance² &gt; point-to-segment distance², approach param &lt; 1).
        /// Used by the attack-approach prediction to aim at where a fleeing target WILL be.
        /// </summary>
        private static Vector2 GetPathCollisionPosition(AttackableUnit unit, float distanceToTravel, Vector2 otherUnitPos)
        {
            var waypoints = unit.Waypoints;
            int i = unit.CurrentWaypointKey;
            if (i >= waypoints.Count)
            {
                return waypoints.Count > 0 ? waypoints[waypoints.Count - 1] : unit.Position;
            }

            Vector2 lastPoint = unit.Position;
            float totalDist = 0f;
            while (true)
            {
                Vector2 wp = waypoints[i];
                float prevDist = totalDist;
                totalDist += Vector2.Distance(lastPoint, wp);

                if (totalDist > distanceToTravel)
                {
                    // Final partial segment: check close approach, else walk distanceRemaining in.
                    float distSq = DistPointSegmentSq(otherUnitPos, lastPoint, wp, out float s);
                    if (prevDist * prevDist > distSq && s > 0f)
                    {
                        return Vector2.Lerp(lastPoint, wp, s);
                    }
                    float segLen = Vector2.Distance(lastPoint, wp);
                    float remaining = Math.Max(distanceToTravel - prevDist, 0f);
                    return segLen > 1e-4f
                        ? lastPoint + (wp - lastPoint) * (remaining / segLen)
                        : wp;
                }

                float dSq = DistPointSegmentSq(otherUnitPos, lastPoint, wp, out float t);
                if (totalDist * totalDist > dSq && t > 0f)
                {
                    return Vector2.Lerp(lastPoint, wp, t);
                }

                lastPoint = wp;
                i++;
                if (i >= waypoints.Count)
                {
                    return waypoints[waypoints.Count - 1];
                }
            }
        }

        /// <summary>
        /// Squared distance from <paramref name="p"/> to segment AB; <paramref name="t"/> is the
        /// closest-approach parameter along A→B in [0,1]. (Client's DistPoint3DLine3DSq measures
        /// its param from B toward A; ours is A→B, with the `t &gt; 0` guard mirroring the
        /// client's `intersection &lt; 1`.)
        /// </summary>
        private static float DistPointSegmentSq(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 1e-6f)
            {
                t = 0f;
                return Vector2.DistanceSquared(p, a);
            }
            t = Math.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
            return Vector2.DistanceSquared(p, a + ab * t);
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
            // Temp-ghost (stuck-recovery mIgnoreCollisions) gets the same treatment.
            if (unit.Status.HasFlag(StatusFlags.Ghosted) || unit.IsTemporarilyGhosted)
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
            float targetRadius = 0,
            Func<Vector2, bool> cellAccept = null,
            bool preferOnAxis = false)
        {
            if (unit == null)
            {
                return _map.NavigationGrid.SetToNearestGetToAbleCell(target, Vector2.Zero, null, radius, ignoreTargetRadius, targetRadius, cellAccept, preferOnAxis);
            }
            if (unit.Status.HasFlag(StatusFlags.Ghosted) || unit.IsTemporarilyGhosted)
            {
                return target;
            }
            var pred = BuildActorBlockedPredicate(unit);
            return _map.NavigationGrid.SetToNearestGetToAbleCell(target, unit.Position, pred, radius, ignoreTargetRadius, targetRadius, cellAccept, preferOnAxis);
        }

        // Public since 2026-07-04: HandleMove threads the predicate through champion move-order
        // validation + re-A* (the Riot-faithful actor-aware near-window — GridLineOfSightTest2
        // checks HasStuckActor within the first 1500u, NavigationGrid.cpp:673-681).
        public NavigationGrid.ActorBlockedPredicate BuildActorBlockedPredicate(AttackableUnit attacker)
        {
            if (attacker == null)
            {
                return null;
            }
            // A ghosted attacker mirrors the client's mIgnoreCollisions short-circuit at S1:6556
            // the entire HasStuckActor is no-op'd. Cheap to detect once at build time.
            if (attacker.Status.HasFlag(StatusFlags.Ghosted) || attacker.IsTemporarilyGhosted)
            {
                return null;
            }

            var collision = _map.CollisionHandler;
            float pathRadius = attacker.PathfindingRadius;
            float halfCell = _map.NavigationGrid.CellSize * 0.5f;
            // radiusTestField = max(-1.8*halfCell + radius, 15) — HasStuckActor's dontUseRadius=false
            // branch (NavigationGrid.cpp:3537). This is the actor-sizing radius the cell-occupancy
            // tests are built around.
            float effRadius = Math.Max(15f, pathRadius - halfCell * 1.8f);
            float searchRadius = pathRadius * 2f; // ForeachInRadius(center, radius + radius)

            // The path START is fixed for this request (decomp: startPos = inActor->Position() at the
            // HasStuckActor call site). Captured once so the start-proximity exemption below can tell
            // which blockers are part of the unit's own departure-clump.
            Vector2 startPos = attacker.Position;
            // travelDistFactor = arrivalCost / maxSpeed (=1 if speed 0). Captured divisor.
            float maxSpeed = attacker.GetMoveSpeed();
            float divisor = maxSpeed > 0f ? maxSpeed : 1f;
            // minionIncreaseSize = !IsSlowerButMoreAccurateSearch() = UsesFastPath: 2.0 for
            // minions/monsters (fast class), 1.0 for champions/pets (slower-accurate) — the decomp
            // param name is literal (NavigationGrid.cpp:292 passes !isSlower; :3592 isSlower != 1).
            bool fastPath = (attacker as ObjAIBase)?.UsesFastPath ?? true;

            // NO team filter (2026-06-18, new mac decomp): Riot's server A* probes actor-blocking with
            // a ZEROED collisionState (mTeamID=0), and a non-ghosted actor blocks regardless of team —
            // so a unit routes AROUND allied bodies, not through them. Lane-wave clumping is preserved
            // by the farFromOrigin exemption in NavigationGrid.GetPath + the start-proximity exemption.
            return (cellCenterWorld, _, arrivalCost) =>
            {
                var nearby = collision.GetNearestObjects(
                    new System.Activities.Presentation.View.Circle(cellCenterWorld, searchRadius));

                // Reachability mode (Riot HasBlockedActor, signalled by arrivalCost < 0): a plain
                // current-position block with no motion prediction. Used by CheckIsGetToAble /
                // SetToNearestGetToAbleCell — "can I stand here right now", not "route around traffic".
                if (arrivalCost < 0f)
                {
                    for (int i = 0; i < nearby.Count; i++)
                    {
                        var other = nearby[i];
                        if (other == attacker || other.IsToRemove()) continue;
                        // Dead units neither collide nor block pathing: Riot removes the actor from
                        // the nav mesh on death (Actor_Common::HandleDeath → RemoveNavigationActor).
                        // The per-tick responders (CollectHardColliders) already filter IsDead; without
                        // this a dead-but-not-yet-removed unit still blocked A* route-finding.
                        if (other is AttackableUnit ru && (ru.IsDead || ru.Status.HasFlag(StatusFlags.Ghosted) || ru.IsTemporarilyGhosted)) continue;
                        // Neighbour pathing footprint = mRadius = PathfindingRadius (NOT the gameplay
                        // CollisionRadius), matching the attacker's effRadius which is derived from
                        // attacker.PathfindingRadius.
                        float combined = other.PathfindingRadius + effRadius;
                        if (Vector2.DistanceSquared(other.Position, cellCenterWorld) < combined * combined)
                        {
                            return true;
                        }
                    }
                    return false;
                }

                // HasStuckActor mode (A* expansion, NavigationGrid.cpp:3526). sizeMultiplier is a
                // PER-CELL crowd accumulator: each near-but-not-blocking actor bumps it +0.12, which
                // enlarges the effective footprint the next actors are tested against, so dense clumps
                // push the route out a little further (Riot's anti-stacking nudge).
                float travelDistFactor = arrivalCost / divisor;
                float sizeMultiplier = fastPath ? 2f : 1f;
                float rTF = effRadius;
                for (int i = 0; i < nearby.Count; i++)
                {
                    var other = nearby[i];
                    if (other == attacker || other.IsToRemove()) continue;
                    // Dead units don't block pathing (see the reachability-mode filter above).
                    if (other is AttackableUnit ou && (ou.IsDead || ou.Status.HasFlag(StatusFlags.Ghosted) || ou.IsTemporarilyGhosted)) continue;

                    // Neighbour pathing footprint = mRadius = PathfindingRadius (NOT gameplay CollisionRadius).
                    float aR = other.PathfindingRadius;
                    Vector2 op = other.Position;
                    float thr = (rTF + aR) * (rTF + aR) * sizeMultiplier;

                    // A moving actor blocks where it WILL be (predicted = pos + velocity*arrivalTime);
                    // a stationary/stuck one blocks where it IS. Velocity is reconstructed from the
                    // heading to its current waypoint × move speed (= decomp Movement()/GetLastTimeDelta()).
                    bool moving = other is AttackableUnit mu && !mu.IsPathEnded() && mu.GetMoveSpeed() > 0f;
                    if (moving)
                    {
                        if (travelDistFactor <= 0f) continue; // decomp: no lookahead → ignore movers
                        var mv = (AttackableUnit)other;
                        Vector2 toWp = mv.CurrentWaypoint - op;
                        Vector2 vel = toWp.LengthSquared() > 1e-6f
                            ? Vector2.Normalize(toWp) * mv.GetMoveSpeed()
                            : Vector2.Zero;
                        Vector2 predicted = op + vel * travelDistFactor;
                        // decomp moving branch: predicted FAR → skip WITHOUT bumping; CLOSE → bump,
                        // fall through (opposite of the stuck branch, which bumps on FAR).
                        if (thr < Vector2.DistanceSquared(predicted, cellCenterWorld)) continue;
                        sizeMultiplier += 0.12f;
                    }
                    else
                    {
                        if (Vector2.DistanceSquared(op, cellCenterWorld) >= thr) { sizeMultiplier += 0.12f; continue; }
                    }

                    // Start-proximity directional exemption (NavigationGrid.cpp:3584-3600): a blocker
                    // that is ALSO close to the path's start is part of the clump we're leaving behind
                    // — the two dot-product guards (which together cover every sign) drop it so the
                    // route doesn't balloon around our own origin pack. THIS is the piece the earlier
                    // partial port lacked, which let A* route minions to the far side of the wave.
                    float startProx = aR * 0.95f + rTF;
                    if (Vector2.DistanceSquared(op, startPos) <= startProx * startProx)
                    {
                        Vector2 toActor = op - cellCenterWorld;
                        Vector2 toStart = startPos - cellCenterWorld;
                        if (Vector2.Dot(toActor, toStart) <= 0f) continue;
                        Vector2 fromActor = cellCenterWorld - op;
                        if (Vector2.Dot(toStart, fromActor) <= 0f) continue;
                    }
                    return true; // cell is blocked by this actor
                }
                return false;
            };
        }
    }
}
