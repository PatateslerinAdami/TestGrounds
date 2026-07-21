using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using System.Collections.Generic;
using GameServerCore.Domain;
using System.Numerics;
using System.Linq;
using LeagueSandbox.GameServer.GameObjects;
using            GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.Content;

namespace MapScripts.Map10
{
    public static class LevelScriptObjects
    {
        private static Dictionary<GameObjectTypes, List<MapObject>> _mapObjects;

        public static Dictionary<TeamId, Fountain> FountainList = new Dictionary<TeamId, Fountain>();
        public static Dictionary<TeamId, Dictionary<Lane, MapObject>> SpawnBarracks = new Dictionary<TeamId, Dictionary<Lane, MapObject>>();
        public static Dictionary<TeamId, bool> AllInhibitorsAreDead = new Dictionary<TeamId, bool> { { TeamId.TEAM_BLUE, false }, { TeamId.TEAM_PURPLE, false } };
        public static Dictionary<Lane, List<Vector2>> MinionPaths = new Dictionary<Lane, List<Vector2>> { { Lane.LANE_L, new List<Vector2>() }, { Lane.LANE_R, new List<Vector2>() } };
        public static List<Nexus> NexusList = new List<Nexus>();
        static string LaneTurretAI = "TurretAI";

        static Dictionary<TeamId, Dictionary<Inhibitor, float>> DeadInhibitors = new Dictionary<TeamId, Dictionary<Inhibitor, float>> 
        { 
            { TeamId.TEAM_BLUE, new Dictionary<Inhibitor, float>() }, 
            { TeamId.TEAM_PURPLE, new Dictionary<Inhibitor, float>() } 
        };

        public static Dictionary<TeamId, Dictionary<Lane, List<LaneTurret>>> TurretList = new Dictionary<TeamId, Dictionary<Lane, List<LaneTurret>>>
        {
            {TeamId.TEAM_BLUE, new Dictionary<Lane, List<LaneTurret>>{
                { Lane.LANE_Unknown, new List<LaneTurret>()},
                { Lane.LANE_L, new List<LaneTurret>()},
                { Lane.LANE_C, new List<LaneTurret>()},
                { Lane.LANE_R, new List<LaneTurret>()}}
            },
            {TeamId.TEAM_PURPLE, new Dictionary<Lane, List<LaneTurret>>{
                { Lane.LANE_Unknown, new List<LaneTurret>()},
                { Lane.LANE_L, new List<LaneTurret>()},
                { Lane.LANE_C, new List<LaneTurret>()},
                { Lane.LANE_R, new List<LaneTurret>()}}
            }
        };

        public static Dictionary<TeamId, Dictionary<Lane, Inhibitor>> InhibitorList = new Dictionary<TeamId, Dictionary<Lane, Inhibitor>>
        {
            {TeamId.TEAM_BLUE, new Dictionary<Lane, Inhibitor>{
                { Lane.LANE_L, null },
                { Lane.LANE_R, null }}
            },
            {TeamId.TEAM_PURPLE, new Dictionary<Lane, Inhibitor>{
                { Lane.LANE_L, null },
                { Lane.LANE_R, null }}
            }
        };

        //Nexus models
        static Dictionary<TeamId, string> NexusModels { get; set; } = new Dictionary<TeamId, string>
        {
            {TeamId.TEAM_BLUE, "OrderNexus" },
            {TeamId.TEAM_PURPLE, "ChaosNexus" }
        };

        //Inhib models
        static Dictionary<TeamId, string> InhibitorModels { get; set; } = new Dictionary<TeamId, string>
        {
            {TeamId.TEAM_BLUE, "TT_OrderInhibitor" },
            {TeamId.TEAM_PURPLE, "TT_ChaosInhibitor" }
        };

