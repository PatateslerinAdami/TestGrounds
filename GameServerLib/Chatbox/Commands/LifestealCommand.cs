using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Chatbox.Commands
{
    public class LifestealCommand : ChatCommandBase
    {
        private readonly PlayerManager _playerManager;

        public override string Command => "lifesteal";
        public override string Syntax => $"{Command} bonusLifesteal";

        public LifestealCommand(ChatCommandManager chatCommandManager, Game game)
            : base(chatCommandManager, game)
        {
            _playerManager = game.PlayerManager;
        }

        public override void Execute(int userId, bool hasReceivedArguments, string arguments = "")
        {
            var split = arguments.ToLower().Split(' ');
            if (split.Length < 2)
            {
                ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.SYNTAXERROR);
                ShowSyntax();
            }
            else if (float.TryParse(split[1], out var Lifesteal))
            {
                _playerManager.GetPeerInfo(userId).Champion.Stats.LifeSteal.FlatBonus += Lifesteal / 100f;
            }
        }
    }
}
