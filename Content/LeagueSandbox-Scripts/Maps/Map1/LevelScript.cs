using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Domain;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;
using static LeagueSandbox.GameServer.API.ApiGameEvents;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace MapScripts.Map1
{
    public class CLASSIC : IMapScript
    {
        public virtual MapScriptMetadata MapScriptMetadata { get; set; } = new MapScriptMetadata();

        public bool HasFirstBloodHappened { get; set; } = false;
        public long NextSpawnTime { get; set; } = 10 * 1000;
        public string LaneMinionAI { get; set; } = "LaneMinionAI";
        public Dictionary<TeamId, Dictionary<int, Dictionary<int, Vector2>>> PlayerSpawnPoints { get; }

        public Dictionary<TeamId, Dictionary<MinionSpawnType, string>> MinionModels { get; set; } = new Dictionary<TeamId, Dictionary<MinionSpawnType, string>>
        {
            {TeamId.TEAM_BLUE, new Dictionary<MinionSpawnType, string>{
                {MinionSpawnType.MINION_TYPE_MELEE, "Blue_Minion_Basic"},
                {MinionSpawnType.MINION_TYPE_CASTER, "Blue_Minion_Wizard"},
                {MinionSpawnType.MINION_TYPE_CANNON, "Blue_Minion_MechCannon"},
                {MinionSpawnType.MINION_TYPE_SUPER, "Blue_Minion_MechMelee"}
            }},
            {TeamId.TEAM_PURPLE, new Dictionary<MinionSpawnType, string>{
                {MinionSpawnType.MINION_TYPE_MELEE, "Red_Minion_Basic"},
                {MinionSpawnType.MINION_TYPE_CASTER, "Red_Minion_Wizard"},
                {MinionSpawnType.MINION_TYPE_CANNON, "Red_Minion_MechCannon"},
                {MinionSpawnType.MINION_TYPE_SUPER, "Red_Minion_MechMelee"}
            }}
        };

        //List of every path minions will take, separated by team and lane
        public Dictionary<Lane, List<Vector2>> MinionPaths { get; set; } = new Dictionary<Lane, List<Vector2>>
        {
            //Pathing coordinates for Top lane
            {Lane.LANE_L, new List<Vector2> {
                new Vector2(917.0f, 1725.0f),
                new Vector2(1170.0f, 4041.0f),
                new Vector2(861.0f, 6459.0f),
                new Vector2(880.0f, 10180.0f),
                new Vector2(1268.0f, 11675.0f),
                new Vector2(2806.0f, 13075.0f),
                new Vector2(3907.0f, 13243.0f),
                new Vector2(7550.0f, 13407.0f),
                new Vector2(10244.0f, 13238.0f),
                new Vector2(10947.0f, 13135.0f),
                new Vector2(12511.0f, 12776.0f) }
            },

            //Pathing coordinates for Mid lane
            {Lane.LANE_C, new List<Vector2> {
                new Vector2(1418.0f, 1686.0f),
                new Vector2(2997.0f, 2781.0f),
                new Vector2(4472.0f, 4727.0f),
                new Vector2(8375.0f, 8366.0f),
                new Vector2(10948.0f, 10821.0f),
                new Vector2(12511.0f, 12776.0f) }
            },

            //Pathing coordinates for Bot lane
            {Lane.LANE_R, new List<Vector2> {
                new Vector2(1487.0f, 1302.0f),
                new Vector2(3789.0f, 1346.0f),
                new Vector2(6430.0f, 1005.0f),
                new Vector2(10995.0f, 1234.0f),
                new Vector2(12841.0f, 3051.0f),
                new Vector2(13148.0f, 4202.0f),
                new Vector2(13249.0f, 7884.0f),
                new Vector2(12886.0f, 10356.0f),
                new Vector2(12511.0f, 12776.0f) }
            }
        };

        //List of every wave type
        public Dictionary<string, List<MinionSpawnType>> MinionWaveTypes = new Dictionary<string, List<MinionSpawnType>>
        { {"RegularMinionWave", new List<MinionSpawnType>
        {
            MinionSpawnType.MINION_TYPE_MELEE,
            MinionSpawnType.MINION_TYPE_MELEE,
            MinionSpawnType.MINION_TYPE_MELEE,
            MinionSpawnType.MINION_TYPE_CASTER,
            MinionSpawnType.MINION_TYPE_CASTER,
            MinionSpawnType.MINION_TYPE_CASTER }
        },
        {"CannonMinionWave", new List<MinionSpawnType>{
            MinionSpawnType.MINION_TYPE_MELEE,
            MinionSpawnType.MINION_TYPE_MELEE,
            MinionSpawnType.MINION_TYPE_MELEE,
            MinionSpawnType.MINION_TYPE_CANNON,
            MinionSpawnType.MINION_TYPE_CASTER,
            MinionSpawnType.MINION_TYPE_CASTER,
            MinionSpawnType.MINION_TYPE_CASTER }
        },
        {"SuperMinionWave", new List<MinionSpawnType>{
            MinionSpawnType.MINION_TYPE_SUPER,
            MinionSpawnType.MINION_TYPE_MELEE,
            MinionSpawnType.MINION_TYPE_MELEE,
            MinionSpawnType.MINION_TYPE_MELEE,
            MinionSpawnType.MINION_TYPE_CASTER,
            MinionSpawnType.MINION_TYPE_CASTER,
            MinionSpawnType.MINION_TYPE_CASTER }
        },
        {"DoubleSuperMinionWave", new List<MinionSpawnType>{
            MinionSpawnType.MINION_TYPE_SUPER,
            MinionSpawnType.MINION_TYPE_SUPER,
            MinionSpawnType.MINION_TYPE_MELEE,
            MinionSpawnType.MINION_TYPE_MELEE,
            MinionSpawnType.MINION_TYPE_MELEE,
            MinionSpawnType.MINION_TYPE_CASTER,
            MinionSpawnType.MINION_TYPE_CASTER,
            MinionSpawnType.MINION_TYPE_CASTER }
        }};

        // 4.20 Map1 (classic SR) lane-minion stat ramp — values from LEVELS/Map1/Scripts/LevelScript.lua.
        // Map1 ramps Armor/MR and keeps HpUpgrade constant (no growth); the ramp is advanced every
        // UPGRADE_MINION_TIMER (90s) and applied additively to each spawning minion. GoldGiven (ramps) /
        // ExpGiven (static) override the chardata reward. See MapScripts.MinionStatRamp / docs/LANE_MINION_DECOMP_AUDIT.md.
        private readonly Dictionary<MinionSpawnType, MinionStatRamp> _minionRamps = new Dictionary<MinionSpawnType, MinionStatRamp>
        {
            { MinionSpawnType.MINION_TYPE_MELEE, new MinionStatRamp { HpUpgrade = 10f, DamageUpgrade = 0.5f, ArmorUpgrade = 1f, MagicResistUpgrade = 0.625f, GoldUpgrade = 0.2f, GoldMaximumBonus = 12f, GoldGivenBase = 18.8f, ExpGivenBase = 64f } },
            { MinionSpawnType.MINION_TYPE_CASTER, new MinionStatRamp { HpUpgrade = 7.5f, DamageUpgrade = 1f, ArmorUpgrade = 0.625f, MagicResistUpgrade = 1f, GoldUpgrade = 0.2f, GoldMaximumBonus = 8f, GoldGivenBase = 13.8f, ExpGivenBase = 32f } },
            { MinionSpawnType.MINION_TYPE_CANNON, new MinionStatRamp { HpUpgrade = 13.5f, DamageUpgrade = 1.5f, ArmorUpgrade = 1.5f, MagicResistUpgrade = 1.5f, GoldUpgrade = 0.5f, GoldMaximumBonus = 30f, GoldGivenBase = 39.5f, ExpGivenBase = 100f } },
            { MinionSpawnType.MINION_TYPE_SUPER, new MinionStatRamp { HpUpgrade = 100f, DamageUpgrade = 5f, ArmorUpgrade = 0f, MagicResistUpgrade = 0f, GoldUpgrade = 0.5f, GoldMaximumBonus = 30f, GoldGivenBase = 39.5f, ExpGivenBase = 100f } },
        };
        private const float UPGRADE_MINION_TIMER = 90000f;
        private float _minionUpgradeTimer = 0f;

        //This function is executed in-between Loading the map structures and applying the structure protections. Is the first thing on this script to be executed
        public virtual void Init(Dictionary<GameObjectTypes, List<MapObject>> mapObjects)
        {
            MapScriptMetadata.MinionSpawnEnabled = IsMinionSpawnEnabled();
            AddSurrender(1200000.0f, 300000.0f, 30.0f);
            // Team-balance vote: same timing as surrender; grant amounts are PLACEHOLDERS (the
            // server-side trigger/amounts aren't in the 4.20 client decomp — tune here, content-owned).
            AddTeamBalance(1200000.0f, 300000.0f, 30.0f, 300.0f, 200, 0);

            // Replace the hardcoded MinionPaths with the client-side __NAV_<lane>NN
            // waypoints from the .sco.json scene files. Trailing-digit sort gives the correct
            // direction (blue to purple). the SetUpLaneMinion code already handles reversing for
            // the purple-team barracks.
            if (mapObjects.TryGetValue(GameObjectTypes.ObjBuilding_NavPoint, out var navPoints))
            {
                var byLane = new Dictionary<Lane, List<(int idx, Vector2 pos)>>();
                foreach (var np in navPoints)
                {
                    var lane = np.GetLaneID();
                    if (lane == Lane.LANE_Unknown)
                    {
                        continue;
                    }
                    int idx = ParseTrailingIndex(np.Name);
                    if (idx < 0)
                    {
                        continue;
                    }
                    if (!byLane.TryGetValue(lane, out var list))
                    {
                        list = new List<(int, Vector2)>();
                        byLane[lane] = list;
                    }
                    list.Add((idx, new Vector2(np.CentralPoint.X, np.CentralPoint.Z)));
                }

                // The __NAV_<lane>NN points only cover the lane corridor between the two
                // outermost and mid turret area; they stop short of the inhibitor and have
                // nothing past it. We pull the two HQ (nexus) positions from the scene HQ_T1/T2
                // objects so blue minions push to the Chaos Nexus and purple minions (with the
                // path reversed in SetUpLaneMinion) push to the Order Nexus.
                Vector2? orderNexusPos = null;
                Vector2? chaosNexusPos = null;
                if (mapObjects.TryGetValue(GameObjectTypes.ObjAnimated_HQ, out var hqObjs))
                {
                    foreach (var hq in hqObjs)
                    {
                        var pos = new Vector2(hq.CentralPoint.X, hq.CentralPoint.Z);
                        if (hq.GetTeamID() == TeamId.TEAM_BLUE)
                        {
                            orderNexusPos = pos;
                        }
                        else if (hq.GetTeamID() == TeamId.TEAM_PURPLE)
                        {
                            chaosNexusPos = pos;
                        }
                    }
                }

                foreach (var kv in byLane)
                {
                    kv.Value.Sort((a, b) => a.idx.CompareTo(b.idx));
                    var path = new List<Vector2>(kv.Value.Count + 2);
                    if (orderNexusPos.HasValue)
                    {
                        path.Add(orderNexusPos.Value);
                    }
                    foreach (var t in kv.Value)
                    {
                        path.Add(t.pos);
                    }
                    if (chaosNexusPos.HasValue)
                    {
                        path.Add(chaosNexusPos.Value);
                    }
                    MinionPaths[kv.Key] = path;
                }
            }

            CreateLevelProps.CreateProps();
            LevelScriptObjects.LoadBuildings(mapObjects);
        }

        // Extracts the trailing integer suffix of a name like "__NAV_C02" -> 2 or "__NAV_C010" -> 10.
        // Returns -1 if the name has no trailing digits.
        private static int ParseTrailingIndex(string name)
        {
            int i = name.Length - 1;
            while (i >= 0 && char.IsDigit(name[i]))
            {
                i--;
            }
            if (i == name.Length - 1)
            {
                return -1;
            }
            return int.Parse(name.Substring(i + 1));
        }

        public virtual void OnMatchStart()
        {
            NeutralMinionSpawn.InitializeCamps();
            LevelScriptObjects.OnMatchStart();
        }

        public void Update(float diff)
        {
            LevelScriptObjects.OnUpdate(diff);
            NeutralMinionSpawn.OnUpdate(diff);

            // Lane-minion stat ramp (4.20 UpgradeMinionTimer): every 90s, advance every type's running
            // bonus. Ticked before the spawn clock so a wave spawning on the same frame as an upgrade
            // already carries it (matches the replay: wave-1 minions at 90s already have one upgrade).
            _minionUpgradeTimer += diff;
            while (_minionUpgradeTimer >= UPGRADE_MINION_TIMER)
            {
                _minionUpgradeTimer -= UPGRADE_MINION_TIMER;
                foreach (var ramp in _minionRamps.Values)
                {
                    ramp.Tick();
                }
            }

            var gameTime = GameTime();

            if (MapScriptMetadata.MinionSpawnEnabled)
            {
                if (_minionNumber > 0)
                {
                    // Spawn new Minion every 0.8s
                    if (gameTime >= NextSpawnTime + _minionNumber * 8 * 100)
                    {
                        if (SetUpLaneMinion())
                        {
                            _minionNumber = 0;
                            _waveCount++;
                            NextSpawnTime = (long)gameTime + MapScriptMetadata.SpawnInterval;
                        }
                        else
                        {
                            _minionNumber++;
                        }
                    }
                }
                else if (gameTime >= NextSpawnTime)
                {
                    SetUpLaneMinion();
                    _minionNumber++;
                }
            }

            if (!AllAnnouncementsAnnounced)
            {
                CheckInitialMapAnnouncements(gameTime);
            }
        }

        public void SpawnAllCamps()
        {
            NeutralMinionSpawn.ForceCampSpawn();
        }

        public Vector2 GetFountainPosition(TeamId team)
        {
            return LevelScriptObjects.FountainList[team].Position;
        }

        bool AllAnnouncementsAnnounced = false;
        List<EventID> AnnouncedEvents = new List<EventID>();
        public void CheckInitialMapAnnouncements(float time)
        {
            if (time >= 90.0f * 1000)
            {
                // Minions have spawned
                AnnounceMinionsSpawn();
                AnnouceNexusCrystalStart();
                AllAnnouncementsAnnounced = true;
            }
            else if (time >= 60.0f * 1000 && !AnnouncedEvents.Contains(EventID.OnStartGameMessage2))
            {
                // 30 seconds until minions spawn
                AnnounceStartGameMessage(2, 1);
                AnnouncedEvents.Add(EventID.OnStartGameMessage2);
            }
            else if (time >= 30.0f * 1000 && !AnnouncedEvents.Contains(EventID.OnStartGameMessage1))
            {
                // Welcome to Summoners Rift
                AnnounceStartGameMessage(1, 1);
                AnnouncedEvents.Add(EventID.OnStartGameMessage1);
            }
        }

        //Here you setup the conditions of which wave will be spawned
        public Tuple<int, List<MinionSpawnType>> MinionWaveToSpawn(float gameTime, int cannonMinionCount, bool isInhibitorDead, bool areAllInhibitorsDead)
        {
            // Cannon-wave frequency (4.20 LEVELS/Map1: CANNON_MINION_SPAWN_FREQUENCY 3 -> 2 at
            // INCREASE_CANNON_RATE_TIMER=2090s; never reaches 1). cap = freq-1 (cap2 = cannon every 3rd
            // wave, cap1 = every 2nd). See docs/LANE_MINION_DECOMP_AUDIT.md.
            var cannonMinionTimestamps = new List<Tuple<long, int>>
            {
                new Tuple<long, int>(0, 2),
                new Tuple<long, int>(2090 * 1000, 1)
            };
            var cannonMinionCap = 2;

            foreach (var timestamp in cannonMinionTimestamps)
            {
                if (gameTime >= timestamp.Item1)
                {
                    cannonMinionCap = timestamp.Item2;
                }
            }
            var list = "RegularMinionWave";
            if (cannonMinionCount >= cannonMinionCap)
            {
                list = "CannonMinionWave";
            }

            if (isInhibitorDead)
            {
                list = "SuperMinionWave";
            }

            if (areAllInhibitorsDead)
            {
                list = "DoubleSuperMinionWave";
            }
            return new Tuple<int, List<MinionSpawnType>>(cannonMinionCap, MinionWaveTypes[list]);
        }

        public int _minionNumber;
        public int _cannonMinionCount;
        public int _waveCount;
        // 4.20 SR lane-minion reward give-radii (LevelScript.lua EXP_GIVEN_RADIUS / GOLD_GIVEN_RADIUS).
        // EXP is proximity-shared within 1400; GOLD is last-hit on SR (shared portion = 0), so only the
        // exp radius is wired through to the minion. See docs/LANE_MINION_WIRE_VERIFICATION.md (#5).
        private const float EXP_GIVEN_RADIUS = 1400f;

        public bool SetUpLaneMinion()
        {
            int cannonMinionCap = 2;
            // Largest wave among the barracks spawning this cycle — the drip runs exactly this many
            // ticks (Riot paces NumToSpawnForWave, not a fixed 8), so smaller waves don't burn phantom
            // drip slots. See docs/LANE_MINION_DECOMP_AUDIT.md (S6).
            int maxWaveSize = 0;
            foreach (var barrack in LevelScriptObjects.SpawnBarracks)
            {
                TeamId opposed_team = barrack.Value.GetOpposingTeamID();
                TeamId barrackTeam = barrack.Value.GetTeamID();
                Lane lane = barrack.Value.GetSpawnBarrackLaneID();
                Inhibitor inhibitor = LevelScriptObjects.InhibitorList[opposed_team][lane];
                Vector2 position = new Vector2(barrack.Value.CentralPoint.X, barrack.Value.CentralPoint.Z);
                bool isInhibitorDead = inhibitor.InhibitorState == DampenerState.RegenerationState;
                Tuple<int, List<MinionSpawnType>> spawnWave = MinionWaveToSpawn(GameTime(), _cannonMinionCount, isInhibitorDead, LevelScriptObjects.AllInhibitorsAreDead[opposed_team]);
                cannonMinionCap = spawnWave.Item1;
                maxWaveSize = Math.Max(maxWaveSize, spawnWave.Item2.Count);

                List<Vector2> waypoint = new List<Vector2>(MinionPaths[lane]);

                if (barrackTeam == TeamId.TEAM_PURPLE)
                {
                    waypoint.Reverse();
                }

                while (waypoint.Count >= 2)
                {
                    Vector2 axis = waypoint[1] - waypoint[0];
                    Vector2 spawnRel = position - waypoint[0];
                    if (Vector2.Dot(spawnRel, axis) <= 0) break;
                    waypoint.RemoveAt(0);
                }

                var outerTurret = LevelScriptObjects.TurretList[barrackTeam][lane]
                    .Find(t => t.Type == TurretType.OUTER_TURRET);
                Vector2? outerTurretPos = outerTurret != null ? (Vector2?)outerTurret.Position : null;

                // Per S4 NavPointManager::GetNextNavLocIter -> minions hold at the next alive
                // enemy turret in their lane. Build the parallel (turret, waypointIdx) list
                // sorted by appearance order so LaneMinion can cap its advance accordingly.
                var enemyTurretsAhead = new List<BaseTurret>();
                var enemyTurretIndices = new List<int>();
                if (LevelScriptObjects.TurretList.TryGetValue(opposed_team, out var enemyTurretsByLane)
                    && enemyTurretsByLane.TryGetValue(lane, out var enemyLaneTurrets))
                {
                    var pairs = new List<(BaseTurret turret, int wpIdx)>();
                    foreach (var turret in enemyLaneTurrets)
                    {
                        // FOUNTAIN_TURRET sits at the team's spawn fountain so its never a frontier
                        // a pushing wave should regroup against. Skip it.
                        if (turret.Type == TurretType.FOUNTAIN_TURRET) continue;

                        int closestIdx = -1;
                        float closestDistSq = float.MaxValue;
                        for (int i = 0; i < waypoint.Count; i++)
                        {
                            float d = Vector2.DistanceSquared(waypoint[i], turret.Position);
                            if (d < closestDistSq)
                            {
                                closestDistSq = d;
                                closestIdx = i;
                            }
                        }
                        if (closestIdx >= 0)
                        {
                            pairs.Add((turret, closestIdx));
                        }
                    }
                    pairs.Sort((a, b) => a.wpIdx.CompareTo(b.wpIdx));
                    foreach (var p in pairs)
                    {
                        enemyTurretsAhead.Add(p.turret);
                        enemyTurretIndices.Add(p.wpIdx);
                    }
                }

                // Per-wave stat ramp + level for this drip's minion type. The accumulated bonus (HP/AD/
                // Armor/MR) is applied additively at spawn; the level = average champion level (minions
                // have no per-level stats, so this only drives the XP-on-death level delta).
                StatsModifier rampModifier = null;
                float rampGold = -1f, rampExp = -1f;
                if (_minionNumber < spawnWave.Item2.Count
                    && _minionRamps.TryGetValue(spawnWave.Item2[_minionNumber], out var ramp))
                {
                    rampModifier = ramp.ToStatsModifier();
                    rampGold = ramp.GoldGiven;
                    rampExp = ramp.ExpGiven;
                }
                int minionLevel = Math.Max(1, GetPlayerAverageLevel());

                var spawnedMinion = CreateLaneMinion(spawnWave.Item2, position, barrackTeam, _minionNumber, barrack.Value.Name, waypoint, LaneMinionAI,
                    isFirstWave: _waveCount == 0, outerTurretPosition: outerTurretPos, waveNumber: _waveCount,
                    enemyLaneTurretsAhead: enemyTurretsAhead, enemyLaneTurretWaypointIndices: enemyTurretIndices,
                    lane: lane, statModifier: rampModifier, initialLevel: minionLevel, goldGiven: rampGold, expGiven: rampExp);

                // Lane-minion death-XP give-radius (4.20 LevelScript.lua EXP_GIVEN_RADIUS = 1400; engine
                // default ai_ExpRadius2 = 1600 still applies to champions/other units). Gold is last-hit
                // (GOLD_GIVEN_RADIUS only governs the shared-gold portion, which is 0 on SR), so no gold
                // radius is wired. See docs/LANE_MINION_WIRE_VERIFICATION.md (#5).
                if (spawnedMinion != null)
                {
                    spawnedMinion.ExperienceGiveRadius = EXP_GIVEN_RADIUS;
                }
            }

            if (_minionNumber < maxWaveSize - 1)
            {
                return false;
            }

            if (_cannonMinionCount >= cannonMinionCap)
            {
                _cannonMinionCount = 0;
            }
            else
            {
                _cannonMinionCount++;
            }
            return true;
        }
    }
}
