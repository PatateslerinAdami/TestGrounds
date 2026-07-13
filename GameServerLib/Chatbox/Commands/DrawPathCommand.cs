using GameServerCore.Enums;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Chatbox.Commands
{
    /// <summary>
    /// Toggles the client's draw-path mode on the player's champion (S2C_UnitSetDrawPathMode,
    /// Riot's internal path-drawing dev tool — never seen in live 4.20 replays). While enabled,
    /// the client streams cursor points back on every move click / held right-click
    /// (C2S_UnitSendDrawPath); HandleDrawPath paints them as debug circles and logs the stroke
    /// boundaries. Live-verified working on the 4.20 retail client. Usage: !drawpath [0|1]
    /// (no argument = enable).
    /// </summary>
    public class DrawPathCommand : ChatCommandBase
    {
        private readonly PlayerManager _playerManager;

        public override string Command => "drawpath";
        public override string Syntax => $"{Command} [0|1]";

        public DrawPathCommand(ChatCommandManager chatCommandManager, Game game)
            : base(chatCommandManager, game)
        {
            _playerManager = game.PlayerManager;
        }

        public override void Execute(int userId, bool hasReceivedArguments, string arguments = "")
        {
            var mode = DrawPathMode.Line;
            var split = arguments.ToLower().Split(' ');
            if (split.Length >= 2 && split[1] == "0")
            {
                mode = DrawPathMode.Disabled;
            }

            var champion = _playerManager.GetPeerInfo(userId).Champion;
            Game.PacketNotifier.NotifyUnitSetDrawPathMode(userId, champion, champion, mode);
            ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.NORMAL,
                $"Draw-path mode {(mode == DrawPathMode.Disabled ? "disabled" : "enabled")} — "
                + "hold/drag right-click to stream cursor points.");
        }
    }
}
