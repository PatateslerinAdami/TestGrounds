using GameServerCore.Domain;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Collections.Generic;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static LeagueSandbox.GameServer.API.ApiGameEvents;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;

namespace MapScripts.Map8
{
    public class HIDEANDSEEK : IMapScript
    {
        public MapScriptMetadata MapScriptMetadata { get; set; } = new MapScriptMetadata
        {
            MinionSpawnEnabled = false,
            OverrideSpawnPoints = true,
            RecallSpellItemId = 2005,
            InitialLevel = 3,
        };

        public bool HasFirstBloodHappened { get; set; } = false;
        public long NextSpawnTime { get; set; } = 90 * 1000;
        public string LaneMinionAI { get; set; } = "OdinLaneMinionAI";

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

        // Timers
        private float _gameTime = 0f;
        private float _scoreTickTimer = 0f;
        private bool _seekersReleased = false;
        private bool _gameEnded = false;
        private const int MAX_HIDER_LIVES = 5;
        private const int MAX_SEEKER_TIME = 300;

        private Dictionary<TeamId, int> TeamScores = new Dictionary<TeamId, int>
        {
            { TeamId.TEAM_BLUE, MAX_HIDER_LIVES },
            { TeamId.TEAM_PURPLE, MAX_SEEKER_TIME }
        };

        public Dictionary<TeamId, LevelProp> TeamStairs = new Dictionary<TeamId, LevelProp>();
        public Dictionary<TeamId, LevelProp> Nexus = new Dictionary<TeamId, LevelProp>();
        public Dictionary<int, LevelProp> SwainBeams = new Dictionary<int, LevelProp>();

        public Dictionary<TeamId, Dictionary<int, Dictionary<int, Vector2>>> PlayerSpawnPoints { get; } = new Dictionary<TeamId, Dictionary<int, Dictionary<int, Vector2>>>
        {
            {TeamId.TEAM_BLUE, new Dictionary<int, Dictionary<int, Vector2>>{
                { 5, new Dictionary<int, Vector2>{
                    { 1, new Vector2(687.99036f, 4281.2314f) }, { 2, new Vector2(687.99036f, 4061.2314f) },
                    { 3, new Vector2(478.79034f,3993.2314f) }, { 4, new Vector2(349.39032f,4171.2314f) },
                    { 5, new Vector2(438.79034f,4349.2314f) }
                }},
                {1, new Dictionary<int, Vector2>{ { 1, new Vector2(580f, 4124f) } }}
            }},
            {TeamId.TEAM_PURPLE, new Dictionary<int, Dictionary<int, Vector2>>{
                { 5, new Dictionary<int, Vector2>{
                    { 1, new Vector2(13468.365f,4281.2324f) }, { 2, new Vector2(13468.365f,4061.2324f) },
                    { 3, new Vector2(13259.165f,3993.2324f) }, { 4, new Vector2(13129.765f,4171.2324f) },
                    { 5, new Vector2(13219.165f,4349.2324f) }
                }},
                {1, new Dictionary<int, Vector2>{ { 1, new Vector2(13310f, 4124f) } }}
            }},
        };

        private class ScheduledEvent
        {
            public float Time;
            public Action Action;
            public bool Executed;
        }
        private List<ScheduledEvent> _events = new List<ScheduledEvent>();

        public void Init(Dictionary<GameObjectTypes, List<MapObject>> mapObjects)
        {
            LevelScriptObjects.LoadObjects(mapObjects);
            CreateLevelProps.CreateProps(this);
        }

        private void Schedule(float timeInSeconds, Action action)
        {
            _events.Add(new ScheduledEvent { Time = timeInSeconds * 1000f, Action = action, Executed = false });
        }

