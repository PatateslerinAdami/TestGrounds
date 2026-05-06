using System;
using System.Collections.Generic;
using GameServerCore.Domain;
using GameServerCore.Enums;
using System.Numerics;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings
{
    public class Nexus : ObjAnimatedBuilding
    {
        private List<int> _blockedNavCells;

        public Nexus(
            Game game,
            string model,
            TeamId team,
            int collisionRadius = 40,
            Vector2 position = new Vector2(),
            int visionRadius = 0,
            Stats stats = null,
            uint netId = 0,
            int pathfindingRadius = 0
        ) : base(game, model, collisionRadius, position, visionRadius, netId, team, stats)
        {
            if (pathfindingRadius > 0)
            {
                PathfindingRadius = pathfindingRadius;
            }
        }

        public override void OnAdded()
        {
            base.OnAdded();

            // Map1 script sets PathfindingRadius=353 (Riot ObjectCFG.cfg HQ_T1/T2 PathfindingCollisionRadius).
            _blockedNavCells = _game.Map.NavigationGrid.AddDynamicBlocker(Position, PathfindingRadius);
        }

        public override void OnRemoved()
        {
            if (_blockedNavCells != null)
            {
                _game.Map.NavigationGrid.RemoveDynamicBlocker(_blockedNavCells);
                _blockedNavCells = null;
            }
            base.OnRemoved();
        }

        public override void SetToRemove()
        {
        }
    }
}
