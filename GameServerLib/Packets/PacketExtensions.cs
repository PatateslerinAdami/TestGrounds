using LeaguePackets.Game.Common;
using LeagueSandbox.GameServer.Content.Navigation;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace PacketDefinitions420
{
    public static class PacketExtensions
    {
        private static long _movementSyncEpochMs = -1;

        /// <summary>
        /// The single shared server-side "sync clock" stamped into EVERY syncID-bearing wire field.
        /// Replay a6db3774 (decoded 2026-06-20) shows Riot reads ONE per-client, session-relative
        /// monotonic clock for all of them — WaypointGroup (0x61) / WaypointGroupWithSpeed (0x64) /
        /// WaypointListHeroWithSpeed (0x83) headers, the MovementData embedded in the spawn
        /// OnEnterVisibilityClient (0xBA) via WriteMovementDataWithHeader, the NPC_CastSpellAns
        /// casterPosSyncID, AND OnReplication (0xC4). Proof they share one clock: in the replay the
        /// OnReplication and WaypointGroup syncIDs interleave at the same magnitude (rep=682 @t=125640
        /// vs wp=697 @t=125706) and both advance ~0.67/ms even while idle, both starting at ~0 on
        /// client join.
        ///
        /// The clock is ~2/3 per millisecond. We reproduce its SHAPE with a global clock that starts
        /// at ~0 on the first packet (= game start for our single-game-per-process server, where
        /// game-relative == session-relative because all clients join at start) and scales elapsed ms
        /// by 2/3. A single global value is mandatory because BroadcastPacketVision serializes one
        /// packet for all viewers (a true per-client origin would need per-client serialization).
        ///
        /// Several of these feed the SAME per-object client gate (AIManager_Client::CanSyncUpdate —
        /// the 3 callers are WaypointGroup, Pause/Stop and CastSpell's casterPosSyncID): a packet is
        /// dropped unless its syncID >= the last that object saw. So mixing scales is a real bug, not
        /// just cosmetics: e.g. a huge TickCount casterPosSyncID followed by small WaypointGroup
        /// values poisons mSyncID and the client drops every post-cast move order ("can't move after
        /// casting"). OnReplication uses its OWN separate gate variables (mSyncIDClientOnly etc.) so
        /// its scale can't break movement — but it samples the same clock on the wire, so we keep it
        /// here for fidelity. Behaviour is identical to any monotonic source; the value choice is
        /// cosmetic, the SINGLE-SOURCE rule is not. See docs/LANE_MINION_WIRE_VERIFICATION.md.
        /// </summary>
        public static int WireSyncID
        {
            get
            {
                long now = Environment.TickCount64;
                if (_movementSyncEpochMs < 0)
                {
                    _movementSyncEpochMs = now;
                }
                return (int)((now - _movementSyncEpochMs) * 2 / 3);
            }
        }

        /// <summary>
        /// Converts the given list of Vector2s into a list of CompressedWaypoints compatible with LeaguePackets, which are Vector2s with their origin at the center of the map.
        /// </summary>
        /// <param name="wp">List of Vector2s to convert.</param>
        /// <param name="grid">NavigationGrid to use for conversion.</param>
        /// <returns>List of CompressedWaypoints (Vector2s with origin at the center of the map).</returns>
        public static List<CompressedWaypoint> Vector2ToWaypoint(List<Vector2> wp, NavigationGrid grid)
        {
            return wp.ConvertAll(v => Vector2ToWaypoint(TranslateToCenteredCoordinates(v, grid)));
        }

        /// <summary>
        /// Converts the given CompressedWaypoint into a Vector2, however it does not unconvert it, meaning it will still have its origin at the center of the map.
        /// </summary>
        /// <param name="cw">CompressedWaypoint to convert.</param>
        /// <returns>Vector2 with equivalent coordinates.</returns>
        public static Vector2 WaypointToVector2(CompressedWaypoint cw)
        {
            return new Vector2(cw.X, cw.Y);
        }

        /// <summary>
        /// Converts the given Vector2 into a CompressedWaypoint, however the origin is not converted. Vector2 must have its origin at the center of the map before conversion.
        /// </summary>
        /// <param name="cw">Vector2 to convert.</param>
        /// <returns>CompressedWaypoint with equivalent coordinates.</returns>
        public static CompressedWaypoint Vector2ToWaypoint(Vector2 cw)
        {
            return new CompressedWaypoint((short)cw.X, (short)cw.Y);
        }

        /// <summary>
        /// Converts the given Vector2 back into a Vector2 with an origin at the bottom left corner of the map.
        /// </summary>
        /// <param name="vector">Vector2 to convert.</param>
        /// <param name="grid">NavigationGrid used for grabbing center of the map.</param>
        /// <returns>Vector2 with origin at the center of the map.</returns>
        public static Vector2 TranslateFromCenteredCoordinates(Vector2 vector, NavigationGrid grid)
        {
            // For unk reason coordinates are translated to 0,0 as a map center, so we gotta get back the original
            // mapSize contains the real center point coordinates, meaning width/2, height/2
            return new Vector2(2 * vector.X + grid.MiddleOfMap.X, 2 * vector.Y + grid.MiddleOfMap.Y);
        }

        /// <summary>
        /// Converts the given Vector2 into a Vector2 with an origin at the center of the map.
        /// </summary>
        /// <param name="vector">Vector2 to convert.</param>
        /// <param name="grid">NavigationGrid used for grabbing center of the map.</param>
        /// <returns>Vector2 with origin at the center of the map.</returns>
        public static Vector2 TranslateToCenteredCoordinates(Vector2 vector, NavigationGrid grid)
        {
            // For unk reason coordinates are translated to 0,0 as a map center, so we gotta get back the original
            // mapSize contains the real center point coordinates, meaning width/2, height/2
            return new Vector2((vector.X - grid.MiddleOfMap.X) / 2, (vector.Y - grid.MiddleOfMap.Y) / 2);
        }

        /// <summary>
        /// Creates the MovementDataStop.
        /// </summary>
        /// <param name="o">GameObject to create MovementData for.</param>
        public static MovementDataStop CreateMovementDataStop(GameObject o)
        {
            return new MovementDataStop
            {
                SyncID = WireSyncID,
                Position = o.Position,
                Forward = new Vector2(o.Direction.X, o.Direction.Z)
            };
        }

        /// <summary>
        /// Creates the MovementDataNone.
        /// </summary>
        /// <param name="o">GameObject to create MovementData for.</param>
        public static MovementDataNone CreateMovementDataNone(GameObject o)
        {
            return new MovementDataNone
            {
                SyncID = 0 // Always zero in replays
            };
        }

        private static List<CompressedWaypoint> GetCenteredWaypoints(AttackableUnit unit, NavigationGrid grid)
        {
            // When CurrentWaypointKey >= Waypoints.Count (path ended) the prior `count = 2 + ...`
            // arithmetic underflows to <2 and the trim branch is skipped, so the packet carries the
            // full stale path with [0] overwritten by Position. Client interprets that as "still
            // moving, teleport between old waypoints" each time the keepalive WaypointGroup fires.
            // Building the list explicitly from CurrentWaypointKey forward keeps it [Position] (=
            // stationary) once the path is done.
            var result = new List<Vector2> { unit.Position };

            // Waypoint budget. The Position+3 trim was designed for the OLD per-tick (96ms) hero
            // streamer: each WaypointGroup REPLACES the client's path, but a fresh one arrived
            // every ~33u so 3 lookahead always sufficed. Champions are now event-driven (the 96ms
            // streamer was removed 2026-06-08) — between path changes the only updates are the
            // ~5s vision batches. With Position+3 the client runs out of its 4-waypoint copy on a
            // long route (waypoints closer than ~1725u of 5s travel), STOPS, then snaps forward on
            // the next batch ("stopped then teleported to a position on the way", MOVEPKT-confirmed:
            // order broadcast nWp=9 → next vision-batch nWp=4). So a champion must carry its FULL
            // remaining route every broadcast; it then walks autonomously until the next real
            // change. Non-champions keep the trim (they still have the 100u distance-streamer
            // above, so 3 lookahead always covers the gap) — full lists per update caused FPS
            // drops on minion waves. Fresh orders (FullPathBroadcastPending) always send full.
            bool needsFullRoute = unit.FullPathBroadcastPending
                || unit is LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Champion;
            int maxAhead = needsFullRoute ? int.MaxValue : 3;
            for (int i = unit.CurrentWaypointKey; i < unit.Waypoints.Count && maxAhead > 0; i++, maxAhead--)
            {
                result.Add(unit.Waypoints[i]);
            }
            return result.ConvertAll(v => Vector2ToWaypoint(TranslateToCenteredCoordinates(v, grid)));
        }

        /// <summary>
        /// Creates a `MovementDataNormal` for the unit. Replay-verified shape: when the unit is
        /// stationary (no waypoints, or `CurrentWaypointKey >= Waypoints.Count`), Riot still emits
        /// a Normal with exactly one waypoint at the unit's current position — never `Type=Stop`
        /// (=3), which the 4.x replay never carries in `movementData[]` entries. The 1-waypoint
        /// fallback is produced naturally by `GetCenteredWaypoints` (its seed is `unit.Position`).
        /// </summary>
        public static MovementDataNormal CreateMovementDataNormal(AttackableUnit unit, NavigationGrid grid, bool useTeleportID = false)
        {
            return new MovementDataNormal
            {
                SyncID = WireSyncID,
                TeleportNetID = unit.NetId,
                HasTeleportID = useTeleportID,
                TeleportID = useTeleportID ? unit.TeleportID : (byte)0,
                Waypoints = GetCenteredWaypoints(unit, grid)
            };
        }

        /// <summary>
        /// Creates a `MovementDataWithSpeed` if the unit has `MovementParameters` (i.e. is dashing),
        /// otherwise a `MovementDataNormal`. Stationary units emit a 1-waypoint Normal at their
        /// current position (replay-verified shape — `MovementDataStop`/Type=3 is unused in 4.x).
        /// </summary>
        public static MovementData CreateMovementDataWithSpeedIfPossible(AttackableUnit unit, NavigationGrid grid, bool useTeleportID = false)
        {
            if (unit.MovementParameters == null)
            {
                return CreateMovementDataNormal(unit, grid, useTeleportID);
            }
            return CreateMovementDataWithSpeed(unit, grid, useTeleportID);
        }

        /// <summary>
        /// Creates the MovementDataWithSpeed.
        /// </summary>
        /// <param name="unit">AttackableUnit to create MovementData for.</param>
        /// <param name="grid">NavigationGrid used for grabbing center of the map.</param>
        /// <returns>MovementDataWithSpeed if unit has MovementParameters (!= null) and enough waypoints (>= 1), otherwise crashes.</returns>
        public static MovementDataWithSpeed CreateMovementDataWithSpeed(AttackableUnit unit, NavigationGrid grid, bool useTeleportID = false)
        {
            System.Diagnostics.Debug.Assert(unit.Waypoints.Count >= 1);
            System.Diagnostics.Debug.Assert(unit.MovementParameters != null);

            return new MovementDataWithSpeed
            {
                SyncID = WireSyncID,
                TeleportNetID = unit.NetId,
                HasTeleportID = useTeleportID,
                TeleportID = useTeleportID ? unit.TeleportID : (byte)0,
                Waypoints = GetCenteredWaypoints(unit, grid),
                SpeedParams = new SpeedParams
                {
                    PathSpeedOverride = unit.MovementParameters.PathSpeedOverride,
                    ParabolicGravity = unit.MovementParameters.ParabolicGravity,
                    // TODO: Implement as parameter (ex: Aatrox Q).
                    ParabolicStartPoint = unit.MovementParameters.ParabolicStartPoint,
                    Facing = unit.MovementParameters.KeepFacingDirection,
                    FollowNetID = unit.MovementParameters.FollowNetID,
                    FollowDistance = unit.MovementParameters.FollowDistance,
                    FollowBackDistance = unit.MovementParameters.FollowBackDistance,
                    FollowTravelTime = unit.MovementParameters.FollowTravelTime
                }
            };
        }
    }
}
