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
            // C2S_SoftReconnect is the faithful soft-reconnect trigger; HandleSpawn also runs the
            // mark-sweep when a SpawnRequest arrives mid-game. ReconnectSpawnReady guards against doing
            // the resync twice if a reconnecting client sends both — whichever arrives first does it.
            if (!peerInfo.ReconnectSpawnReady)
            {
                _game.ObjectManager.OnReconnect(userId, peerInfo.Team);
                peerInfo.ReconnectSpawnReady = true;
            }
            _game.TryFinishReconnectStart(userId);
            return true;
        }
    }
}