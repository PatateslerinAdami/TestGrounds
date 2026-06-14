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

        private Vector2 _glideGoal;
        private float _glideSpeed;
        private bool _gliding;

        // The goal sent in the previous MoveTo. Riot's MoveMarker stream sets each packet's
        // Position field to the *previous* Goal exactly (replay a6db3774: |Position - prevGoal|
        // = 0.00 across every Vel'Koz R arc packet), NOT the marker's interpolated current
        // position. The client has already glided to the previous goal by the time the next
        // packet arrives (step cap == glide speed × interval), so re-stating it as the start
        // keeps the visual seamless; sending a lagging interpolated position causes a tiny
        // backward snap each update.
        private Vector2 _lastGoal;
        private bool _hasLastGoal;

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
        public void MoveTo(Vector2 goal, float speed = 1033.3333f, bool faceGoal = true)
        {
            // Position field = the previous goal (Riot's wire semantics, see _lastGoal). On the
            // very first MoveTo there is no previous goal, so fall back to the current position
            // (the spawn point). The server position still glides toward the goal in Update
            // below, so server-side consumers (e.g. Vel'Koz R's beam damage that reads this
            // marker's position) stay in lockstep with the visual.
            var from = _hasLastGoal ? _lastGoal : Position;
            _game.PacketNotifier.NotifyS2C_MoveMarker(this, from, goal, speed, faceGoal);
            _lastGoal = goal;
            _hasLastGoal = true;
            _glideGoal = goal;
            _glideSpeed = speed;
            _gliding = true;
        }

        public override void OnRemoved()
        {
            // The client marker is an obj_AI_Base living in the client actor-collision grid.
            // SetToRemove → base.OnRemoved only drops server-side collision/vision and never tells
            // the client, so the marker would persist forever as an invisible collider (minions
            // path around accumulated dead markers). Riot kills each marker with NPC_Die at end of
            // life (replay a6db3774); mirror that so the client actually despawns it.
            _game.PacketNotifier.NotifyMarkerDeath(this);
            base.OnRemoved();
        }

        public override void Update(float diff)
        {
            base.Update(diff);

            if (!_gliding)
            {
                return;
            }

            var toGoal = _glideGoal - Position;
            var dist = toGoal.Length();
            var step = _glideSpeed * (diff / 1000.0f);
            if (step >= dist || dist <= 0.001f)
            {
                Position = _glideGoal;
                _gliding = false;
            }
            else
            {
                Position += (toGoal / dist) * step;
            }
        }
    }
}
