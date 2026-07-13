using GameServerCore.Enums;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Chatbox.Commands
{
    /// <summary>
    /// Swaps the issuing player's champion in-game, the way Riot's own change-hero cheat did it on
    /// the wire: S2C_CreateHero with the ChangeHero bit (client re-binds its existing hero entity
    /// on the same NetID) plus S2C_ChangeCharacterData with ReplaceCharacterPackage (the actual
    /// model+spell swap — the same packet Elise/Nidalee form swaps use). Level, XP and gold are
    /// carried over; items and buffs are not (spend the gold again via !gold if needed), and
    /// spell points must be re-assigned.
    /// </summary>
    public class ChangeChampionCommand : ChatCommandBase
    {
        private readonly PlayerManager _playerManager;

        public override string Command => "changechampion";
        public override string Syntax => $"{Command} championName";

        public ChangeChampionCommand(ChatCommandManager chatCommandManager, Game game)
            : base(chatCommandManager, game)
        {
            _playerManager = game.PlayerManager;
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
            var model = split[1];

            try
            {
                Game.Config.ContentManager.GetCharData(model);
            }
            catch (ContentNotFoundException)
            {
                ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.SYNTAXERROR,
                    $"Champion '{model}' not found.");
                return;
            }

            var clientInfo = _playerManager.GetPeerInfo(userId);
            var old = clientInfo.Champion;

            var c = new Champion(
                Game,
                model,
                old.RuneList,
                old.TalentInventory,
                clientInfo,
                old.NetId,
                old.Team
            );

            c.SetPosition(old.Position, false);
            c.StopMovement();
            c.UpdateMoveOrder(OrderType.Stop);

            // Carry progression over; the fresh champion starts unleveled.
            c.Stats.Gold = old.Stats.Gold;
            c.Stats.Experience = old.Stats.Experience;
            while (c.Stats.Level < old.Stats.Level && c.LevelUp(true)) { }

            c.SpawnAsChangeHero = true;

            Game.ObjectManager.RemoveObject(old);
            Game.ObjectManager.AddObject(c);
            clientInfo.Champion = c;

            ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO,
                $"Changed champion to {model} (level {c.Stats.Level}, items/buffs not carried over).");
        }
    }
}
