using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Enums;
using GameServerCore.Packets.Handlers;
using System.Numerics;
using System.Collections.Generic;
using LeagueSandbox.GameServer.Players;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleMove : PacketHandlerBase<MovementRequest>
    {
        private readonly Game _game;
        private readonly PlayerManager _playerManager;

        public HandleMove(Game game)
        {
            _game = game;
            _playerManager = game.PlayerManager;
        }

        public override bool HandlePacket(int userId, MovementRequest req)
        {
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
                List<Vector2> waypoints;

                switch (req.OrderType)
                {
                    case OrderType.MoveTo:
                    case OrderType.AttackTo:
                    case OrderType.AttackMove:
                    case OrderType.Use:
                        if (req.Waypoints == null || req.Waypoints.Count == 0)
                        {
                            return false;
                        }


                        if (u != null)
                        {
                            champion.UpdateMoveOrder(req.OrderType, true);
                            champion.SetTargetUnit(u);
                        }
                        else
                        {
                            waypoints = req.Waypoints.ConvertAll(TranslateFromCenteredCoordinates);

                            // Client-prediction smoothing: snap server-side position to client's
                            // claimed start if it's a small drift. Gated on walkability — if the
                            // client predicted a position inside one of our dynamic blockers (e.g.,
                            // their nav-grid uses Riot's 186/214/353 bake while ours is reduced
                            // to ~100/150 to match body-push-at-visual-edge), unconditional snap
                            // would put us inside NOT_PASSABLE → OnCollision fires every tick →
                            // can't move regardless of click direction.
                            if (Vector2.Distance(champion.Position, waypoints[0]) < 150f
                                && nav.IsWalkable(waypoints[0], 0f))
                            {
                                champion.SetPosition(waypoints[0], false);
                            }

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

                            waypoints[0] = champion.Position;

                            for(int i = 0; i < waypoints.Count - 1; i++)
                            {
                                if(nav.CastCircle(waypoints[i], waypoints[i + 1], champion.PathfindingRadius, true))
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
                                    if (path == null)
                                    {
                                        var snappedFrom = nav.GetClosestTerrainExit(ithWaypoint, 0f);
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
                                            waypoints.Add(champion.Position);
                                        }
                                        waypoints.Add(clamped);
                                    }
                                    break;
                                }
                            }
                            champion.UpdateMoveOrder(req.OrderType, true);
                            champion.SetWaypoints(waypoints);
                            champion.SetTargetUnit(null);
                        }
                        break;
                    case OrderType.PetHardAttack:
                    case OrderType.PetHardMove:
                    case OrderType.PetHardReturn:
                        if (pet != null)
                        {
                            waypoints = nav.GetPath(pet.Position, req.Position, pet.PathfindingRadius, pet.UsesFastPath);
                            if (waypoints == null)
                            {
                                // Stuck-recovery (mirrors champion path above): pet may be on a
                                // dynamic-blocker cell. Snap to nearest walkable, retry path, and
                                // hard-snap pet position if successful.
                                var snappedFrom = nav.GetClosestTerrainExit(pet.Position, 0f);
                                if (snappedFrom != pet.Position)
                                {
                                    waypoints = nav.GetPath(snappedFrom, req.Position, pet.PathfindingRadius, pet.UsesFastPath);
                                    if (waypoints != null)
                                    {
                                        pet.SetPosition(snappedFrom, repath: false);
                                    }
                                }
                                if (waypoints == null)
                                {
                                    return false;
                                }
                            }
                            pet.UpdateMoveOrder(req.OrderType, true);
                            pet.SetWaypoints(waypoints);
                            pet.SetTargetUnit(u, true);
                        }
                        break;
                    case OrderType.Taunt:
                        champion.UpdateMoveOrder(req.OrderType);
                        return true;
                    case OrderType.Stop:
                        champion.SetTargetUnit(null,true);
                        champion.UpdateMoveOrder(req.OrderType, true);
                        break;
                    case OrderType.PetHardStop:
                        if (pet != null)
                        {
                            pet.UpdateMoveOrder(req.OrderType, true);
                        }
                        break;
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
