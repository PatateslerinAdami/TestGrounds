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

        /// <summary>
        /// Enemy lane-turrets the minion will encounter along its path, sorted by appearance
        /// order (= ascending waypoint-index). Mirrors S4 NavPointManager turret-aware advance
        /// gating: minions hold at <see cref="GetMaxAllowedWaypointIndex"/> until the next
        /// alive enemy turret in this list dies.
        /// Empty list -> unrestricted push to nexus.
        /// </summary>
        public IReadOnlyList<BaseTurret> EnemyLaneTurretsAhead { get; }
        /// <summary>
        /// Per-turret waypoint index — the cap waypoint the minion is allowed to reach when
        /// the corresponding turret in <see cref="EnemyLaneTurretsAhead"/> is alive. Same length
        /// as <see cref="EnemyLaneTurretsAhead"/>; sorted ascending.
        /// </summary>
        public IReadOnlyList<int> EnemyLaneTurretWaypointIndices { get; }

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
            int waveNumber = 0,
            IReadOnlyList<BaseTurret> enemyLaneTurretsAhead = null,
            IReadOnlyList<int> enemyLaneTurretWaypointIndices = null
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
            EnemyLaneTurretsAhead = enemyLaneTurretsAhead ?? System.Array.Empty<BaseTurret>();
            EnemyLaneTurretWaypointIndices = enemyLaneTurretWaypointIndices ?? System.Array.Empty<int>();
            _aiPaused = false;

            SetPosition(position);

            StopMovement();

            MoveOrder = OrderType.Hold;
            Replication = new ReplicationLaneMinion(this);
        }

        /// <summary>
        /// Highest waypoint index the minion is currently allowed to reach. Walks the
        /// <see cref="EnemyLaneTurretsAhead"/> list in order, returning the waypoint cap of
        /// the first alive entry. If all entries are dead (or list is empty), returns
        /// <c>PathingWaypoints.Count - 1</c> (= push to nexus).
        ///
        /// Mirrors S4 NavPointManager::GetNextNavLocIter (NavPointManager.cpp:1259-1503): walk
        /// nav-points, stop at first alive enemy turret (= regroup point); friendly/dead turrets
        /// are skipped. Server-side simplification: we don't model friendly turrets in this
        /// list because they're already implicitly skipped because we only stored enemy turrets at
        /// spawn time.
        /// </summary>
        public int GetMaxAllowedWaypointIndex()
        {
            int totalWaypoints = PathingWaypoints?.Count ?? 0;
            int defaultMax = totalWaypoints > 0 ? totalWaypoints - 1 : 0;
            if (EnemyLaneTurretsAhead == null || EnemyLaneTurretsAhead.Count == 0)
            {
                return defaultMax;
            }
            for (int i = 0; i < EnemyLaneTurretsAhead.Count; i++)
            {
                var turret = EnemyLaneTurretsAhead[i];
                if (turret == null || turret.IsDead) continue;
                int idx = EnemyLaneTurretWaypointIndices[i];
                if (idx > defaultMax) idx = defaultMax;
                if (idx < 0) idx = 0;
                return idx;
            }
            return defaultMax;
        }
    }
}