        //Tower Models
        static Dictionary<TeamId, Dictionary<TurretType, string>> TowerModels { get; set; } = new Dictionary<TeamId, Dictionary<TurretType, string>>
        {
            {TeamId.TEAM_BLUE, new Dictionary<TurretType, string>
            {
                {TurretType.FOUNTAIN_TURRET, "TT_OrderTurret4" },
                {TurretType.NEXUS_TURRET, "TT_OrderTurret3" },
                {TurretType.INHIBITOR_TURRET, "TT_OrderTurret1" },
                {TurretType.INNER_TURRET, "TT_OrderTurret2" }
            } },
            {TeamId.TEAM_PURPLE, new Dictionary<TurretType, string>
            {
                {TurretType.FOUNTAIN_TURRET, "TT_ChaosTurret4" },
                {TurretType.NEXUS_TURRET, "TT_ChaosTurret3" },
                {TurretType.INHIBITOR_TURRET, "TT_ChaosTurret1" },
                {TurretType.INNER_TURRET, "TT_ChaosTurret2" }
            } }
        };

        //Turret Items
        public static Dictionary<TurretType, int[]> TurretItems { get; set; } = new Dictionary<TurretType, int[]>
        {
            { TurretType.OUTER_TURRET, new[] { 1500, 1501, 1502, 1503 } },
            // No 1504 (Vanguard): that item is the inner-turret champion-shield aura, which is a
            // Summoner's-Rift-only mechanic in 4.20. Riot applies it via the SR level script
            // (map11/Scripts/LevelScript.lua:552 ApplyPersistentBuff(innerTurret,
            // "SRTurretSecondaryShielder") — SR-prefixed, referenced by NO other map). TT (Map10)
            // never grants a turret shield, so its inner turrets carry the same base set as the outer.
            { TurretType.INNER_TURRET, new[] { 1500, 1501, 1502, 1503 } },
            { TurretType.INHIBITOR_TURRET, new[] { 1501, 1502, 1503, 1505 } },
            { TurretType.NEXUS_TURRET, new[] { 1501, 1502, 1503, 1505 } }
        };

        static StatsModifier TurretStatsModifier = new StatsModifier();
        public static void LoadObjects(Dictionary<GameObjectTypes, List<MapObject>> mapObjects)
        {
            _mapObjects = mapObjects;

            CreateBuildings();
            LoadProtection();

            LoadSpawnBarracks();
            LoadMinionPathing();
            LoadFountains();
        }

        static void LoadMinionPathing()
        {
            foreach (var minionPath in _mapObjects[GameObjectTypes.ObjBuilding_NavPoint])
            {
                var lane = minionPath.GetLaneID();

                MinionPaths[lane].Add(new Vector2(minionPath.CentralPoint.X, minionPath.CentralPoint.Z));
            }
        }

        public static void OnMatchStart()
        {
            LoadShops();

            Dictionary<TeamId, List<Champion>> Players = new Dictionary<TeamId, List<Champion>>
            {
                {TeamId.TEAM_BLUE, ApiFunctionManager.GetAllPlayersFromTeam(TeamId.TEAM_BLUE) },
                {TeamId.TEAM_PURPLE, ApiFunctionManager.GetAllPlayersFromTeam(TeamId.TEAM_PURPLE) }
            };

            StatsModifier TurretHealthModifier = new StatsModifier();
            foreach (var team in TurretList.Keys)
            {
                TeamId enemyTeam = TeamId.TEAM_BLUE;

                if (team == TeamId.TEAM_BLUE)
                {
                    enemyTeam = TeamId.TEAM_PURPLE;
                }

                TurretHealthModifier.HealthPoints.BaseBonus = 250.0f * Players[enemyTeam].Count;

                foreach (var lane in TurretList[team].Keys)
                {
                    foreach (var turret in TurretList[team][lane])
                    {
                        if (turret.Type == TurretType.FOUNTAIN_TURRET)
                        {
                            continue;
                        }

                        turret.AddStatModifier(TurretHealthModifier);
                        turret.Stats.CurrentHealth += turret.Stats.HealthPoints.Total;
                        AddTurretItems(turret, GetTurretItems(TurretItems, turret.Type));
                    }
                }
            }

            TurretStatsModifier.Armor.FlatBonus = 1;
            TurretStatsModifier.MagicResist.FlatBonus = 1;
            TurretStatsModifier.AttackDamage.FlatBonus = 4;

        }

