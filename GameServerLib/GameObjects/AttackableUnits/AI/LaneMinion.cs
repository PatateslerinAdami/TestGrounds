using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
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
        /// <summary>
        /// Which lane this minion belongs to (set at spawn from the spawning barracks' lane). Lets the
        /// bot AI query minions per lane (Riot GetMinions(team, laneId)) for PushLane.
        /// </summary>
        public Lane Lane { get; }

        public bool IsFirstWave { get; set; }

        public Vector2? OuterTurretPosition { get; set; }

        public bool HasPassedFirstTurret { get; set; }

        public Vector2 SpawnPosition { get; }

        // RoamState moved to the shared Minion base (jungle Monster needs it too). For LaneMinion it
        // is engine-managed: first-wave minions spawn Inactive (dormant — push the lane but ignore
        // enemies) and UpdateRoamState flips them to Hostile when an enemy enters WakeUpRange.

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
            IReadOnlyList<int> enemyLaneTurretWaypointIndices = null,
            Lane lane = Lane.LANE_C
        ) : base(game, null, new Vector2(), model, model, netId, team, stats: stats, AIScript: AIScript)
        {
            IsLaneMinion = true;
            MinionSpawnType = spawnType;
            BarracksName = barracksName;
            Lane = lane;
            PathingWaypoints = mainWaypoints;
            IsFirstWave = isFirstWave;
            // FirstWave minions start dormant; all later waves spawn already aggressive.
            RoamState = isFirstWave ? MinionRoamState.Inactive : MinionRoamState.Hostile;
            OuterTurretPosition = outerTurretPosition;
            SpawnPosition = position;
            WaveNumber = waveNumber;
            EnemyLaneTurretsAhead = enemyLaneTurretsAhead ?? System.Array.Empty<BaseTurret>();
            EnemyLaneTurretWaypointIndices = enemyLaneTurretWaypointIndices ?? System.Array.Empty<int>();
            _aiPaused = false;

            SetPosition(position);

            // Find the first lane waypoint ahead of the spawn point (skip points coincident with spawn).
            Vector2? firstLaneWaypoint = null;
            if (mainWaypoints != null)
            {
                foreach (var waypoint in mainWaypoints)
                {
                    if (Vector2.DistanceSquared(position, waypoint) <= 1f)
                    {
                        continue;
                    }
                    firstLaneWaypoint = waypoint;
                    break;
                }
            }

            if (firstLaneWaypoint.HasValue)
            {
                var laneDir = Vector2.Normalize(firstLaneWaypoint.Value - position);
                if (!float.IsNaN(laneDir.X))
                {
                    Direction = new Vector3(laneDir.X, 0, laneDir.Y);
                }

                // Spawn already IN MOTION toward the first lane waypoint so the spawn OnEnterVisibilityClient
                // carries a 2-waypoint moving path (Riot does this — replay-verified, single-player 2026-06-20).
                // The client orients a spawning minion by its MOTION; a stationary (1-waypoint) spawn instead
                // leaves facing to the LookAt field, which the client does NOT honor for a static unit (it
                // falls back to a world-ward default heading, so minions visibly face map-center / "toward mid"
                // regardless of the LookAt value — the long-standing spawn-facing bug). The AI's forward-nav
                // refines this to a blocker-aware A* path on its first tick. broadcastImmediately:false keeps
                // it out of a separate 0x61 — the path rides along in the spawn 0xBA's MovementData.
                // See docs/LANE_MINION_WIRE_VERIFICATION.md.
                SetWaypoints(new List<Vector2> { position, firstLaneWaypoint.Value }, broadcastImmediately: false);
                MoveOrder = OrderType.MoveTo;
            }
            else
            {
                StopMovement();
                MoveOrder = OrderType.Hold;
            }

            Replication = new ReplicationLaneMinion(this);
        }

        /// <summary>
        /// Applies the per-wave stat ramp (HP/AD/Armor/MR bonus) and average-champion level to this
        /// minion at spawn. Must run BEFORE the unit is added to the ObjectManager — the spawn packet
        /// (0xBA's embedded Barrack_SpawnUnit + 0xAE health) is built synchronously on AddObject, so the
        /// transmitted HealthBonus/DamageBonus + boosted max-HP have to be in place already (4.20 OQ#15:
        /// the client re-derives max-HP from the packet, so server and wire must agree).
        /// </summary>
        public void ApplySpawnStatRamp(StatsModifier bonus, int level, float goldGiven = -1f, float expGiven = -1f)
        {
            if (bonus != null)
            {
                AddStatModifier(bonus);
                HealthBonus = (int)bonus.HealthPoints.FlatBonus;
                DamageBonus = (int)bonus.AttackDamage.FlatBonus;
            }
            if (level > 0)
            {
                Stats.Level = (byte)level;
            }
            // Reward override (4.20 GetMinionSpawnInfo): the level script's GoldGiven/ExpGiven (base +
            // ramp) REPLACE the chardata reward, not stack on it — so set the base directly. Gold ramps
            // (+GoldUpgrade/90s, capped); Exp is static (ExpUpgrade = 0 on every type). -1 = leave as-is.
            if (goldGiven >= 0f)
            {
                Stats.GoldGivenOnDeath.BaseValue = goldGiven;
            }
            if (expGiven >= 0f)
            {
                Stats.ExpGivenOnDeath.BaseValue = expGiven;
            }
            // Bring current HP up to the ramped maximum so the minion spawns at full (boosted) health.
            Stats.CurrentHealth = Stats.HealthPoints.Total;
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
        public override void Update(float diff)
        {
            // Engine-managed RoamState transition runs before the AI script so the AI sees the
            // up-to-date state this tick (S4: the engine owns MinionRoamState, the Lua AI reads it).
            UpdateRoamState();
            base.Update(diff);
        }

        /// <summary>
        /// Wakes a dormant first-wave minion (kInactive -> kHostile) once an enemy enters
        /// <see cref="CharData.WakeUpRange"/> (default 600). This is the lane-clash trigger: both
        /// waves walk to lane dormant and flip to hostile when they meet. No-op once hostile.
        /// </summary>
        private void UpdateRoamState()
        {
            if (RoamState != MinionRoamState.Inactive || IsDead)
            {
                return;
            }

            float wakeRange = CharData.WakeUpRange;
            float wakeRangeSq = wakeRange * wakeRange;
            foreach (var u in ApiFunctionManager.EnumerateUnitsInRange(Position, wakeRange, true))
            {
                if (u.Team == Team || u.IsDead
                    || !u.IsVisibleByTeam(Team)
                    || !u.Status.HasFlag(StatusFlags.Targetable))
                {
                    continue;
                }

                if (Vector2.DistanceSquared(Position, u.Position) <= wakeRangeSq)
                {
                    RoamState = MinionRoamState.Hostile;
                    return;
                }
            }
        }

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

        /// <summary>
        /// The alive enemy turret currently capping forward movement — the same turret whose
        /// waypoint index <see cref="GetMaxAllowedWaypointIndex"/> returns — or null when no alive
        /// enemy turret is ahead (push is unrestricted to the nexus). Lets the forward-nav target
        /// the turret's exact position once the cap waypoint is reached, matching Riot's inline
        /// turret nav node (GetNextNavLocIter halts the iterator at the enemy turret loc) instead
        /// of stopping at the nearest lane waypoint.
        /// </summary>
        public BaseTurret GetCappingTurret()
        {
            if (EnemyLaneTurretsAhead == null)
            {
                return null;
            }
            for (int i = 0; i < EnemyLaneTurretsAhead.Count; i++)
            {
                var turret = EnemyLaneTurretsAhead[i];
                if (turret == null || turret.IsDead) continue;
                return turret;
            }
            return null;
        }
    }
}