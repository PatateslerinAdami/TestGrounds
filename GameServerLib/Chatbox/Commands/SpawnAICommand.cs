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
                GenerateBotName(),
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


        private static readonly Random _random = new Random();

        private static readonly string[] _prefixes =
        {
    "xX", "XX", "x_", "XX_", "[ ", "# ", "- ", "« ", "★ ", "",
    "", "", "", "" // weighted toward no prefix
};

        private static readonly string[] _adjectives =
        {
    "Dark", "Shadow", "Hyper", "Ultra", "Turbo", "Mega", "Epic", "Pro",
    "Elite", "Toxic", "Stealth", "Raging", "Silent", "Blazing", "Frozen",
    "Neon", "Cyber", "Omega", "Alpha", "Delta", "Bloody", "Savage", "Crispy",
    "Sweaty", "Tryhard", "Cracked", "Goated", "Based", "Cursed", "Speedy"
};

        private static readonly string[] _nouns =
        {
    "Wolf", "Sniper", "Dragon", "Slayer", "Killer", "Hunter", "Reaper",
    "Ninja", "Wizard", "Beast", "Demon", "Phoenix", "Viper", "Raven",
    "Storm", "Ghost", "Titan", "Legend", "Gamer", "Warrior", "Noob",
    "Blade", "Hawk", "Fox", "Bear", "Shark", "Rage", "God", "King", "Bot"
};

        private static readonly string[] _suffixes =
        {
    "Xx", "XX", "_x", "_XX", " ]", " #", " -", " »", " ★", "",
    "", "", "", "" // weighted toward no suffix
};

        private static readonly string[] _separators =
        {
    "_", "-", ".", "", "", "" // weighted toward no separator
};

        private static string GenerateBotName()
        {
            string prefix = _prefixes[_random.Next(_prefixes.Length)];
            string adjective = _adjectives[_random.Next(_adjectives.Length)];
            string separator = _separators[_random.Next(_separators.Length)];
            string noun = _nouns[_random.Next(_nouns.Length)];
            string suffix = _suffixes[_random.Next(_suffixes.Length)];

            // 60% chance to add a number at the end
            string number = _random.NextDouble() < 0.6
                ? _random.Next(1, 9999).ToString()
                : "";

            // 30% chance to add a second separator + number block like "_420"
            string extraNumber = _random.NextDouble() < 0.3
                ? $"_{_random.Next(1, 999)}"
                : "";

            return $"{prefix}{adjective}{separator}{noun}{number}{extraNumber}{suffix}";
        }
    }
}