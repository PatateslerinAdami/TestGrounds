using GameServerCore.Enums;
using GameServerCore.NetInfo;
using GameServerCore.Packets.Handlers;
using GameServerCore.Packets.PacketDefinitions.Requests;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.Players;
using LeagueSandbox.GameServer.Quests;
using System.Linq;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleStartGame : PacketHandlerBase<StartGameRequest>
    {
        private readonly Game _game;
        private readonly PlayerManager _playerManager;
        private bool _shouldStartAsSoonAsPossible = false;

        public HandleStartGame(Game game)
        {
            _game = game;
            _playerManager = game.PlayerManager;
        }

        public override bool HandlePacket(int userId, StartGameRequest req)
        {
            var peerInfo = _playerManager.GetPeerInfo(userId);
            peerInfo.IsDisconnected = false;

            if (_game.IsRunning)
            {
                StartFor(peerInfo);
                return true;
            }
            else
            {    
                TryStart();
            }
            return true;
        }

        public void ForceStart()
        {
            _shouldStartAsSoonAsPossible = true;
            TryStart();
        }

        private void TryStart()
        {
            var players = _playerManager.GetPlayers(false);

            bool isPossibleToStart;
            if(_shouldStartAsSoonAsPossible)
            {
                isPossibleToStart = players.Any(p => !p.IsDisconnected);
            }
            else
            {
                isPossibleToStart = players.All(p => !p.IsDisconnected);
            }

            if(!isPossibleToStart)
            {
                return;
            }

            foreach (var player in players)
            {
                if(!player.IsDisconnected)
                {
                    StartFor(player);
                }
            }
            _game.Start();
        }

        private void StartFor(ClientInfo player)
        {
            if (_game.IsPaused)
            {
                _game.PacketNotifier.NotifyPausePacket(player, (int)_game.PauseTimeLeft, true);
            }
            
            _game.PacketNotifier.NotifyGameStart(player.ClientId);

            if (_game.IsRunning)
            {
                var announcement = new OnReconnect { OtherNetID = player.Champion.NetId };
                _game.PacketNotifier.NotifyS2C_OnEventWorld(announcement, player.Champion);
                _game.PacketNotifier.NotifySyncMissionStartTimeS2C(player.ClientId, 0);
            }
            else _game.PacketNotifier.NotifySyncMissionStartTimeS2C(player.ClientId, _game.GameTime);
            _game.PacketNotifier.NotifySynchSimTimeS2C(player.ClientId, _game.GameTime);

            if (!player.IsMatchingVersion)
            {
                _game.PacketNotifier.NotifyS2C_SystemMessage(
                    player.ClientId,
                    "Your client version does not match the server. " +
                    "Check the server log for more information."
                );
            }

            var questManager = player.Champion.PlayerQuestManager;

            var systemTipsGroup = new QuestDisplayGroup { IsActionable = false, SourceQuestId = "System_Tips" };

            systemTipsGroup.AddQuest(new UIQuestData
            {
                QuestId = questManager.GetNextQuestId(),
                Objective = "Welcome to League Sandbox!",
                Tooltip = "This is a WIP project.",
                IsTip = true
            });

            systemTipsGroup.AddQuest(new UIQuestData
            {
                QuestId = questManager.GetNextQuestId(),
                Objective = "Server Build Date",
                Tooltip = ServerContext.BuildDateString,
                IsTip = true
            });

            systemTipsGroup.AddQuest(new UIQuestData
            {
                QuestId = questManager.GetNextQuestId(),
                Objective = "Your Champion:",
                Tooltip = player.Champion.Model,
                IsTip = true
            });

            questManager.AddQuest(systemTipsGroup);
            questManager.SyncActiveQuests();

        }
    }
}