        public static void OnUpdate(float diff)
        {
            var gameTime = GameTime();

            if (gameTime >= timeCheck && timesApplied < 30)
            {
                UpdateTowerStats();
            }

            foreach (var fountain in FountainList.Values)
            {
                fountain.Update(diff);
            }

            foreach (var team in DeadInhibitors.Keys)
            {
                foreach (var inhibitor in DeadInhibitors[team].Keys.ToList())
                {
                    DeadInhibitors[team][inhibitor] -= diff;
                    if (DeadInhibitors[team][inhibitor] <= 0)
                    {
                        inhibitor.Stats.CurrentHealth = inhibitor.Stats.HealthPoints.Total;
                        inhibitor.NotifyState();
                        DeadInhibitors[inhibitor.Team].Remove(inhibitor);
                    }
                    else if (DeadInhibitors[team][inhibitor] <= 15.0f * 1000)
                    {
                        inhibitor.SetState(DampenerState.RespawningState);
                    }
                }
            }
        }

        static void OnNexusDeath(DeathData deathaData)
        {
            var nexus = deathaData.Unit;
            // Switch the destroyed team's nexus to its destruction skin (S2C_AnimatedBuildingSetCurrentSkin).
            // Replay-verified on TT: team=loser, skinID=1, sent ~2s before the end-game screen.
            SetAnimatedBuildingSkin(nexus.Team, 1);
            EndGame(nexus.Team, new Vector3(nexus.Position.X, nexus.GetHeight(), nexus.Position.Y), deathData: deathaData);
        }

        public static void OnInhibitorDeath(DeathData deathData)
        {
            var inhibitor = deathData.Unit as Inhibitor;

            // Death-side dampener transition — moved out of Inhibitor.Die (the map script owns the
            // structure state machine now, matching Riot's per-map LevelScript.lua model).
            inhibitor.SetState(DampenerState.RegenerationState);
            inhibitor.NotifyState(deathData);

            DeadInhibitors[inhibitor.Team].Add(inhibitor, inhibitor.RespawnTime * 1000);

            if (DeadInhibitors[inhibitor.Team].Count == InhibitorList[inhibitor.Team].Count)
            {
                AllInhibitorsAreDead[inhibitor.Team] = true;
            }
        }

        static float timeCheck = 480.0f * 1000;
        static byte timesApplied = 0;
        static void UpdateTowerStats()
        {
            foreach (var team in TurretList.Keys)
            {
                foreach (var lane in TurretList[team].Keys)
                {
                    foreach (var turret in TurretList[team][lane])
                    {
                        if (turret.Type == TurretType.FOUNTAIN_TURRET || ((turret.Type != TurretType.NEXUS_TURRET) && timesApplied >= 20))
                        {
                            continue;
                        }

                        turret.AddStatModifier(TurretStatsModifier);
                    }
                }
            }

            timesApplied++;
            timeCheck += 60.0f * 1000;
        }

        static void LoadFountains()
        {
            foreach (var fountain in _mapObjects[GameObjectTypes.ObjBuilding_SpawnPoint])
            {
                var team = fountain.GetTeamID();
                FountainList.Add(team, CreateFountain(team, new Vector2(fountain.CentralPoint.X, fountain.CentralPoint.Z)));
            }
        }

        static void LoadShops()
        {
            foreach (var shop in _mapObjects[GameObjectTypes.ObjBuilding_Shop])
            {
                CreateShop(shop.Name, new Vector2(shop.CentralPoint.X, shop.CentralPoint.Z), shop.GetTeamID());
            }
        }

