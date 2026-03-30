using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Chatbox.Commands
{
    public class ResetCooldownsCommand : ChatCommandBase
    {
        private readonly PlayerManager _playerManager;

        public override string Command => "resetcd";
        public override string Syntax => $"{Command}";

        public ResetCooldownsCommand(ChatCommandManager chatCommandManager, Game game)
            : base(chatCommandManager, game)
        {
            _playerManager = game.PlayerManager;
        }

        public override void Execute(int userId, bool hasReceivedArguments, string arguments = "")
        {
            var champion = _playerManager.GetPeerInfo(userId).Champion;

            // Reset cooldowns for all spells
            foreach (var spell in champion.Spells)
            {
                if (spell.Value != null)
                {
                    spell.Value.SetCooldown(0f, true); // Access the Spell value first
                }
            }

            ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO, "All spell cooldowns have been reset!");
        }
    }
}