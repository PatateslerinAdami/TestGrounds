using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Packets.Handlers;
using LeagueSandbox.GameServer.Inventory;
using LeagueSandbox.GameServer.Logging;
using log4net;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleSpawn : PacketHandlerBase<SpawnRequest>
    {
        private static ILog _logger = LoggerProvider.GetLogger();
        private readonly Game _game;
        private readonly ItemManager _itemManager;
        private readonly PlayerManager _playerManager;
        private readonly NetworkIdManager _networkIdManager;

        public HandleSpawn(Game game)
        {
            _game = game;
            _itemManager = game.ItemManager;
            _playerManager = game.PlayerManager;
            _networkIdManager = game.NetworkIdManager;
        }

        public override bool HandlePacket(int userId, SpawnRequest req)
        {
            _logger.Debug("Spawning map");
            // Per-team bot counts on the wire (replay-verified — see NotifyS2C_StartSpawn).
            byte botsOrder = 0, botsChaos = 0;
            foreach (var kv in _playerManager.GetPlayers(includeBots: true))
            {
                if (kv.Champion?.IsBot ?? false)
                {
                    if (kv.Team == GameServerCore.Enums.TeamId.TEAM_BLUE) botsOrder++;
                    else if (kv.Team == GameServerCore.Enums.TeamId.TEAM_PURPLE) botsChaos++;
                }
            }
            _game.PacketNotifier.NotifyS2C_StartSpawn(userId, botsOrder, botsChaos);

            var userInfo = _playerManager.GetPeerInfo(userId);
            var om = _game.ObjectManager as ObjectManager;
            if (_game.IsRunning)
            {
                // Mid-game spawn = a reconnect. Skip the mark-sweep if C2S_SoftReconnect already ran it
                // this reconnect (ReconnectSpawnReady guard) so we don't resync twice.
                if (!userInfo.ReconnectSpawnReady)
                {
                    om.OnReconnect(userId, userInfo.Team);
                }
                userInfo.ReconnectSpawnReady = true;
            }
            else
            {
                om.SpawnObjects(userInfo);
            }

            _game.PacketNotifier.NotifySpawnEnd(userId);

            // Enable the shop UI for this player's own champion (S2C_SetShopEnabled). Without it the
            // client leaves the buy/sell buttons disabled. See docs/SHOP_PACKETS_PLAN.md (P0).
            if (userInfo.Champion != null)
            {
                _game.PacketNotifier.NotifySetShopEnabled(userInfo.Champion);
            }

            if (_game.IsRunning)
            {
                _game.TryFinishReconnectStart(userId);
            }
            return true;
        }
    }
}