        static void LoadSpawnBarracks()
        {
            foreach (var spawnBarrack in _mapObjects[GameObjectTypes.ObjBuildingBarracks])
            {
                var team = spawnBarrack.GetTeamID();
                if (!SpawnBarracks.ContainsKey(team))
                {
                    SpawnBarracks.Add(team, new Dictionary<Lane, MapObject>());
                }
                // Lane from the spawn-barrack name's __L/__C/__R tag — Riot's single authoritative
                // parser (Barracks::ComputeLane); matches GetSpawnBarrackLaneID used by the engine
                // BarracksSpawnManager and by SetUpLaneMinion. (The spawn barracks are named
                // __P_<team>_Spawn_Barracks__{L|R}01.)
                SpawnBarracks[team].Add(spawnBarrack.GetSpawnBarrackLaneID(), spawnBarrack);
            }
        }

        static void CreateBuildings()
        {
            foreach (var nexusObj in _mapObjects[GameObjectTypes.ObjAnimated_HQ])
            {
                var teamId = nexusObj.GetTeamID();
                var position = new Vector2(nexusObj.CentralPoint.X, nexusObj.CentralPoint.Z);
                var nexusStats = new Stats();
                nexusStats.HealthPoints.BaseValue = 5500.0f;
                nexusStats.CurrentHealth = nexusStats.HealthPoints.BaseValue;

                // sightRange 1350 = real Map10 ObjectCFG.cfg HQ_T1/T2 PerceptionBubbleRadius
                // (verified; was an invented 1700). Intrinsic auto-vision, no RevealStealth.
                var nexus = CreateNexus(nexusObj.Name, NexusModels[teamId], position, teamId, 353, 1350, nexusStats);
                ApiEventManager.OnDeath.AddListener(nexus, nexus, OnNexusDeath, true);
                NexusList.Add(nexus);
                AddObject(nexus);
            }

            foreach (var inhibitorObj in _mapObjects[GameObjectTypes.ObjAnimated_BarracksDampener])
            {
                var teamId = inhibitorObj.GetTeamID();
                var lane = inhibitorObj.GetLaneID();
                var position = new Vector2(inhibitorObj.CentralPoint.X, inhibitorObj.CentralPoint.Z);
                var inhibitorStats = new Stats();
                inhibitorStats.HealthPoints.BaseValue = GlobalData.BarrackVariables.MaxHP;
                inhibitorStats.Armor.BaseValue = GlobalData.BarrackVariables.Armor;
                inhibitorStats.CurrentHealth = inhibitorStats.HealthPoints.BaseValue;

                // sightRange 1350 = real Map10 ObjectCFG.cfg Barracks_T1/T2 PerceptionBubbleRadius
                // (verified; was 0 = no vision, a bug). Intrinsic auto-vision, no RevealStealth.
                var inhibitor = CreateInhibitor(inhibitorObj.Name, InhibitorModels[teamId], position, teamId, lane, 214, 1350, inhibitorStats);
                ApiEventManager.OnDeath.AddListener(inhibitor, inhibitor, OnInhibitorDeath, false);
                inhibitor.RespawnTime = 240.0f;
                InhibitorList[teamId][lane] = inhibitor;
                AddObject(inhibitor);
            }

            foreach (var turretObj in _mapObjects[GameObjectTypes.ObjAIBase_Turret])
            {
                var teamId = turretObj.GetTeamID();
                var lane = turretObj.GetLaneID();
                var position = new Vector2(turretObj.CentralPoint.X, turretObj.CentralPoint.Z);

                if (turretObj.Name.Contains("Shrine"))
                {
                    var fountainTurret = CreateLaneTurret(turretObj.Name + "_A", TowerModels[teamId][TurretType.FOUNTAIN_TURRET], position, teamId, TurretType.FOUNTAIN_TURRET, Lane.LANE_Unknown, LaneTurretAI, turretObj);
                    TurretList[teamId][lane].Add(fountainTurret);
                    AddObject(fountainTurret);
                    continue;
                }

                switch (turretObj.Name)
                {
                    case "Turret_T1_C_07":
                        lane = Lane.LANE_R;
                        break;
                    case "Turret_T1_C_06":
                        lane = Lane.LANE_L;
                        break;
                }

                var turretType = GetTurretType(turretObj.ParseIndex(), lane, teamId);

                var turret = CreateLaneTurret(turretObj.Name + "_A", TowerModels[teamId][turretType], position, teamId, turretType, lane, LaneTurretAI, turretObj);
                TurretList[teamId][lane].Add(turret);
                AddObject(turret);
            }
        }

