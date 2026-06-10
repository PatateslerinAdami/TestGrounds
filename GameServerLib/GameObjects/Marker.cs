using GameServerCore.Enums;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects
{
    /// <summary>
    /// Lightweight position-only GameObject. Spawned client-side via
    /// <c>SpawnMarkerS2C</c> (packet id <c>0x100</c>) inside an
    /// <c>OnEnterVisibilityClient</c> wrapper. No model, no animation rig,
    /// no minimap presence — just a NetID + world position the client can
    /// reference (e.g. as a particle's <c>BindNetID</c> or <c>TargetNetID</c>).
    /// <para>Use for hidden anchor entities like the beam endpoint of
    /// Vel'Koz R or Xerath Q. The previous workaround was spawning a
    /// <c>TestCubeRender10Vision</c> minion, which has a much heavier wire
    /// footprint and leaks an icon onto the minimap.</para>
    /// </summary>
    public class Marker : GameObject
    {
        public byte NetNodeID { get; }
        public float VisibilitySize { get; }

        public Marker(
            Game game,
            Vector2 position,
            float visibilitySize = 100f,
            byte netNodeId = 0x40,
            uint netId = 0,
            TeamId team = TeamId.TEAM_NEUTRAL
        ) : base(game, position, collisionRadius: 0f, pathingRadius: 0f, visionRadius: 0f, netId, team)
        {
            NetNodeID = netNodeId;
            VisibilitySize = visibilitySize;
        }

        // GetHeight inherits from GameObject — auto-resolves terrain at this marker's
        // own XZ position via NavigationGrid. Previously this was overridden to return
        // a constructor-supplied Y, but that meant scripts had to know the right terrain
        // height for the spawn point; passing caster.GetHeight() would put the marker at
        // the wrong altitude when caster and marker are on different terrain heights.

        /// <summary>
        /// Smoothly moves the marker to <paramref name="goal"/> and broadcasts
        /// <c>S2C_MoveMarker</c> (packet 0x114). The client interpolates from the marker's
        /// current position to <paramref name="goal"/> at <paramref name="speed"/> world
        /// units/second. Use to steer the beam endpoint of charge spells like Vel'Koz R
        /// (replay-verified Speed=1033, FaceGoal=true).
        /// </summary>
        public void MoveTo(Vector2 goal, float speed = 1033f, bool faceGoal = true)
        {
            var from = Position;
            Position = goal;
            _game.PacketNotifier.NotifyS2C_MoveMarker(this, from, goal, speed, faceGoal);
        }
    }
}
