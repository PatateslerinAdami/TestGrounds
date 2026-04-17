using LeagueSandbox.GameServer.Players;
﻿using GameServerCore.Enums;
using LeagueSandbox.GameServer;
using LeagueSandbox.GameServer.Chatbox;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Players;
using System.Linq;

namespace LeagueSandbox.GameServer.Chatbox.Commands
{
    public class GoldCommand : ChatCommandBase
    {
        private readonly PlayerManager _playerManager;

        public override string Command => "gold";
        public override string Syntax => $"{Command} goldAmount [blue/purple]";

        public GoldCommand(ChatCommandManager chatCommandManager, Game game)
            : base(chatCommandManager, game)
        {
            _playerManager = game.PlayerManager;
        }

        public override void Execute(int userId, bool hasReceivedArguments, string arguments = "")
        {
            var split = arguments.ToLower().Split(' ');
            if (split.Length < 2 || !float.TryParse(split[1], out var gold))
            {
                ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.SYNTAXERROR);
                ShowSyntax();
                return;
            }

            // No team argument — give gold to just the calling player
            if (split.Length < 3)
            {
                var ch = _playerManager.GetPeerInfo(userId).Champion;
                ch.AddGold(ch, gold);
                return;
            }
            // Team argument provided — split gold evenly among team members
            var teamArg = split[2];
            TeamId targetTeam;
            if (teamArg == "blue")
                targetTeam = TeamId.TEAM_BLUE;
            else if (teamArg == "purple")
                targetTeam = TeamId.TEAM_PURPLE;
            else
            {
                ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.SYNTAXERROR);
                ShowSyntax();
                return;
            }
            // Collect all champions on the target team
            var teamChampions = (from peer in _playerManager.GetPlayers()
                                 let champion = peer.Champion
                                 where champion != null && champion.Team == targetTeam
                                 select champion).ToList();
            if (teamChampions.Count == 0)
                return;

            // Divide gold evenly and grant it
            var splitGold = gold / teamChampions.Count;
            foreach (var champion in teamChampions)
            {
                champion.AddGold(champion, splitGold);
            }
        }
    }
}