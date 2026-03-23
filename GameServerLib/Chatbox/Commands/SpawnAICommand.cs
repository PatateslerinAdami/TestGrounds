using GameServerCore.Enums;
using GameServerCore.NetInfo;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Inventory;
using LeagueSandbox.GameServer.Players;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace LeagueSandbox.GameServer.Chatbox.Commands
{
    public class SpawnAICommand : ChatCommandBase
    {
        private readonly PlayerManager _playerManager;
        private readonly Game _game;

        public override string Command => "spawnbot";
        public override string Syntax => $"{Command} <blue|purple> [championName] [count] [level]";

        public SpawnAICommand(ChatCommandManager chatCommandManager, Game game)
            : base(chatCommandManager, game)
        {
            _game = game;
            _playerManager = game.PlayerManager;
        }

        private static readonly Dictionary<string, string> _dedicatedAIScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Ezreal",   "EzrealBot"   },
            // Add more champions with dedicated scripts here
        };

        private static string GetAIScript(string model)
        {
            return _dedicatedAIScripts.TryGetValue(model, out var script) ? script : "BasicAI";
        }

        public override void Execute(int userId, bool hasReceivedArguments, string arguments = "")
        {
            var split = arguments.Trim().Split(' ');

            if (split.Length < 2)
            {
                ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.SYNTAXERROR);
                ShowSyntax();
                return;
            }

            // Map "blue"/"purple" to TeamId
            var teamArg = split[1].ToLower();
            TeamId team;
            switch (teamArg)
            {
                case "blue": team = TeamId.TEAM_BLUE; break;
                case "purple": team = TeamId.TEAM_PURPLE; break;
                default:
                    ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.SYNTAXERROR,
                        $"Unknown team '{split[1]}'. Use 'blue' or 'purple'.");
                    ShowSyntax();
                    return;
            }

            // Optional champion name — defaults to a fallback if not provided
            string championModel = split.Length > 2 ? split[2] : "Ezreal";

            // Optional count — last parameter, defaults to 1
            int count = 1;
            if (split.Length > 3)
            {
                if (!int.TryParse(split[3], out count) || count < 1 || count > 100)
                {
                    ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.SYNTAXERROR,
                        $"Invalid count '{split[3]}'. Must be a number between 1 and 100.");
                    ShowSyntax();
                    return;
                }
            }

            int level = 1;
            if (split.Length > 4)
            {
                if (!int.TryParse(split[4], out level) || level < 1 || level > 18)
                {
                    ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.SYNTAXERROR,
                        $"Invalid level '{split[4]}'. Must be a number between 1 and 18.");
                    ShowSyntax();
                    return;
                }
            }

            try
            {
                Game.Config.ContentManager.GetCharData(championModel);
            }
            catch (ContentNotFoundException)
            {
                ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.SYNTAXERROR,
                    $"Champion '{championModel}' not found.");
                ShowSyntax();
                return;
            }

            for (int i = 0; i < count; i++)
            {
                SpawnChampForTeam(team, userId, championModel, level);
            }
        }

        public void SpawnChampForTeam(TeamId team, int userId, string model, int level = 1)
        {
            var spawnPos = _playerManager.GetPeerInfo(userId).Champion.Position;
            var clientInfo = new ClientInfo(
                "", team, 0, 0, 0,
                $"{model} Bot",
                new string[] { "SummonerHeal", "SummonerFlash" },
                -1
            );

            _playerManager.AddPlayer(clientInfo);

            var champion = new Champion(
                _game,
                model,
                new RuneCollection(),
                new TalentInventory(),
                clientInfo,
                AIScript: GetAIScript(model),  // <-- replaces "BasicAI"
                team: team
            );

            clientInfo.IsDisconnected = false;
            clientInfo.IsStartedClient = true;
            clientInfo.Champion = champion;
            champion.SetPosition(spawnPos, false);
            champion.StopMovement();
            champion.UpdateMoveOrder(OrderType.Stop);
            champion.LevelUp();

            // LevelUp() increments by 1 each call, so loop to reach target level
            for (int i = 0; i < level; i++)
            {
                while (champion.Stats.Level < level)
                {
                    champion.LevelUp(true);
                }
            }

            _game.ObjectManager.AddObject(champion);

            ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO,
                $"Spawned Bot '{champion.Name}' ({champion.Model}) on team {team} at level {level} with NetID: {champion.NetId}.");
        }
    }
}