using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Chatbox.Commands
{
    public class DeathTimerModCommand : ChatCommandBase
    {
        private readonly PlayerManager _playerManager;

        public override string Command => "deathtimermod";
        public override string Syntax => $"{Command} percentDeathTimerReduction";

        public DeathTimerModCommand(ChatCommandManager chatCommandManager, Game game)
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
            else if (float.TryParse(split[1], out var DeathTimerMod))
            {
                var modifier = -(DeathTimerMod / 100f);
                _playerManager.GetPeerInfo(userId).Champion.Stats.DeathTimerReduction.FlatBonus += modifier;
                var total = _playerManager.GetPeerInfo(userId).Champion.Stats.DeathTimerReduction.Total;

                var reductionPercent = (int)Math.Round(-total * 100);
                ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO, $"Death timer reduction set to {reductionPercent}%");
            }
        }
    }
}