        public void OnMatchStart()
        {
            LevelScriptObjects.OnMatchStart();

            AddParticle(null, Nexus[TeamId.TEAM_BLUE], "Odin_Crystal_blue", Nexus[TeamId.TEAM_BLUE].Position, 25000, bone: "center_crystal").DisableFoW = true;
            AddParticle(null, Nexus[TeamId.TEAM_PURPLE], "Odin_Crystal_purple", Nexus[TeamId.TEAM_PURPLE].Position, 25000, bone: "center_crystal").DisableFoW = true;

            AddParticle(null, null, "Odin_Forcefield_blue", new Vector2(580f, 4124f), 15.0f).DisableFoW = true;
            AddParticle(null, null, "Odin_Forcefield_purple", new Vector2(13310f, 4124f), 60.0f).DisableFoW = true;

            foreach (var stair in TeamStairs.Values)
            {
                NotifyPropAnimation(stair, "Open", (AnimationFlags)1, 0.0f, false);
            }

            NotifyPropAnimation(SwainBeams[1], "PeckA", (AnimationFlags)1, 0.0f, false);
            NotifyPropAnimation(SwainBeams[2], "PeckB", (AnimationFlags)1, 0.0f, false);
            NotifyPropAnimation(SwainBeams[3], "PeckC", (AnimationFlags)1, 0.0f, false);

            foreach (var champion in GetAllPlayers())
            {
                SetMovementRestriction(champion, GetFountainPosition(champion.Team), 400f, restrictCam: false);

                if (champion.Team == TeamId.TEAM_BLUE)
                {
                    ApiEventManager.OnPreDealDamage.AddListener(this, champion, OnPreDealDamage, false);
                }

                ApiEventManager.OnDeath.AddListener(this, champion, OnChampionDeath, false);
            }

            NotifyGameScore(TeamId.TEAM_BLUE, 500);
            NotifyGameScore(TeamId.TEAM_PURPLE, 500);

            PrintChat("<font color='#00FFFF'>[Hide and Seek]</font> Hiders (Blue) have 15 seconds to hide!");
            PrintChat("<font color='#00FFFF'>[Hide and Seek]</font> Seekers (Purple) will be released in 60 seconds!");

            SetupEvents();
        }

        private void SetupEvents()
        {
            //For blue
            Schedule(3f, () => {
                NotifyPropAnimation(TeamStairs[TeamId.TEAM_BLUE], "Close1", (AnimationFlags)2, 17.5f, false);
                SpawnCrystalBeams(TeamId.TEAM_BLUE, 4);
                SpawnCenterCrystalHit(TeamId.TEAM_BLUE);
            });
            Schedule(6f, () => {
                NotifyPropAnimation(TeamStairs[TeamId.TEAM_BLUE], "Close2", (AnimationFlags)2, 20.0f, false);
                SpawnCrystalBeams(TeamId.TEAM_BLUE, 3);
            });
            Schedule(9f, () => {
                NotifyPropAnimation(TeamStairs[TeamId.TEAM_BLUE], "Close3", (AnimationFlags)2, 20.0f, false);
                SpawnCrystalBeams(TeamId.TEAM_BLUE, 2);
            });
            Schedule(12f, () => {
                NotifyPropAnimation(TeamStairs[TeamId.TEAM_BLUE], "Close4", (AnimationFlags)2, 20.0f, false);
                SpawnCrystalBeams(TeamId.TEAM_BLUE, 1);
            });
            Schedule(15f, () => {
                NotifyPropAnimation(TeamStairs[TeamId.TEAM_BLUE], "Raise", (AnimationFlags)2, 6.7f, false);
                ReleaseTeam(TeamId.TEAM_BLUE);
                PrintChat("<font color='#00FF00'>[Hide and Seek]</font> Hiders have been released! Run!");
            });
            Schedule(16f, () => {
                NotifyPropAnimation(TeamStairs[TeamId.TEAM_BLUE], "Raised_Idle", (AnimationFlags)1, 4.25f, false);
            });

            // For purple
            Schedule(12f, () => {
                NotifyPropAnimation(TeamStairs[TeamId.TEAM_PURPLE], "Close1", (AnimationFlags)2, 17.5f, false);
                SpawnCrystalBeams(TeamId.TEAM_PURPLE, 4);
                SpawnCenterCrystalHit(TeamId.TEAM_PURPLE);
            });
            Schedule(24f, () => {
                NotifyPropAnimation(TeamStairs[TeamId.TEAM_PURPLE], "Close2", (AnimationFlags)2, 20.0f, false);
                SpawnCrystalBeams(TeamId.TEAM_PURPLE, 3);
            });
            Schedule(36f, () => {
                NotifyPropAnimation(TeamStairs[TeamId.TEAM_PURPLE], "Close3", (AnimationFlags)2, 20.0f, false);
                SpawnCrystalBeams(TeamId.TEAM_PURPLE, 2);
            });
            Schedule(48f, () => {
                NotifyPropAnimation(TeamStairs[TeamId.TEAM_PURPLE], "Close4", (AnimationFlags)2, 20.0f, false);
                SpawnCrystalBeams(TeamId.TEAM_PURPLE, 1);
            });
            Schedule(60f, () => {
                NotifyPropAnimation(TeamStairs[TeamId.TEAM_PURPLE], "Raise", (AnimationFlags)2, 6.7f, false);
                ReleaseTeam(TeamId.TEAM_PURPLE);
                _seekersReleased = true;
                PrintChat("<font color='#FF0000'>[Hide and Seek]</font> Seekers have been released! Ready or not, here they come!");
            });
            Schedule(62f, () => {
                NotifyPropAnimation(TeamStairs[TeamId.TEAM_PURPLE], "Raised_Idle", (AnimationFlags)1, 4.25f, false);
            });
        }

