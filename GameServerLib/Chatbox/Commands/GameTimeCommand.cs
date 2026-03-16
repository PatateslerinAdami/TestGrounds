using LeagueSandbox.GameServer.Players;
using System;

namespace LeagueSandbox.GameServer.Chatbox.Commands
{
    public class GameTimeCommand : ChatCommandBase
    {
        private readonly PlayerManager _playerManager;
        private readonly Game _game;

        public override string Command => "gametime";
        public override string Syntax => $"{Command} gameTime";

        public GameTimeCommand(ChatCommandManager chatCommandManager, Game game)
            : base(chatCommandManager, game)
        {
            _game = game;
            _playerManager = game.PlayerManager;
        }

        public override void Execute(int userId, bool hasReceivedArguments, string arguments = "")
        {
            var split = arguments.ToLower().Split(' ');
            if (split.Length != 2)
            {
                ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.SYNTAXERROR);
                ShowSyntax();
                return;
            }

            if (float.TryParse(split[1], out float newMinutes))
            {
                _game.SetCurrentMinutes(newMinutes);
                ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO, ($"Game time set to {newMinutes} minutes."));
            }
            else
            {
                ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.ERROR);
                ShowSyntax();
            }
        }
    }
}
