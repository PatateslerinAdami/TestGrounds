using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Packets.Handlers;
using LeagueSandbox.GameServer.Logging;
using PacketDefinitions420;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleMoveConfirm : PacketHandlerBase<MoveConfirmRequest>
    {
        private readonly Game _game;

        public HandleMoveConfirm(Game game)
        {
            _game = game;
        }

        public override bool HandlePacket(int userId, MoveConfirmRequest req)
        {
            // Riot's server ignores this packet (move-ack no-op, decomp-verified) — no gameplay
            // handling here. But the client echoes the SyncID of the movement packet it just
            // APPLIED, and our WireSyncID is a session clock (2/3 per ms), so the delta to "now"
            // measures round-trip + client apply delay per movement packet. Diagnostic only.
            if (PathLogger.Enabled)
            {
                var champion = _game.PlayerManager.GetPeerInfo(userId)?.Champion;
                if (champion != null)
                {
                    PathLogger.LogMoveAck(_game.GameTime, champion.NetId, req.SyncID,
                        PacketExtensions.WireSyncID, req.TeleportCount);
                }
            }
            return true;
        }
    }
}
