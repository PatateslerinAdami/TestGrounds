using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Packets.Handlers;
using LeagueSandbox.GameServer.Chatbox;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleQuestClicked : PacketHandlerBase<QuestClickedRequest>
    {
        private readonly Game _game;
        private readonly ChatCommandManager _chatCommandManager;

        public HandleQuestClicked(Game game)
        {
            _game = game;
            _chatCommandManager = game.ChatCommandManager;
        }

        public override bool HandlePacket(int userId, QuestClickedRequest req)
        {
            var player = _game.PlayerManager.GetPeerInfo(userId);
            if (player == null || player.Champion == null) return false;

            player.Champion.PlayerQuestManager.OnQuestClicked(req.QuestID);

            return true;
        }
    }
}