        static TurretType GetTurretType(int trueIndex, Lane lane, TeamId teamId)
        {
            TurretType returnType = TurretType.NEXUS_TURRET;
            switch (trueIndex)
            {
                case 1:
                case 6:
                case 7:
                    returnType = TurretType.INHIBITOR_TURRET;
                    break;
                case 2:
                    returnType = TurretType.INNER_TURRET;
                    break;
            }

            if (trueIndex == 1 && lane == Lane.LANE_C)
            {
                returnType = TurretType.NEXUS_TURRET;
            }

            return returnType;
        }

        static void LoadProtection()
        {
            //I can't help but feel there's a better way to do this
            Dictionary<TeamId, List<Inhibitor>> TeamInhibitors = new Dictionary<TeamId, List<Inhibitor>> { { TeamId.TEAM_BLUE, new List<Inhibitor>() }, { TeamId.TEAM_PURPLE, new List<Inhibitor>() } };
            foreach (var teams in InhibitorList.Keys)
            {
                foreach (var lane in InhibitorList[teams].Keys)
                {
                    TeamInhibitors[teams].Add(InhibitorList[teams][lane]);
                }
            }

            foreach (var nexus in NexusList)
            {
                // Adds Protection to Nexus
                AddProtection(nexus, TurretList[nexus.Team][Lane.LANE_C].FindAll(turret => turret.Type == TurretType.NEXUS_TURRET).ToArray(), TeamInhibitors[nexus.Team].ToArray());
            }

            foreach (var InhibTeam in TeamInhibitors.Keys)
            {
                foreach (var inhibitor in TeamInhibitors[InhibTeam])
                {
                    var inhibitorTurret = TurretList[inhibitor.Team][inhibitor.Lane].First(turret => turret.Type == TurretType.INHIBITOR_TURRET);

                    // Adds Protection to Inhibitors
                    if (inhibitorTurret != null)
                    {
                        // Depends on the first available inhibitor turret.
                        AddProtection(inhibitor, false, inhibitorTurret);
                    }

                    // Adds Protection to Turrets. On Twisted Treeline each lane has exactly two
                    // turrets: the inner-type turret (L_02/R_02) is the OUTERMOST turret a minion
                    // meets and must stay targetable from the start (no protection entry), while the
                    // inhibitor turret (C_06/C_07) sits behind it and is protected until the inner
                    // turret falls. Mirror of Map1's per-type chain, minus the OUTER tier TT lacks.
                    // (The old loop gave every lane turret an INNER dependency, so the inner turret
                    // depended on itself and never became targetable.)
                    foreach (var turret in TurretList[inhibitor.Team][inhibitor.Lane])
                    {
                        if (turret.Type == TurretType.INHIBITOR_TURRET)
                        {
                            AddProtection(turret, false, TurretList[inhibitor.Team][inhibitor.Lane].First(dependTurret => dependTurret.Type == TurretType.INNER_TURRET));
                        }
                    }
                }
                foreach (var turret in TurretList[InhibTeam][Lane.LANE_C])
                {
                    AddProtection(turret, false, TeamInhibitors[InhibTeam].ToArray());
                }
            }
        }
    }
}
