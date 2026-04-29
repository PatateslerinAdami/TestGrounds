using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
    public class LaneMinion : Minion
    {
        /// <summary>
        /// Const waypoints that define the minion's route
        /// </summary>
        public List<Vector2> PathingWaypoints { get; }
        /// <summary>
        /// Name of the Barracks that spawned this lane minion.
        /// </summary>
        public string BarracksName { get; }
        public MinionSpawnType MinionSpawnType { get; }
        
        public bool IsFirstWave { get; set; }

        public Vector2? OuterTurretPosition { get; set; }

        public bool HasPassedFirstTurret { get; set; }

        public Vector2 SpawnPosition { get; }

        public bool IsAsleep { get; set; }

        public int WaveNumber { get; }

        public int SpreadSlotIndex { get; set; } = -1;

        public override bool SpawnShouldBeHidden => false;

        public LaneMinion(
            Game game,
            MinionSpawnType spawnType,
            Vector2 position,
            string barracksName,
            List<Vector2> mainWaypoints,
            string model,
            uint netId = 0,
            TeamId team = TeamId.TEAM_BLUE,
            Stats stats = null,
            string AIScript = "",
            bool isFirstWave = false,
            Vector2? outerTurretPosition = null,
            int waveNumber = 0
        ) : base(game, null, new Vector2(), model, model, netId, team, stats: stats, AIScript: AIScript)
        {
            IsLaneMinion = true;
            MinionSpawnType = spawnType;
            BarracksName = barracksName;
            PathingWaypoints = mainWaypoints;
            IsFirstWave = isFirstWave;
            IsAsleep = isFirstWave; // FirstWave Minions start asleep
            OuterTurretPosition = outerTurretPosition;
            SpawnPosition = position;
            WaveNumber = waveNumber;
            _aiPaused = false;

            SetPosition(position);

            StopMovement();

            MoveOrder = OrderType.Hold;
            Replication = new ReplicationLaneMinion(this);
        }
    }
}