        private void SpawnCrystalBeams(TeamId team, int stage)
        {
            string boneL = team == TeamId.TEAM_BLUE ? $"Crystal_l_{stage}_aim" : $"chaos_Crystal_l_{stage}_aim";
            string boneR = team == TeamId.TEAM_BLUE ? $"Crystal_r_{stage}_aim" : $"chaos_Crystal_r_{stage}_aim";
            TeamId enemyTeam = team == TeamId.TEAM_BLUE ? TeamId.TEAM_PURPLE : TeamId.TEAM_BLUE;

            AddParticleTarget(null, TeamStairs[team], "odin_crystal_beam_green", Nexus[team], 25000.0f, 1, boneL, "center_crystal", teamOnly: team).DisableFoW = true;
            AddParticleTarget(null, TeamStairs[team], "odin_crystal_beam_green", Nexus[team], 25000.0f, 1, boneR, "center_crystal", teamOnly: team).DisableFoW = true;

            AddParticleTarget(null, TeamStairs[team], "odin_crystal_beam_red", Nexus[team], 25000.0f, 1, boneL, "center_crystal", teamOnly: enemyTeam).DisableFoW = true;
            AddParticleTarget(null, TeamStairs[team], "odin_crystal_beam_red", Nexus[team], 25000.0f, 1, boneR, "center_crystal", teamOnly: enemyTeam).DisableFoW = true;
        }

        private void SpawnCenterCrystalHit(TeamId team)
        {
            string particle = team == TeamId.TEAM_BLUE ? "Odin_crystal_beam_hit_blue" : "Odin_crystal_beam_hit_purple";
            AddParticle(null, Nexus[team], particle, Nexus[team].Position, 25000.0f, 1, "center_crystal").DisableFoW = true;
        }

        private void ReleaseTeam(TeamId team)
        {
            foreach (var champion in GetAllPlayersFromTeam(team))
            {
                SetMovementRestriction(champion, Vector2.Zero, 0f, restrictCam: false);
            }
        }

        private void OnPreDealDamage(DamageData damageData)
        {
            if (damageData.Target == null || damageData.Target.Team == TeamId.TEAM_PURPLE)
            {
                damageData.Damage = 0;
                damageData.PostMitigationDamage = 0;
            }
        }

        public void Update(float diff)
        {
            if (_gameEnded) return;

            LevelScriptObjects.OnUpdate(diff);
            _gameTime += diff;

            foreach (var ev in _events)
            {
                if (!ev.Executed && _gameTime >= ev.Time)
                {
                    ev.Executed = true;
                    ev.Action?.Invoke();
                }
            }

            if (_seekersReleased)
            {
                _scoreTickTimer += diff;
                if (_scoreTickTimer >= 1000f)
                {
                    _scoreTickTimer -= 1000f;
                    DrainScore(TeamId.TEAM_PURPLE, 1);
                }
            }
        }

        private void OnChampionDeath(DeathData deathData)
        {
            if (_gameEnded) return;

            if (deathData.Unit is Champion victim && victim.Team == TeamId.TEAM_BLUE)
            {
                DrainScore(TeamId.TEAM_BLUE, 1);
            }
        }

        private void DrainScore(TeamId team, int amount)
        {
            TeamScores[team] = Math.Max(0, TeamScores[team] - amount);

            int displayScore = 0;
            if (team == TeamId.TEAM_BLUE)
            {
                displayScore = (int)(((float)TeamScores[team] / (float)MAX_HIDER_LIVES) * 500f);
            }
            else if (team == TeamId.TEAM_PURPLE)
            {
                displayScore = (int)(((float)TeamScores[team] / (float)MAX_SEEKER_TIME) * 500f);
            }

            NotifyGameScore(team, displayScore);

            if (TeamScores[team] <= 0)
            {
                _gameEnded = true;
                EndGame(team, Nexus[team].GetPosition3D());
            }
        }

        public Vector2 GetFountainPosition(TeamId team)
        {
            return LevelScriptObjects.FountainList[team].Position;
        }

        public void OnPlayerJoin(int userId)
        {
            LevelScriptObjects.SyncStateForPlayer(userId);
        }
    }
}