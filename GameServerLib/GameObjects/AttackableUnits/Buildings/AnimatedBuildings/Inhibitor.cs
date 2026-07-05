using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Domain;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings
{
    public class Inhibitor : ObjAnimatedBuilding
    {
        public Lane Lane { get; private set; }
        public DampenerState InhibitorState { get; private set; }
        public float RespawnTime { get; set; }
        public bool RespawnAnimationAnnounced { get; set; }
        private const float GOLD_WORTH = 50.0f;

        private List<int> _blockedNavCells;

        public Inhibitor(
            Game game,
            string model,
            Lane laneId,
            TeamId team,
            int collisionRadius = 40,
            Vector2 position = new Vector2(),
            int visionRadius = 0,
            Stats stats = null,
            uint netId = 0,
            int pathfindingRadius = 0
        ) : base(game, model, collisionRadius, position, visionRadius, netId, team, stats)
        {
            // Separate pathing footprint (mirrors Nexus): the nav-grid bake radius comes from
            // the map's ObjectCFG.cfg PathfindingCollisionRadius, independent of the gameplay
            // CollisionRadius used for attack range / contact.
            if (pathfindingRadius > 0)
            {
                PathfindingRadius = pathfindingRadius;
            }
            InhibitorState = DampenerState.RespawningState;
            Lane = laneId;
        }

        public override void OnAdded()
        {
            base.OnAdded();
            _game.ObjectManager.AddInhibitor(this);
            BakeFootprint();
        }

        public override void OnRemoved()
        {
            UnbakeFootprint();
            base.OnRemoved();
        }

        public override void Die(DeathData data)
        {
            base.Die(data);

            if (data.Killer is Champion c)
            {
                c.AddGold(this, GOLD_WORTH);
            }

            SetState(DampenerState.RegenerationState);
            NotifyState(data);
        }

        private void BakeFootprint()
        {
            if (_blockedNavCells != null)
            {
                return;
            }
            // Footprint radius: explicit pathfinding radius when provided (ObjectCFG bake
            // values, e.g. Map1 Order ~187 / Chaos 213.75), else the legacy CollisionRadius.
            float footprint = PathfindingRadius > 0 ? PathfindingRadius : CollisionRadius;
            _blockedNavCells = _game.Map.NavigationGrid.AddDynamicBlocker(Position, footprint);
        }

        private void UnbakeFootprint()
        {
            if (_blockedNavCells != null)
            {
                _game.Map.NavigationGrid.RemoveDynamicBlocker(_blockedNavCells);
                _blockedNavCells = null;
            }
        }

        //TODO: Investigate if we want the switch of states to be handled by each script
        public void SetState(DampenerState state)
        {
            if (state == DampenerState.RespawningState)
            {
                IsDead = false;
                BakeFootprint();
            }
            InhibitorState = state;
        }

        public void NotifyState(DeathData data = null)
        {
            var opposingTeam = Team == TeamId.TEAM_BLUE ? TeamId.TEAM_PURPLE : TeamId.TEAM_BLUE;

            SetIsTargetableToTeam(opposingTeam, InhibitorState == DampenerState.RespawningState);
            _game.PacketNotifier.NotifyInhibitorState(this, data);
        }

        public override void SetToRemove()
        {
        }
    }
}
