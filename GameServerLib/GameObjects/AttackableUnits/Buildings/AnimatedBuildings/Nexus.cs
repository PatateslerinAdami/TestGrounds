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
            uint netId = 0
        ) : base(game, model, collisionRadius, position, visionRadius, netId, team, stats)
        {
        }

        public override void OnAdded()
        {
            base.OnAdded();

            _blockedNavCells = _game.Map.NavigationGrid.AddDynamicBlocker(Position, CollisionRadius);
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
