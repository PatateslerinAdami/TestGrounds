using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using System.Collections.Generic;
using GameServerCore.Domain;
using System.Numerics;
using System.Linq;
using LeagueSandbox.GameServer.GameObjects;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.Content;

namespace MapScripts.Map1
{
    public static class LevelScriptObjects
    {
        private static Dictionary<GameObjectTypes, List<MapObject>> _mapObjects;

        public static Dictionary<TeamId, Fountain> FountainList = new Dictionary<TeamId, Fountain>();
        public static Dictionary<string, MapObject> SpawnBarracks = new Dictionary<string, MapObject>();
        public static Dictionary<TeamId, bool> AllInhibitorsAreDead = new Dictionary<TeamId, bool> { { TeamId.TEAM_BLUE, false }, { TeamId.TEAM_PURPLE, false } };
        static Dictionary<TeamId, Dictionary<Inhibitor, float>> DeadInhibitors = new Dictionary<TeamId, Dictionary<Inhibitor, float>> { { TeamId.TEAM_BLUE, new Dictionary<Inhibitor, float>() }, { TeamId.TEAM_PURPLE, new Dictionary<Inhibitor, float>() } };
        static List<Nexus> NexusList = new List<Nexus>();
        // One-shot for Riot's InitTimer("AllowDamageOnBuildings", 10, false) — see OnUpdate.
        static bool _allowDamageOnBuildingsFired = false;
        public static string LaneTurretAI = "TurretAI";

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
                { Lane.LANE_C, null },
                { Lane.LANE_R, null }}
            },
            {TeamId.TEAM_PURPLE, new Dictionary<Lane, Inhibitor>{
                { Lane.LANE_L, null },
                { Lane.LANE_C, null },
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
            {TeamId.TEAM_BLUE, "OrderInhibitor" },
            {TeamId.TEAM_PURPLE, "ChaosInhibitor" }
        };

        //Tower Models
        static Dictionary<TeamId, Dictionary<TurretType, string>> TowerModels { get; set; } = new Dictionary<TeamId, Dictionary<TurretType, string>>
        {
            {TeamId.TEAM_BLUE, new Dictionary<TurretType, string>
            {
                {TurretType.FOUNTAIN_TURRET, "OrderTurretShrine" },
                {TurretType.NEXUS_TURRET, "OrderTurretAngel" },
                {TurretType.INHIBITOR_TURRET, "OrderTurretDragon" },
                {TurretType.INNER_TURRET, "OrderTurretNormal2" },
                {TurretType.OUTER_TURRET, "OrderTurretNormal" },
            } },
            {TeamId.TEAM_PURPLE, new Dictionary<TurretType, string>
            {
                {TurretType.FOUNTAIN_TURRET, "ChaosTurretShrine" },
                {TurretType.NEXUS_TURRET, "ChaosTurretNormal" },
                {TurretType.INHIBITOR_TURRET, "ChaosTurretGiant" },
                {TurretType.INNER_TURRET, "ChaosTurretWorm2" },
                {TurretType.OUTER_TURRET, "ChaosTurretWorm" },
            } }
        };

        //Turret Items
        static Dictionary<TurretType, int[]> TurretItems { get; set; } = new Dictionary<TurretType, int[]>
        {
            { TurretType.OUTER_TURRET, new[] { 1500, 1501, 1502, 1503 } },
            { TurretType.INNER_TURRET, new[] { 1500, 1501, 1502, 1503 } },
            { TurretType.INHIBITOR_TURRET, new[] { 1501, 1502, 1503 } },
            { TurretType.NEXUS_TURRET, new[] { 1501, 1502, 1503 } }
        };

        static StatsModifier TurretStatsModifier = new StatsModifier();
        static StatsModifier OuterTurretStatsModifier = new StatsModifier();
        public static void LoadBuildings(Dictionary<GameObjectTypes, List<MapObject>> mapObjects)
        {
            _mapObjects = mapObjects;

            CreateBuildings();

            LoadSpawnBarracks();
            LoadFountains();
        }

        static TeamId EnemyTeam(TeamId team)
        {
            return team == TeamId.TEAM_BLUE ? TeamId.TEAM_PURPLE : TeamId.TEAM_BLUE;
        }

        /// <summary>
        /// Structure protection (Riot Map1 LevelScript.lua): SetInvulnerable(true) +
        /// SetNotTargetableToTeam(true, enemy) — the locked state every structure spawns in and
        /// deeper chain members keep until the structure in front of them falls. Enemy-untargetable
        /// (not globally) so friendly systems still see it; minions/champions of the enemy can
        /// neither target nor damage it.
        /// </summary>
        static void LockStructure(AttackableUnit unit)
        {
            unit.SetStatus(StatusFlags.Invulnerable, true);
            unit.SetIsTargetableToTeam(EnemyTeam(unit.Team), false);
        }

        /// <summary>
        /// Riot's chain unlock: SetInvulnerable(false) + SetTargetable(true)
        /// (DeactivateCorrectStructure / HandleDestroyedObject / BarrackReactiveEvent).
        /// </summary>
        static void UnlockStructure(AttackableUnit unit)
        {
            unit.SetStatus(StatusFlags.Invulnerable, false);
            unit.SetStatus(StatusFlags.Targetable, true);
            unit.SetIsTargetableToTeam(EnemyTeam(unit.Team), true);
        }

        static IEnumerable<LaneTurret> GetNexusTurrets(TeamId team)
        {
            return TurretList[team].Values.SelectMany(l => l).Where(t => t.Type == TurretType.NEXUS_TURRET);
        }

        static void UnlockNextInLane(TeamId team, Lane lane, TurretType type)
        {
            var next = TurretList[team][lane].Find(t => t.Type == type && !t.IsDead);
            if (next != null)
            {
                UnlockStructure(next);
            }
        }

        /// <summary>
        /// 1:1 port of Riot's DeactivateCorrectStructure (Map1 LevelScript.lua): each turret death
        /// unlocks the next structure down its lane — outer -> inner -> inhibitor turret ->
        /// inhibitor; a nexus turret death opens the nexus once BOTH nexus turrets are down.
        /// </summary>
        public static void OnTurretDeath(DeathData deathData)
        {
            if (deathData.Unit is not LaneTurret turret)
            {
                return;
            }

            switch (turret.Type)
            {
                case TurretType.OUTER_TURRET:
                    UnlockNextInLane(turret.Team, turret.Lane, TurretType.INNER_TURRET);
                    break;
                case TurretType.INNER_TURRET:
                    UnlockNextInLane(turret.Team, turret.Lane, TurretType.INHIBITOR_TURRET);
                    break;
                case TurretType.INHIBITOR_TURRET:
                    var inhibitor = InhibitorList[turret.Team].GetValueOrDefault(turret.Lane);
                    if (inhibitor != null && !inhibitor.IsDead)
                    {
                        UnlockStructure(inhibitor);
                    }
                    break;
                case TurretType.NEXUS_TURRET:
                    // HQ_TOWER1/2 branch: only when the OTHER nexus turret is already down.
                    if (GetNexusTurrets(turret.Team).All(t => t.IsDead))
                    {
                        var nexus = NexusList.Find(n => n.Team == turret.Team);
                        if (nexus != null)
                        {
                            UnlockStructure(nexus);
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Riot AllowDamageOnBuildings (fired by InitTimer 10s after level init): opens the FRONT
        /// tower of every lane; everything deeper stays locked until the chain unlocks it.
        /// </summary>
        static void AllowDamageOnBuildings()
        {
            foreach (var team in TurretList.Keys)
            {
                foreach (var lane in TurretList[team].Keys)
                {
                    foreach (var turret in TurretList[team][lane])
                    {
                        if (turret.Type == TurretType.OUTER_TURRET)
                        {
                            UnlockStructure(turret);
                        }
                    }
                }
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

                foreach (var lane in TurretList[team].Keys)
                {
                    foreach (var turret in TurretList[team][lane])
                    {
                        if (turret.Type == TurretType.FOUNTAIN_TURRET)
                        {
                            continue;
                        }
                        else if (turret.Type != TurretType.NEXUS_TURRET)
                        {
                            TurretHealthModifier.HealthPoints.BaseBonus = 250.0f * Players[enemyTeam].Count;
                        }
                        else
                        {
                            TurretHealthModifier.HealthPoints.BaseBonus = 125.0f * Players[enemyTeam].Count;
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

            //Outer turrets dont get armor
            OuterTurretStatsModifier.MagicResist.FlatBonus = 1;
            OuterTurretStatsModifier.AttackDamage.FlatBonus = 4;
        }

        public static void OnUpdate(float diff)
        {
            var gameTime = GameTime();

            // Riot InitTimer("AllowDamageOnBuildings", 10, false): 10s after init the front towers
            // open up; every structure spawned locked (see LockStructure calls at creation).
            if (!_allowDamageOnBuildingsFired && gameTime >= 10.0f * 1000)
            {
                _allowDamageOnBuildingsFired = true;
                AllowDamageOnBuildings();
            }

            if (gameTime >= timeCheck && timesApplied < 30)
            {
                UpdateTowerStats();
            }

            if (gameTime >= outerTurretTimeCheck && outerTurretTimesApplied < 7)
            {
                UpdateOuterTurretStats();
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
                        // Revive ONLY now, at the actual respawn. SetState(RespawningState) flips
                        // IsDead=false (re)bakes the footprint and makes the inhibitor targetable
                        // again via NotifyState. Doing this earlier made lane minions acquire the
                        // not-yet-respawned inhibitor as a target (IsDead=false + the global
                        // StatusFlags.Targetable still set) and bunch up "stuck" in front of it.
                        inhibitor.SetState(DampenerState.RespawningState);
                        // Riot BarrackReactiveEvent: SetInvulnerable(false) + SetTargetable(true)
                        // on the respawned dampener (it stays attackable — its turret never respawns).
                        inhibitor.SetStatus(StatusFlags.Invulnerable, false);
                        inhibitor.Stats.CurrentHealth = inhibitor.Stats.HealthPoints.Total;
                        inhibitor.NotifyState();
                        DeadInhibitors[inhibitor.Team].Remove(inhibitor);
                        // Riot ApplyBarracksRespawnReductions: once ALL of the team's barracks are
                        // restored, the (still alive) nexus towers become invulnerable + enemy-
                        // untargetable again.
                        if (DeadInhibitors[inhibitor.Team].Count == 0)
                        {
                            foreach (var nexusTurret in GetNexusTurrets(inhibitor.Team))
                            {
                                if (!nexusTurret.IsDead)
                                {
                                    LockStructure(nexusTurret);
                                }
                            }
                        }
                        // A respawned inhibitor means the team is no longer fully down — recompute the
                        // gate that drives DoubleSuperMinionWave. Without this it stays true forever once
                        // all inhibitors were simultaneously dead, spawning double-supers permanently
                        // (S4 recomputes totalNumberBarracks each wave). See docs/LANE_MINION_DECOMP_AUDIT.md.
                        AllInhibitorsAreDead[inhibitor.Team] =
                            DeadInhibitors[inhibitor.Team].Count == InhibitorList[inhibitor.Team].Count;
                    }
                }
            }
        }

        public static void OnNexusDeath(DeathData deathaData)
        {
            var nexus = deathaData.Unit;
            EndGame(nexus.Team, new Vector3(nexus.Position.X, nexus.GetHeight(), nexus.Position.Y), deathData: deathaData);
        }

        public static void OnInhibitorDeath(DeathData deathData)
        {
            var inhibitor = deathData.Unit as Inhibitor;

            // Riot HandleDestroyedObject dampener branch (Map1 LevelScript.lua): the LEVEL SCRIPT
            // drives the state machine — SetDampenerState(RegenerationState) + SetInvulnerable(true)
            // + SetTargetable(false) (NotifyState handles the enemy-targetability off Regeneration).
            inhibitor.SetState(DampenerState.RegenerationState);
            inhibitor.SetStatus(StatusFlags.Invulnerable, true);
            inhibitor.NotifyState(deathData);

            DeadInhibitors[inhibitor.Team].Add(inhibitor, inhibitor.RespawnTime * 1000);

            if (DeadInhibitors[inhibitor.Team].Count == InhibitorList[inhibitor.Team].Count)
            {
                AllInhibitorsAreDead[inhibitor.Team] = true;
            }

            // Any fallen dampener opens BOTH of its team's nexus towers; if both are already gone,
            // the nexus itself opens (Riot's GetTurret(...) == Nil fallback in the same branch).
            bool anyNexusTurretAlive = false;
            foreach (var nexusTurret in GetNexusTurrets(inhibitor.Team))
            {
                if (!nexusTurret.IsDead)
                {
                    UnlockStructure(nexusTurret);
                    anyNexusTurretAlive = true;
                }
            }
            if (!anyNexusTurretAlive)
            {
                var nexus = NexusList.Find(n => n.Team == inhibitor.Team);
                if (nexus != null)
                {
                    UnlockStructure(nexus);
                }
            }
        }

        // Super minions spawn for only the first part of an inhibitor's down-window, then stop while
        // it is still down. Riot (obj_Barracks::DisableInhibitor, mac decomp 4.17): the super-minion
        // timer is set to (respawn − 2×waveSpawnInterval) and DisableSuperMinions fires when it
        // expires; the inhibitor itself respawns at the full respawn time. With respawn = 240s and a
        // 30s wave interval that is a 180s super window followed by a ~60s super-less tail. We derive
        // the window from the existing respawn countdown (no separate timer): supers are active while
        // the remaining respawn time is still greater than 2×waveSpawnInterval.
        // See docs/LANE_MINION_ENGINE_INVERSION_PLAN.md (Phase 4) / LANE_MINION_DECOMP_AUDIT.md (S5).
        public const float SUPER_MINION_STOP_BEFORE_RESPAWN_MS = 2f * 30f * 1000f; // 2 × 30s wave interval

        /// <summary>
        /// True while the inhibitor of <paramref name="inhibitorTeam"/> in <paramref name="lane"/> is
        /// down AND still within its super-minion window (the first respawn−60s of the down-window).
        /// The enemy team's lane minions are super minions during this window.
        /// </summary>
        /// <summary>
        /// Number of <paramref name="team"/>'s inhibitors currently down (within their respawn
        /// window). Each one buffs the opposing team's minions (Riot ApplyBarracksDestructionBonuses,
        /// applied team-wide per dead inhibitor, reversed at full respawn — hence the full down-window
        /// count, not the 180s super window).
        /// </summary>
        public static int CountDeadInhibitors(TeamId team)
        {
            return DeadInhibitors.TryGetValue(team, out var dead) ? dead.Count : 0;
        }

        public static bool IsSuperMinionWindowActive(TeamId inhibitorTeam, Lane lane)
        {
            if (!InhibitorList.TryGetValue(inhibitorTeam, out var byLane)
                || !byLane.TryGetValue(lane, out var inhibitor) || inhibitor == null)
            {
                return false;
            }
            return DeadInhibitors[inhibitorTeam].TryGetValue(inhibitor, out var respawnRemaining)
                && respawnRemaining > SUPER_MINION_STOP_BEFORE_RESPAWN_MS;
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
                        if (turret.Type == TurretType.OUTER_TURRET || turret.Type == TurretType.FOUNTAIN_TURRET || (turret.Type == TurretType.INNER_TURRET && timesApplied >= 20))
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

        static float outerTurretTimeCheck = 30.0f * 1000;
        static byte outerTurretTimesApplied = 0;
        static void UpdateOuterTurretStats()
        {
            foreach (var team in TurretList.Keys)
            {
                foreach (var lane in TurretList[team].Keys)
                {
                    var turret = TurretList[team][lane].Find(x => x.Type == TurretType.OUTER_TURRET);

                    if (turret != null)
                    {
                        turret.AddStatModifier(OuterTurretStatsModifier);
                    }
                }
            }

            outerTurretTimesApplied++;
            outerTurretTimeCheck += 60.0f * 1000;
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
                SpawnBarracks.Add(spawnBarrack.Name, spawnBarrack);
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

                // ObjectCFG.cfg HQ_T1/T2: Collision Radius = 319.4445, PathfindingCollisionRadius
                // = 352.7778. Both restored 2026-06-07 (were 150/150): the old reduction targeted a
                // body-push drift loop that no longer exists (buildings are excluded from the
                // body-push quad; their footprints act via the nav grid only). The two values are
                // COUPLED: with CR=150 and the 352.78 footprint, melee reach (125+65+150=340)
                // couldn't touch the nexus — Riot's CR=319.44 gives reach 509 > 352.78.
                //
                // Position correction (2026-06-07): the HQ .sco header CentralPoint sits (-37,-13)
                // off the mesh bbox center (same delta on both HQ_T1/T2 — shared mesh). The nexus
                // geometry statically baked into the .aimesh navgrid is centered on the BBOX center
                // (measured blue ≈ (1167,1441), purple ≈ (12800,13040)), as is the rendered scene
                // mesh. Anchoring our 353 footprint at the raw CentralPoint pushed ~75u of blocked
                // air one-sidedly toward (-X,-Z) past the visible building (purple: toward the left
                // nexus turret; blue: toward the fountain).
                position += new Vector2(37f, 13f);
                // sightRange 1350 = real Map1 ObjectCFG.cfg HQ_T1/T2 PerceptionBubbleRadius
                // (verified; the prior 1700 was invented). Vision is provided intrinsically via
                // ObjBuilding auto-vision; no RevealStealth (structures grant sight, don't reveal).
                var nexus = CreateNexus(nexusObj.Name, NexusModels[teamId], position, teamId, 319, 1350, nexusStats, 353);

                ApiEventManager.OnDeath.AddListener(nexus, nexus, OnNexusDeath, true);
                // Spawns locked (Riot: HQ opens only once both nexus towers are down —
                // DeactivateCorrectStructure HQ_TOWER1/2, or the dampener-death fallback).
                LockStructure(nexus);
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

                // Pathing footprint restored to Riot's nav-grid bake (2026-06-07): ObjectCFG.cfg
                // PathfindingCollisionRadius — Order Barracks_T1 ≈ 185.5-187.2 per lane, Chaos
                // Barracks_T2 = 213.75. The earlier reduction to 100 targeted a body-push drift
                // loop that no longer exists (buildings are excluded from the body-push quad;
                // footprints act via the nav grid only). Without the full bake, lane minions cut
                // the corner ~90-115u tighter than Riot's, bunch at the inhibitor and briefly
                // stop on each other (replays show them passing on the wide arc without stopping).
                // Gameplay CollisionRadius is COUPLED to the footprint like the nexus
                // (HQ: CR 319.44 = bake 352.78 × 0.9055 — same ratio applied here). The old
                // CR=100 made melee attack range (125 + 30 + 100 = 255 from center for Akali)
                // fall short of the nearest standable spot outside the footprint
                // (214 bake + ~35 terrain-exit margin ≈ 250-266) → champions ran to the
                // recommended attack point and stood 10u out of range forever (2026-06-07).
                int inhibFootprint = teamId == TeamId.TEAM_BLUE ? 187 : 214;
                int inhibCollisionRadius = teamId == TeamId.TEAM_BLUE ? 169 : 194;
                // sightRange 1350 = real Map1 ObjectCFG.cfg Barracks_T1/T2 PerceptionBubbleRadius
                // (verified; was 0 = no vision, a bug — inhibitors DO grant sight). Intrinsic
                // auto-vision via ObjBuilding; no RevealStealth.
                var inhibitor = CreateInhibitor(inhibitorObj.Name, InhibitorModels[teamId], position, teamId, lane, inhibCollisionRadius, 1350, inhibitorStats, inhibFootprint);
                ApiEventManager.OnDeath.AddListener(inhibitor, inhibitor, OnInhibitorDeath, false);
                // Spawns locked (Riot: engine-intrinsic — obj_Barracks::OnNetworkIDAssigned sets
                // SetIsTargetable(false) + mIsInvulnerable=1, Barracks.cpp; the back turret's death
                // unlocks it via DeactivateCorrectStructure).
                LockStructure(inhibitor);
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
                    // The fountain/shrine turret is the ONE permanently-indestructible structure
                    // (engine-intrinsic in Riot; the level-script protection chain never covers it).
                    // Invulnerable so it can never be damaged, and untargetable to the enemy so AI
                    // units never path to it to attack an unkillable target (the inhibitor soft-lock
                    // failure mode). It still fires at enemies in the fountain.
                    var enemyOfTurret = teamId == TeamId.TEAM_BLUE ? TeamId.TEAM_PURPLE : TeamId.TEAM_BLUE;
                    fountainTurret.SetStatus(StatusFlags.Invulnerable, true);
                    fountainTurret.SetIsTargetableToTeam(enemyOfTurret, false);
                    continue;
                }

                var turretType = GetTurretType(turretObj.ParseIndex(), lane, teamId);

                if (turretType == TurretType.FOUNTAIN_TURRET)
                {
                    continue;
                }

                switch (turretObj.Name)
                {
                    case "Turret_T1_C_06":
                        lane = Lane.LANE_L;
                        break;
                    case "Turret_T1_C_07":
                        lane = Lane.LANE_R;
                        break;
                }

                var turret = CreateLaneTurret(turretObj.Name + "_A", TowerModels[teamId][turretType], position, teamId, turretType, lane, LaneTurretAI, turretObj);
                // Structure protection chain (Riot Map1 LevelScript.lua): every turret spawns
                // locked; AllowDamageOnBuildings (t=10s) opens the front towers, and each death
                // unlocks the next structure via OnTurretDeath (= DeactivateCorrectStructure).
                LockStructure(turret);
                ApiEventManager.OnDeath.AddListener(turret, turret, OnTurretDeath, true);
                TurretList[teamId][lane].Add(turret);
                AddObject(turret);
            }
        }

        static TurretType GetTurretType(int trueIndex, Lane lane, TeamId teamId)
        {
            TurretType returnType = TurretType.FOUNTAIN_TURRET;

            if (lane == Lane.LANE_C)
            {
                if (trueIndex < 3)
                {
                    returnType = TurretType.NEXUS_TURRET;
                    return returnType;
                }

                trueIndex -= 2;
            }

            switch (trueIndex)
            {
                case 1:
                case 4:
                case 5:
                    returnType = TurretType.INHIBITOR_TURRET;
                    break;
                case 2:
                    returnType = TurretType.INNER_TURRET;
                    break;
                case 3:
                    returnType = TurretType.OUTER_TURRET;
                    break;
            }

            return returnType;
        }

        // NOTE: the old ProtectionManager LoadProtection() (dependency-graph targetability poller)
        // was removed here — it duplicated the faithful event-driven chain above (LockStructure at
        // creation, AllowDamageOnBuildings at 10s, OnTurretDeath/OnInhibitorDeath unlocks, respawn
        // re-lock), which is the 1:1 port of Riot's Map1 LevelScript.lua state machine.
    }
}
