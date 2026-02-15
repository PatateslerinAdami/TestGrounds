using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Packets.Handlers;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleSpellChargeUpdateReq : PacketHandlerBase<SpellChargeUpdateReq>
    {
        private readonly Game _game;
        private readonly PlayerManager _playerManager;

        public HandleSpellChargeUpdateReq(Game game)
        {
            _game = game;
            _playerManager = game.PlayerManager;
        }

        public override bool HandlePacket(int userId, SpellChargeUpdateReq req)
        {
            if (req == null)
            {
                return false;
            }
            var peerInfo = _playerManager.GetPeerInfo(userId);
            if (peerInfo == null)
            {
                return false;
            }
            var champion = peerInfo.Champion;
            if (champion == null)
            {
                return false;
            }

            var spell = champion.Spells[req.Slot];
            if (spell != null)
            {
                spell.UpdateCharge(req.Position, req.ForceStop);
            }

            return true;
        }
    }
}