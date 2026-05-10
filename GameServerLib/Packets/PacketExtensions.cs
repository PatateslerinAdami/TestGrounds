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
                SyncID = Environment.TickCount,
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
            for (int i = unit.CurrentWaypointKey; i < unit.Waypoints.Count; i++)
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
                SyncID = Environment.TickCount,
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
                SyncID = Environment.TickCount,
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
        public static MovementDataWithSpeed CreateCustomMovementDataWithSpeed(AttackableUnit unit, NavigationGrid grid, Vector2 targetPos, float speed, float gravity, Vector2 parabolicStartPoint)
        {
            var waypoints = new List<Vector2> { parabolicStartPoint, unit.Position };
            var compressedWaypoints = waypoints.ConvertAll(v => Vector2ToWaypoint(TranslateToCenteredCoordinates(v, grid)));

            return new MovementDataWithSpeed
            {
                SyncID = Environment.TickCount,
                TeleportNetID = unit.NetId,
                HasTeleportID = false,
                TeleportID = 0,
                Waypoints = compressedWaypoints,
                SpeedParams = new SpeedParams
                {
                    PathSpeedOverride = speed,
                    ParabolicGravity = gravity,
                    ParabolicStartPoint = parabolicStartPoint, 
                    Facing = false,
                    FollowNetID = 0,
                    FollowDistance = 0,
                    FollowBackDistance = 0,
                    FollowTravelTime = 0
                }
            };
        }
    }
}
