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
                //var msg = $"Client {peerInfo.ClientId} sent an invalid heartbeat - Timestamp error (diff: {diff})";
                //_logger.Warn(msg);
            }

            // Reply with SyncSimTimeFinalS2C (0x76). Field semantics replay-verified against Riot
            // (343e3502, four samples exact to ±0.01, 2026-07-04):
            //   TimeLastClient      = ECHO of the client's reported clock (req.TimeLastClient)
            //   TimeRTTLastOverhead = serverNow − req.TimeLastServer, RAW — the full round trip
            //                         since the 0xC1 stamp the client last saw (the C2S heartbeat
            //                         is the client's reply to 0xC1, GameClient.cpp:4017), i.e.
            //                         RTT + frame/scheduling overhead: ~0.3-0.5s steady state in
            //                         Riot games, seconds-large during load. NO clamping — the
            //                         old 0.5s sanity clamp fired on virtually every heartbeat
            //                         (the echoed stamp can be many seconds old whenever a
            //                         heartbeat isn't an immediate 0xC1 reply) and zeroed the
            //                         field permanently.
            //   TimeConvergance     = server GameTime now, seconds (the 4.17 client's 0x76
            //                         handler doesn't read it; Riot sends it, so do we).
            // Client consumption (GameClient.cpp:3353-3361): gAverageLatency = f9·0.3 + old·0.7;
            // gLastConvergenceDelta = (clock − f5)·0.4 + gAverageLatency; and the periodic 0xC1
            // HARD clock-set applies SetTime(stamp + gAverageLatency) (GameClient.cpp:4022) — so
            // a zeroed f9 disabled the client's latency compensation entirely (invisible on
            // localhost, real lag for networked players).
            var serverTimeNow = _game.GameTime / 1000.0f;
            var rttOverhead = System.Math.Max(0f, serverTimeNow - req.TimeLastServer);
            _game.PacketNotifier.NotifySyncSimTimeFinalS2C(userId, req.TimeLastClient, rttOverhead, serverTimeNow);

            return true;
        }
    }
}
