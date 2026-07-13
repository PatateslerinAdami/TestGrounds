using GameServerCore.NetInfo;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Packets;
using System.Collections.Generic;
using System.Numerics;

namespace LeagueSandbox.GameServer.Players
{
    public class PlayerManager
    {
        private NetworkIdManager _networkIdManager;
        private Game _game;

        private List<ClientInfo> _players = new List<ClientInfo>();
        private Dictionary<TeamId, int> _userIdsPerTeam = new Dictionary<TeamId, int>
        {
            { TeamId.TEAM_BLUE, 0 },
            { TeamId.TEAM_PURPLE, 0 }
        };

        public PlayerManager(Game game)
        {
            _game = game;
            _networkIdManager = game.NetworkIdManager;
        }

        public void AddPlayer(PlayerConfig config)
        {
            var summonerSkills = new[]
            {
                config.Summoner1,
                config.Summoner2
            };
            var teamId = config.Team;
            var info = new ClientInfo(
                config.Rank,
                teamId,
                config.Ribbon,
                config.Icon,
                config.Skin,
                config.Name,
                summonerSkills,
                config.PlayerID,
                config.EnemyRibbon,
                config.SummonerLevel
            );

            info.ClientId = _players.Count;
            _userIdsPerTeam[teamId]++;

            // Bot slot: either declared explicitly ("isBot": true in GameInfo) or via the legacy
            // PlayerId <= -1 convention. Bots are spawned at game start with the bot AI and no
            // client is ever expected on the slot.
            bool isBot = config.IsBot || config.PlayerID <= -1;

            if (isBot)
            {
                // Mark the slot as "connected" so game start / AFK protection / all-players-left
                // checks never wait on it.
                info.IsDisconnected = false;
                info.IsStartedClient = true;
                info.IsMatchingVersion = true;
            }

            var c = new Champion(
                _game,
                config.Champion,
                config.Runes,
                config.Talents,
                info,
                0,
                teamId,
                AIScript: isBot && string.IsNullOrEmpty(config.AIScript) ? "BotAI" : config.AIScript
            );

            if (isBot)
            {
                // Riot flags bots with IsBot=true on S2C_CreateHero (replay-verified: 4.20 bot games send
                // CreateHero with IsBot=1 / ClientID=-1 for every bot; SpawnBotS2C is never used). The
                // client then renders the bot name as "(Champion) Bot" — the faithful behaviour. (Previously
                // forced false to keep custom gamertag names; we choose fidelity over that gimmick.)
                c.IsBot = true;
                c.SetStatus(StatusFlags.CanMove, true);
                c.SetStatus(StatusFlags.CanMoveEver, true);
                c.SetStatus(StatusFlags.CanAttack, true);
                c.SetStatus(StatusFlags.CanCast, true);
                c.SetStatus(StatusFlags.Targetable, true);
            }

            var pos = c.GetSpawnPosition(_userIdsPerTeam[teamId]);
            c.SetPosition(pos, false);
            c.StopMovement();
            c.UpdateMoveOrder(OrderType.Stop);
            info.Champion = c;
            _players.Add(info);

            _game.ObjectManager.AddObject(c);
        }

        public void AddPlayer(ClientInfo info)
        {
            info.ClientId = _players.Count;
            _players.Add(info);
        }

        // GetPlayerFromPeer
        public ClientInfo GetPeerInfo(int clientId)
        {
            if (0 <= clientId && clientId < _players.Count)
            {
                return _players[clientId];
            }
            return null;
        }

        public ClientInfo GetClientInfoByPlayerId(long playerId)
        {
            return _players.Find(c => c.PlayerId == playerId);
        }

        public ClientInfo GetClientInfoByChampion(Champion champ)
        {
            return _players.Find(c => c.Champion == champ);
        }

        public List<ClientInfo> GetPlayers(bool includeBots = true)
        {
            if (!includeBots)
            {
                return _players.FindAll(c => !c.Champion.IsBot);
            }

            return _players;
        }
    }
}
