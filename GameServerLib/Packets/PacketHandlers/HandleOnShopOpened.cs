using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Packets.Handlers;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleOnShopOpened : PacketHandlerBase<OnShopOpenedRequest>
    {
        private readonly Game _game;

        public HandleOnShopOpened(Game game)
        {
            _game = game;
        }

        public override bool HandlePacket(int userId, OnShopOpenedRequest req)
        {
            // Fire-and-forget world event the client sends when the store screen opens; it never waits
            // on a reply. Server-side this is currently just bookkeeping (shopkeeper VO can hook here
            // later). Accept it so it isn't logged as an unhandled packet. See docs/SHOP_PACKETS_PLAN.md.
            return true;
        }
    }
}
