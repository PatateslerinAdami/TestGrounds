using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Enums;
using GameServerCore.Packets.Handlers;
using System.Numerics;
using System.Collections.Generic;
using LeagueSandbox.GameServer.Content.Navigation;
using LeagueSandbox.GameServer.Players;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.Logging;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleMove : PacketHandlerBase<MovementRequest>
    {
        private static readonly log4net.ILog _logger = LeagueSandbox.GameServer.Logging.LoggerProvider.GetLogger();
        private readonly Game _game;
        private readonly PlayerManager _playerManager;

        public HandleMove(Game game)
        {
            _game = game;
            _playerManager = game.PlayerManager;
        }

        public override bool HandlePacket(int userId, MovementRequest req)
        {
            // Per move-order (event-driven, on the network drain path). The pathing port added
            // stuck-recovery retries and clamp fallbacks here, each potentially an extra A*.
            // Scoped "pathing" so client-issued orders are distinguishable from per-tick A* in the
            // trace — a burst of orders shouldn't be mistaken for a simulation hotspot.
            using var _scope = Profiler.Scope("HandleMove", "pathing");

            var peerInfo = _playerManager.GetPeerInfo(userId);
            if (peerInfo == null || req == null)
            {
                return true;
            }

            var champion = peerInfo.Champion;
            if (champion.MovementParameters == null && champion.CanIssueMoveOrders())
            {
                var nav = _game.Map.NavigationGrid;

                var u = _game.ObjectManager.GetObjectById(req.TargetNetID) as AttackableUnit;
                var pet = champion.GetPet();
                NavigationPath waypoints;

                // NOTE on pet control: while a pet is alive the client mirrors the champion's plain move
                // orders with a duplicate carrying SenderNetID = the pet (a normal champion right-click
                // arrives as MoveTo sender=champ AND MoveTo sender=pet). That pet-sender duplicate is NOT
                // a command to relocate the pet — the pet just follows the owner — so we do NOT route it
                // to the pet (an earlier attempt remapped it to PetHardMove and made the pet teleport to
                // every click). The explicit pet commands are the PetHard* orders (alt-click pet bar),
                // routed to the pet by the IssueOrder block after the switch. Directing the pet via the
                // owner's R-guide is a separate client-side mechanism that does not come through here.
                switch (req.OrderType)
                {
                    case OrderType.MoveTo:
                    case OrderType.AttackTo:
                    case OrderType.AttackMove:
                    case OrderType.Use:
                    // Attack-terrain orders carry a ground position like a move; the AI's
                    // RefreshWaypoints already treats AttackTerrainOnce/Sustained as walk-to-coord
                    // states (ObjAIBase: targetPos = Waypoints.Last). Route them through the same
                    // path-building so they aren't silently dropped (was: no case → no-op).
                    case OrderType.AttackTerrainOnce:
                    case OrderType.AttackTerrainSustained:
                        if (req.Waypoints == null || req.Waypoints.Count == 0)
                        {
                            return false;
                        }

                        // Attack-move with a target already in acquisition range → attack it
                        // immediately, no walking toward the click first (Riot Hero.lua OnOrder
                        // ATTACKMOVE → FindTargetInAcR → SetStateAndCloseToTarget). No click
                        // destination is stored here, so there's no resume-to-point after the kill —
                        // the engine only AssignTargetPosInPos when NO target is found at order time.
                        if (req.OrderType == OrderType.AttackMove && u == null)
                        {
                            var acquired = champion.AcquireAttackMoveTarget();
                            if (acquired != null)
                            {
                                champion.AttackMoveDestination = System.Numerics.Vector2.Zero;
                                champion.UpdateMoveOrder(OrderType.AttackTo, true);
                                champion.SetTargetUnit(acquired);
                                // A fresh attack order must drop the stale settle/chase state and
                                // re-approach to ACTUAL attack range. Without this, re-attack-moving
                                // onto the SAME target you previously engaged leaves _attackSettledNetId
                                // == that target → RefreshWaypoints takes the settled "hold" branch and
                                // the champion stops ~ATTACK_SETTLE_HYSTERESIS (150u) SHORT of range,
                                // never closing in nor attacking ("attacks the first time, then never
                                // again after a second attack-move"). Mirrors the u != null path below.
                                champion.ForceChaseRepath();
                                break;
                            }
                        }

                        if (u != null)
                        {
                            // Ordered onto a specific unit → no a-move-to-point destination to resume.
                            champion.AttackMoveDestination = System.Numerics.Vector2.Zero;
                            champion.UpdateMoveOrder(req.OrderType, true);
                            champion.SetTargetUnit(u);
                            // A fresh attack order must re-path toward the target NOW, not finish the
                            // previous MoveTo path first. The chase re-path throttle would otherwise
                            // suppress the recompute (no path-end / no drift / same target NetID when
                            // re-attacking), so the champion kept walking the old waypoint while the
                            // target was out of range. Invalidate the throttle to force the recompute.
                            champion.ForceChaseRepath();
                        }
                        else
                        {
                            waypoints = new NavigationPath(req.Waypoints.ConvertAll(TranslateFromCenteredCoordinates));

                            // REMOVED (2026-06-07): the old "client-prediction smoothing" snapped
                            // the SERVER position to the client's claimed start (up to 150u). With
                            // the 96ms hero-streaming cadence the client structurally trails the
                            // server by ~one packet flight, so every click yanked the server BACK
                            // onto a stale client position — and the resulting full-path
                            // WaypointGroup then yanked the walking client back too (visible
                            // occasional snap/zigzag on plain click-to-move). Riot's server is
                            // authoritative over position: paths start at the SERVER position
                            // (waypoints.Replace(0, ...) below) and the 0x61 response resyncs the
                            // client — client drift is folded into the regular streamed
                            // corrections instead of being trusted.

                            // Upfront extraction: if the champion is currently on a NOT_PASSABLE
                            // cell (post-teleport, knockback-into-wall, or stale client-prediction
                            // snap that landed inside a building blocker before we added the
                            // walkability gate above), extract them to the nearest walkable cell
                            // BEFORE attempting path computation. Without this, every fallback
                            // below — including GetFirstWallHitPoint — sees an unwalkable origin
                            // and produces degenerate paths or no paths at all, leaving the unit
                            // permanently unable to move via subsequent move-orders. The unit is
                            // teleported a small distance to the nearest exit; this is a one-shot
                            // recovery, not a regular flow.
                            if (!nav.IsWalkable(champion.Position, 0f))
                            {
                                var escape = nav.GetClosestTerrainExit(champion.Position, 0f);
                                if (escape != champion.Position)
                                {
                                    champion.SetPosition(escape, repath: false);
                                }
                            }

                            waypoints.Replace(0, champion.Position);

                            for(int i = 0; i < waypoints.Count - 1; i++)
                            {
                                // CELL-BASED validation (radius 0), matching how the client computed
                                // this path and how GetPath / PathingHandler.UpdatePaths validate:
                                // the navgrid cells are pre-inflated (unit footprints baked in), so a
                                // radius-aware sweep here would re-flag wall-hugging client paths the
                                // client (and Riot) walk straight — producing a spurious A* detour
                                // (n>=3 wire shape, visible arc near walls) where Riot keeps the
                                // straight 2-point path. Only re-A* on genuine cell-level terrain block.
                                if(nav.CastCircle(waypoints[i], waypoints[i + 1], 0f, true))
                                {
                                    var ithWaypoint = waypoints[i];
                                    var lastWaypoint = waypoints[waypoints.Count - 1];
                                    var path = nav.GetPath(ithWaypoint, lastWaypoint, champion.PathfindingRadius, champion.UsesFastPath);

                                    // Stuck-recovery: if GetPath failed (champion is in or
                                    // immediately adjacent to a dynamic-blocker cell that even
                                    // Escape Mode can't path out of, e.g., Inhibitor-respawn or
                                    // wedge against wall), try snapping ithWaypoint to a walkable
                                    // cell and retrying. Without this fallback, the next branch
                                    // would land waypoints = [currentPos, currentPos] for i==0,
                                    // silently dropping every subsequent move order ("stuck"
                                    // bug Vayne/Katarina at Inhibitor/Nexus/walls).
                                    Vector2 snappedFrom = ithWaypoint;
                                    if (path == null)
                                    {
                                        snappedFrom = nav.GetClosestTerrainExit(ithWaypoint, 0f);
                                        if (snappedFrom != ithWaypoint)
                                        {
                                            path = nav.GetPath(snappedFrom, lastWaypoint, champion.PathfindingRadius, champion.UsesFastPath);
                                            if (path != null)
                                            {
                                                // Hard-snap champion to the unstuck position so
                                                // the path's first waypoint matches their actual
                                                // location. Without this they'd be left in the
                                                // blocker cell with a path starting elsewhere.
                                                champion.SetPosition(snappedFrom, repath: false);
                                            }
                                        }
                                    }

                                    // Second-tier snap-retry: champion's position may pass radius=0
                                    // walkability but fail the PathfindingRadius corridor check
                                    // (sitting right at a wall edge -> cell is walkable but cells
                                    // within PathfindingRadius are not). GetPath rejects expansion
                                    // from a non-radius-clear start, the radius=0 snap above doesn't
                                    // help (origin already walkable at 0), and the fallback below
                                    // would just clamp to a tiny wall-grazing distance — unit only
                                    // walks far when the click direction happens to be along the
                                    // wall edge (the "click opposite direction works, nothing else
                                    // does" Vayne-at-wall symptom). Snap to a PathfindingRadius-
                                    // clear cell so A* has a viable starting cell, then route from
                                    // there to the click destination — unit naturally walks out and
                                    // around to the click instead of stopping at the edge.
                                    if (path == null)
                                    {
                                        var snappedClear = nav.GetClosestTerrainExit(ithWaypoint, champion.PathfindingRadius);
                                        if (snappedClear != ithWaypoint && snappedClear != snappedFrom)
                                        {
                                            path = nav.GetPath(snappedClear, lastWaypoint, champion.PathfindingRadius, champion.UsesFastPath);
                                            if (path != null)
                                            {
                                                champion.SetPosition(snappedClear, repath: false);
                                            }
                                        }
                                    }

                                    // Degenerate-path guard: GetPath internally snaps the goal to
                                    // the nearest walkable cell (NavigationGrid.cs:532). When the
                                    // user clicks on a wall or into a building, that snap can
                                    // collapse the goal back into the same cell as the start —
                                    // GetPath then early-returns `[from, to]` (line 540) where the
                                    // two points are within one cell of each other. The unit
                                    // takes a sub-cell step and stops, presenting as "stuck near
                                    // terrain". When that happens AND the user's original intent
                                    // was meaningfully far, drop the snapped path and walk the
                                    // straight line — collision will stop the unit at the wall,
                                    // and the result reflects what the user clicked.
                                    if (path != null && path.Count == 2
                                        && Vector2.DistanceSquared(path[0], path[1]) < 50f * 50f
                                        && Vector2.DistanceSquared(ithWaypoint, lastWaypoint) > 200f * 200f)
                                    {
                                        path = null;
                                    }

                                    waypoints.RemoveRange(i, waypoints.Count - i);
                                    if (path != null)
                                    {
                                        waypoints.AddRange(path);
                                    }
                                    else
                                    {
                                        // Click landed inside a blocker (or path computation gave
                                        // a degenerate result). Clamp the destination to the last
                                        // walkable cell along the ray from champion → click. The
                                        // unit walks toward the click only as far as terrain allows
                                        // and stops cleanly at the boundary — no OnCollision-snap
                                        // drift loop, no walking-into-the-building. Champion is
                                        // guaranteed to be on walkable terrain at this point (the
                                        // upfront extraction earlier in HandleMove handles the
                                        // in-blocker case), so GetFirstWallHitPoint will produce
                                        // a meaningful clamp rather than returning origin.
                                        var clamped = nav.GetFirstWallHitPoint(champion.Position, lastWaypoint);
                                        // For i==0, RemoveRange above cleared the start waypoint. The
                                        // path-not-null branch implicitly restores it (path[0] equals
                                        // ithWaypoint = champion.Position), but the fallback only adds
                                        // the clamped destination. Re-add the start so SetWaypoints
                                        // accepts (it rejects count <= 1) — without this the unit can't
                                        // move at all when CastCircle is blocked but GetPath returns null
                                        // (= ray is walkable cell-by-cell but fails the PathfindingRadius
                                        // corridor check, common near building blocker boundaries).
                                        if (waypoints.Count == 0)
                                        {
                                            waypoints.AddWaypoint(champion.Position);
                                        }
                                        waypoints.AddWaypoint(clamped);
                                    }
                                    break;
                                }
                            }
                            // Degenerate-order guard (2026-06-07): clicking INTO an adjacent
                            // blocker (building footprint right next to the champion) ends up
                            // with the clamp fallback producing [pos, pos] — start == end.
                            // Broadcasting that as a full-path WaypointGroup makes the client
                            // hard-snap to Waypoint[0] and process a zero-length path (one-frame
                            // visible hitch). No walkable progress exists for this order, so
                            // treat it as a no-op: keep the current path/state, send nothing.
                            bool degenerateOrder = waypoints.Count <= 1
                                || (waypoints.Count == 2
                                    && Vector2.DistanceSquared(waypoints[0], waypoints[waypoints.Count - 1]) < 16f);
                            if (!degenerateOrder)
                            {
                                champion.UpdateMoveOrder(req.OrderType, true);

                                // Move-order rate limiter (2026-06-07): holding the mouse issues a
                                // fresh MoveTo every mouse-move frame (log shows 10-30 orders/s).
                                // Broadcasting each as a full-path WaypointGroup makes the client
                                // hard-snap to Waypoint[0] at that rate — visible jitter while held.
                                // If the champion is ALREADY moving and the last broadcast was within
                                // the streaming window, apply the new path silently: the server walks
                                // it and the 96ms champion streamer carries a small Position+3
                                // correction. Well-spaced single clicks (> window apart) still
                                // broadcast immediately and stay responsive.
                                const float MoveOrderBroadcastWindowMs = 96f;
                                bool alreadyMoving = !champion.IsPathEnded();
                                bool broadcast = !alreadyMoving
                                    || (_game.GameTime - champion.LastMoveOrderBroadcastTime) >= MoveOrderBroadcastWindowMs;

                                // Distinct-destination bypass (2026-06-17): the limiter exists to dedupe
                                // held-mouse spam to ~the same spot (same endpoint every frame). A new
                                // order whose ENDPOINT differs materially from the path we're already
                                // walking is a fresh click (kiting/dodging), not a repeat — broadcast it
                                // now so rapid distinct clicks aren't throttled to the ~96ms cadence (the
                                // "sluggish when clicking a lot" symptom). Held / slow-drag orders keep
                                // ~the same endpoint frame-to-frame (SetWaypoints runs on every order, so
                                // the current endpoint already reflects the previous one) → still throttled,
                                // so the 10-30 hard-snaps/s jitter the limiter prevents stays prevented.
                                const float MoveOrderDistinctDestSq = 50f * 50f;
                                if (!broadcast && alreadyMoving
                                    && champion.Waypoints.Count > 0 && waypoints.Count > 0)
                                {
                                    var curDest = champion.Waypoints[champion.Waypoints.Count - 1];
                                    var newDest = waypoints[waypoints.Count - 1];
                                    if (Vector2.DistanceSquared(curDest, newDest) > MoveOrderDistinctDestSq)
                                    {
                                        broadcast = true;
                                    }
                                }

                                // Reversal override (2026-06-08): throttling is only safe when the
                                // client's forward extrapolation moves the SAME way the server does
                                // — then the gap stays under the client's ~20u drift-snap threshold.
                                // When the new order points BACK against the current heading (mouse
                                // held, dragged to the opposite side), the client keeps extrapolating
                                // forward for up to one window while the server already walks
                                // backward; the two diverge at ~2*speed*window (~65u) and the
                                // eventual throttled WaypointGroup snaps the client back onto the
                                // server position. Direction reversal is exactly the event that must
                                // resync immediately, so bypass the limiter when headings oppose.
                                if (!broadcast && alreadyMoving && waypoints.Count >= 2)
                                {
                                    var curDir = champion.CurrentWaypoint - champion.Position;
                                    var newDir = waypoints[1] - waypoints[0];
                                    if (curDir.LengthSquared() > float.Epsilon
                                        && newDir.LengthSquared() > float.Epsilon
                                        && Vector2.Dot(Vector2.Normalize(curDir), Vector2.Normalize(newDir)) < 0f)
                                    {
                                        broadcast = true;
                                    }
                                }

                                champion.SetWaypoints(waypoints, broadcastImmediately: broadcast);
                                if (broadcast)
                                {
                                    champion.LastMoveOrderBroadcastTime = _game.GameTime;
                                }
                                champion.SetTargetUnit(null);

                                // Attack-move to a ground point: remember the destination so the champion
                                // resumes walking to it after killing/losing a target acquired along the way.
                                // Any other order (plain move) clears it.
                                champion.AttackMoveDestination = req.OrderType == OrderType.AttackMove
                                    ? waypoints[waypoints.Count - 1]
                                    : System.Numerics.Vector2.Zero;
                            }
                        }
                        break;
                    case OrderType.PetHardAttack:
                    case OrderType.PetHardMove:
                    case OrderType.PetHardReturn:
                    case OrderType.PetHardStop:
                        // Pet-hard commands are owned by the pet's brain (PetAI.OnOrder), reached via
                        // pet.IssueOrder below — it sets the pet state and does its OWN pathing
                        // (SetStateAndCloseToTarget / SetStateAndMove / Stand). No engine pre-pathing
                        // here: the old pre-build dropped the order (`return false`) when the click
                        // point was unpathable, which broke PetHardReturn (carries no position).
                        break;
                    case OrderType.Hold:
                    {
                        // Hold (player 'H'): KEEP current target, clear movement path, no chase, no
                        // new auto-acquire. H = Stop+Hold on the wire and the Hold packet carries no
                        // target (req.TargetNetID=0, verified), so restore the target the leading Stop
                        // just cleared. UpdateMoveOrder(Hold) then clears the path (StopMovement) but
                        // keeps the target; the AI's auto-promote + idle-acquire guards keep it from
                        // chasing/acquiring. Net: keep attacking an in-range target, don't follow it.
                        var holdTarget = u ?? champion.ConsumeRecentStopClearedTarget(250f);
                        if (holdTarget != null)
                        {
                            champion.SetTargetUnit(holdTarget, true);
                        }
                        champion.UpdateMoveOrder(OrderType.Hold, true);
                        break;
                    }
                    case OrderType.Taunt:
                        champion.UpdateMoveOrder(req.OrderType);
                        return true;
                    case OrderType.Stop:
                        champion.AttackMoveDestination = System.Numerics.Vector2.Zero;
                        // Remember the target in case this Stop is the leading half of a Hold-key
                        // press (client sends Stop+Hold); the trailing Hold restores it. A standalone
                        // Stop (S key) never gets a trailing Hold, so the remembered target expires.
                        champion.NoteStopClearedTarget(champion.TargetUnit);
                        champion.SetTargetUnit(null,true);
                        champion.UpdateMoveOrder(req.OrderType, true);
                        break;
                    default:
                        // Catch unhandled order types instead of silently dropping them (the bug
                        // that hid the missing Hold/AttackTerrain handling). If this logs in-game,
                        // the client emits an order we don't dispatch yet — add a case for it.
                        _logger.Debug($"[HandleMove] unhandled OrderType {req.OrderType} (netid {champion.NetId})");
                        break;
                }

                // Order/State split (Phase 1, docs/AI_ORDER_STATE_SPLIT_PLAN.md): mirror the champion's
                // OWN order into _aiState via HeroAI.OnOrder, parallel to the MoveOrder set in the cases
                // above. Label-only — no behaviour change (the brain still reads MoveOrder this phase).
                // Pet-hard orders command the pet, not the champion, so they are excluded.
                if (req.OrderType != OrderType.PetHardAttack && req.OrderType != OrderType.PetHardMove
                    && req.OrderType != OrderType.PetHardReturn && req.OrderType != OrderType.PetHardStop)
                {
                    champion.IssueOrder(req.OrderType, u, req.Position);
                }
                else if (pet != null)
                {
                    // Pet-hard commands target the PET's brain, not the champion's. Route them to the
                    // pet's OnOrder so the (new) PetAI BaseAIScript can translate them into pet states.
                    // The legacy IAIScript Pet.cs is not a BaseAIScript, so IssueOrder is a no-op for it
                    // (it keeps using its OnUnitUpdateMoveOrder listener) — behaviour-neutral until P-B.
                    pet.IssueOrder(req.OrderType, u, req.Position);
                }
            }

            // TODO: Shouldn't be here.
            if (champion.SpellToCast != null)
            {
                champion.SetSpellToCast(null, Vector2.Zero);
            }

            return true;
        }

        private Vector2 TranslateFromCenteredCoordinates(Vector2 vector)
        {
            // For some reason, League coordinates are translated into center-based coordinates (origin at the center of the map),
            // so we have to translate them back into normal coordinates where the origin is at the bottom left of the map.
            return new Vector2(2 * vector.X + _game.Map.NavigationGrid.MiddleOfMap.X, 2 * vector.Y + _game.Map.NavigationGrid.MiddleOfMap.Y);
        }
    }
}
