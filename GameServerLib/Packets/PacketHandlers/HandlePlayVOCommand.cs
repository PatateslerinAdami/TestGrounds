using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Packets.Handlers;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandlePlayVOCommand : PacketHandlerBase<PlayVOCommandRequest>
    {
        private readonly Game _game;
        private readonly PlayerManager _playerManager;

        public HandlePlayVOCommand(Game game)
        {
            _game = game;
            _playerManager = game.PlayerManager;
        }

        public override bool HandlePacket(int userId, PlayVOCommandRequest req)
        {
            // Relay the SmartPing/VO command to the other clients (S2C_PlayVOCommand) — they play the
            // source champion's voice line + highlight its icon. Pure relay; the client throttles ping VO.
            var champion = _playerManager.GetPeerInfo(userId).Champion;
            if (champion != null)
            {
                _game.PacketNotifier.NotifyS2C_PlayVOCommand(
                    champion, req.CommandID, req.TargetNetID, req.HighlightPlayerIcon, req.FromPing, req.AlliesOnly);
            }
            return true;
        }
    }
}
