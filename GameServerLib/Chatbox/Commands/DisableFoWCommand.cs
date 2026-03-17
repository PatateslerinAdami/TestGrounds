using System.Xml.Linq;

namespace LeagueSandbox.GameServer.Chatbox.Commands
{
    public class DisableFoWCommand : ChatCommandBase
    {
        public override string Command => "disablefow";
        public override string Syntax => $"{Command}";

        public DisableFoWCommand(ChatCommandManager chatCommandManager, Game game)
            : base(chatCommandManager, game)
        {
        }

        public override void Execute(int userId, bool hasReceivedArguments, string arguments = "")
        {
            Game.PacketNotifier.NotifyToggleFoW(true);

            Game.ObjectManager.IsServerFoWDisabled = true;
            foreach (var obj in Game.ObjectManager.GetObjects().Values)
            {
                Game.ObjectManager.RefreshUnitVision(obj);
            }
        }
    }
}