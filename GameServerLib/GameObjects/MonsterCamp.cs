using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using System.Collections.Generic;
using System.Numerics;

namespace GameServerLib.GameObjects
{
    public class MonsterCamp : GameObject
    {
        private TeamId[] _playerTeams = new TeamId[] { TeamId.TEAM_BLUE, TeamId.TEAM_PURPLE };
        private Dictionary<TeamId, bool> _teamSawLastDeath = new Dictionary<TeamId, bool>{
            { TeamId.TEAM_BLUE, true },
            { TeamId.TEAM_PURPLE, true },
        };
        private Dictionary<TeamId, bool> _isAliveForTeam = new Dictionary<TeamId, bool>{
            { TeamId.TEAM_BLUE, false },
            { TeamId.TEAM_PURPLE, false },
        };
        private Dictionary<int, bool> _isAliveForPlayer = new Dictionary<int, bool>();

        public byte CampIndex { get; set; }
        public new Vector3 Position { get; set; }
        public byte SideTeamId { get; set; }
        public string MinimapIcon { get; set; }
        public byte RevealEvent { get; set; }
        public float Expire { get; set; }
        public int TimerType { get; set; }
        public float SpawnDuration { get; set; }
        public float DoPlayVO { get; set; }
        public bool IsAlive { get; set; } = false;
        public float RespawnTimer { get; set; }
        public List<Monster> Monsters { get; set; } = new List<Monster>();

        public override bool IsAffectedByFoW => true;

        public MonsterCamp(
            Game game, Vector3 position, byte groupNumber, TeamId teamSideOfTheMap,
            string campTypeIcon, float respawnTimer, bool doPlayVO = true, byte revealEvent = 74,
            float spawnDuration = 0
        ): base(
            game, new Vector2(position.X, position.Z), 0, 0, 0, team: TeamId.TEAM_NEUTRAL
        )
        {
            Position = position;
            CampIndex = groupNumber;
            RevealEvent = revealEvent;
            MinimapIcon = campTypeIcon;
            RespawnTimer = respawnTimer;
            SideTeamId = (byte)teamSideOfTheMap;
            SpawnDuration = spawnDuration;
        }

        public override void LateUpdate(float diff)
        {
            foreach(TeamId team in _playerTeams)
            {
                if(IsVisibleByTeam(team))
                {
                    _isAliveForTeam[team] = IsAlive;
                }
            }
        }

        public override void Sync(int userId, TeamId team, bool visible, bool forceSpawn = false)
        {
            base.Sync(userId, team, visible, forceSpawn);

            bool isAliveForTeam = _isAliveForTeam[team];
            bool isAliveForPlayer = _isAliveForPlayer.GetValueOrDefault(userId, false);
            if
            (
                (forceSpawn && isAliveForPlayer) // Reconnect
                || (isAliveForPlayer != isAliveForTeam
                    // NearSighted STATUS is the correct gate here: nearsight cuts the player off from SHARED team
                    // vision knowledge, which is exactly what the camp alive/dead minimap state is.
                    // ObjectManager.UpdateVisionSpawnAndSync branches per-player visibility on the
                    // same flag. A transition missed while nearsighted self-heals: the next per-tick
                    // Sync sees isAliveForPlayer != isAliveForTeam and catches up. Null peer info
                    // (invalid userId) counts as not-nearsighted.
                    && _game.PlayerManager.GetPeerInfo(userId)?.Champion?.Status.HasFlag(StatusFlags.NearSighted) != true)
            )
            {
                if(_isAliveForPlayer[userId] = isAliveForTeam)
                {
                    _game.PacketNotifier.NotifyS2C_ActivateMinionCamp(this, userId);
                }
                else
                {
                    _game.PacketNotifier.NotifyS2C_Neutral_Camp_Empty(this, userId: userId);
                }
            }

        }

        protected override void OnSpawn(int userId, TeamId team, bool doVision = false)
        {
            _game.PacketNotifier.NotifyS2C_CreateMinionCamp(this, userId);
        }

        protected override void OnEnterVision(int userId, TeamId team)
        {
        }

        protected override void OnLeaveVision(int userId, TeamId team)
        {
        }

        /// <summary>
        /// Absolute respawn time in game-SECONDS, for the HUD respawn timer carried in
        /// S2C_Neutral_Camp_Empty.TimerExpire (only meaningful for camps with a TimerType != 0, i.e.
        /// Baron/Dragon). The camp-empty packet is only sent while the camp is dead and RespawnTimer is
        /// counting down, so GameTime + RespawnTimer is the (constant) absolute respawn time.
        /// </summary>
        public float GetTimerExpire()
        {
            return (_game.GameTime + RespawnTimer) / 1000f;
        }

        public Monster AddMonster(Monster monster)
        {
            var aiscript = monster.AIScript.ToString().Remove(0, 10);
            var campMonster = new Monster
            (
                _game, monster.Name, monster.Model, monster.Position,
                monster.FacePoint, this, monster.Team, 0,
                monster.SpawnAnimation, monster.IsTargetable, monster.IgnoresCollision, null, aiscript,
                monster.DamageBonus, monster.HealthBonus, monster.InitialLevel
            );
            while(campMonster.Stats.Level < monster.InitialLevel)
            {
                campMonster.Stats.LevelUp();
            }
            Monsters.Add(campMonster);
            ApiEventManager.OnDeath.AddListener(campMonster, campMonster, OnMonsterDeath, true);
            _game.ObjectManager.AddObject(campMonster);
            // (Spawn facing is carried by S2C_CreateNeutral.FaceDirectionPosition — see
            // PacketNotifier.ConstructCreateNeutralPacket. No separate FaceDirection needed here.)

            IsAlive = true;
            foreach(TeamId team in _playerTeams)
            {
                if(_teamSawLastDeath[team])
                {
                    _teamSawLastDeath[team] = false;
                    _isAliveForTeam[team] = true;
                }
            }

            return campMonster;
        }

        public void OnMonsterDeath(DeathData deathData)
        {
            Monster monster = deathData.Unit as Monster;
            Monsters.Remove(monster);
            if (Monsters.Count == 0)
            {
                IsAlive = false;
                foreach(TeamId team in _playerTeams)
                {
                    _teamSawLastDeath[team] = monster.IsVisibleByTeam(team) || IsVisibleByTeam(team);
                    if (_teamSawLastDeath[team])
                    {
                        _isAliveForTeam[team] = false;
                    }
                }
            }
        }
    }
}
