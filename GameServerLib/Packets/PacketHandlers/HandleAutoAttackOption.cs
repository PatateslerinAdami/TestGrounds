using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Packets.Handlers;
using LeagueSandbox.GameServer.Chatbox;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    /// <summary>
    /// Handles packet 0x47 / C2S_UpdateGameOptions — the client pushes this whenever the
    /// player toggles the "Auto Attack" checkbox in the game-options menu (in the 4.20
    /// client implemented via OptionsManager / AIHeroClient::PushAutoAcquireTargetToServer
    /// at offset 0x00072af0). The bool maps onto Champion.AutoAcquireTargetEnabled, which
    /// gates the auto-acquire scan in ObjAIBase.UpdateTarget. Default state is true on the
    /// client; we mirror that so champions auto-acquire until the player explicitly opts out.
    /// </summary>
    public class HandleAutoAttackOption : PacketHandlerBase<AutoAttackOptionRequest>
    {
        private readonly Game _game;
        private readonly PlayerManager _playerManager;

        public HandleAutoAttackOption(Game game)
        {
            _game = game;
            _playerManager = game.PlayerManager;
        }

        public override bool HandlePacket(int userId, AutoAttackOptionRequest req)
        {
            var peerInfo = _playerManager.GetPeerInfo(userId);
            if (peerInfo?.Champion != null)
            {
                peerInfo.Champion.AutoAcquireTargetEnabled = req.Activated;
            }
            return true;
        }
    }
}
