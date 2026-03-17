using System.Xml.Linq;

namespace LeagueSandbox.GameServer.Chatbox.Commands
{
    public class EnableFoWCommand : ChatCommandBase
    {
        public override string Command => "enablefow";
        public override string Syntax => $"{Command}";

        public EnableFoWCommand(ChatCommandManager chatCommandManager, Game game)
            : base(chatCommandManager, game)
        {
        }

        public override void Execute(int userId, bool hasReceivedArguments, string arguments = "")
        {
            Game.PacketNotifier.NotifySetFoWStatus(true);

            Game.ObjectManager.IsServerFoWDisabled = false;
            foreach (var obj in Game.ObjectManager.GetObjects().Values)
            {
                Game.ObjectManager.RefreshUnitVision(obj);
            }
        }
    }
}