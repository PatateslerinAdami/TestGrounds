using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Packets.Handlers;
using PacketDefinitions420;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleSoftReconnect : PacketHandlerBase<SoftReconnectRequest>
    {
        private readonly Game _game;

        public HandleSoftReconnect(Game game)
        {
            _game = game;
        }

        public override bool HandlePacket(int userId, SoftReconnectRequest req)
        {
            var peerInfo = _game.PlayerManager.GetPeerInfo(userId);
            peerInfo.IsStartedClient = true;
            peerInfo.IsDisconnected = false;
            _game.ObjectManager.OnReconnect(userId, peerInfo.Team);
            return true;
        }
    }
}