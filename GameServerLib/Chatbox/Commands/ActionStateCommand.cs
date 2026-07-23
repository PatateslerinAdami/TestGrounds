using GameServerCore.Enums;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Chatbox.Commands
{
    /// <summary>
    /// Debug/verification tool: force an arbitrary wire ActionState bit on the calling player's
    /// champion, to observe what (if anything) the 4.20 client does with it. Usage:
    ///   !actionstate 15 1   -> set bit 15
    ///   !actionstate 15 0   -> clear bit 15
    /// Sets the bit directly on Stats.ActionState; UpdateActionState only touches the MAPPED bits, so
    /// an unmapped bit (15, 21, ...) persists and replicates on the next cycle. Testing a MAPPED bit
    /// (0/1/2/6/9/10/12/13/16/17/19/20/23/25) will be overwritten on the next state change — expected.
    /// </summary>
    public class ActionStateCommand : ChatCommandBase
    {
        private readonly PlayerManager _playerManager;

        public override string Command => "actionstate";
        public override string Syntax => $"{Command} <bit 0-31> <0|1>";

        public ActionStateCommand(ChatCommandManager chatCommandManager, Game game)
            : base(chatCommandManager, game)
        {
            _playerManager = game.PlayerManager;
        }

        public override void Execute(int userId, bool hasReceivedArguments, string arguments = "")
        {
            var split = arguments.Split(' ');
            if (split.Length < 3
                || !int.TryParse(split[1], out var bit) || bit < 0 || bit > 31
                || !int.TryParse(split[2], out var onOff) || (onOff != 0 && onOff != 1))
            {
                ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.SYNTAXERROR);
                ShowSyntax();
                return;
            }

            var champion = _playerManager.GetPeerInfo(userId)?.Champion;
            if (champion == null)
            {
                return;
            }

            var state = (ActionState)(1u << bit);
            var enabled = onOff == 1;
            champion.Stats.SetActionState(state, enabled);

            ChatCommandManager.SendDebugMsgFormatted(
                DebugMsgType.INFO,
                $"ActionState bit {bit} -> {(enabled ? "SET" : "clear")} (raw now 0x{(uint)champion.Stats.ActionState:X8})");
        }
    }
}
