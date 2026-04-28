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

        // TODO assists
        public Inhibitor(
            Game game,
            string model,
            Lane laneId,
            TeamId team,
            int collisionRadius = 40,
            Vector2 position = new Vector2(),
            int visionRadius = 0,
            Stats stats = null,
            uint netId = 0
        ) : base(game, model, collisionRadius, position, visionRadius, netId, team, stats)
        {
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
            // Inhibitors respawn -> release the navgrid cells so units can walk through the area
            // while it's down. SetState(Respawning) on respawn re-bakes via the path below.
            UnbakeFootprint();

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
            var mo = FindMatchingMapObject();
            if (mo.Vertices2D != null && mo.Vertices2D.Length >= 3)
            {
                _blockedNavCells = _game.Map.NavigationGrid.AddDynamicBlocker(mo.Vertices2D);
            }
            else
            {
                _blockedNavCells = _game.Map.NavigationGrid.AddDynamicBlocker(Position, CollisionRadius);
            }
        }

        private void UnbakeFootprint()
        {
            if (_blockedNavCells != null)
            {
                _game.Map.NavigationGrid.RemoveDynamicBlocker(_blockedNavCells);
                _blockedNavCells = null;
            }
        }

        private MapObject FindMatchingMapObject()
        {
            foreach (var kv in _game.Map.MapData.MapObjects)
            {
                var mo = kv.Value;
                if (Math.Abs(mo.CentralPoint.X - Position.X) < 1f
                    && Math.Abs(mo.CentralPoint.Z - Position.Y) < 1f)
                {
                    return mo;
                }
            }
            return MapObject.Empty;
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
