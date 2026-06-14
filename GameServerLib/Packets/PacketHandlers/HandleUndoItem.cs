using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Packets.Handlers;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleUndoItem : PacketHandlerBase<UndoItemRequest>
    {
        private readonly Game _game;
        private readonly PlayerManager _playerManager;

        public HandleUndoItem(Game game)
        {
            _game = game;
            _playerManager = game.PlayerManager;
        }

        public override bool HandlePacket(int userId, UndoItemRequest req)
        {
            // Empty request ("undo my last shop action"); the target is the server-tracked top of the
            // champion's undo stack. See docs/SHOP_PACKETS_PLAN.md (P2).
            var champion = _playerManager.GetPeerInfo(userId).Champion;
            return champion.Shop.HandleUndo();
        }
    }
}
