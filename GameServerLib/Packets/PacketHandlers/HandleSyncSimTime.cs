using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Packets.Handlers;
using LeagueSandbox.GameServer.Logging;
using log4net;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleSyncSimTime : PacketHandlerBase<SyncSimTimeRequest>
    {
        private readonly Game _game;
        private static ILog _logger = LoggerProvider.GetLogger();
        private readonly PlayerManager _playerManager;

        public HandleSyncSimTime(Game game)
        {
            _game = game;
            _playerManager = game.PlayerManager;
        }

        public override bool HandlePacket(int userId, SyncSimTimeRequest req)
        {
            //Check this
            var diff = req.TimeLastServer - req.TimeLastClient;
            if (req.TimeLastClient > req.TimeLastServer)
            {
                var peerInfo = _playerManager.GetPeerInfo(userId);
                var msg = $"Client {peerInfo.ClientId} sent an invalid heartbeat - Timestamp error (diff: {diff})";
                _logger.Warn(msg);
            }

            // Reply with SyncSimTimeFinalS2C (0x76) so the client refines its latency average + clock
            // convergence. serverTime in seconds; estLatency = half the round-trip since the server time
            // the client echoed (TimeLastServer), clamped to a sane range so a stale/garbage timestamp
            // can never corrupt the client clock (0 => no latency compensation, still drives convergence).
            var serverTimeNow = _game.GameTime / 1000.0f;
            var roundTrip = serverTimeNow - req.TimeLastServer;
            var estLatency = (roundTrip > 0f && roundTrip < 0.5f) ? roundTrip * 0.5f : 0f;
            _game.PacketNotifier.NotifySyncSimTimeFinalS2C(userId, serverTimeNow, estLatency);

            return true;
        }
    }
}
